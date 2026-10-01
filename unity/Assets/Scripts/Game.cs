using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class SaveData
{
    public string course = "garden", skin = "knight";
    public bool muted, howto;
    public int runs, finishes, ghostMode = 1;           // 0 off, 1 my best, 2 world record
    public float[] best = new float[3];                 // seconds, 0 = none
    public int[] stars = new int[3];                    // bitmask of stars ever collected
    public string unlocked = "";                        // skins unlocked early with an ad: "id;id;"
    public int TotalStars() { int n = 0; foreach (var m in stars) for (int b = 0; b < 8; b++) if ((m & (1 << b)) != 0) n++; return n; }
    public bool HasSkin(SkinDef s) => s.stars <= TotalStars() || unlocked.Contains(s.id + ";");
}

// OBBY RUSH: 3D obstacle-course speedruns. Solo against your ghost or the world record, or live with
// up to 8 real players. Times go to a per-course world leaderboard.
public class Game : MonoBehaviour
{
    public static Game I;
    public enum St { Menu, Countdown, Run, Finished }
    public St State = St.Menu;
    public SaveData Save;
    public Camera Cam;
    public Course Course;
    public Player Player;
    public bool Online, Dev;
    public float T;                     // run clock: negative during the countdown
    public float RunTime => Mathf.Max(0, T);
    public int Cp = -1;                 // last checkpoint reached
    public int RunStars;                // bitmask this run
    public int Falls;
    public float FinishTime;
    Transform world;
    Light sun;
    Material ghostMat;
    readonly GhostRecorder rec = new GhostRecorder();
    readonly List<GhostRunner> ghosts = new List<GhostRunner>();
    public readonly List<Puppet> Remotes = new List<Puppet>();
    public readonly List<(string name, float ms, bool me, bool remote)> Results = new List<(string, float, bool, bool)>();
    string wrName; float wrMs; GSample[] wrGhost; string wrSkin; string wrCourse;
    float respawnT, netT, resultsT, camYaw, camPitch = 16f, camDist = 8f, lastDrag;
    bool jumpPressed;
    Vector3 camVel, camLook;

    static readonly string[] Names = { "PIXEL", "NOVA", "ZIGGY", "BLINK", "TURBO", "MOCHI", "COMET", "BOUNCE" };

    // ======================================================================
    void Awake()
    {
        I = this;
        Application.targetFrameRate = -1;
        QualitySettings.shadowDistance = 55f; QualitySettings.shadowCascades = 1;
        QualitySettings.shadowResolution = ShadowResolution.Medium; QualitySettings.antiAliasing = 2;
        QualitySettings.pixelLightCount = 0;
        Physics.autoSyncTransforms = false;
#if UNITY_WEBGL && !UNITY_EDITOR
        WebGLInput.captureAllKeyboardInput = false;
#endif
        var url = Application.absoluteURL ?? "";
        Dev = url.Contains("dev=1");
        DevCam.Install(Dev);
        var json = PlayerPrefs.GetString("or_save", "");
        Save = string.IsNullOrEmpty(json) ? new SaveData() : JsonUtility.FromJson<SaveData>(json) ?? new SaveData();
        if (Save.best == null || Save.best.Length < 3) Save.best = new float[3];
        if (Save.stars == null || Save.stars.Length < 3) Save.stars = new int[3];
        if (Dev && url.Contains("fresh=1")) Save = new SaveData();
        if (Dev && url.Contains("stars=9")) Save.stars = new[] { 7, 7, 7 };

        gameObject.AddComponent<Sfx>();
        Sfx.I.SetMuted(Save.muted);
        new GameObject("WebBridge").AddComponent<WebBridge>();
        Cam = Camera.main;
        Cam.nearClipPlane = 0.2f; Cam.farClipPlane = 400f;
        Cam.clearFlags = CameraClearFlags.SolidColor;
        sun = new GameObject("Sun").AddComponent<Light>();
        sun.type = LightType.Directional; sun.shadows = LightShadows.Soft; sun.shadowStrength = 0.55f;
        sun.shadowBias = 0.04f; sun.shadowNormalBias = 0.35f;
        world = new GameObject("World").transform;
        ghostMat = new Material(Kit.UnlitAlpha) { color = new Color(0.55f, 0.9f, 1f, 0.45f) };
        FX.Init(world);
        new GameObject("UI").AddComponent<UI>().Init();

        Player = Player.Create(Save.skin, world);
        int dc = url.IndexOf("course=");
        if (Dev && dc >= 0 && dc + 7 < url.Length && char.IsDigit(url[dc + 7])) Save.course = Course.Ids[Mathf.Clamp(url[dc + 7] - '0', 0, 2)];
        LoadCourse(Save.course);
        GoMenu();
        if (!Save.howto) UI.I.ShowHowTo();
        WebBridge.Ready();
    }

