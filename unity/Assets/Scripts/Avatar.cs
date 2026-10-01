using System.Collections.Generic;
using UnityEngine;

public class SkinDef { public string id, name, model; public int stars; }

public static class Skins
{
    // Unlocked by total stars collected across all courses (3 per course).
    public static readonly SkinDef[] All =
    {
        new SkinDef { id = "knight",    name = "KNIGHT",       model = "Kay/Knight",           stars = 0 },
        new SkinDef { id = "barbarian", name = "BARBARIAN",    model = "Kay/Barbarian",        stars = 0 },
        new SkinDef { id = "rogue",     name = "ROGUE",        model = "Kay/Rogue",            stars = 0 },
        new SkinDef { id = "mage",      name = "MAGE",         model = "Kay/Mage",             stars = 1 },
        new SkinDef { id = "ranger",    name = "RANGER",       model = "Kay/Ranger",           stars = 2 },
        new SkinDef { id = "minion",    name = "BONEY",        model = "Kay/Skeleton_Minion",  stars = 3 },
        new SkinDef { id = "hooded",    name = "SHADOW",       model = "Kay/Rogue_Hooded",     stars = 4 },
        new SkinDef { id = "skrogue",   name = "RATTLES",      model = "Kay/Skeleton_Rogue",   stars = 5 },
        new SkinDef { id = "warrior",   name = "BONE KNIGHT",  model = "Kay/Skeleton_Warrior", stars = 7 },
        new SkinDef { id = "skmage",    name = "LICH",         model = "Kay/Skeleton_Mage",    stars = 9 },
    };
    public static SkinDef Get(string id) { foreach (var s in All) if (s.id == id) return s; return All[0]; }
    public static int IndexOf(string id) { for (int i = 0; i < All.Length; i++) if (All[i].id == id) return i; return 0; }
}

// Animation ids shared by the player, network messages and ghost recordings.
public static class Anim
{
    public const int Idle = 0, Run = 1, Jump = 2, Fall = 3, Land = 4, Hit = 5, Spawn = 6, Flip = 7, Win = 8;
}

// A KayKit Rig_Medium character driven by the shared clip library. Visual only.
public class Avatar
{
    public GameObject Root;
    public Transform Model;
    Animation anim;
    string clip = "";
    int cur = -1;
    float landT, flipT;
    static Dictionary<string, AnimationClip> clips;
    public const float Height = 1.55f;

    static void LoadClips()
    {
        if (clips != null) return;
        clips = new Dictionary<string, AnimationClip>();
        foreach (var f in new[] { "Kenney/Kay/Rig_Medium_General", "Kenney/Kay/Rig_Medium_MovementBasic" })
            foreach (var c in Resources.LoadAll<AnimationClip>(f))
                if (!c.name.StartsWith("__preview") && !clips.ContainsKey(c.name)) clips[c.name] = c;
    }

