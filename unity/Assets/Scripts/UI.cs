using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UI : MonoBehaviour
{
    public static UI I;
    Canvas canvas; CanvasScaler scaler;
    RectTransform root, hud, screens, controls, tags;
    Font F => Kit.Font;
    public static readonly Color Ink = Kit.Hex("#1B1340"), Pink = Kit.Hex("#FF4F8B"), Cyan = Kit.Hex("#35D6FF"), Gold = Kit.Hex("#FFD84A"),
        Lime = Kit.Hex("#7CF06B"), Purple = Kit.Hex("#8E6BFF"), Soft = new Color(1, 1, 1, 0.7f), Ghosty = Kit.Hex("#A8F0FF");

    // input exposed to Game
    public Vector2 Stick => stick ? stick.Value : Vector2.zero;
    public bool JumpHeld => jumpBtn && jumpBtn.Held;
    public bool JumpPressed { get { if (jumpBtn && jumpBtn.Pressed) { jumpBtn.Pressed = false; return true; } return false; } }
    VirtualStick stick; HoldButton jumpBtn;

    // hud
    Text timeText, deltaText, cpText, placeText, bannerText, bannerSub, countText, toastText, rankText, keysHint, wrText;
    float bannerT, countT, toastT;
    readonly List<(Transform t, Text x)> nameTags = new List<(Transform, Text)>();

    static readonly Dictionary<string, Sprite> icons = new Dictionary<string, Sprite>();
    public static Sprite Icon(string n) { if (!icons.TryGetValue(n, out var s)) icons[n] = s = Resources.Load<Sprite>("Icons/" + n); return s; }
    static Sprite disc, ring, starSpr;
    readonly Image[] hudStars = new Image[3];
    static readonly Color Dim = new Color(0.12f, 0.08f, 0.32f, 0.62f);   // secondary buttons: readable on bright skies
    static Sprite Spr(Texture2D t) => Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(.5f, .5f));

    public void Init()
    {
        I = this;
        var es = new GameObject("EventSystem"); es.AddComponent<EventSystem>().pixelDragThreshold = 4; es.AddComponent<StandaloneInputModule>();
        canvas = gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1080, 1920);
        gameObject.AddComponent<GraphicRaycaster>();
        root = (RectTransform)transform;
        disc = Spr(Kit.Disc); ring = Spr(Kit.Ring); starSpr = Spr(StarTex());

        tags = Fill("tags", root);
        BuildHud();
        screens = Fill("screens", root);

        countText = Txt(root, "", 280, new Vector2(.5f, .6f), Vector2.zero, Color.white, TextAnchor.MiddleCenter, 1000);
        countText.fontStyle = FontStyle.BoldAndItalic; Outline(countText, 7); countText.gameObject.SetActive(false);
        bannerText = Txt(root, "", 100, new Vector2(.5f, .7f), Vector2.zero, Gold, TextAnchor.MiddleCenter, 1400);
        bannerText.fontStyle = FontStyle.BoldAndItalic; Outline(bannerText, 5);
        bannerSub = Txt(root, "", 46, new Vector2(.5f, .7f), new Vector2(0, -92), Color.white, TextAnchor.MiddleCenter, 1400);
        Outline(bannerSub, 3);
        bannerText.gameObject.SetActive(false); bannerSub.gameObject.SetActive(false);
        toastText = Txt(root, "", 40, new Vector2(.5f, 1), new Vector2(0, -360), Color.white, TextAnchor.MiddleCenter, 1200);
        Outline(toastText, 3); toastText.gameObject.SetActive(false);
    }

    // ---------------------------------------------------------------- building blocks
    RectTransform Rect(string n, Transform p, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(n, typeof(RectTransform)); go.transform.SetParent(p, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = anchor; rt.pivot = new Vector2(.5f, .5f); rt.anchoredPosition = pos; rt.sizeDelta = size;
        return rt;
    }
    RectTransform Fill(string n, Transform p)
    {
        var rt = Rect(n, p, Vector2.zero, Vector2.zero, Vector2.zero);
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero; return rt;
    }
    RectTransform Box(Transform p, Vector2 anchor, Vector2 pos, Vector2 size, Color c, bool ray = false)
    {
        var rt = Rect("box", p, anchor, pos, size);
        var img = rt.gameObject.AddComponent<Image>(); img.sprite = Kit.RoundedSprite; img.type = Image.Type.Sliced; img.color = c; img.raycastTarget = ray;
        return rt;
    }
    Image Img(Transform p, Sprite s, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        var rt = Rect("img", p, anchor, pos, size);
        var img = rt.gameObject.AddComponent<Image>(); img.sprite = s; img.preserveAspect = true; img.raycastTarget = false; return img;
    }
    Text Txt(Transform p, string s, int size, Vector2 anchor, Vector2 pos, Color c, TextAnchor align = TextAnchor.MiddleCenter, float w = 700)
    {
        var rt = Rect("txt", p, anchor, pos, new Vector2(w, size * 1.4f));
        var t = rt.gameObject.AddComponent<Text>();
        t.font = F; t.fontSize = size; t.fontStyle = FontStyle.Bold; t.alignment = align; t.color = c; t.text = s;
        t.raycastTarget = false; t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
        return t;
    }
    static void Outline(Text t, float d) { var o = t.gameObject.AddComponent<Outline>(); o.effectColor = new Color(0.08f, 0.04f, 0.2f, 0.85f); o.effectDistance = new Vector2(d, -d); }
    Button Btn(Transform p, string label, Vector2 anchor, Vector2 pos, Vector2 size, Color bg, Color fg, Action onClick, int fs = 48)
    {
        var rt = Box(p, anchor, pos, size, bg, true);
        // chunky toy-button lip
        var lip = Box(rt, new Vector2(.5f, 0), new Vector2(0, -6), new Vector2(size.x, 16), Color.Lerp(bg, Color.black, 0.35f));
        lip.SetAsFirstSibling(); lip.pivot = new Vector2(.5f, 0);
        var b = rt.gameObject.AddComponent<Button>(); b.targetGraphic = rt.GetComponent<Image>();
        b.onClick.AddListener(() => { Sfx.I.Click(); onClick(); });
        var t = Txt(rt, label, fs, new Vector2(.5f, .5f), Vector2.zero, fg, TextAnchor.MiddleCenter, size.x);
        t.fontStyle = FontStyle.BoldAndItalic;
        return b;
    }

    // Five-point star, anti-aliased (the built-in font has no star glyph).
    static Texture2D StarTex()
    {
        int n = 96; var t = new Texture2D(n, n, TextureFormat.RGBA32, false); var px = new Color[n * n];
        var pts = new Vector2[10];
        for (int i = 0; i < 10; i++)
        {
            float a = Mathf.PI / 2f + i * Mathf.PI / 5f, r = i % 2 == 0 ? 0.48f : 0.21f;
            pts[i] = new Vector2(0.5f + Mathf.Cos(a) * r, 0.47f + Mathf.Sin(a) * r);
        }
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                var q = new Vector2((x + .5f) / n, (y + .5f) / n);
                bool inside = false; float dmin = 9f;
                for (int i = 0, j = 9; i < 10; j = i++)
                {
                    var a = pts[i]; var b = pts[j];
                    if ((a.y > q.y) != (b.y > q.y) && q.x < (b.x - a.x) * (q.y - a.y) / (b.y - a.y) + a.x) inside = !inside;
                    var ab = b - a; float k = Mathf.Clamp01(Vector2.Dot(q - a, ab) / ab.sqrMagnitude);
                    dmin = Mathf.Min(dmin, (q - (a + ab * k)).magnitude);
                }
                float sd = (inside ? -dmin : dmin) * n;
                px[y * n + x] = new Color(1, 1, 1, Mathf.Clamp01(0.5f - sd));
            }
        t.SetPixels(px); t.Apply(); return t;
    }

    RectTransform StarsRow(Transform p, int mask, int count, Vector2 anchor, Vector2 pos, float size, Color on, Color off)
    {
        var row = Rect("stars", p, anchor, pos, new Vector2(size * count * 1.1f, size));
        for (int i = 0; i < count; i++)
        {
            var im = Img(row, starSpr, new Vector2(.5f, .5f), new Vector2((i - (count - 1) / 2f) * size * 1.1f, 0), new Vector2(size, size));
            im.color = (mask & (1 << i)) != 0 ? on : off;
        }
        return row;
    }
    Image StarIcon(Transform p, Vector2 anchor, Vector2 pos, float size, Color c) { var im = Img(p, starSpr, anchor, pos, new Vector2(size, size)); im.color = c; return im; }

    // ---------------------------------------------------------------- HUD
    void BuildHud()
    {
        hud = Fill("hud", root);
        controls = Fill("controls", hud);

        // camera drag: the whole screen underneath everything else
        var camZone = Fill("camzone", controls);
        camZone.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0);
        camZone.gameObject.AddComponent<CamDrag>();

        // touch: floating stick on the left 45%, jump on the right
        var sz = Rect("stickzone", controls, Vector2.zero, Vector2.zero, Vector2.zero);
        sz.anchorMin = Vector2.zero; sz.anchorMax = new Vector2(0.45f, 0.62f); sz.offsetMin = sz.offsetMax = Vector2.zero;
        sz.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0);
        stick = sz.gameObject.AddComponent<VirtualStick>();
        var baseImg = Img(controls, ring, Vector2.zero, new Vector2(240, 280), new Vector2(260, 260)); baseImg.color = new Color(1, 1, 1, 0.45f);
        var knob = Img(controls, disc, Vector2.zero, new Vector2(240, 280), new Vector2(120, 120)); knob.color = new Color(1, 1, 1, 0.7f);
        stick.Base = baseImg.rectTransform; stick.Knob = knob.rectTransform; stick.Home = new Vector2(240, 280);

        var j = Rect("jump", controls, new Vector2(1, 0), new Vector2(-220, 250), new Vector2(270, 270));
        var jImg = j.gameObject.AddComponent<Image>(); jImg.sprite = disc; jImg.color = Kit.A(Pink, 0.75f);
        jumpBtn = j.gameObject.AddComponent<HoldButton>();
        var jr = Img(j, ring, new Vector2(.5f, .5f), Vector2.zero, new Vector2(280, 280)); jr.color = Color.white;
        var jt = Txt(j, "JUMP", 50, new Vector2(.5f, .5f), Vector2.zero, Color.white); jt.fontStyle = FontStyle.BoldAndItalic; Outline(jt, 3);

        keysHint = Txt(hud, "MOVE  WASD / ARROWS     JUMP  SPACE (x2 in the air)     CAMERA  DRAG or Q / E     R  CHECKPOINT", 24, new Vector2(.5f, 0), new Vector2(0, 36), new Color(1, 1, 1, 0.55f), TextAnchor.MiddleCenter, 1800);
        Outline(keysHint, 2);

        // top: timer + delta
        var tb = Box(hud, new Vector2(.5f, 1), new Vector2(0, -95), new Vector2(430, 120), new Color(0.1f, 0.06f, 0.25f, 0.55f));
        timeText = Txt(tb, "0:00.00", 80, new Vector2(.5f, .5f), new Vector2(0, 2), Color.white, TextAnchor.MiddleCenter, 430);
        timeText.fontStyle = FontStyle.BoldAndItalic; Outline(timeText, 3);
        deltaText = Txt(hud, "", 40, new Vector2(.5f, 1), new Vector2(0, -190), Lime, TextAnchor.MiddleCenter, 600);
        deltaText.fontStyle = FontStyle.BoldAndItalic; Outline(deltaText, 3);

        // top-left: checkpoint + stars
        cpText = Txt(hud, "", 40, new Vector2(0, 1), new Vector2(480, -78), Color.white, TextAnchor.MiddleLeft, 400);
        cpText.fontStyle = FontStyle.BoldAndItalic; Outline(cpText, 3);
        for (int i = 0; i < 3; i++) hudStars[i] = StarIcon(hud, new Vector2(0, 1), new Vector2(305 + i * 52, -132), 48, Gold);
        placeText = Txt(hud, "", 110, new Vector2(1, 1), new Vector2(-150, -110), Gold, TextAnchor.MiddleCenter, 300);
        placeText.fontStyle = FontStyle.BoldAndItalic; Outline(placeText, 5);

        Btn(hud, "II", new Vector2(0, 1), new Vector2(80, -100), new Vector2(110, 110), new Color(0.1f, 0.06f, 0.25f, 0.6f), Color.white, () => Game.I.Pause(), 46);
        Btn(hud, "<<", new Vector2(0, 1), new Vector2(205, -100), new Vector2(110, 110), new Color(0.1f, 0.06f, 0.25f, 0.6f), Gold, () => Game.I.BackToCheckpoint(), 40);
        hud.gameObject.SetActive(false);
    }

    public void ShowHud(bool on)
    {
        hud.gameObject.SetActive(on);
        bool touch = Application.isMobilePlatform || Input.touchSupported;
        stick.gameObject.SetActive(touch); stick.Base.gameObject.SetActive(touch); stick.Knob.gameObject.SetActive(touch);
        jumpBtn.gameObject.SetActive(touch);
        keysHint.gameObject.SetActive(!touch);
    }

    public void UpdateHud()
    {
        var g = Game.I;
        timeText.text = Time(g.State == Game.St.Finished ? g.FinishTime : g.RunTime);
        int n = g.Course.cps.Count;
        cpText.text = "FLAG  " + (g.Cp + 1) + "/" + n;
        for (int i = 0; i < hudStars.Length; i++)
        {
            hudStars[i].gameObject.SetActive(i < g.Course.stars.Count);
            hudStars[i].color = (g.RunStars & (1 << i)) != 0 ? Gold : new Color(1, 1, 1, 0.28f);
        }
        int place = g.Place();
        placeText.gameObject.SetActive(place > 0);
        if (place > 0) { placeText.text = "#" + place; placeText.color = place == 1 ? Gold : Color.white; }
        if (Input.GetKeyDown(KeyCode.R)) g.BackToCheckpoint();
        if (Input.GetKeyDown(KeyCode.Escape)) g.Pause();
    }

    public void Delta(float d)
    {
        deltaText.text = (d <= 0 ? "-" : "+") + Mathf.Abs(d).ToString("0.00");
        deltaText.color = d <= 0 ? Lime : Pink;
    }

    public void CountdownNumber(string s, bool go)
    {
        countText.text = s; countText.color = go ? Lime : s == "1" ? Gold : Color.white;
        countT = 0.85f;
        countText.gameObject.SetActive(true);
    }

    public void Banner(string title, string sub)
    {
        bannerText.text = title; bannerSub.text = sub; bannerT = 2.0f;
        bannerText.gameObject.SetActive(true); bannerSub.gameObject.SetActive(!string.IsNullOrEmpty(sub));
        deltaText.text = "";
    }

    public void Toast(string s) { toastText.text = s; toastT = 2.6f; toastText.gameObject.SetActive(true); }

    public void NameTag(Transform t, string name, Color c)
    {
        var x = Txt(tags, name, 30, Vector2.zero, Vector2.zero, c, TextAnchor.MiddleCenter, 400);
        Outline(x, 2);
        nameTags.Add((t, x));
    }
    public void ClearTags() { foreach (var n in nameTags) if (n.x) Destroy(n.x.gameObject); nameTags.Clear(); }

    // ---------------------------------------------------------------- screens
    RectTransform Screen(bool dim = true)
    {
        foreach (Transform c in screens) Destroy(c.gameObject);
        var s = Fill("screen", screens);
        if (dim) { var img = s.gameObject.AddComponent<Image>(); img.color = new Color(0.08f, 0.04f, 0.2f, 0.8f); }
        return s;
    }

    public void CloseScreens() { foreach (Transform c in screens) Destroy(c.gameObject); rankText = null; wrText = null; }

    IEnumerator Pop(RectTransform r, float delay = 0)
    {
        r.localScale = Vector3.zero;
        float k = -delay / 0.28f;
        while (k < 1f) { k += UnityEngine.Time.unscaledDeltaTime / 0.28f; r.localScale = Vector3.one * Kit.EaseOutBack(Mathf.Clamp01(k)); yield return null; }
        r.localScale = Vector3.one;
    }
    IEnumerator Pulse(Transform t)
    {
        while (t) { t.localScale = Vector3.one * (1f + Mathf.Sin(UnityEngine.Time.unscaledTime * 4f) * 0.035f); yield return null; }
    }
    IEnumerator Bob(Transform t, float phase)
    {
        var p0 = ((RectTransform)t).anchoredPosition;
        while (t) { ((RectTransform)t).anchoredPosition = p0 + new Vector2(0, Mathf.Sin(UnityEngine.Time.unscaledTime * 2.2f + phase) * 10f); yield return null; }
    }

    Text Title(Transform p, string s, float y, int size, Color c)
    {
        Txt(p, s, size, new Vector2(.5f, 1), new Vector2(7, y - 9), new Color(0.1f, 0.05f, 0.3f, 0.8f), TextAnchor.MiddleCenter, 1400).fontStyle = FontStyle.BoldAndItalic;
        var t = Txt(p, s, size, new Vector2(.5f, 1), new Vector2(0, y), c, TextAnchor.MiddleCenter, 1400);
        t.fontStyle = FontStyle.BoldAndItalic;
        return t;
    }


    public void ShowMenu()
    {
        ShowHud(false);
        var s = MenuPanel(Screen(false));
        var g = Game.I;
        // logo: each letter its own colour, gently bobbing
        string logo = "OBBY"; Color[] lc = { Pink, Gold, Lime, Cyan };
        for (int i = 0; i < logo.Length; i++)
        {
            var l = Title(s, logo[i].ToString(), -170, 200, lc[i]);
            l.rectTransform.anchoredPosition = new Vector2(-195 + i * 130, -170);
            var sh = (RectTransform)l.transform.parent.GetChild(l.transform.GetSiblingIndex() - 1);
            sh.anchoredPosition = new Vector2(-188 + i * 130, -179);
            StartCoroutine(Bob(l.transform, i * 0.8f)); StartCoroutine(Bob(sh, i * 0.8f));
        }
        Title(s, "RUSH", -350, 170, Color.white);
        var tag = Txt(s, "3D OBBY SPEEDRUNS  -  RACE LIVE ONLINE", 32, new Vector2(.5f, 1), new Vector2(0, -455), Gold, TextAnchor.MiddleCenter, 1000);
        Outline(tag, 2);

        // course cards
        for (int i = 0; i < Course.Ids.Length; i++)
        {
            bool sel = i == g.Course.index;
            var card = Box(s, new Vector2(.5f, 0), new Vector2((i - 1) * 345, 900), new Vector2(330, 290), sel ? Kit.A(Course.Accent[i], 0.95f) : new Color(0.1f, 0.06f, 0.25f, 0.6f), true);
            var nm = Txt(card, Course.Names[i].Replace(" ", "\n"), 46, new Vector2(.5f, .5f), new Vector2(0, 70), sel ? Ink : Color.white, TextAnchor.MiddleCenter, 320);
            nm.fontStyle = FontStyle.BoldAndItalic; nm.lineSpacing = 0.85f;
            Txt(card, Course.Tags[i].Split(' ')[0], 26, new Vector2(.5f, .5f), new Vector2(0, -10), sel ? Kit.A(Ink, 0.75f) : Soft, TextAnchor.MiddleCenter, 320);
            float best = g.Save.best[i];
            Txt(card, best > 0 ? Time(best) : "--:--", 40, new Vector2(.5f, .5f), new Vector2(0, -62), sel ? Ink : Color.white, TextAnchor.MiddleCenter, 320).fontStyle = FontStyle.BoldAndItalic;
            StarsRow(card, g.Save.stars[i], 3, new Vector2(.5f, .5f), new Vector2(0, -112), 44, sel ? Ink : Gold, sel ? Kit.A(Ink, 0.25f) : new Color(1, 1, 1, 0.25f));
            var btn = card.gameObject.AddComponent<Button>(); btn.targetGraphic = card.GetComponent<Image>();
            var id = Course.Ids[i];
            btn.onClick.AddListener(() => { Sfx.I.Click(); Game.I.SelectCourse(id); });
        }
        wrText = Txt(s, "", 30, new Vector2(.5f, 0), new Vector2(0, 725), Soft, TextAnchor.MiddleCenter, 1000);
        Outline(wrText, 2);

        var play = Btn(s, "PLAY", new Vector2(.5f, 0), new Vector2(-235, 560), new Vector2(450, 170), Pink, Color.white, () => g.StartSolo(), 80);
        StartCoroutine(Pulse(play.transform));
        Btn(s, "RACE LIVE", new Vector2(.5f, 0), new Vector2(235, 560), new Vector2(450, 170), Cyan, Ink, () => g.OpenOnline(), 62);
        string[] gm = { "GHOST: OFF", "GHOST: MY BEST", "GHOST: WORLD #1" };
        Btn(s, gm[Mathf.Clamp(g.Save.ghostMode, 0, 2)], new Vector2(.5f, 0), new Vector2(-235, 400), new Vector2(450, 115), Dim, Ghosty, () => { g.Save.ghostMode = (g.Save.ghostMode + 1) % 3; g.Persist(); ShowMenu(); }, 36);
        var sk = Btn(s, "SKINS   " + g.Save.TotalStars(), new Vector2(.5f, 0), new Vector2(235, 400), new Vector2(450, 115), Dim, Gold, ShowSkins, 40);
        StarIcon(sk.transform, new Vector2(1, .5f), new Vector2(-70, 2), 52, Gold);
        Btn(s, "LEADERBOARD", new Vector2(.5f, 0), new Vector2(0, 265), new Vector2(920, 110), Dim, Gold, () => WebBridge.ShowBoard(g.Course.id), 44);
        Btn(s, "HOW TO PLAY", new Vector2(.5f, 0), new Vector2(-235, 130), new Vector2(450, 100), Dim, Color.white, ShowHowTo, 36);
        Btn(s, g.Save.muted ? "SOUND OFF" : "SOUND ON", new Vector2(.5f, 0), new Vector2(235, 130), new Vector2(450, 100), Dim, Color.white, () => { g.ToggleMute(); ShowMenu(); }, 36);
        ApplyWr();
    }

    // The menu is a portrait column: centred on phones, pushed to the right in landscape so the runner shows.
    public static bool Landscape => (float)UnityEngine.Screen.width / Mathf.Max(1, UnityEngine.Screen.height) > 1.05f;
    RectTransform MenuPanel(RectTransform s)
    {
        var p = Rect("panel", s, new Vector2(.5f, .5f), Vector2.zero, new Vector2(1080, 1920));
        if (Landscape) { p.anchorMin = p.anchorMax = new Vector2(1, .5f); p.pivot = new Vector2(1, .5f); p.anchoredPosition = new Vector2(-30, 0); }
        return p;
    }
    float lastAspect;

    string wrCourse, wrName; float wrTime;
    public void SetWorldRecord(string course, string name, float t) { wrCourse = course; wrName = name; wrTime = t; ApplyWr(); }
    void ApplyWr()
    {
        if (!wrText) return;
        wrText.text = wrCourse == Game.I.Course.id && wrTime > 0 ? "WORLD RECORD  " + Time(wrTime) + "  by " + wrName : "";
    }

    public void ShowSkins()
    {
        var s = Screen();
        var g = Game.I;
        Title(s, "SKINS", -150, 110, Gold);
        int total = g.Save.TotalStars();
        Txt(s, total + "        COLLECTED  -  find stars on every course", 34, new Vector2(.5f, 1), new Vector2(0, -255), Color.white, TextAnchor.MiddleCenter, 1000);
        StarIcon(s, new Vector2(.5f, 1), new Vector2(-305, -255), 44, Gold);
        for (int i = 0; i < Skins.All.Length; i++)
        {
            var sk = Skins.All[i];
            bool owned = g.Save.HasSkin(sk), sel = sk.id == g.Save.skin;
            var card = Box(s, new Vector2(.5f, 1), new Vector2((i % 3 - 1) * 330, -480 - (i / 3) * 330), new Vector2(310, 310), sel ? Kit.A(Pink, 0.9f) : owned ? new Color(1, 1, 1, 0.14f) : new Color(0, 0, 0, 0.35f), true);
            var ic = Img(card, Icon(sk.id), new Vector2(.5f, .5f), new Vector2(0, 28), new Vector2(230, 230));
            if (!owned) ic.color = new Color(0.15f, 0.12f, 0.3f, 1f);
            Txt(card, sk.name, 32, new Vector2(.5f, 0), new Vector2(0, 34), Color.white, TextAnchor.MiddleCenter, 310).fontStyle = FontStyle.BoldAndItalic;
            if (!owned)
            {
                Txt(card, sk.stars.ToString(), 70, new Vector2(.5f, .5f), new Vector2(-30, 40), Gold, TextAnchor.MiddleCenter, 160).fontStyle = FontStyle.BoldAndItalic;
                StarIcon(card, new Vector2(.5f, .5f), new Vector2(40, 42), 64, Gold);
            }
            var b = card.gameObject.AddComponent<Button>(); b.targetGraphic = card.GetComponent<Image>();
            var id = sk.id;
            b.onClick.AddListener(() =>
            {
                Sfx.I.Click();
                if (owned) { g.SetSkin(id); ShowSkins(); return; }
                if (WebBridge.AdsAvailable)
                    WebBridge.I.ShowRewarded(ok => { if (ok) { g.Save.unlocked += id + ";"; g.SetSkin(id); } ShowSkins(); });
                else Toast("COLLECT " + (Skins.Get(id).stars - total) + " MORE STARS TO UNLOCK");
            });
        }
        if (WebBridge.AdsAvailable) Txt(s, "Tap a locked skin to unlock it now with a short ad", 28, new Vector2(.5f, 0), new Vector2(0, 330), Soft, TextAnchor.MiddleCenter, 1000);
        Btn(s, "DONE", new Vector2(.5f, 0), new Vector2(0, 190), new Vector2(520, 140), Pink, Color.white, ShowMenu, 56);
    }

    public void ShowHowTo()
    {
        var s = Screen();
        Title(s, "HOW TO PLAY", -200, 100, Gold);
        string[] rows =
        {
            "RUN to the finish as fast as you can",
            "JUMP - and JUMP AGAIN in the air\n(tap lightly for a small hop)",
            "FLAGS are checkpoints: fall off and\nyou pop back at the last one",
            "SPRINGS launch you  -  ARROWS push you\nRED TILES crumble  -  dodge the SWEEPERS",
            "3 hidden STARS per course unlock skins",
            "Race YOUR GHOST, the WORLD RECORD,\nor up to 8 real players LIVE",
            "Touch: left thumb moves, drag right to look\nKeys: WASD + SPACE, Q / E camera, R checkpoint",
        };
        for (int i = 0; i < rows.Length; i++)
        {
            var row = Box(s, new Vector2(.5f, 1), new Vector2(0, -370 - i * 170), new Vector2(960, 150), new Color(1, 1, 1, 0.09f));
            var t = Txt(row, rows[i], 34, new Vector2(.5f, .5f), Vector2.zero, Color.white, TextAnchor.MiddleCenter, 920);
            t.lineSpacing = 1.1f;
            StartCoroutine(Pop(row, 0.05f * i));
        }
        Btn(s, "LET'S GO", new Vector2(.5f, 0), new Vector2(0, 160), new Vector2(560, 150), Pink, Color.white, () =>
        {
            Game.I.Save.howto = true; Game.I.Persist();
            if (Game.I.State == Game.St.Menu) ShowMenu(); else CloseScreens();
        }, 60);
    }

    public void ShowPause()
    {
        var s = Screen();
        var g = Game.I;
        Title(s, g.Online ? "MENU" : "PAUSED", -520, 120, Color.white);
        if (g.Online) Txt(s, "The race keeps going!", 36, new Vector2(.5f, 1), new Vector2(0, -640), Soft);
        Btn(s, "RESUME", new Vector2(.5f, .5f), new Vector2(0, 160), new Vector2(600, 160), Lime, Ink, () => g.Resume(), 64);
        if (!g.Online)
            Btn(s, "RESTART", new Vector2(.5f, .5f), new Vector2(0, -40), new Vector2(600, 130), new Color(1, 1, 1, 0.15f), Color.white, () => { g.Resume(); g.Restart(); }, 48);
        Btn(s, "QUIT", new Vector2(.5f, .5f), new Vector2(0, -210), new Vector2(600, 130), new Color(1, 1, 1, 0.15f), Pink, () => { g.Resume(); g.Quit(); }, 48);
    }

    RectTransform resultsList;
    public void ShowResults()
    {
        ShowHud(false);
        var s = Screen();
        var g = Game.I;
        int ci = g.Course.index;
        bool pb = Mathf.Abs(g.Save.best[ci] - g.FinishTime) < 0.001f;
        Title(s, pb ? "NEW BEST!" : "FINISHED!", -210, 130, pb ? Gold : Color.white);
        var big = Txt(s, Time(g.FinishTime), 150, new Vector2(.5f, 1), new Vector2(0, -380), Color.white, TextAnchor.MiddleCenter, 1000);
        big.fontStyle = FontStyle.BoldAndItalic; Outline(big, 5);
        Txt(s, g.Course.name + "   -   " + (g.Falls == 0 ? "NO FALLS!" : g.Falls + (g.Falls == 1 ? " FALL" : " FALLS")), 36, new Vector2(.5f, 1), new Vector2(0, -490), Gold, TextAnchor.MiddleCenter, 1000);
        StarsRow(s, g.RunStars, g.Course.stars.Count, new Vector2(.5f, 1), new Vector2(0, -548), 48, Gold, new Color(1, 1, 1, 0.25f));
        rankText = Txt(s, pb ? "Saving your ghost..." : "PERSONAL BEST  " + Time(g.Save.best[ci]), 36, new Vector2(.5f, 1), new Vector2(0, -610), pb ? Lime : Soft, TextAnchor.MiddleCenter, 1000);
        if (lastRank != null) ApplyRank();
        resultsList = Rect("list", s, new Vector2(.5f, 1), new Vector2(0, -700), new Vector2(940, 10));
        RefreshResults();

        float y = 520;
        var again = Btn(s, g.Online ? "RACE AGAIN" : "RETRY", new Vector2(.5f, 0), new Vector2(-235, y), new Vector2(450, 160), Pink, Color.white, () => { if (g.Online) g.OpenOnline(); else g.StartSolo(); }, 64);
        StartCoroutine(Pulse(again.transform));
        int next = (ci + 1) % Course.Ids.Length;
        Btn(s, "NEXT: " + Course.Names[next].Split(' ')[0], new Vector2(.5f, 0), new Vector2(235, y), new Vector2(450, 160), Kit.A(Course.Accent[next], 1f), Ink, () => { if (g.Online) g.Quit(); g.SelectCourse(Course.Ids[next]); g.StartSolo(); }, 40);
        var share = Btn(s, "SHARE", new Vector2(.5f, 0), new Vector2(-320, y - 175), new Vector2(290, 120), Dim, Color.white, () => { }, 42);
        share.gameObject.AddComponent<ShareOnPress>().Text = () => g.ShareText();
        Btn(s, "RANKS", new Vector2(.5f, 0), new Vector2(0, y - 175), new Vector2(290, 120), Dim, Gold, () => WebBridge.ShowBoard(g.Course.id), 42);
        Btn(s, "MENU", new Vector2(.5f, 0), new Vector2(320, y - 175), new Vector2(290, 120), Dim, Color.white, () => g.Quit(), 42);
    }

    public void RefreshResults()
    {
        if (!resultsList) return;
        foreach (Transform c in resultsList) Destroy(c.gameObject);
        var list = Game.I.Standings();
        for (int i = 0; i < Mathf.Min(list.Count, 8); i++)
        {
            var (name, t, kind) = list[i];
            var row = Box(resultsList, new Vector2(.5f, 1), new Vector2(0, -i * 82), new Vector2(940, 74), kind == 0 ? Kit.A(Pink, 0.55f) : new Color(1, 1, 1, i % 2 == 0 ? 0.1f : 0.05f));
            Txt(row, (t > 0 ? (i + 1).ToString() : "-"), 40, new Vector2(0, .5f), new Vector2(55, 0), i == 0 && t > 0 ? Gold : Color.white, TextAnchor.MiddleCenter, 90).fontStyle = FontStyle.BoldAndItalic;
            Txt(row, name + (kind == 2 ? "  (GHOST)" : ""), 36, new Vector2(0, .5f), new Vector2(380, 0), kind == 1 ? Cyan : kind == 2 ? Ghosty : Color.white, TextAnchor.MiddleLeft, 520);
            Txt(row, t > 0 ? Time(t) : "RUNNING...", 36, new Vector2(1, .5f), new Vector2(-140, 0), Color.white, TextAnchor.MiddleRight, 260);
        }
    }

    Game.RankMsg lastRank;
    public void SetRank(Game.RankMsg m) { lastRank = m; ApplyRank(); }
    public void ClearRank() => lastRank = null;
    void ApplyRank()
    {
        if (!rankText || lastRank == null) return;
        if (lastRank.rank > 0)
        {
            rankText.text = "WORLD RANK  #" + lastRank.rank + " OF " + lastRank.total + (lastRank.newBest ? "   NEW RECORD!" : "   BEST " + Time(lastRank.best / 1000f));
            rankText.color = lastRank.rank <= 10 ? Gold : Lime;
        }
        else if (lastRank.error == "bad time" || lastRank.error == "implausible") { rankText.text = "RUN NOT VERIFIED - saved on this device only"; rankText.color = Soft; }
        else if (!string.IsNullOrEmpty(lastRank.error)) { rankText.text = "SAVED ON THIS DEVICE" + (lastRank.error == "offline" ? "  (OFFLINE)" : ""); rankText.color = Soft; }
    }

    public static string Time(float secs)
    {
        if (secs < 0 || secs > 5999) return "-:--.--";
        int m = Mathf.FloorToInt(secs / 60f);
        float s = secs - m * 60;
        return m + ":" + s.ToString("00.00");
    }

    // ---------------------------------------------------------------- per frame
    void Update()
    {
        float aspect = (float)UnityEngine.Screen.width / Mathf.Max(1, UnityEngine.Screen.height);
        scaler.matchWidthOrHeight = aspect > 0.75f ? 1f : 0f;
        if ((aspect > 1.05f) != (lastAspect > 1.05f) && lastAspect > 0 && Game.I && Game.I.State == Game.St.Menu && screens.childCount > 0 && screens.GetChild(0).Find("panel")) ShowMenu();
        lastAspect = aspect;
        float udt = UnityEngine.Time.unscaledDeltaTime;

        if (countT > 0)
        {
            countT -= udt;
            float k = 1f - countT / 0.85f;
            countText.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.7f, 1f, Mathf.Clamp01(k * 4f));
            countText.color = Kit.A(countText.color, Mathf.Clamp01(countT * 3f));
            if (countT <= 0) countText.gameObject.SetActive(false);
        }
        if (bannerT > 0)
        {
            bannerT -= udt;
            float k = bannerT > 1.7f ? (2.0f - bannerT) / 0.3f : 1f;
            bannerText.rectTransform.localScale = Vector3.one * Kit.EaseOutBack(Mathf.Clamp01(k));
            float a = Mathf.Clamp01(bannerT * 2f);
            bannerText.color = Kit.A(bannerText.color, a); bannerSub.color = Kit.A(bannerSub.color, a);
            if (bannerT <= 0) { bannerText.gameObject.SetActive(false); bannerSub.gameObject.SetActive(false); }
        }
        if (toastT > 0)
        {
            toastT -= udt;
            toastText.color = Kit.A(toastText.color, Mathf.Clamp01(toastT * 2f));
            if (toastT <= 0) toastText.gameObject.SetActive(false);
        }
        var cam = Game.I ? Game.I.Cam : null;
        for (int i = nameTags.Count - 1; i >= 0; i--)
        {
            var (t, x) = nameTags[i];
            if (!t || !x) { if (x) Destroy(x.gameObject); nameTags.RemoveAt(i); continue; }
            var sp = cam.WorldToScreenPoint(t.position + Vector3.up * 2.1f);
            x.gameObject.SetActive(sp.z > 0 && sp.z < 60);
            x.rectTransform.position = sp;
        }
    }
}

