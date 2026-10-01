mergeInto(LibraryManager.library, {
  SD_Rewarded: function (goPtr) {
    var go = UTF8ToString(goPtr);
    var reply = function (ok) { try { window.unityInstance && window.unityInstance.SendMessage(go, "OnRewarded", ok ? "1" : "0"); } catch (e) {} };
    if (window.SD && window.SD.rewarded) window.SD.rewarded().then(function (ok) { reply(ok); }, function () { reply(false); });
    else reply(false);
  },
  SD_AdsAvailable: function () { return (window.SD && window.SD.adsAvailable && window.SD.adsAvailable()) ? 1 : 0; },
  SD_Gameplay: function (on) { if (window.SD && window.SD.gameplay) window.SD.gameplay(!!on); },
  SD_Event: function (namePtr, value) { if (window.SD && window.SD.track) window.SD.track(UTF8ToString(namePtr), value); },
  SD_Ready: function () { if (window.SD && window.SD.ready) window.SD.ready(); },

  OR_Vibrate: function (ms) { try { if (navigator.vibrate && (!navigator.userActivation || navigator.userActivation.hasBeenActive)) navigator.vibrate(ms); } catch (e) {} },

  OR_ArmShare: function (textPtr) {
    var text = UTF8ToString(textPtr), w = window;
    var url = w.location.origin + w.location.pathname;
    var doShare = function () {
      if (!w.__orPending) return;
      var t = w.__orPending; w.__orPending = null;
      if (navigator.share) navigator.share({ title: "OBBY RUSH", text: t, url: url }).catch(function () {});
      else if (navigator.clipboard) navigator.clipboard.writeText(t + "\n" + url).then(function () { w.orToast && w.orToast("Copied! Paste it anywhere"); });
      if (w.SD && w.SD.track) w.SD.track("share", 0);
    };
    if (!w.__orHooked) {
      w.__orHooked = true;
      ["pointerup", "touchend", "click"].forEach(function (ev) { w.addEventListener(ev, doShare, true); });
    }
    w.__orPending = text;
    setTimeout(doShare, 450);
  },

  OR_RunStart: function (c) { if (window.obby) window.obby.start(UTF8ToString(c)); },
  OR_RunSubmit: function (c, ms, skin, ghost) { if (window.obby) window.obby.submit(UTF8ToString(c), ms, UTF8ToString(skin), UTF8ToString(ghost)); },
  OR_ShowBoard: function (c) { if (window.obby) window.obby.board(UTF8ToString(c)); },
  OR_FetchGhost: function (c) { if (window.obby) window.obby.ghost(UTF8ToString(c)); },
  OR_NetOpen: function (c, skin) { if (window.obby) window.obby.lobby(UTF8ToString(c), UTF8ToString(skin)); },
  OR_NetState: function (x, y, z, r, a, cp) { if (window.obby) window.obby.state(x, y, z, r, a, cp); },
  OR_NetFinish: function (ms) { if (window.obby) window.obby.finish(ms); },
  OR_NetLeave: function () { if (window.obby) window.obby.leave(); }
});