    public void Persist() { PlayerPrefs.SetString("or_save", JsonUtility.ToJson(Save)); PlayerPrefs.Save(); }

    // ======================================================================
    public void LoadCourse(string id)
    {
        Course?.Destroy();
        Course = Course.Build(Course.Index(id), world);
        Save.course = Course.id;
        Cam.backgroundColor = Course.skyBottom;
        RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = Course.fog; RenderSettings.fogStartDistance = 60f; RenderSettings.fogEndDistance = 220f;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = Course.ambient;
        RenderSettings.ambientEquatorColor = Color.Lerp(Course.ambient, Course.fog, 0.5f);
        RenderSettings.ambientGroundColor = Course.ambient * 0.55f;
        sun.transform.rotation = Quaternion.Euler(48, -35, 0);
        sun.color = Course.sunColor; sun.intensity = 1.15f;
        SkyDome();
        Physics.SyncTransforms();
        wrGhost = null; wrCourse = null;
    }

    GameObject sky;
    void SkyDome()
    {
        if (sky) Destroy(sky);
        // a big inverted sphere with a vertical gradient: horizon colour at the bottom
        sky = Kit.MeshObject("sky", Kit.UVSphere);
        sky.transform.SetParent(world, false);
        sky.transform.localScale = Vector3.one * 700f;
        var t = new Texture2D(1, 64, TextureFormat.RGBA32, false);
        for (int y = 0; y < 64; y++) t.SetPixel(0, y, Color.Lerp(Course.skyBottom, Course.skyTop, Mathf.Clamp01((y / 63f - 0.45f) * 2.2f)));
        t.Apply(); t.wrapMode = TextureWrapMode.Clamp;
        var m = new Material(Kit.UnlitAlpha) { mainTexture = t, color = Color.white, renderQueue = 1000 };   // background: before ghosts
        var mr = sky.GetComponent<MeshRenderer>(); mr.sharedMaterial = m;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
    }

    void ClearRunners()
    {
        foreach (var g in ghosts) g.Destroy();
        ghosts.Clear();
        foreach (var r in Remotes) r.Av.Destroy();
        Remotes.Clear();
        UI.I.ClearTags();
    }

    public void GoMenu()
    {
        State = St.Menu;
        Online = false;
        Time.timeScale = 1;
        ClearRunners();
        Course.ResetRun();
        Player.Teleport(Course.start, Course.startYaw + 180f);   // face the camera, course behind
        Player.SetAnim(Anim.Win);
        // attract: your best run plays behind the menu
        var pb = GhostCodec.Decode(PlayerPrefs.GetString("or_ghost_" + Course.id, ""));
        if (pb != null && pb.Length > 4) ghosts.Add(new GhostRunner(PlayerPrefs.GetString("or_ghostskin_" + Course.id, Save.skin), pb, "YOUR BEST", 0, world, ghostMat));
        T = 0;
        UI.I.ShowMenu();
        WebBridge.Gameplay(false);
        WebBridge.FetchGhost(Course.id);
    }

    // ---------------------------------------------------------------- solo
    public void StartSolo()
    {
        Online = false;
        UI.I.CloseScreens();
        ClearRunners();
        if (Save.ghostMode == 1)
        {
            var pb = GhostCodec.Decode(PlayerPrefs.GetString("or_ghost_" + Course.id, ""));
            if (pb != null && pb.Length > 4) ghosts.Add(new GhostRunner(PlayerPrefs.GetString("or_ghostskin_" + Course.id, Save.skin), pb, "YOUR BEST", Save.best[Course.index], world, ghostMat));
        }
        else if (Save.ghostMode == 2 && wrGhost != null && wrCourse == Course.id)
            ghosts.Add(new GhostRunner(wrSkin, wrGhost, wrName, wrMs / 1000f, world, ghostMat));
        foreach (var g in ghosts) UI.I.NameTag(g.Av.Root.transform, g.Name, UI.Ghosty);
        BeginCountdown();
        WebBridge.Event("run_" + Course.id);
        WebBridge.RunStart(Course.id);
    }

