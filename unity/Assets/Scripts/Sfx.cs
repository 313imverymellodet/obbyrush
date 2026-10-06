using UnityEngine;

// All audio is synthesized at boot: zero audio files.
public class Sfx : MonoBehaviour
{
    public static Sfx I;
    const int SR = 22050;
    const float TAU = Mathf.PI * 2f;
    AudioSource[] voices; int next;
    AudioSource music;
    AudioClip jump, airJump, land, spring, bonk, crumble, checkpoint, star, fall, beep, go, finish, record, click, whoosh, rewind;
    AudioSource rewindSrc;
    public bool Muted { get; private set; }
    System.Random rnd = new System.Random(7);
    float N() => (float)(rnd.NextDouble() * 2 - 1);
    float lastLand, lastCrumble;

    void Awake()
    {
        I = this;
        voices = new AudioSource[10];
        for (int i = 0; i < voices.Length; i++) { voices[i] = gameObject.AddComponent<AudioSource>(); voices[i].playOnAwake = false; }
        music = gameObject.AddComponent<AudioSource>();
        music.loop = true; music.volume = 0.22f; music.playOnAwake = false;
        Build();
        music.clip = Music();
    }

    public void SetMuted(bool m) { Muted = m; AudioListener.volume = m ? 0 : 1; }
    public void StartMusic() { if (!music.isPlaying) music.Play(); }

    void Play(AudioClip c, float vol, float pitch = 1f)
    {
        var s = voices[next]; next = (next + 1) % voices.Length;
        s.pitch = pitch; s.PlayOneShot(c, vol);
    }

    public void Jump() => Play(jump, 0.32f, Random.Range(0.95f, 1.08f));
    public void AirJump() => Play(airJump, 0.34f, Random.Range(0.97f, 1.05f));
    public void Land() { if (Time.unscaledTime - lastLand < 0.12f) return; lastLand = Time.unscaledTime; Play(land, 0.3f, Random.Range(0.9f, 1.1f)); }
    public void Spring() => Play(spring, 0.45f, Random.Range(0.96f, 1.04f));
    public void Bonk() => Play(bonk, 0.5f, Random.Range(0.9f, 1.1f));
    public void Crumble() { if (Time.unscaledTime - lastCrumble < 0.1f) return; lastCrumble = Time.unscaledTime; Play(crumble, 0.35f, Random.Range(0.9f, 1.1f)); }
    public void Checkpoint() => Play(checkpoint, 0.5f);
    public void Star() => Play(star, 0.5f);
    public void Fall() => Play(fall, 0.4f);
    public void Beep(bool isGo) => Play(isGo ? go : beep, 0.5f);
    public void Finish(bool pb) => Play(pb ? record : finish, 0.65f);
    public void Click() => Play(click, 0.4f);
    public void Whoosh() => Play(whoosh, 0.35f, Random.Range(0.9f, 1.1f));
    // tape-rewind warble, looped while held
    public void Rewind()
    {
        if (!rewindSrc) { rewindSrc = gameObject.AddComponent<AudioSource>(); rewindSrc.loop = true; rewindSrc.playOnAwake = false; rewindSrc.clip = rewind; rewindSrc.volume = 0.35f; }
        rewindSrc.Play();
    }
    public void RewindStop() { if (rewindSrc) rewindSrc.Stop(); }

    static AudioClip Clip(string n, float[] d) { var c = AudioClip.Create(n, d.Length, 1, SR, false); c.SetData(d, 0); return c; }
    delegate float Gen(float t, float dt);
    static float[] R(float dur, Gen g, bool loop = false)
    {
        int n = (int)(SR * dur); var d = new float[n]; float dt = 1f / SR;
        for (int i = 0; i < n; i++) d[i] = Mathf.Clamp(g(i * dt, dt) * (loop ? 1f : Mathf.Clamp01((n - i) / (SR * 0.008f))), -1, 1);
        return d;
    }

