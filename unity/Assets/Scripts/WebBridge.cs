using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.EventSystems;

// Page integrations: portal ads, share sheet, haptics, leaderboards, ghosts and live races (obby.js).
public class WebBridge : MonoBehaviour
{
    public static WebBridge I;
    Action<bool> pending;

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] static extern void SD_Rewarded(string goName);
    [DllImport("__Internal")] static extern void SD_Gameplay(int on);
    [DllImport("__Internal")] static extern void SD_Event(string name, int value);
    [DllImport("__Internal")] static extern void SD_Ready();
    [DllImport("__Internal")] static extern int SD_AdsAvailable();
    [DllImport("__Internal")] static extern void OR_ArmShare(string text);
    [DllImport("__Internal")] static extern void OR_Vibrate(int ms);
    [DllImport("__Internal")] static extern void OR_RunStart(string course);
    [DllImport("__Internal")] static extern void OR_RunSubmit(string course, int ms, string skin, string ghost);
    [DllImport("__Internal")] static extern void OR_ShowBoard(string course);
    [DllImport("__Internal")] static extern void OR_FetchGhost(string course);
    [DllImport("__Internal")] static extern void OR_NetOpen(string course, string skin);
    [DllImport("__Internal")] static extern void OR_NetState(float x, float y, float z, float r, int a, int c);
    [DllImport("__Internal")] static extern void OR_NetFinish(int ms);
    [DllImport("__Internal")] static extern void OR_NetLeave();
#endif

    void Awake() { I = this; gameObject.name = "WebBridge"; }

    public static bool AdsAvailable
    {
        get
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return SD_AdsAvailable() == 1;
#else
            return false;
#endif
        }
    }

    public void ShowRewarded(Action<bool> done)
    {
        pending = done;
        Sfx.I.SetMuted(true);
#if UNITY_WEBGL && !UNITY_EDITOR
        SD_Rewarded(gameObject.name);
#else
        OnRewarded("1");
#endif
    }

    public void OnRewarded(string ok)
    {
        Sfx.I.SetMuted(Game.I.Save.muted);
        var cb = pending; pending = null;
        cb?.Invoke(ok == "1");
    }

#if UNITY_WEBGL && !UNITY_EDITOR
    public static void Gameplay(bool on) => SD_Gameplay(on ? 1 : 0);
    public static void Event(string name, int value = 0) => SD_Event(name, value);
    public static void Ready() => SD_Ready();
    public static void Vibrate(int ms) => OR_Vibrate(ms);
    public static void RunStart(string c) => OR_RunStart(c);
    public static void RunSubmit(string c, int ms, string skin, string ghost) => OR_RunSubmit(c, ms, skin, ghost);
    public static void ShowBoard(string c) => OR_ShowBoard(c);
    public static void FetchGhost(string c) => OR_FetchGhost(c);
    public static void NetOpen(string c, string skin) => OR_NetOpen(c, skin);
    public static void NetState(float x, float y, float z, float r, int a, int cp) => OR_NetState(x, y, z, r, a, cp);
    public static void NetFinish(int ms) => OR_NetFinish(ms);
    public static void NetLeave() => OR_NetLeave();
    // Web Share needs a live gesture: arm on pointer-down, the page fires it on pointer-up.
    public static void ArmShare(string text) => OR_ArmShare(text);
#else
    public static void Gameplay(bool on) { }
    public static void Event(string name, int value = 0) { }
    public static void Ready() { }
    public static void Vibrate(int ms) { }
    public static void RunStart(string c) { }
    public static void RunSubmit(string c, int ms, string skin, string ghost) { }
    public static void ShowBoard(string c) { }
    public static void FetchGhost(string c) { }
    public static void NetOpen(string c, string skin) { }
    public static void NetState(float x, float y, float z, float r, int a, int cp) { }
    public static void NetFinish(int ms) { }
    public static void NetLeave() { }
    public static void ArmShare(string text) { GUIUtility.systemCopyBuffer = text; }
#endif
}

public class ShareOnPress : MonoBehaviour, IPointerDownHandler
{
    public Func<string> Text;
    public void OnPointerDown(PointerEventData e) { if (Text != null) WebBridge.ArmShare(Text()); }
}
