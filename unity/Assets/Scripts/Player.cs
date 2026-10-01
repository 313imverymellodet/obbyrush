using UnityEngine;

// The local runner: a CharacterController with platformer feel (coyote time, jump buffer,
// variable jump height, one air jump), carried by moving platforms and knocked by sweepers.
public class Player : MonoBehaviour
{
    public CharacterController CC;
    public Avatar Av;
    public Vector3 Vel;
    public float Yaw;
    public bool Grounded;
    public int AnimId;
    public bool Stunned => stun > 0;
    public float Speed01 => new Vector2(Vel.x, Vel.z).magnitude / RunSpeed;

    public const float RunSpeed = 8f, Gravity = 34f, JumpV = 12.2f, AirJumpV = 10.6f, SpringV = 21.5f, MaxFall = 32f;
    const float Coyote = 0.11f, Buffer = 0.13f;

    float coyote, buffer, stun, airTime, landT, spawnT;
    int airJumps;
    bool jumpHeld;
    Collider groundCol, frameGround;
    Vector3 groundNormal = Vector3.up;
    public System.Action OnJumped, OnLanded;

    public static Player Create(string skin, Transform parent)
    {
        var go = new GameObject("Player");
        go.transform.SetParent(parent, false);
        var p = go.AddComponent<Player>();
        p.CC = go.AddComponent<CharacterController>();
        p.CC.height = 1.5f; p.CC.radius = 0.36f; p.CC.center = new Vector3(0, 0.77f, 0);
        p.CC.stepOffset = 0.35f; p.CC.slopeLimit = 50f; p.CC.skinWidth = 0.04f; p.CC.minMoveDistance = 0f;
        p.SetSkin(skin);
        return p;
    }

    public void SetSkin(string skin)
    {
        Av?.Destroy();
        Av = new Avatar(skin, transform);
        Av.Root.transform.localRotation = Quaternion.Euler(0, Yaw, 0);
        AnimId = -1;
    }

    public void Teleport(Vector3 pos, float yaw)
    {
        CC.enabled = false;
        transform.position = pos + Vector3.up * 0.05f;
        CC.enabled = true;
        Vel = Vector3.zero; Yaw = yaw; stun = 0; airJumps = 0; coyote = 0; buffer = 0;
        Av.Root.transform.localRotation = Quaternion.Euler(0, Yaw, 0);
        groundCol = null;
    }

    public void Respawn(Vector3 pos, float yaw)
    {
        Teleport(pos, yaw);
        spawnT = 0.45f;
        SetAnim(Anim.Spawn);
    }

    void OnControllerColliderHit(ControllerColliderHit h)
    {
        if (h.normal.y > 0.55f && h.point.y < transform.position.y + 0.4f) { frameGround = h.collider; groundNormal = h.normal; }
        // bonk your head on a ceiling
        else if (h.normal.y < -0.5f && Vel.y > 0) Vel.y = 0;
    }

    public void Knock(Vector3 push)
    {
        if (stun > 0.15f) return;
        push.y = Mathf.Max(push.y, 6.5f);
        Vel = push; stun = 0.45f; Grounded = false; groundCol = null;
        SetAnim(Anim.Hit);
        Sfx.I.Bonk();
    }

    public void SetAnim(int id, float run01 = 1f)
    {
        if (id == AnimId && id != Anim.Run) return;
        AnimId = id;
        Av.Set(id, run01);
    }