    float[] Arp(float[] notes, float step, float tail, float vol, float bright = 0.25f)
    {
        float ph = 0;
        return R(step * notes.Length + tail, (t, dt) =>
        {
            int k = Mathf.Min((int)(t / step), notes.Length - 1);
            ph += TAU * notes[k] * dt;
            float lt = t - k * step;
            float tri = Mathf.Abs(2f * (ph / TAU % 1f) - 1f) * 2f - 1f;
            return (Mathf.Sin(ph) * 0.75f + tri * bright) * Mathf.Exp(-lt * (k < notes.Length - 1 ? 9 : 3f)) * vol;
        });
    }

    void Build()
    {
        float ph = 0, lp = 0;
        jump = Clip("jump", R(0.16f, (t, dt) => { ph += TAU * Mathf.Lerp(330, 760, t / 0.16f) * dt; return (Mathf.Sin(ph) * 0.7f + (Mathf.Sin(ph) > 0 ? 0.12f : -0.12f)) * Mathf.Exp(-t * 14); }));
        ph = 0; float ph2 = 0;
        airJump = Clip("airjump", R(0.22f, (t, dt) => { ph += TAU * Mathf.Lerp(600, 1250, t / 0.22f) * dt; ph2 += TAU * Mathf.Lerp(900, 1875, t / 0.22f) * dt; return (Mathf.Sin(ph) * 0.55f + Mathf.Sin(ph2) * 0.25f) * Mathf.Exp(-t * 11); }));
        ph = 0; lp = 0;
        land = Clip("land", R(0.12f, (t, dt) => { lp += (N() - lp) * 0.25f; ph += TAU * Mathf.Lerp(140, 60, t / 0.12f) * dt; return (Mathf.Sin(ph) * 0.7f + lp * 0.5f) * Mathf.Exp(-t * 30); }));
        ph = 0;
        spring = Clip("spring", R(0.55f, (t, dt) => { ph += TAU * (220 + 260 * t / 0.55f + Mathf.Sin(t * 70f) * 70f * Mathf.Exp(-t * 4)) * dt; return Mathf.Sin(ph) * 0.7f * Mathf.Exp(-t * 4.5f); }));
        ph = 0; lp = 0;
        bonk = Clip("bonk", R(0.3f, (t, dt) => { lp += (N() - lp) * 0.4f; ph += TAU * Mathf.Lerp(420, 120, Mathf.Sqrt(t / 0.3f)) * dt; return (Mathf.Sin(ph) * 0.7f + lp * 0.35f * Mathf.Exp(-t * 30)) * Mathf.Exp(-t * 9); }));
        lp = 0;
        crumble = Clip("crumble", R(0.35f, (t, dt) => { lp += (N() - lp) * 0.3f; float grit = (Mathf.Sin(t * 300f) > 0.6f ? 1f : 0.3f); return lp * grit * Mathf.Exp(-t * 7) * 0.9f; }));
        checkpoint = Clip("checkpoint", Arp(new[] { 659.25f, 987.77f, 1318.5f }, 0.07f, 0.5f, 0.5f));
        star = Clip("star", Arp(new[] { 1046.5f, 1318.5f, 1568f, 2093f, 2637f }, 0.045f, 0.45f, 0.38f, 0.1f));
        ph = 0;
        fall = Clip("fall", R(0.7f, (t, dt) => { ph += TAU * Mathf.Lerp(900, 180, t / 0.7f) * dt; return Mathf.Sin(ph) * 0.5f * Mathf.Exp(-t * 2.5f); }));
        beep = Clip("beep", R(0.22f, (t, dt) => (Mathf.Sin(TAU * 660 * t) * 0.6f + (Mathf.Sin(TAU * 660 * t) > 0 ? 0.12f : -0.12f)) * Mathf.Min(1, (0.22f - t) * 20)));
        go = Clip("go", R(0.6f, (t, dt) => (Mathf.Sin(TAU * 1320 * t) * 0.55f + (Mathf.Sin(TAU * 1320 * t) > 0 ? 0.12f : -0.12f)) * Mathf.Exp(-t * 3f)));
        finish = Clip("finish", Arp(new[] { 523.25f, 659.25f, 783.99f, 1046.5f }, 0.11f, 1.0f, 0.42f));
        record = Clip("record", Arp(new[] { 523.25f, 659.25f, 783.99f, 1046.5f, 783.99f, 1046.5f, 1318.5f, 1568f }, 0.09f, 1.4f, 0.42f));
        ph = 0;
        click = Clip("click", R(0.04f, (t, dt) => { ph += TAU * 1200 * dt; return Mathf.Sin(ph) * Mathf.Exp(-t * 90) * 0.6f; }));
        lp = 0;
        whoosh = Clip("whoosh", R(0.4f, (t, dt) => { lp += (N() - lp) * Mathf.Lerp(0.05f, 0.5f, Mathf.Sin(t / 0.4f * Mathf.PI)); return lp * Mathf.Sin(t / 0.4f * Mathf.PI) * 1.2f; }));
        float rph = 0; lp = 0;
        rewind = Clip("rewind", R(0.5f, (t, dt) => { lp += (N() - lp) * 0.2f; rph += TAU * (900f + Mathf.Sin(t * TAU * 6f) * 300f) * dt; return (Mathf.Sin(rph) * 0.25f + lp * 0.35f) * (0.7f + 0.3f * Mathf.Sin(t * TAU * 12f)); }));
    }

