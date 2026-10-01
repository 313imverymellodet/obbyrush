using System;
using System.Collections.Generic;
using UnityEngine;

// Ghost runs: 10 samples a second, 8 bytes each (x, y, z in cm as int16, yaw byte, anim byte),
// base64 for storage. A 60 s run is ~6 KB of text.
public struct GSample { public Vector3 p; public float yaw; public int anim; }

public static class GhostCodec
{
    public const float Rate = 10f;

    public static string Encode(List<GSample> s)
    {
        var b = new byte[s.Count * 8];
        for (int i = 0; i < s.Count; i++)
        {
            int o = i * 8;
            Put(b, o, s[i].p.x); Put(b, o + 2, s[i].p.y); Put(b, o + 4, s[i].p.z);
            b[o + 6] = (byte)(Mathf.RoundToInt(Mathf.Repeat(s[i].yaw, 360f) / 360f * 256f) & 255);
            b[o + 7] = (byte)Mathf.Clamp(s[i].anim, 0, 255);
        }
        return Convert.ToBase64String(b);
    }

    public static GSample[] Decode(string b64)
    {
        if (string.IsNullOrEmpty(b64)) return null;
        byte[] b;
        try { b = Convert.FromBase64String(b64); } catch { return null; }
        var s = new GSample[b.Length / 8];
        for (int i = 0; i < s.Length; i++)
        {
            int o = i * 8;
            s[i] = new GSample { p = new Vector3(Get(b, o), Get(b, o + 2), Get(b, o + 4)), yaw = b[o + 6] / 256f * 360f, anim = b[o + 7] };
        }
        return s;
    }

    static void Put(byte[] b, int o, float v)
    {
        short s = (short)Mathf.Clamp(Mathf.RoundToInt(v * 100f), short.MinValue, short.MaxValue);
        b[o] = (byte)(s & 255); b[o + 1] = (byte)((s >> 8) & 255);
    }
    static float Get(byte[] b, int o) => (short)(b[o] | (b[o + 1] << 8)) / 100f;
}

public class GhostRecorder
{
    public readonly List<GSample> Samples = new List<GSample>();
    public void Clear() => Samples.Clear();
    public void Tick(float runTime, Player p)
    {
        while (Samples.Count <= runTime * GhostCodec.Rate)
            Samples.Add(new GSample { p = p.transform.position, yaw = p.Yaw, anim = p.AnimId < 0 ? 0 : p.AnimId });
    }
}

// Plays a recorded run back on a translucent avatar, in step with the run clock.
public class GhostRunner
{
    public Avatar Av;
    public GSample[] S;
    public string Name;
    public float Ms;
    int lastAnim = -1;
    public bool Done;

    public GhostRunner(string skin, GSample[] samples, string name, float ms, Transform parent, Material mat)
    {
        S = samples; Name = name; Ms = ms;
        Av = new Avatar(skin, parent, mat);
    }

    public Vector3 Pos => Av.Root.transform.position;

    public void Tick(float runTime)
    {
        if (S == null || S.Length == 0) return;
        float f = Mathf.Max(0, runTime) * GhostCodec.Rate;
        int i = Mathf.Min((int)f, S.Length - 1);
        int j = Mathf.Min(i + 1, S.Length - 1);
        float k = Mathf.Clamp01(f - i);
        var a = S[i]; var b = S[j];
        // respawn teleports: don't slide through the air
        var pos = (b.p - a.p).sqrMagnitude > 36f ? (k < 0.5f ? a.p : b.p) : Vector3.Lerp(a.p, b.p, k);
        Av.Root.transform.position = pos;
        Av.Root.transform.rotation = Quaternion.Euler(0, Mathf.LerpAngle(a.yaw, b.yaw, k), 0);
        Done = i >= S.Length - 1;
        int an = Done ? Anim.Win : a.anim;
        if (an != lastAnim || an == Anim.Run) { Av.Set(an); lastAnim = an; }
    }

    public void Destroy() => Av?.Destroy();
}