// Floating thumbstick: touch anywhere in the zone, the stick appears under the thumb.
public class VirtualStick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    public RectTransform Base, Knob;
    public Vector2 Home;
    public Vector2 Value;
    int id = -999;
    Vector2 origin;
    const float Radius = 110f;

    Vector2 Local(PointerEventData e)
    {
        var parent = (RectTransform)Base.parent;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, e.position, e.pressEventCamera, out var lp);
        return lp + parent.rect.size * 0f - parent.rect.min;   // bottom-left anchored coordinates
    }
    public void OnPointerDown(PointerEventData e)
    {
        if (id != -999) return;
        id = e.pointerId; origin = Local(e);
        Base.anchoredPosition = Knob.anchoredPosition = origin;
        Value = Vector2.zero;
    }
    public void OnDrag(PointerEventData e)
    {
        if (e.pointerId != id) return;
        var d = Vector2.ClampMagnitude(Local(e) - origin, Radius);
        Knob.anchoredPosition = origin + d;
        Value = d / Radius;
        if (Value.magnitude < 0.12f) Value = Vector2.zero;
    }
    public void OnPointerUp(PointerEventData e)
    {
        if (e.pointerId != id) return;
        id = -999; Value = Vector2.zero;
        Base.anchoredPosition = Knob.anchoredPosition = Home;
    }
    void OnDisable() { id = -999; Value = Vector2.zero; if (Base) Base.anchoredPosition = Knob.anchoredPosition = Home; }
}

// Drag on empty screen to orbit the camera.
public class CamDrag : MonoBehaviour, IDragHandler
{
    public void OnDrag(PointerEventData e)
    {
        var s = 1080f / Mathf.Max(1, Mathf.Min(UnityEngine.Screen.width, UnityEngine.Screen.height));
        Game.I.CameraDrag(e.delta * s);
    }
}

public class HoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    readonly HashSet<int> ids = new HashSet<int>();
    public bool Held => ids.Count > 0;
    public bool Pressed;
    public void OnPointerDown(PointerEventData e) { if (ids.Count == 0) Pressed = true; ids.Add(e.pointerId); transform.localScale = Vector3.one * 0.92f; }
    public void OnPointerUp(PointerEventData e) { ids.Remove(e.pointerId); if (ids.Count == 0) transform.localScale = Vector3.one; }
    void OnDisable() { ids.Clear(); Pressed = false; transform.localScale = Vector3.one; }
}
