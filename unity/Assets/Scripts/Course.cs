using System.Collections.Generic;
using UnityEngine;

// OBBY RUSH courses. Every course is laid out in code on the KayKit Platformer grid (1 unit = 1 m,
// pieces pivot at the bottom-centre; names are X x Z x height). Obstacle motion is a pure function of
// the run clock, so every racer, ghost and replay sees spinners and movers in exactly the same place.
public enum Kind { Solid, Mover, Rotator, Fader, Spring, Conveyor, Knocker }

public class Piece
{
    public Kind kind;
    public Transform t;
    public Collider col;
    public Matrix4x4 prev, cur;
    public bool moving, knock;
    // motion
    public Vector3 a, b; public float period, phase;          // movers: ping-pong a <-> b
    public Vector3 pivot; public float speed, yaw0;           // rotators / spinners: degrees per second
    public Vector3 dir; public float push;                    // conveyors
    // faders (local to this client)
    public int fade; public float fadeT; public Renderer[] rends; public Vector3 baseScale;
    public float squash;                                       // springs

    public Vector3 PointVelocity(Vector3 p, float dt)
    {
        if (!moving || dt <= 0) return Vector3.zero;
        var was = prev.MultiplyPoint3x4(cur.inverse.MultiplyPoint3x4(p));
        return (p - was) / dt;
    }
}

public class Checkpoint { public Vector3 pos; public float yaw; public Transform flag; public bool hit; }

public class Course
{
    public static readonly string[] Ids = { "garden", "tower", "storm" };
    public static readonly string[] Names = { "SKY GARDEN", "SUNSET TOWER", "STORM PEAK" };
    public static readonly string[] Tags = { "EASY - learn the ropes", "MEDIUM - climb to the top", "HARD - only the brave" };
    public static readonly Color[] Accent = { Kit.Hex("#5BE37D"), Kit.Hex("#FFB13D"), Kit.Hex("#FF5A6E") };
    public static int Index(string id) { for (int i = 0; i < Ids.Length; i++) if (Ids[i] == id) return i; return 0; }

    public string id, name;
    public int index;
    public Transform root;
    public Vector3 start; public float startYaw;
    public float killY = -14f;
    public readonly List<Checkpoint> cps = new List<Checkpoint>();
    public readonly List<Piece> pieces = new List<Piece>();
    public readonly List<Piece> dynamic = new List<Piece>();
    public readonly List<Piece> knockers = new List<Piece>();
    public readonly List<Transform> stars = new List<Transform>();
    public readonly List<Vector3> path = new List<Vector3>();      // flyover route for the menu camera
    public Vector3 finishPos; public float finishYaw; public Bounds finishZone;
    public Color skyTop, skyBottom, fog, sunColor, ambient;
    readonly Dictionary<Collider, Piece> byCol = new Dictionary<Collider, Piece>();
    Transform staticRoot;

    public Piece Of(Collider c) => c != null && byCol.TryGetValue(c, out var p) ? p : null;

    // ------------------------------------------------------------------ building helpers
    GameObject Model(string piece, Vector3 pos, float yaw, Transform parent)
    {
        var go = Kit.Spawn("Plat/" + piece, 1f, parent, pos, yaw);
        foreach (var r in go.GetComponentsInChildren<Renderer>()) { r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On; r.receiveShadows = true; }
        return go;
    }

    // Local-space box of a freshly spawned (unrotated, unit-scale) piece.
    static Bounds LocalBox(GameObject go)
    {
        var keepPos = go.transform.position; var keepRot = go.transform.rotation;
        go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        var b = Kit.WorldBounds(go);
        go.transform.SetPositionAndRotation(keepPos, keepRot);
        return b;
    }