    public Avatar(string skinId, Transform parent, Material ghost = null)
    {
        LoadClips();
        var skin = Skins.Get(skinId);
        Root = new GameObject("avatar_" + skin.id);
        Root.transform.SetParent(parent, false);
        var src = Resources.Load<GameObject>("Kenney/" + skin.model);
        var inst = Object.Instantiate(src, Root.transform, false);
        inst.name = "model";
        Model = inst.transform;
        Kit.FixMaterials(Root);
        var b = Kit.WorldBounds(inst);
        float s = Height / Mathf.Max(0.01f, b.size.y);
        inst.transform.localScale *= s;
        b = Kit.WorldBounds(inst);
        inst.transform.position += new Vector3(Root.transform.position.x - b.center.x, Root.transform.position.y - b.min.y, Root.transform.position.z - b.center.z);
        anim = inst.GetComponent<Animation>();
        if (!anim) anim = inst.AddComponent<Animation>();
        foreach (var kv in clips) if (anim.GetClip(kv.Key) == null) anim.AddClip(kv.Value, kv.Key);
        anim.cullingType = AnimationCullingType.AlwaysAnimate;
        foreach (AnimationState st in anim) st.wrapMode = WrapMode.Loop;
        foreach (var once in new[] { "Jump_Start", "Jump_Land", "Hit_A", "Spawn_Air", "Death_A" }) if (anim[once] != null) anim[once].wrapMode = WrapMode.ClampForever;
        foreach (var smr in inst.GetComponentsInChildren<SkinnedMeshRenderer>()) smr.updateWhenOffscreen = true;
        foreach (var r in inst.GetComponentsInChildren<Renderer>())
        {
            r.shadowCastingMode = ghost ? UnityEngine.Rendering.ShadowCastingMode.Off : UnityEngine.Rendering.ShadowCastingMode.On;
            if (ghost)
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) { var m = new Material(ghost); if (mats[i] && mats[i].mainTexture) m.mainTexture = mats[i].mainTexture; mats[i] = m; }
                r.sharedMaterials = mats;
            }
        }
        Play("Idle_A");
    }

    void Play(string c, float fade = 0.12f, float speed = 1f)
    {
        if (anim[c] == null) return;
        if (clip != c) { anim.CrossFade(c, fade); clip = c; }
        anim[c].speed = speed;
    }

    // Shows animation `id`; `run01` scales the run cycle with speed.
    public void Set(int id, float run01 = 1f)
    {
        if (id == Anim.Run) { Play("Running_A", 0.1f, Mathf.Lerp(0.8f, 1.35f, run01)); cur = id; return; }
        if (id == cur && id != Anim.Jump) return;
        cur = id;
        switch (id)
        {
            case Anim.Idle: Play("Idle_A", 0.15f); break;
            case Anim.Jump: anim.Stop("Jump_Start"); clip = ""; Play("Jump_Start", 0.05f, 1.6f); break;
            case Anim.Flip: anim.Stop("Jump_Start"); clip = ""; Play("Jump_Start", 0.04f, 2.2f); break;
            case Anim.Fall: Play("Jump_Idle", 0.2f); break;
            case Anim.Land: anim.Stop("Jump_Land"); clip = ""; Play("Jump_Land", 0.05f, 1.8f); break;
            case Anim.Hit: anim.Stop("Hit_A"); clip = ""; Play("Hit_A", 0.05f, 1.3f); break;
            case Anim.Spawn: anim.Stop("Spawn_Air"); clip = ""; Play("Spawn_Air", 0.02f, 1.4f); break;
            case Anim.Win: Play("Idle_B", 0.2f); break;
        }
    }

    public void SetVisible(bool v) { foreach (var r in Root.GetComponentsInChildren<Renderer>()) r.enabled = v; }
    public void Destroy() { if (Root) Object.Destroy(Root); }
}

// Remote racers and ghosts: interpolated towards sampled states.
public class Puppet
{
    public Avatar Av;
    public string Id, Name;
    public Vector3 Pos; public float Yaw; public int AnimId;
    Vector3 from, to; float yFrom, yTo, t, span = 0.1f;
    public int Checkpoint;
    public bool Finished; public float FinishTime;

    public Puppet(string skin, Transform parent, Material ghost, string id, string name)
    {
        Av = new Avatar(skin, parent, ghost); Id = id; Name = name;
    }

    public void Target(Vector3 p, float yaw, int animId, float interval)
    {
        from = Av.Root.transform.position; to = p;
        yFrom = Yaw; yTo = yaw; t = 0; span = Mathf.Max(0.04f, interval);
        // big jumps (respawns) snap instead of sliding through the air
        if ((to - from).sqrMagnitude > 64f) { from = to; Av.Root.transform.position = to; }
        if (animId != AnimId || animId == Anim.Run) Av.Set(animId);
        AnimId = animId;
    }

    public void Tick(float dt)
    {
        t += dt;
        float k = Mathf.Clamp01(t / span);
        Pos = Vector3.Lerp(from, to, k);
        Yaw = Mathf.LerpAngle(yFrom, yTo, k);
        Av.Root.transform.position = Pos;
        Av.Root.transform.rotation = Quaternion.Euler(0, Yaw, 0);
    }
}
