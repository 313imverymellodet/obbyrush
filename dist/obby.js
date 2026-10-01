// OBBY RUSH page side: per-course leaderboard overlay, world-record ghosts, and the live race lobby.
// Unity calls window.obby.*; results go back via SendMessage("Game", "OnRank" | "OnNet" | "OnGhost", json).
(function () {
  var qs = new URLSearchParams(location.search);
  var API = qs.get("api") || "https://orbyt-api-production-29f6.up.railway.app";
  var WS_RACE = API.replace(/^http/, "ws") + "/obby", WS_LIVE = API.replace(/^http/, "ws") + "/live";
  var MAPS = { garden: "SKY GARDEN", tower: "SUNSET TOWER", storm: "STORM PEAK" };
  var CARS = { knight: "KNIGHT", barbarian: "BARBARIAN", rogue: "ROGUE", mage: "MAGE", ranger: "RANGER", minion: "BONEY", hooded: "SHADOW", skrogue: "RATTLES", warrior: "BONE KNIGHT", skmage: "LICH" };
  var tokens = {}, meName = store("or_name"), boardMap = null, live = null;
  var ws = null, racing = false, lobbyMap = "garden", myCar = store("or_skin") || "knight", wantJoin = qs.get("room");

  function store(k, v) { try { if (v === undefined) return localStorage.getItem(k); localStorage.setItem(k, v); } catch (e) { return null; } }
  function playerId() {
    var id = store("or_player");
    if (!id) { id = crypto.randomUUID ? crypto.randomUUID() : (Date.now().toString(16) + Math.random().toString(16).slice(2)); store("or_player", id); }
    return id;
  }
  function toUnity(method, payload) { try { window.unityInstance && window.unityInstance.SendMessage("Game", method, JSON.stringify(payload)); } catch (e) {} }
  function post(path, body) { return fetch(API + path, { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(body || {}) }).then(function (r) { return r.json(); }); }
  function esc(s) { return String(s).replace(/[&<>"]/g, function (c) { return { "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;" }[c]; }); }
  function fmt(ms) { if (!ms) return "-:--.--"; var s = ms / 1000, m = Math.floor(s / 60); s -= m * 60; return m + ":" + (s < 10 ? "0" : "") + s.toFixed(2); }
  function toast(m) { window.orToast && window.orToast(m); }
  function track(n, v) { window.SD && window.SD.track && window.SD.track(n, v || 0); }

  // ---------------------------------------------------------------- styles
  var css = document.createElement("style");
  css.textContent = [
    ".crov{position:fixed;inset:0;z-index:20;display:none;align-items:center;justify-content:center;background:rgba(16,8,40,.78);backdrop-filter:blur(3px);font-family:system-ui,-apple-system,'Segoe UI',Roboto,sans-serif;color:#fff}",
    ".crov.on{display:flex}",
    ".crov .card{width:min(430px,92vw);max-height:88vh;display:flex;flex-direction:column;background:#241a55;border:1px solid rgba(255,255,255,.12);border-radius:24px;box-shadow:0 20px 60px rgba(0,0,0,.5);overflow:hidden;animation:crin .25s ease}",
    "@keyframes crin{from{transform:scale(.9);opacity:0}}",
    ".crov .hd{padding:18px 18px 10px;display:flex;align-items:center;justify-content:space-between}",
    ".crov h2{margin:0;font-size:26px;font-weight:900;font-style:italic;letter-spacing:2px;text-shadow:0 3px 0 #6b3cff}",
    ".crov .x{background:rgba(255,255,255,.1);border:0;color:#fff;width:38px;height:38px;border-radius:12px;font-size:18px;cursor:pointer}",
    ".crov .tabs{display:flex;gap:8px;padding:0 18px 10px}",
    ".crov .tab{flex:1;padding:11px;border-radius:12px;border:0;background:rgba(255,255,255,.08);color:#fff;font-weight:900;font-style:italic;letter-spacing:1px;cursor:pointer}",
    ".crov .tab.on{background:#FF4F8B}",
    ".crov .live{padding:0 18px 8px;font-size:12px;opacity:.7;display:flex;align-items:center;gap:6px}",
    ".crov .dot{width:8px;height:8px;border-radius:50%;background:#9BFF5C;box-shadow:0 0 8px #9BFF5C;animation:crp 1.4s infinite}",
    "@keyframes crp{50%{opacity:.35}}",
    ".crov ol{list-style:none;margin:0;padding:0 10px 14px;overflow:auto}",
    ".crov li{display:flex;align-items:center;gap:10px;padding:10px;border-radius:12px;font-weight:700}",
    ".crov li:nth-child(odd){background:rgba(255,255,255,.04)}",
    ".crov li.me{background:rgba(255,79,139,.22);outline:1px solid rgba(255,79,139,.6)}",
    ".crov li.flash{animation:crf 1.2s ease}@keyframes crf{0%{background:rgba(255,209,102,.6)}}",
    ".crov .rk{width:30px;text-align:center;font-weight:900;font-style:italic;opacity:.8}",
    ".crov li:nth-child(1) .rk{color:#FFD166;opacity:1}.crov li:nth-child(2) .rk{color:#D9E2F2;opacity:1}.crov li:nth-child(3) .rk{color:#FF9A5C;opacity:1}",
    ".crov .nm{flex:1;min-width:0}.crov .nm b{display:block;overflow:hidden;text-overflow:ellipsis;white-space:nowrap}.crov .nm small{opacity:.55;font-size:11px;letter-spacing:1px}",
    ".crov .tm{font-weight:900;font-size:18px;font-variant-numeric:tabular-nums}",
    ".crov .empty{padding:30px;text-align:center;opacity:.6}",
    ".crov .body{padding:4px 22px 22px;text-align:center;overflow:auto}",
    ".crov .sub{opacity:.72;font-size:14px;margin:0 0 16px}",
    ".crov .btn{display:block;width:100%;padding:16px;margin:8px 0;border-radius:16px;border:0;font-weight:900;font-style:italic;font-size:18px;letter-spacing:1px;cursor:pointer}",
    ".crov .p{background:#FF4F8B;color:#fff}.crov .c{background:#35D6FF;color:#1b1340}.crov .s{background:rgba(255,255,255,.1);color:#fff}",
    ".crov .btn:disabled{opacity:.35}",
    ".crov input{width:100%;box-sizing:border-box;padding:14px;margin:8px 0 0;border-radius:14px;border:2px solid rgba(53,214,255,.5);background:#1b1340;color:#fff;font-size:22px;font-weight:900;text-align:center;letter-spacing:4px;text-transform:uppercase;outline:none}",
    ".crov .code{font-size:54px;font-weight:900;letter-spacing:10px;margin:4px 0;color:#35D6FF;text-shadow:0 0 24px rgba(53,214,255,.5)}",
    ".crov .spin{width:46px;height:46px;margin:16px auto;border-radius:50%;border:4px solid rgba(255,255,255,.15);border-top-color:#FF4F8B;animation:crs .8s linear infinite}",
    "@keyframes crs{to{transform:rotate(360deg)}}",
    ".crov .err{color:#FF7A9A;min-height:18px;font-size:13px;margin-top:6px}",
    ".crov .racers{list-style:none;padding:0;margin:10px 0;text-align:left}",
    ".crov .racers li{background:rgba(255,255,255,.06);margin:6px 0}",
    ".crov .racers .you{color:#FF4F8B}",
    ".crov .big{font-size:40px;font-weight:900;font-style:italic;color:#FFD166;margin:6px 0}"
  ].join("\n");
  document.head.appendChild(css);

  function overlay(id) {
    var o = document.createElement("div"); o.id = id; o.className = "crov";
    o.innerHTML = '<div class="card"></div>';
    document.body.appendChild(o);
    ["keydown", "keyup", "keypress"].forEach(function (t) { o.addEventListener(t, function (e) { e.stopPropagation(); }, true); });
    return o;
  }

  // ---------------------------------------------------------------- leaderboard
  var lb = overlay("crlb");
  lb.querySelector(".card").innerHTML = '<div class="hd"><h2>LEADERBOARD</h2><button class="x" aria-label="Close">&#10005;</button></div>' +
    '<div class="tabs"><button class="tab" data-m="garden">GARDEN</button><button class="tab" data-m="tower">TOWER</button><button class="tab" data-m="storm">STORM</button></div>' +
    '<div class="live"><span class="dot"></span><span>FASTEST RUNS · LIVE</span></div><ol></ol>';
  var lbList = lb.querySelector("ol");
  lb.querySelector(".x").onclick = function () { lb.classList.remove("on"); boardMap = null; };
  lb.addEventListener("click", function (e) { if (e.target === lb) { lb.classList.remove("on"); boardMap = null; } });
  lb.querySelectorAll(".tab").forEach(function (b) { b.onclick = function () { showBoard(b.getAttribute("data-m")); }; });

  function renderBoard(rows, flash) {
    if (!rows.length) { lbList.innerHTML = '<div class="empty">No times yet. Set the first record!</div>'; return; }
    lbList.innerHTML = rows.map(function (r, i) {
      var me = meName && r.name === meName.toUpperCase();
      return '<li class="' + (me ? "me " : "") + (flash && r.name === flash ? "flash" : "") + '"><span class="rk">' + (i + 1) + '</span>' +
        '<span class="nm"><b>' + esc(r.name) + (me ? " (YOU)" : "") + '</b><small>' + esc(CARS[r.skin] || "") + '</small></span>' +
        '<span class="tm">' + fmt(r.ms) + "</span></li>";
    }).join("");
  }
  function loadBoard(map, flash) {
    return fetch(API + "/api/obby/board?course=" + map + "&limit=30").then(function (r) { return r.json(); })
      .then(function (d) { if (boardMap === map) renderBoard(d.top || [], flash); })
      .catch(function () { lbList.innerHTML = '<div class="empty">Leaderboard offline. Try again soon.</div>'; });
  }
  function showBoard(map) {
    boardMap = MAPS[map] ? map : "garden";
    lb.querySelectorAll(".tab").forEach(function (b) { b.classList.toggle("on", b.getAttribute("data-m") === boardMap); });
    lb.classList.add("on");
    lbList.innerHTML = '<div class="empty">Loading...</div>';
    connectLive();
    loadBoard(boardMap);
    track("lb_open");
  }
  function connectLive() {
    if (live && live.readyState <= 1) return;
    try { live = new WebSocket(WS_LIVE); } catch (e) { return; }
    live.onmessage = function (ev) {
      var m; try { m = JSON.parse(ev.data); } catch (e) { return; }
      if (m.type === "obby" && boardMap === m.course) loadBoard(m.course, m.name);
    };
    live.onclose = function () { live = null; if (boardMap) setTimeout(connectLive, 3000); };
  }

  // ---------------------------------------------------------------- name prompt
  var nm = overlay("crname");
  function askName(cb, err, onSkip) {
    nm.querySelector(".card").innerHTML = '<div class="body" style="padding-top:22px"><h2>YOUR NAME</h2><p class="sub" style="margin-top:8px">Shown on the leaderboard and to other runners</p>' +
      '<input maxlength="12" autocomplete="off" autocapitalize="characters" spellcheck="false" placeholder="NAME">' +
      '<div class="err">' + (err || "") + '</div><button class="btn p" data-a="ok">GO</button><button class="btn s" data-a="skip">SKIP</button></div>';
    var input = nm.querySelector("input");
    input.value = meName || "";
    nm.classList.add("on");
    setTimeout(function () { input.focus(); }, 50);
    nm.querySelector('[data-a="ok"]').onclick = function () {
      var v = input.value.toUpperCase().replace(/[^A-Z0-9 _.-]/g, "").trim();
      if (v.length < 2) { nm.querySelector(".err").textContent = "At least 2 letters or numbers."; return; }
      meName = v; store("or_name", v);
      nm.classList.remove("on");
      cb(v);
    };
    nm.querySelector('[data-a="skip"]').onclick = function () { nm.classList.remove("on"); if (onSkip) onSkip(); };
    input.onkeydown = function (e) { if (e.key === "Enter") nm.querySelector('[data-a="ok"]').click(); };
  }

  // ---------------------------------------------------------------- online lobby
  var lob = overlay("crlobby");
  var card = lob.querySelector(".card");
  function view(html) { card.innerHTML = '<div class="hd"><h2>RACE LIVE</h2><button class="x" aria-label="Close">&#10005;</button></div><div class="body">' + html + "</div>"; lob.classList.add("on"); card.querySelector(".x").onclick = closeLobby; bind(); }
  function closeLobby() { send({ t: "leave" }); lob.classList.remove("on"); }
  function send(m) { if (ws && ws.readyState === 1) ws.send(JSON.stringify(m)); }

  function home(err) {
    view('<p class="sub">Up to 8 real players on the same course at once. First across the line wins.</p>' +
      '<button class="btn p" data-a="quick">QUICK RACE · ' + MAPS[lobbyMap] + '</button>' +
      '<button class="btn c" data-a="create">CREATE PRIVATE ROOM</button>' +
      '<input maxlength="4" placeholder="CODE" autocomplete="off" autocapitalize="characters" spellcheck="false">' +
      '<button class="btn s" data-a="join">JOIN WITH CODE</button><div class="err">' + (err || "") + "</div>");
  }

  function bind() {
    card.querySelectorAll("[data-a]").forEach(function (b) {
      b.onclick = function () {
        var a = b.getAttribute("data-a");
        if (a === "quick") connect(function () { send({ t: "quick", course: lobbyMap }); view('<div class="spin"></div><p class="sub">Finding runners on ' + MAPS[lobbyMap] + '...<br>If nobody shows up soon you will race the world record ghost.</p><button class="btn s" data-a="cancel">CANCEL</button>'); track("online_quick"); });
        if (a === "create") connect(function () { send({ t: "create", course: lobbyMap }); track("online_create"); });
        if (a === "join") {
          var code = (card.querySelector("input").value || "").toUpperCase().replace(/[^A-Z]/g, "");
          if (code.length !== 4) { card.querySelector(".err").textContent = "Codes are 4 letters."; return; }
          joinCode(code);
        }
        if (a === "cancel") { send({ t: "leave" }); home(); }
        if (a === "go") send({ t: "go" });
        if (a === "invite") invite(b.getAttribute("data-code"));
      };
    });
  }

  function joinCode(code) { connect(function () { send({ t: "join", code: code }); view('<div class="spin"></div><p class="sub">Joining ' + esc(code) + "...</p>"); track("online_join"); }); }

  function invite(code) {
    var url = location.origin + location.pathname + "?room=" + code;
    var text = "Race me in OBBY RUSH! Room " + code;
    if (navigator.share) navigator.share({ title: "OBBY RUSH", text: text, url: url }).catch(function () {});
    else if (navigator.clipboard) navigator.clipboard.writeText(text + "\n" + url).then(function () { toast("Invite link copied!"); });
  }

  var countdownTimer = null;
  function showRoom(m) {
    clearInterval(countdownTimer);
    var rows = m.players.map(function (p) {
      return '<li class="' + (p.name === (meName || "").toUpperCase() ? "you" : "") + '"><span class="nm"><b>' + esc(p.name) + '</b><small>' + esc(CARS[p.skin] || "") + "</small></span></li>";
    }).join("");
    var status, html = "";
    if (m.priv) html += '<p class="sub" style="margin:0">ROOM CODE</p><div class="code">' + m.code + '</div><button class="btn c" data-a="invite" data-code="' + m.code + '">INVITE FRIENDS</button>';
    html += '<p class="sub" style="margin:10px 0 0">' + MAPS[m.course] + " · " + m.players.length + "/8 RUNNERS</p><ul class=\"racers\">" + rows + "</ul>";
    if (m.startsIn > 0) html += '<div class="big" id="crcount">' + Math.ceil(m.startsIn / 1000) + "</div><p class=\"sub\">Starting soon... more racers can still join</p>";
    else if (m.priv && m.host) html += '<button class="btn p" data-a="go"' + (m.players.length < 2 ? " disabled" : "") + ">START RACE</button>" + (m.players.length < 2 ? '<p class="sub">Waiting for at least one friend...</p>' : "");
    else if (m.priv) html += '<div class="spin"></div><p class="sub">Waiting for the host to start...</p>';
    else html += '<div class="spin"></div><p class="sub">Waiting for more runners...</p>';
    html += '<button class="btn s" data-a="cancel">LEAVE</button>';
    view(html);
    if (m.startsIn > 0) {
      var end = Date.now() + m.startsIn;
      countdownTimer = setInterval(function () { var el = document.getElementById("crcount"); if (el) el.textContent = Math.max(0, Math.ceil((end - Date.now()) / 1000)); }, 200);
    }
  }

  function connect(then) {
    if (ws && ws.readyState === 1) { then(); return; }
    if (ws) try { ws.close(); } catch (e) {}
    view('<div class="spin"></div><p class="sub">Connecting...</p>');
    ws = new WebSocket(WS_RACE);
    var opened = false;
    ws.onopen = function () { opened = true; send({ t: "hello", name: meName || "RUNNER", skin: myCar }); then(); };
    ws.onmessage = function (ev) {
      var m; try { m = JSON.parse(ev.data); } catch (e) { return; }
      switch (m.t) {
        case "lobby": if (!racing) showRoom(m); break;
        case "error": home(m.msg); break;
        case "solo": clearInterval(countdownTimer); lob.classList.remove("on"); toUnity("OnNet", { t: "solo" }); break;
        case "start": clearInterval(countdownTimer); racing = true; lob.classList.remove("on"); toUnity("OnNet", m); break;
        case "state": case "fin": case "left": if (racing) toUnity("OnNet", m); break;
        case "results": if (racing) { racing = false; toUnity("OnNet", m); } break;
      }
    };
    ws.onclose = function () {
      clearInterval(countdownTimer);
      if (!opened) { home("Can't reach the race server. Try again in a moment."); return; }
      if (racing) { racing = false; toast("Connection lost - finishing solo"); }
      else if (lob.classList.contains("on")) home("Disconnected.");
      ws = null;
    };
  }

  function openLobby(map, car) {
    lobbyMap = MAPS[map] ? map : "garden";
    if (car) { myCar = car; store("or_skin", car); }
    var go = function () { if (wantJoin) { var c = wantJoin; wantJoin = null; joinCode(c); } else home(); };
    if (meName) go(); else askName(go, null, go);
  }

  // ---------------------------------------------------------------- API for Unity
  window.obby = {
    ready: function () { if (wantJoin) setTimeout(function () { openLobby(lobbyMap, null); }, 800); },
    start: function (course) {
      tokens[course] = null;
      post("/api/obby/run", { course: course }).then(function (d) { tokens[course] = d.token; }).catch(function () {});
    },
    submit: function (course, ms, skin, ghost) {
      var t = tokens[course]; tokens[course] = null;
      if (!t) { toUnity("OnRank", { course: course, rank: 0, error: "offline" }); return; }
      var sendTime = function (name) {
        post("/api/obby/finish", { token: t, player: playerId(), name: name, ms: ms, skin: skin, ghost: ghost })
          .then(function (r) {
            if (r.error === "bad name") { meName = null; store("or_name", ""); askName(sendTime, "That name isn't allowed. Try another."); return; }
            if (r.error) { toUnity("OnRank", { course: course, rank: 0, error: r.error }); return; }
            toUnity("OnRank", { course: course, rank: r.rank, total: r.total, best: r.best, newBest: r.newBest });
          }).catch(function () { toUnity("OnRank", { course: course, rank: 0, error: "offline" }); });
      };
      if (meName) sendTime(meName); else askName(sendTime, null, function () { toUnity("OnRank", { course: course, rank: 0, error: "skipped" }); });
    },
    ghost: function (course) {
      fetch(API + "/api/obby/ghost?course=" + encodeURIComponent(course)).then(function (r) { return r.json(); })
        .then(function (d) { if (d && d.g) toUnity("OnGhost", { course: course, name: d.name, skin: d.skin, ms: d.ms, g: d.g }); })
        .catch(function () {});
    },
    board: showBoard,
    lobby: openLobby,
    state: function (x, y, z, r, a, c) { if (racing) send({ t: "state", x: x, y: y, z: z, r: r, a: a, c: c }); },
    finish: function (ms) { send({ t: "fin", ms: ms }); },
    leave: function () { racing = false; send({ t: "leave" }); }
  };
})();