    Piece Add(Kind kind, string piece, Vector3 pos, float yaw, bool meshCollider = false, Transform parent = null)
    {
        bool isStatic = kind == Kind.Solid && parent == null;
        var go = Model(piece, pos, yaw, parent ?? (isStatic ? staticRoot : root));
        Collider col;
        if (meshCollider)
        {
            col = null;
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
            {
                var mc = mf.gameObject.AddComponent<MeshCollider>(); mc.sharedMesh = mf.sharedMesh;
                if (col == null) col = mc; else byColExtra.Add((mc, null));
            }
        }
        else
        {
            var lb = LocalBox(go);
            var bc = go.AddComponent<BoxCollider>(); bc.center = lb.center; bc.size = lb.size;
            col = bc;
        }
        var p = new Piece { kind = kind, t = go.transform, col = col, rends = go.GetComponentsInChildren<Renderer>(), baseScale = go.transform.localScale };
        p.prev = p.cur = go.transform.localToWorldMatrix;
        byCol[col] = p;
        foreach (var (c, _) in byColExtra) byCol[c] = p;
        byColExtra.Clear();
        pieces.Add(p);
        if (kind != Kind.Solid && kind != Kind.Spring && kind != Kind.Conveyor) dynamic.Add(p);
        return p;
    }
    readonly List<(Collider, Piece)> byColExtra = new List<(Collider, Piece)>();

    static float H(string size) => float.Parse(size.Substring(size.LastIndexOf('x') + 1));

    // A platform whose top surface sits at `top`.
    Piece Plat(string size, string col, float x, float top, float z, float yaw = 0)
        => Add(Kind.Solid, "platform_" + size + "_" + col, new Vector3(x, top - H(size), z), yaw);

    Piece Slope(string size, string col, float x, float bottom, float z, float yaw)
        => Add(Kind.Solid, "platform_slope_" + size + "_" + col, new Vector3(x, bottom, z), yaw, true);

    Piece Wood(string piece, float x, float top, float z, float yaw = 0)
        => Add(Kind.Solid, piece, new Vector3(x, top - (piece.StartsWith("floor_wood") ? 0.5f : 1f), z), yaw);

    // Ping-pong between two top-surface positions.
    Piece Mover(string size, string col, Vector3 fromTop, Vector3 toTop, float period, float phase = 0, float yaw = 0)
    {
        var off = new Vector3(0, H(size), 0);
        var p = Add(Kind.Mover, "platform_" + size + "_" + col, fromTop - off, yaw);
        p.moving = true; p.a = fromTop - off; p.b = toTop - off; p.period = period; p.phase = phase;
        return p;
    }

    Piece Rotator(string piece, float x, float top, float z, float degPerSec, float h = 1f)
    {
        var p = Add(Kind.Rotator, piece, new Vector3(x, top - h, z), 0, piece.Contains("hole"));
        p.moving = true; p.pivot = new Vector3(x, top - h, z); p.speed = degPerSec;
        return p;
    }

    Piece Fader(string size, string col, float x, float top, float z)
    {
        var p = Add(Kind.Fader, "platform_" + size + "_" + col, new Vector3(x, top - H(size), z), 0);
        return p;
    }

    // A spring pad standing on a surface at `top`.
    Piece Spring(float x, float top, float z, string col)
    {
        // sunk into the floor so you can run straight onto it
        var p = Add(Kind.Spring, "spring_pad_" + col, new Vector3(x, top - 0.68f, z), 0);
        return p;
    }

    // Arrow tiles push anything standing on them along `yaw` (0 = +z).
    Piece Conveyor(string size, string col, float x, float top, float z, float yaw, float speed)
    {
        var p = Add(Kind.Conveyor, "platform_arrow_" + size + "_" + col, new Vector3(x, top - H(size), z), yaw);
        p.dir = Quaternion.Euler(0, yaw, 0) * Vector3.forward; p.push = speed;
        return p;
    }