    // `move` is a world-space direction (camera relative, magnitude 0..1).
    public void Tick(float dt, Vector3 move, bool jumpPressed, bool jumpDown, Course course, bool control)
    {
        if (!control) { move = Vector3.zero; jumpPressed = false; jumpDown = false; }
        if (stun > 0) { stun -= dt; move = Vector3.zero; jumpPressed = false; }
        spawnT -= dt;
        if (jumpPressed) buffer = Buffer; else buffer -= dt;

        // ride whatever we stood on last frame
        var gp = course.Of(groundCol);
        if (gp != null && gp.moving)
        {
            var delta = gp.cur * gp.prev.inverse;
            var np = delta.MultiplyPoint3x4(transform.position);
            CC.Move(np - transform.position);
            if (gp.kind == Kind.Rotator) Yaw += gp.speed * dt;
        }
        if (gp != null && gp.kind == Kind.Conveyor) CC.Move(gp.dir * gp.push * dt);

        // horizontal: snappy on the ground, a little floatier in the air
        var want = move * RunSpeed;
        var hv = new Vector3(Vel.x, 0, Vel.z);
        float accel = Grounded ? (move.sqrMagnitude > 0.01f ? 70f : 55f) : (move.sqrMagnitude > 0.01f ? 38f : 6f);
        if (stun > 0) accel = 4f;
        hv = Vector3.MoveTowards(hv, want, accel * dt);
        Vel.x = hv.x; Vel.z = hv.z;

        // jump
        bool canGround = Grounded || coyote > 0;
        if (buffer > 0 && stun <= 0)
        {
            if (canGround)
            {
                Vel.y = JumpV; buffer = 0; coyote = 0; Grounded = false; groundCol = null;
                SetAnim(Anim.Jump); Sfx.I.Jump(); OnJumped?.Invoke();
            }
            else if (airJumps > 0)
            {
                airJumps--; Vel.y = AirJumpV; buffer = 0;
                SetAnim(Anim.Flip); Sfx.I.AirJump(); FX.Puff(transform.position, 6);
            }
        }
        // variable height: let go early to hop
        if (!jumpDown && jumpHeld && Vel.y > 4f && stun <= 0) Vel.y *= 0.55f;
        jumpHeld = jumpDown;

        if (!Grounded) Vel.y = Mathf.Max(Vel.y - Gravity * dt, -MaxFall);
        else Vel.y = -3f;

        frameGround = null;
        var flags = CC.Move(Vel * dt);
        bool was = Grounded;
        Grounded = (flags & CollisionFlags.Below) != 0 && frameGround != null;
        if (Grounded)
        {
            groundCol = frameGround;
            coyote = Coyote; airJumps = 1;
            var p = course.Of(groundCol);
            if (p != null)
            {
                if (p.kind == Kind.Spring && Vel.y <= 0.5f)
                {
                    Vel.y = SpringV; Grounded = false; groundCol = null; airJumps = 1; p.squash = 1f;
                    SetAnim(Anim.Jump); Sfx.I.Spring(); FX.Puff(transform.position, 10);
                }
                else course.StepOn(p);
            }
            if (!was && Grounded)
            {
                if (airTime > 0.35f) { landT = 0.18f; FX.Puff(transform.position, 5); Sfx.I.Land(); }
                OnLanded?.Invoke();
            }
            airTime = 0;
        }
        else
        {
            if (was) groundCol = null;
            coyote -= dt; airTime += dt;
            if (!was) groundCol = null;
        }

        // face where we're going
        var flat = new Vector3(Vel.x, 0, Vel.z);
        if (flat.sqrMagnitude > 0.5f && stun <= 0) Yaw = Mathf.MoveTowardsAngle(Yaw, Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg, 900f * dt);
        Av.Root.transform.localRotation = Quaternion.Euler(0, Yaw, 0);

        // animation
        landT -= dt;
        if (spawnT > 0) { }
        else if (stun > 0) { }
        else if (Grounded)
        {
            float sp = flat.magnitude / RunSpeed;
            if (landT > 0 && sp < 0.3f) SetAnim(Anim.Land);
            else if (sp > 0.12f) SetAnim(Anim.Run, sp);
            else SetAnim(Anim.Idle);
        }
        else if (Vel.y < -2f && (AnimId == Anim.Run || AnimId == Anim.Idle || AnimId == Anim.Land || airTime > 0.45f)) SetAnim(Anim.Fall);

        // sweepers and pushers
        var centre = transform.position + Vector3.up * 0.8f;
        foreach (var k in course.knockers)
        {
            var cp = k.col.ClosestPoint(centre);
            var d = centre - cp; d.y *= 0.6f;
            if (d.sqrMagnitude > 0.5f * 0.5f) continue;
            var v = k.PointVelocity(cp, dt); v.y = 0;
            var away = d; away.y = 0;
            var dir = v.sqrMagnitude > 1f ? v.normalized : away.sqrMagnitude > 0.0001f ? away.normalized : -Av.Root.transform.forward;
            // a shove, not a cannon: a hit sends you ~2-4 m, so a quick recovery jump can save it
            Knock(dir * Mathf.Clamp(v.magnitude * 0.8f + 4f, 6f, 11f) + Vector3.up * 6f);
            break;
        }
    }
}