    // 140 bpm bouncy chiptune-pop in C major: C – G – Am – F, plucky bass, marimba-ish lead.
    AudioClip Music()
    {
        float bpm = 140f, beat = 60f / bpm;
        int bars = 8; float dur = beat * 4 * bars;
        int n = (int)(SR * dur); var d = new float[n];
        float[][] chords =
        {
            new[] { 261.63f, 329.63f, 392f },
            new[] { 196f, 246.94f, 293.66f },
            new[] { 220f, 261.63f, 329.63f },
            new[] { 174.61f, 220f, 261.63f },
        };
        float[] roots = { 65.41f, 49f, 55f, 43.65f };
        float[] lead = { 783.99f, 659.25f, 783.99f, 880f, 783.99f, 659.25f, 587.33f, 659.25f,
                         523.25f, 587.33f, 659.25f, 783.99f, 880f, 783.99f, 659.25f, 587.33f };
        float hp = 0;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / SR, bt = t / beat;
            int bi = (int)bt, barIdx = bi / 4, bar = barIdx % 4;
            float ib = (bt - bi) * beat;
            float nz = N();
            float kick = Mathf.Sin(TAU * (55 + 120 * Mathf.Exp(-ib * 40)) * ib) * Mathf.Exp(-ib * 10) * 0.5f;
            float clap = (bi % 2 == 1) ? nz * Mathf.Exp(-ib * 22) * 0.22f : 0;
            float e8 = bt * 2; int e8i = (int)e8; float i8 = (e8 - e8i) * beat / 2;
            float e16 = bt * 4; int e16i = (int)e16; float i16 = (e16 - e16i) * beat / 4;
            float hat = (nz - hp) * Mathf.Exp(-i16 * 80) * (e16i % 2 == 1 ? 0.06f : 0.025f); hp = nz;
            // plucky square bass on 8ths
            float bf = roots[bar] * (e8i % 4 == 2 ? 2f : 1f);
            float bass = ((bf * t) % 1f < 0.5f ? 1f : -1f) * Mathf.Exp(-i8 * 9) * 0.13f;
            // soft chord stabs on the off-beats
            float pad = 0;
            if (e8i % 2 == 1) foreach (var f in chords[bar]) pad += Mathf.Sin(TAU * f * t) + 0.3f * Mathf.Sin(TAU * f * 2 * t);
            pad *= 0.045f * Mathf.Exp(-i8 * 7);
            // marimba lead from bar 3
            float ld = 0;
            if (barIdx >= 2)
            {
                float lf = lead[e8i % 16];
                ld = (Mathf.Sin(TAU * lf * t) + 0.25f * Mathf.Sin(TAU * lf * 4 * t) * Mathf.Exp(-i8 * 30)) * Mathf.Exp(-i8 * 7) * 0.11f;
            }
            float duck = 1f - 0.4f * Mathf.Exp(-ib * 12);
            d[i] = Mathf.Clamp((kick + clap + hat + (bass + pad + ld) * duck) * 0.8f, -1, 1);
        }
        return Clip("music", d);
    }
}