    // A bar sweeping round a hub. `len` is the full bar length; the bar sits on a surface at `top`.
    void Spinner(float x, float top, float z, float len, float degPerSec, string col, float yaw0 = 0, float lift = 0)
    {
        var hub = new GameObject("spinner").transform;
        hub.SetParent(root, false); hub.position = new Vector3(x, top + lift, z);
        Model("pillar_1x1x2", new Vector3(x, top, z), 0, staticRoot).transform.localScale = new Vector3(1.1f, 0.62f + lift * 0.5f, 1.1f);
        var bar = new GameObject("bar").transform; bar.SetParent(hub, false);
        string piece = len >= 6 ? "barrier_3x1x1_" + col : len >= 4 ? "barrier_2x1x1_" + col : "barrier_1x1x1_" + col;
        float seg = len >= 6 ? 3 : len >= 4 ? 2 : 1;
        Model(piece, new Vector3(-seg / 2f, 0.15f, 0), 0, bar);   // local to the hub
        Model(piece, new Vector3(seg / 2f, 0.15f, 0), 0, bar);
        var bc = bar.gameObject.AddComponent<BoxCollider>();
        bc.center = new Vector3(0, 0.65f, 0); bc.size = new Vector3(len, 1f, 0.9f);
        var p = new Piece { kind = Kind.Knocker, t = hub, col = bc, moving = true, knock = true, pivot = hub.position, speed = degPerSec, yaw0 = yaw0 };
        p.prev = p.cur = hub.localToWorldMatrix;
        byCol[bc] = p; pieces.Add(p); dynamic.Add(p); knockers.Add(p);
    }

    // A wall block sliding back and forth across the path.
    void Pusher(string piece, Vector3 from, Vector3 to, float period, float phase = 0, float yaw = 0)
    {
        var p = Add(Kind.Knocker, piece, from, yaw);
        p.moving = true; p.knock = true; p.a = from; p.b = to; p.period = period; p.phase = phase;
        knockers.Add(p);
    }

    void Checkpoint(float x, float top, float z, float yaw = 0)
    {
        var flag = Model("flag_A_" + (cps.Count % 2 == 0 ? "blue" : "green"), new Vector3(x, top, z) + Quaternion.Euler(0, yaw, 0) * new Vector3(2.2f, 0, 0), yaw + 90, root);
        cps.Add(new Checkpoint { pos = new Vector3(x, top, z), yaw = yaw, flag = flag.transform });
        path.Add(new Vector3(x, top, z));
    }