    void BeginCountdown()
    {
        Course.ResetRun();
        Cp = -1; RunStars = 0; Falls = 0; FinishTime = 0; respawnT = 0;
        Results.Clear();
        rec.Clear();
        Player.Teleport(Course.start, Course.startYaw);
        Player.SetAnim(Anim.Idle);
        camYaw = Course.startYaw; camPitch = 16f;
        State = St.Countdown;
        T = -3.4f;
        Save.runs++;
        UI.I.ShowHud(true);
        Sfx.I.StartMusic();
        WebBridge.Gameplay(true);
    }

    public void Restart()
    {
        if (Online) return;
        StartSolo();
    }

    // ---------------------------------------------------------------- online (messages from obby.js)
    [Serializable] class NetPlayer { public string id, name, skin; public int slot; }
    [Serializable] class NetMsg
    {
        public string t, id, course, you, name, msg;
        public float x, y, z, r, ms;
        public int a, c, place;
        public NetPlayer[] players;
    }

    public void OnNet(string json)
    {
        var m = JsonUtility.FromJson<NetMsg>(json);
        if (m == null) return;
        switch (m.t)
        {
            case "solo":
                UI.I.Toast("NOBODY ONLINE YET - RACING THE WORLD RECORD");
                Save.ghostMode = wrGhost != null ? 2 : 1;
                StartSolo();
                break;
            case "start":
                if (m.course != Course.id) LoadCourse(m.course);
                Online = true;
                UI.I.CloseScreens();
                ClearRunners();
                foreach (var p in m.players)
                {
                    if (p.id == m.you) continue;
                    var pu = new Puppet(p.skin, world, null, p.id, p.name);
                    pu.Target(Course.start + SlotOffset(p.slot), Course.startYaw, Anim.Idle, 0.1f);
                    Remotes.Add(pu);
                    UI.I.NameTag(pu.Av.Root.transform, p.name, UI.Cyan);
                }
                BeginCountdown();
                foreach (var p in m.players) if (p.id == m.you) Player.Teleport(Course.start + SlotOffset(p.slot), Course.startYaw);
                WebBridge.Event("run_online_" + Course.id, m.players.Length);
                break;
            case "state":
                foreach (var r in Remotes)
                    if (r.Id == m.id) { r.Target(new Vector3(m.x, m.y, m.z), m.r, m.a, 1f / 12f); r.Checkpoint = m.c; }
                break;
            case "fin":
                foreach (var r in Remotes)
                    if (r.Id == m.id && !r.Finished) { r.Finished = true; r.FinishTime = m.ms / 1000f; UI.I.Toast(r.Name + " FINISHED  #" + m.place); }
                if (State == St.Finished) UI.I.RefreshResults();
                break;
            case "left":
                for (int i = Remotes.Count - 1; i >= 0; i--)
                    if (Remotes[i].Id == m.id) { UI.I.Toast(Remotes[i].Name + " LEFT"); Remotes[i].Av.Destroy(); Remotes.RemoveAt(i); }
                break;
            case "results":
                if (State == St.Finished) UI.I.RefreshResults();
                break;
        }
    }

    static Vector3 SlotOffset(int slot) => new Vector3(((slot % 4) - 1.5f) * 1.2f, 0, -(slot / 4) * 1.4f);

    [Serializable] class GhostMsg { public string course, name, skin, g; public int ms; }
    public void OnGhost(string json)
    {
        var m = JsonUtility.FromJson<GhostMsg>(json);
        if (m == null || string.IsNullOrEmpty(m.g)) return;
        var s = GhostCodec.Decode(m.g);
        if (s == null || s.Length < 5) return;
        wrGhost = s; wrName = m.name; wrMs = m.ms; wrSkin = string.IsNullOrEmpty(m.skin) ? "knight" : m.skin; wrCourse = m.course;
        if (State == St.Menu) UI.I.SetWorldRecord(m.course, m.name, m.ms / 1000f);
    }

    // ======================================================================
    void Update()
    {
        float dt = Mathf.Min(Time.deltaTime, 0.05f);
        if (Input.anyKeyDown || Input.touchCount > 0) Sfx.I.StartMusic();
        if (Input.GetKeyDown(KeyCode.Space)) jumpPressed = true;
        if (UI.I.JumpPressed) jumpPressed = true;

        if (State == St.Menu) { T += dt; if (T > 200f) T = 0; }
        else
        {
            float before = T;
            T += dt;
            if (State == St.Countdown)
            {
                int a = Mathf.CeilToInt(-before - 0.4f), b = Mathf.CeilToInt(-T - 0.4f);
                if (a != b && b >= 0 && b <= 3) { UI.I.CountdownNumber(b == 0 ? "GO!" : b.ToString(), b == 0); Sfx.I.Beep(b == 0); }
                if (T >= 0) { State = St.Run; T = 0; }
            }
        }

        Course.Tick(State == St.Menu ? T : T, dt);
        Physics.SyncTransforms();

        // move input relative to the camera
        var mv = UI.I.Stick;
        float kx = (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) ? 1 : 0) - (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow) ? 1 : 0);
        float kz = (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow) ? 1 : 0) - (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) ? 1 : 0);
        if (kx != 0 || kz != 0) mv = Vector2.ClampMagnitude(new Vector2(kx, kz), 1f);
        var fwd = Quaternion.Euler(0, camYaw, 0) * Vector3.forward;
        var right = Quaternion.Euler(0, camYaw, 0) * Vector3.right;
        var move = fwd * mv.y + right * mv.x;
        if (devPath.Count > 0) DevSteer(ref move);
        else if (devMove.sqrMagnitude > 0.01f) move = devMove;
        bool jumpDown = Input.GetKey(KeyCode.Space) || UI.I.JumpHeld || devJumpHold > 0;
        devJumpHold -= dt;
        bool control = State == St.Run && respawnT <= 0;

        if (State != St.Menu)
        {
            Player.Tick(dt, move, jumpPressed, jumpDown, Course, control);
            if (State == St.Run) RunRules(dt);
            if (State == St.Run || State == St.Finished) rec.Tick(RunTime, Player);
        }
        jumpPressed = false;

        foreach (var g in ghosts) g.Tick(State == St.Menu ? (T % (g.S.Length / GhostCodec.Rate + 2f)) : RunTime);
        foreach (var r in Remotes) r.Tick(dt);

        if (Online && (State == St.Run || State == St.Finished) && (netT -= Time.unscaledDeltaTime) <= 0)
        {
            netT = 1f / 12f;
            var p = Player.transform.position;
            WebBridge.NetState(p.x, p.y, p.z, Player.Yaw, Player.AnimId < 0 ? 0 : Player.AnimId, Cp + 1);
        }
        if (State != St.Menu) UI.I.UpdateHud();
        FX.Tick(dt);

        if (State == St.Finished)
        {
            resultsT -= Time.unscaledDeltaTime;
            if (resultsT <= 0 && resultsT > -1f) { resultsT = -2f; UI.I.ShowResults(); }
        }
    }

    void RunRules(float dt)
    {
        var pos = Player.transform.position;
        // falling off
        if (respawnT > 0)
        {
            respawnT -= dt;
            if (respawnT <= 0)
            {
                var at = Cp >= 0 ? Course.cps[Cp] : null;
                Player.Respawn(at != null ? at.pos : Course.start, at != null ? at.yaw : Course.startYaw);
                camYaw = at != null ? at.yaw : Course.startYaw;
                FX.Burst(Player.transform.position + Vector3.up, Color.white, 14);
            }
            return;
        }
        if (pos.y < Course.killY)
        {
            respawnT = 0.55f; Falls++;
            Sfx.I.Fall();
            WebBridge.Vibrate(60);
            return;
        }
        // checkpoints
        for (int i = Cp + 1; i < Course.cps.Count; i++)
        {
            var c = Course.cps[i];
            var d = pos - c.pos;
            if (Mathf.Abs(d.y) < 2.5f && new Vector2(d.x, d.z).sqrMagnitude < 3.2f * 3.2f)
            {
                Cp = i; c.hit = true;
                Sfx.I.Checkpoint();
                FX.Confetti(c.flag.position + Vector3.up * 2f, 24);
                UI.I.Banner("CHECKPOINT " + (i + 1) + "/" + Course.cps.Count, UI.Time(RunTime) + Split(i));
                WebBridge.Vibrate(30);
            }
        }
        // stars
        for (int i = 0; i < Course.stars.Count; i++)
        {
            var s = Course.stars[i];
            if (!s.gameObject.activeSelf) continue;
            if ((s.position + Vector3.up * 0.5f - (pos + Vector3.up * 0.8f)).sqrMagnitude < 1.25f * 1.25f)
            {
                s.gameObject.SetActive(false);
                RunStars |= 1 << i;
                Sfx.I.Star();
                FX.Burst(s.position + Vector3.up * 0.5f, Kit.Hex("#FFD84A"), 18);
                bool fresh = (Save.stars[Course.index] & (1 << i)) == 0;
                UI.I.Toast(fresh ? "NEW STAR!  " + (Save.TotalStars() + 1) + " TOTAL" : "STAR!");
            }
        }
        // finish line
        if (Course.finishZone.Contains(pos + Vector3.up * 0.8f)) Finish();
    }

    // checkpoint split against the ghost you're racing, if any
    string Split(int cp)
    {
        if (ghosts.Count == 0) return "";
        var g = ghosts[0];
        var c = Course.cps[cp].pos;
        for (int i = 0; i < g.S.Length; i++)
        {
            var d = g.S[i].p - c;
            if (Mathf.Abs(d.y) < 2.5f && new Vector2(d.x, d.z).sqrMagnitude < 3.2f * 3.2f)
            {
                float delta = RunTime - i / GhostCodec.Rate;
                return "   " + (delta <= 0 ? "-" : "+") + Mathf.Abs(delta).ToString("0.00");
            }
        }
        return "";
    }

    void Finish()
    {
        State = St.Finished;
        FinishTime = RunTime;
        resultsT = 2.2f;
        rec.Tick(FinishTime + 0.11f, Player);
        Player.SetAnim(Anim.Win);
        int ci = Course.index;
        float prev = Save.best[ci];
        bool pb = prev <= 0 || FinishTime < prev - 0.0005f;
        int newStars = RunStars & ~Save.stars[ci];
        Save.stars[ci] |= RunStars;
        Save.finishes++;
        if (pb)
        {
            Save.best[ci] = FinishTime;
            PlayerPrefs.SetString("or_ghost_" + Course.id, GhostCodec.Encode(rec.Samples));
            PlayerPrefs.SetString("or_ghostskin_" + Course.id, Save.skin);
        }
        Persist();
        Sfx.I.Finish(pb);
        FX.Confetti(Course.finishPos + Vector3.up * 3.2f, 60);
        UI.I.Banner(pb ? "NEW BEST!" : "FINISH!", UI.Time(FinishTime));
        UI.I.ClearRank();
        WebBridge.RunSubmit(Course.id, Mathf.RoundToInt(FinishTime * 1000), Save.skin, GhostCodec.Encode(rec.Samples));
        if (Online) WebBridge.NetFinish(Mathf.RoundToInt(FinishTime * 1000));
        WebBridge.Event("finish_" + Course.id, Mathf.RoundToInt(FinishTime));
        if (newStars != 0) UI.I.Toast("STARS SAVED: " + Save.TotalStars() + " / 9");
    }

    public List<(string name, float t, int kind)> Standings()
    {
        // kind: 0 you, 1 real player, 2 ghost
        var l = new List<(string, float, int)>();
        l.Add(("YOU", State == St.Finished ? FinishTime : -1, 0));
        foreach (var r in Remotes) l.Add((r.Name, r.Finished ? r.FinishTime : -1, 1));
        foreach (var g in ghosts) if (g.Ms > 0) l.Add((g.Name, g.Ms, 2));
        l.Sort((a, b) => (a.Item2 < 0 ? 9999 : a.Item2).CompareTo(b.Item2 < 0 ? 9999 : b.Item2));
        return l;
    }

    // live race position: by checkpoint, then distance along the course
    public int Place()
    {
        if (!Online) return 0;
        int place = 1;
        float me = Progress(Cp + 1, Player.transform.position);
        foreach (var r in Remotes)
        {
            if (r.Finished) { place++; continue; }
            if (Progress(r.Checkpoint, r.Pos) > me) place++;
        }
        return place;
    }
    float Progress(int cps, Vector3 p) => cps * 1000f + (p - Course.start).magnitude * 0.01f + p.z * 0.001f;

    // ---------------------------------------------------------------- menu actions
    public void SelectCourse(string id)
    {
        if (id == Course.id) return;
        LoadCourse(id);
        Persist();
        GoMenu();
    }

    public void SetSkin(string id)
    {
        Save.skin = id; Persist();
        Player.SetSkin(id);
        Player.SetAnim(Anim.Win);
    }

    public void OpenOnline()
    {
        if (State != St.Menu) GoMenu();
        WebBridge.NetOpen(Course.id, Save.skin);
#if UNITY_EDITOR
        UI.I.Toast("ONLINE NEEDS THE WEB BUILD");
#endif
    }

    public void ToggleMute() { Save.muted = !Save.muted; Sfx.I.SetMuted(Save.muted); Persist(); }

    public void Pause()
    {
        if (State == St.Menu) return;
        if (!Online) Time.timeScale = 0;
        UI.I.ShowPause();
    }
    public void Resume() { Time.timeScale = 1; UI.I.CloseScreens(); }
    public void Quit()
    {
        Time.timeScale = 1;
        if (Online) WebBridge.NetLeave();
        GoMenu();
    }

    public void BackToCheckpoint()
    {
        if (State != St.Run || respawnT > 0) return;
        respawnT = 0.05f;
    }

    public string ShareText()
    {
        return "OBBY RUSH  " + Course.name + " in " + UI.Time(FinishTime) + (Falls == 0 ? " with ZERO falls" : "") + ". Can you beat my ghost?";
    }

    [Serializable] public class RankMsg { public int rank, total, best; public bool newBest; public string error, course; }
    public void OnRank(string json)
    {
        var m = JsonUtility.FromJson<RankMsg>(json);
        if (m == null || m.course != Course.id) return;
        UI.I.SetRank(m);
    }

    // dev: SendMessage("Game", "DevMove", "x,z[,jumpSeconds]") holds a world-space move (and presses jump)
    Vector3 devMove; float devJumpHold;
    public void DevMove(string s)
    {
        if (!Dev) return;
        var p = s.Split(',');
        float F(int i) => float.Parse(p[i], System.Globalization.CultureInfo.InvariantCulture);
        devMove = Vector3.ClampMagnitude(new Vector3(F(0), 0, F(1)), 1f);
        if (p.Length > 2 && F(2) > 0) { jumpPressed = true; devJumpHold = F(2); }
    }
    // dev autopilot: "x,z,j,hold;..." run to each point; j=1 jumps on arrival, hold = seconds to hold jump (2 = double jump)
    readonly List<Vector4> devPath = new List<Vector4>();
    public void DevPath(string s)
    {
        if (!Dev) return;
        devPath.Clear();
        foreach (var w in s.Split(';'))
        {
            var p = w.Split(','); if (p.Length < 2) continue;
            float F(int i) => p.Length > i ? float.Parse(p[i], System.Globalization.CultureInfo.InvariantCulture) : 0;
            devPath.Add(new Vector4(F(0), F(1), F(2), F(3)));
        }
    }
    float devDouble, devJumpOnLand;
    void DevSteer(ref Vector3 move)
    {
        var w = devPath[0];
        var pos = Player.transform.position;
        var d = new Vector3(w.x - pos.x, 0, w.y - pos.z);
        if (devDouble > 0) { devDouble -= Time.deltaTime; if (devDouble <= 0) { jumpPressed = true; devJumpHold = 0.3f; } }
        if (devJumpOnLand > 0 && Player.Grounded) { jumpPressed = true; devJumpHold = devJumpOnLand > 1.5f ? 0.3f : devJumpOnLand; if (devJumpOnLand > 1.5f) devDouble = 0.32f; devJumpOnLand = 0; }
        if (d.magnitude < 0.55f)
        {
            if (w.z > 0)
            {
                // jump points only fire from the ground: passing through one mid-air waits for the landing
                if (Player.Grounded) { jumpPressed = true; devJumpHold = Mathf.Max(0.05f, w.w > 1.5f ? 0.3f : w.w); if (w.w > 1.5f) devDouble = 0.32f; }
                else devJumpOnLand = Mathf.Max(0.05f, w.w);
            }
            devPath.RemoveAt(0);
            if (devPath.Count == 0) { devMove = Vector3.zero; return; }
            w = devPath[0]; d = new Vector3(w.x - pos.x, 0, w.y - pos.z);
        }
        move = d.normalized;
    }

    public string DevState() => Player.transform.position.ToString("F2") + " cp=" + Cp + " t=" + RunTime.ToString("F2") + " st=" + State;
    public void DevLog(string _) { if (Dev) Debug.Log("[DEV] " + DevState()); }

    // dev: SendMessage("Game", "DevWarp", "2") jumps to checkpoint 2 (0 = start)
    public void DevWarp(string s)
    {
        if (!Dev) return;
        int i = int.Parse(s) - 1;
        if (i < 0) { Player.Teleport(Course.start, Course.startYaw); return; }
        if (i >= Course.cps.Count) { Player.Teleport(Course.finishPos - Quaternion.Euler(0, Course.finishYaw, 0) * Vector3.forward * 2.5f, Course.finishYaw); return; }
        Cp = i; Player.Teleport(Course.cps[i].pos, Course.cps[i].yaw); camYaw = Course.cps[i].yaw;
    }

    // ---------------------------------------------------------------- camera
    public void CameraDrag(Vector2 delta)
    {
        camYaw += delta.x * 0.22f;
        camPitch = Mathf.Clamp(camPitch - delta.y * 0.12f, -10f, 60f);
        lastDrag = Time.unscaledTime;
    }

    void LateUpdate()
    {
        float dt = Time.deltaTime;
        float aspect = (float)Screen.width / Mathf.Max(1, Screen.height);
        bool portrait = aspect < 0.8f;

        if (State == St.Menu)
        {
            // slow orbit around the start, showing your character and the course behind
            // low hero angle swinging round the front of the runner, course stretching away behind
            float a = Mathf.Sin(Time.time * 0.15f) * 0.9f + Mathf.PI;
            var focus = Course.start + Vector3.up * 1.0f;
            var want = focus + new Vector3(Mathf.Sin(a) * 6.5f, 1.3f + Mathf.Sin(Time.time * 0.21f) * 0.4f, Mathf.Cos(a) * 6.5f);
            if (portrait) want = focus + (want - focus) * 1.25f + Vector3.up * 1.5f;
            Cam.transform.position = Vector3.SmoothDamp(Cam.transform.position, want, ref camVel, 0.8f);
            // landscape: the menu column sits on the right, so frame the runner on the left
            var look = focus + Vector3.up * (portrait ? -0.6f : 0.8f) + (UI.Landscape ? Cam.transform.right * 2.6f : Vector3.zero);
            Cam.transform.rotation = Quaternion.LookRotation(look - Cam.transform.position);
            Cam.fieldOfView = portrait ? 62f : 48f;
            sky.transform.position = Cam.transform.position;
            return;
        }

        // auto-swing behind the runner when moving (unless the player has been steering the camera)
        var vel = new Vector3(Player.Vel.x, 0, Player.Vel.z);
        if (Time.unscaledTime - lastDrag > 1.2f && vel.sqrMagnitude > 4f && State == St.Run)
        {
            float target = Mathf.Atan2(vel.x, vel.z) * Mathf.Rad2Deg;
            float diff = Mathf.DeltaAngle(camYaw, target);
            if (Mathf.Abs(diff) < 135f) camYaw += diff * (1f - Mathf.Exp(-dt * 1.4f));
        }
        if (Input.GetKey(KeyCode.Q)) camYaw -= 120f * dt;
        if (Input.GetKey(KeyCode.E)) camYaw += 120f * dt;

        float dist = portrait ? 10.5f : 8f;
        var focusP = Player.transform.position + Vector3.up * 1.4f;
        camLook = Vector3.Lerp(camLook, focusP, 1f - Mathf.Exp(-dt * 14f));
        if ((camLook - focusP).sqrMagnitude > 25f) camLook = focusP;
        var rot = Quaternion.Euler(camPitch + (portrait ? 8f : 0f), camYaw, 0);
        var pos = camLook - rot * Vector3.forward * dist;
        // don't dip under the floor the runner is standing on
        pos.y = Mathf.Max(pos.y, Player.transform.position.y + 0.6f);
        Cam.transform.position = pos;
        Cam.transform.rotation = Quaternion.LookRotation(camLook - pos);
        Cam.fieldOfView = Mathf.Lerp(Cam.fieldOfView, (portrait ? 66f : 55f) + Player.Speed01 * 4f, 1f - Mathf.Exp(-dt * 4f));
        sky.transform.position = Cam.transform.position;
        // fade other runners that crowd the camera
        foreach (var r in Remotes) r.Av.SetVisible((r.Pos - Cam.transform.position).sqrMagnitude > 2.5f * 2.5f);
    }
}