    void Star(float x, float y, float z)
    {
        var s = Kit.Spawn("Plat/star_yellow", 1.1f, root, new Vector3(x, y - 0.55f, z));
        foreach (var r in s.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        stars.Add(s.transform);
    }

    void Finish(float x, float top, float z, float yaw = 0)
    {
        var gate = Model("signage_finish_wide", new Vector3(x, top, z), yaw, staticRoot);
        gate.transform.localScale = Vector3.one * 0.66f;
        finishPos = new Vector3(x, top, z); finishYaw = yaw;
        var fwd = Quaternion.Euler(0, yaw, 0) * Vector3.forward;
        // the line itself: cross it (anywhere under the arch, any height) to stop the clock
        finishZone = new Bounds(new Vector3(x, top + 2f, z) + fwd * 0.6f, Quaternion.Euler(0, yaw, 0) * new Vector3(7f, 6f, 1.2f));
        finishZone.extents = new Vector3(Mathf.Abs(finishZone.extents.x), Mathf.Abs(finishZone.extents.y), Mathf.Abs(finishZone.extents.z));
        path.Add(new Vector3(x, top, z));
    }

    void Deco(string piece, float x, float y, float z, float yaw = 0, float scale = 1f)
    {
        var go = Model(piece, new Vector3(x, y, z), yaw, staticRoot);
        go.transform.localScale = Vector3.one * scale;
    }

    // ------------------------------------------------------------------ build
    public static Course Build(int index, Transform parent)
    {
        var c = new Course { index = index, id = Ids[index], name = Names[index] };
        c.root = new GameObject("Course_" + c.id).transform; c.root.SetParent(parent, false);
        c.staticRoot = new GameObject("static").transform; c.staticRoot.SetParent(c.root, false);
        switch (index)
        {
            case 0: c.Garden(); break;
            case 1: c.Tower(); break;
            default: c.Storm(); break;
        }
        c.Scenery();
        StaticBatchingUtility.Combine(c.staticRoot.gameObject);
        return c;
    }

    // A full-speed jump carries ~5.7 m and rises ~2.2 m, so hops are spaced ~5 m centre to centre.
    // ================================================================== 1. SKY GARDEN (easy)
    void Garden()
    {
        skyTop = Kit.Hex("#3FA9F5"); skyBottom = Kit.Hex("#CFEFFF"); fog = Kit.Hex("#BFE6FF");
        sunColor = Kit.Hex("#FFF4DE"); ambient = Kit.Hex("#9CC8F0");
        start = new Vector3(0, 0, -1); startYaw = 0;
        path.Add(start);
        Plat("6x6x4", "green", 0, 0, 0);
        Deco("signage_arrow_stand_blue", 2.3f, 0, 2.2f, 180);
        // stepping stones
        Plat("2x2x1", "blue", 0, 0, 7.5f);
        Plat("2x2x1", "blue", 1.5f, 0.5f, 12.5f);
        Plat("2x2x1", "blue", -0.5f, 1f, 17.5f);
        Plat("6x6x2", "green", 0, 1, 24);
        Checkpoint(0, 1, 24);
        // star 1: a long hop off to the side (needs the air jump)
        Plat("2x2x1", "yellow", 8, 3, 24); Star(8, 4.1f, 24);
        // stairs
        Plat("4x4x1", "blue", 0, 2, 30.5f);
        Plat("4x4x1", "blue", 0, 3, 35);
        Plat("4x4x1", "blue", 0, 4, 39.5f);
        // sliding platform over the gap
        Mover("4x4x1", "yellow", new Vector3(-4, 4, 47), new Vector3(4, 4, 47), 4.5f);
        Plat("6x6x1", "green", 0, 4, 54);
        Spring(0, 4, 55.5f, "red");
        Deco("cone", 2.4f, 4, 52); Deco("cone", -2.4f, 4, 52);
        Plat("6x6x1", "green", 0, 10, 63);
        Checkpoint(0, 10, 63);
        // conveyors shove you sideways; star 2 waits below with a spring back up
        Conveyor("4x4x1", "blue", 0, 10, 69, -90, 2.6f);
        Conveyor("4x4x1", "blue", 0, 10, 73, -90, 2.6f);
        Conveyor("4x4x1", "blue", 0, 10, 77, -90, 2.6f);
        Plat("2x2x1", "yellow", -6.5f, 6, 73); Spring(-6.5f, 6, 73, "yellow"); Star(-6.5f, 9.3f, 73);
        // the sweeper
        Plat("6x6x1", "green", 0, 10, 84);
        Spinner(0, 10, 84, 6, 95, "red");
        Star(0, 13.1f, 84);
        Plat("2x2x1", "blue", 1.6f, 9.5f, 91.5f);
        Plat("2x2x1", "blue", -1.2f, 9f, 96.5f);
        Plat("6x6x2", "green", 0, 9, 103);
        Finish(0, 9, 103.5f);
        Deco("hoop_blue", -9, 2, 33, 30, 1.4f); Deco("arch_tall_yellow", 9, 6, 56, -20, 1.2f);
    }

    // ================================================================== 2. SUNSET TOWER (medium)
    void Tower()
    {
        skyTop = Kit.Hex("#FF8A5B"); skyBottom = Kit.Hex("#FFE0A8"); fog = Kit.Hex("#FFD3A0");
        sunColor = Kit.Hex("#FFE3B8"); ambient = Kit.Hex("#E8A98C");
        start = new Vector3(0, 0, -1); startYaw = 0;
        path.Add(start);
        Plat("6x6x4", "yellow", 0, 0, 0);
        // crumbling tiles
        Fader("2x2x1", "red", 0, 0, 7.5f);
        Fader("2x2x1", "red", 0, 0, 12.5f);
        Fader("2x2x1", "red", 0, 0, 17.5f);
        Plat("4x4x1", "yellow", 0, 0.5f, 23);
        // elevator
        Mover("4x4x1", "blue", new Vector3(0, 0.5f, 27.5f), new Vector3(0, 6.5f, 27.5f), 5f);
        Plat("6x6x1", "yellow", 0, 6.5f, 32.5f);
        Checkpoint(0, 6.5f, 32.5f, 90);
        // pusher bridge heading +x
        Plat("6x2x1", "red", 6, 6.5f, 32.5f);
        Plat("6x2x1", "red", 12, 6.5f, 32.5f);
        Pusher("barrier_2x1x2_blue", new Vector3(7.5f, 6.5f, 29.5f), new Vector3(7.5f, 6.5f, 35.5f), 2.6f, 0, 90);
        Pusher("barrier_2x1x2_blue", new Vector3(12.5f, 6.5f, 35.5f), new Vector3(12.5f, 6.5f, 29.5f), 2.6f, 0.3f, 90);
        Star(10, 9.6f, 32.5f);
        Plat("6x6x2", "yellow", 18, 6.5f, 32.5f);
        // hop up the blocks heading -z
        Plat("2x2x2", "blue", 18, 8, 26);
        Plat("2x2x2", "blue", 18, 9.5f, 21);
        Plat("2x2x2", "blue", 18, 11, 16);
        // spinning ring
        Rotator("platform_hole_6x6x1_red", 18, 11, 9, 50);
        Star(18, 12.1f, 9);
        Plat("4x4x1", "yellow", 18, 11, 1.5f);
        Checkpoint(18, 11, 1.5f, -90);
        // spring across to the upper deck
        Spring(18, 11, 0.5f, "red");
        Star(15.5f, 18.4f, 0.5f);
        Plat("6x6x1", "yellow", 10, 16, 0.5f);
        Spinner(10, 16, 0.5f, 6, 140, "blue");
        // conveyors fighting you
        Conveyor("4x4x1", "blue", 4.5f, 16, 0.5f, 90, 3.6f);
        Conveyor("4x4x1", "blue", 0.5f, 16, 0.5f, 90, 3.6f);
        Plat("4x4x1", "yellow", -3.5f, 16, 0.5f);
        // last ride
        Mover("4x4x1", "red", new Vector3(-9.5f, 16, 0.5f), new Vector3(-15.5f, 16, 0.5f), 4.2f);
        Plat("6x6x2", "yellow", -21, 16, 0.5f);
        Finish(-21.5f, 16, 0.5f, -90);
        // the tower itself (scenery in the middle of the spiral)
        Deco("pillar_2x2x8", 9, -2, 17, 0, 1.6f); Deco("pillar_2x2x8", 9, 10.5f, 17, 0, 1.2f);
        Deco("structure_C", 9, 20.1f, 17, 0, 2f);
    }

    // ================================================================== 3. STORM PEAK (hard)
    void Storm()
    {
        skyTop = Kit.Hex("#2A2F5E"); skyBottom = Kit.Hex("#8C7BB8"); fog = Kit.Hex("#7A6EA6");
        sunColor = Kit.Hex("#DCD6FF"); ambient = Kit.Hex("#6C6A9C");
        start = new Vector3(0, 0, -1); startYaw = 0;
        path.Add(start);
        Plat("6x6x4", "red", 0, 0, 0);
        // planks and tiny posts
        Wood("floor_wood_2x6", 0, 0, 6, 90);
        Wood("platform_wood_1x1x1", 1, 0, 13.5f);
        Wood("platform_wood_1x1x1", -1, 0.5f, 18);
        Wood("platform_wood_1x1x1", 0.5f, 1, 22.5f);
        Star(-4, 1.6f, 18); Wood("platform_wood_1x1x1", -4, 0.2f, 18);
        Plat("4x4x1", "red", 0, 1, 28);
        Checkpoint(0, 1, 28);
        // narrow bridge with twin sweepers
        Plat("6x2x1", "blue", 0, 1, 33, 90);
        Plat("6x2x1", "blue", 0, 1, 39, 90);
        Spinner(0, 1, 33, 4, 130, "red");
        Spinner(0, 1, 39, 4, -130, "red", 90);
        // crossing movers
        Mover("2x2x1", "yellow", new Vector3(-3, 1, 46.5f), new Vector3(3, 1, 46.5f), 2.6f);
        Mover("2x2x1", "yellow", new Vector3(3, 1, 51.5f), new Vector3(-3, 1, 51.5f), 2.6f);
        Star(0, -1.2f, 49); Plat("2x2x1", "yellow", 0, -2.3f, 49); Spring(0, -2.3f, 49, "yellow");
        Plat("4x4x1", "red", 0, 1, 57);
        // guarded steps
        Plat("2x2x2", "blue", 0, 2.5f, 62);
        Plat("2x2x2", "blue", 0, 4, 66.5f);
        Plat("2x2x2", "blue", 0, 5.5f, 71);
        Pusher("barrier_1x1x1_red", new Vector3(-2.5f, 4, 66.5f), new Vector3(2.5f, 4, 66.5f), 1.8f);
        Pusher("barrier_1x1x1_red", new Vector3(2.5f, 5.5f, 71), new Vector3(-2.5f, 5.5f, 71), 1.8f, 0.25f);
        Plat("6x6x1", "blue", 0, 5.5f, 77);
        Checkpoint(0, 5.5f, 77);
        // crumbling causeway: keep moving
        for (int i = 0; i < 4; i++) Fader("2x2x1", "red", i % 2 == 0 ? 1 : -1, 5.5f, 84.5f + i * 5);
        Plat("4x4x1", "red", 0, 5.5f, 105);
        Spring(0, 5.5f, 106, "blue");
        Rotator("platform_hole_6x6x1_blue", 0, 11, 113, -70);
        Star(0, 14.5f, 113);
        // treadmill to the line
        Conveyor("4x4x1", "blue", 0, 11, 120, 180, 3.2f);
        Conveyor("4x4x1", "blue", 0, 11, 124, 180, 3.2f);
        Plat("6x6x1", "red", 0, 11, 130);
        Spinner(0, 11, 130, 6, 160, "yellow");
        Plat("6x6x2", "red", 0, 11, 137);
        Finish(0, 11, 137.5f);
    }

    // ------------------------------------------------------------------ backdrop
    void Scenery()
    {
        var rng = new System.Random(17 + index * 31);
        var cloud = new Material(Shader.Find("Standard")) { color = new Color(1, 1, 1, 1) };
        cloud.SetFloat("_Glossiness", 0f);
        var cloudDark = new Material(cloud) { color = Color.Lerp(Color.white, fog, 0.5f) };
        var sb = new Bounds(start, Vector3.one);
        foreach (var p in pieces) sb.Encapsulate(p.t.position);
        // clouds drifting below and around the course
        for (int i = 0; i < 40; i++)
        {
            var c = Kit.MeshObject("cloud", Kit.UVSphere);
            c.transform.SetParent(root, false);
            float x = sb.center.x + (float)(rng.NextDouble() - 0.5) * (sb.size.x + 140f);
            float z = sb.center.z + (float)(rng.NextDouble() - 0.5) * (sb.size.z + 140f);
            float y = (float)rng.NextDouble() < 0.7 ? -18f - (float)rng.NextDouble() * 10f : sb.max.y + 8f + (float)rng.NextDouble() * 16f;
            if (Mathf.Abs(x - sb.center.x) < sb.extents.x + 10 && Mathf.Abs(z - sb.center.z) < sb.extents.z + 10 && y > -10) y = -22f;
            c.transform.position = new Vector3(x, y, z);
            float s = 6f + (float)rng.NextDouble() * 12f;
            c.transform.localScale = new Vector3(s * 1.8f, s * 0.55f, s);
            var mr = c.GetComponent<MeshRenderer>(); mr.sharedMaterial = y < 0 ? cloud : cloudDark;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        // distant floating islands
        string[] cols = { "green", "blue", "yellow", "red" };
        for (int i = 0; i < 9; i++)
        {
            float a = i / 9f * Mathf.PI * 2f + (float)rng.NextDouble();
            float r = 70f + (float)rng.NextDouble() * 40f;
            var pos = sb.center + new Vector3(Mathf.Cos(a) * r, -6f + (float)rng.NextDouble() * 20f, Mathf.Sin(a) * r);
            var col = cols[(i + index) % cols.Length];
            Deco("platform_6x6x4_" + col, pos.x, pos.y, pos.z, rng.Next(4) * 90, 1.6f + (float)rng.NextDouble());
            if (rng.Next(2) == 0) Deco("pipe_90_A_" + cols[(i + 1) % 4], pos.x + 2f, pos.y + 6.4f, pos.z, rng.Next(4) * 90, 1.5f);
            else Deco("arch_" + cols[(i + 2) % 4], pos.x, pos.y + 6.4f, pos.z, rng.Next(4) * 90, 1.6f);
        }
    }

    // ------------------------------------------------------------------ per frame
    // T is the run clock (seconds since GO; negative during the countdown) and drives all shared motion.
    public void Tick(float T, float dt)
    {
        foreach (var p in dynamic)
        {
            p.prev = p.cur;
            switch (p.kind)
            {
                case Kind.Mover:
                case Kind.Knocker when p.period > 0:
                {
                    float k = 0.5f - 0.5f * Mathf.Cos((T / p.period + p.phase) * Mathf.PI * 2f);
                    p.t.position = Vector3.Lerp(p.a, p.b, k);
                    break;
                }
                case Kind.Rotator:
                case Kind.Knocker:
                    p.t.rotation = Quaternion.Euler(0, p.yaw0 + p.speed * T, 0);
                    break;
                case Kind.Fader:
                    TickFader(p, dt);
                    break;
            }
            p.cur = p.t.localToWorldMatrix;
        }
        foreach (var p in pieces)
            if (p.kind == Kind.Spring && p.squash > 0)
            {
                p.squash = Mathf.Max(0, p.squash - dt * 4f);
                float s = 1f - Mathf.Sin(p.squash * Mathf.PI) * 0.35f;
                p.t.localScale = new Vector3(p.baseScale.x * (2f - s), p.baseScale.y * s, p.baseScale.z * (2f - s));
            }
        // stars bob and spin
        for (int i = 0; i < stars.Count; i++)
            if (stars[i] && stars[i].gameObject.activeSelf)
                stars[i].rotation = Quaternion.Euler(0, Time.time * 120f + i * 40f, 0);
    }

    public void StepOn(Piece p)
    {
        if (p.kind == Kind.Fader && p.fade == 0) { p.fade = 1; p.fadeT = 0.5f; Sfx.I.Crumble(); }
    }

    void TickFader(Piece p, float dt)
    {
        if (p.fade == 0) return;
        p.fadeT -= dt;
        switch (p.fade)
        {
            case 1:   // shaking
                foreach (var r in p.rends) r.transform.localPosition = ShakeBase(r) + new Vector3(Mathf.Sin(Time.time * 70f) * 0.05f, 0, Mathf.Cos(Time.time * 55f) * 0.04f);
                if (p.fadeT <= 0) { p.fade = 2; p.fadeT = 2.2f; p.col.enabled = false; foreach (var r in p.rends) r.enabled = false; }
                break;
            case 2:   // gone
                if (p.fadeT <= 0) { p.fade = 3; p.fadeT = 0.25f; p.col.enabled = true; foreach (var r in p.rends) { r.enabled = true; r.transform.localPosition = ShakeBase(r); } }
                break;
            case 3:   // pop back in
                p.t.localScale = p.baseScale * Kit.EaseOutBack(1f - Mathf.Clamp01(p.fadeT / 0.25f));
                if (p.fadeT <= 0) { p.fade = 0; p.t.localScale = p.baseScale; }
                break;
        }
    }

    readonly Dictionary<Renderer, Vector3> shakeBase = new Dictionary<Renderer, Vector3>();
    Vector3 ShakeBase(Renderer r)
    {
        if (!shakeBase.TryGetValue(r, out var v)) shakeBase[r] = v = r.transform.localPosition;
        return v;
    }

    public void ResetRun()
    {
        foreach (var p in pieces)
            if (p.kind == Kind.Fader && p.fade != 0)
            {
                p.fade = 0; p.col.enabled = true; p.t.localScale = p.baseScale;
                foreach (var r in p.rends) { r.enabled = true; r.transform.localPosition = ShakeBase(r); }
            }
        foreach (var s in stars) if (s) s.gameObject.SetActive(true);
        foreach (var c in cps) c.hit = false;
    }

    public void Destroy() { if (root) Object.Destroy(root.gameObject); }
}
