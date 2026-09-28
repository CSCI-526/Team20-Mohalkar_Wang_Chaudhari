using UnityEngine;

namespace Lockdown {

/// <summary>
/// Movement with the direction lock, plus the fin visuals.
///
/// Rigidbody2D MUST be Dynamic with Gravity Scale 0. A Kinematic
/// body does not resolve collisions - you walk straight through pillars - and Unity's
/// Dynamic default is Gravity Scale 1, which slowly drags you to the bottom of the arena.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour {
    public float speed = 5f;

    [Header("Fins (index order: Up, Down, Left, Right)")]
    public SpriteRenderer[] fins = new SpriteRenderer[4];
    public Transform aimDot;
    public float aimDotRadius = 0.22f;

    [Header("Wall bounce (outer arena walls only - pillars stay solid cover)")]
    [Tooltip("Bounce-back speed = the speed you hit the wall with, times this. " +
             "Head-on is 5 u/s; at 45 degrees only 3.5 u/s of it is heading into the wall.")]
    public float bounceSpeedFactor = 1f;
    [Tooltip("The bounce carries you back to this point's line - the arena centre.")]
    public Vector2 arenaCentre = Vector2.zero;
    [Tooltip("The bounce slows down over this last stretch before it stops.")]
    public float bounceEaseDistance = 1f;
    [Tooltip("Hits slower than this into a wall just slide along it (grazing contact).")]
    public float minBounceSpeed = 1f;
    [Tooltip("Seconds after a bounce ends before the walls can bounce again.")]
    public float bounceCooldown = 0.1f;
    [Tooltip("A bounce never carries you closer than this to a crawler - being thrown " +
             "into an instant kill isn't fun.")]
    public float crawlerClearance = 1.5f;

    /// <summary>Test hook. When set, replaces keyboard movement input.</summary>
    public static Vector2? TestInput;

    Rigidbody2D _rb;
    Vector2 _lastVelocity, _lastInput;
    // Keys already held when the bounce started. Holding them doesn't cancel the bounce;
    // pressing anything else does - see PressedNewKey.
    Vector2 _heldAtBounce;

    // Per-axis bounce: +1/-1 = being carried that way along x (or y), 0 = not bouncing on
    // that axis. Per axis so a corner hit bounces on both at once.
    int _bx, _by;
    float _bsx, _bsy;
    float _bounceReadyAt = -1f;

    /// <summary>True while a wall bounce is carrying the player back toward the centre.</summary>
    public bool Bouncing => _bx != 0 || _by != 0;
    /// <summary>Counts bounces started. Test hook.</summary>
    public int BounceCount { get; private set; }

    void Awake() {
        _rb = GetComponent<Rigidbody2D>();
        _rb.bodyType     = RigidbodyType2D.Dynamic;
        _rb.gravityScale = 0f;
        _rb.freezeRotation = true;
        _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        _rb.interpolation = RigidbodyInterpolation2D.Interpolate;
    }

    /// <summary>
    /// Subscribes in Start, NOT OnEnable.
    ///
    /// Awake/OnEnable ordering between separate GameObjects is undefined, and the player and
    /// the Systems object are separate. If the player initialised first, DirectionSystem.I
    /// would still be null and an OnEnable subscription would silently do nothing, so the fins
    /// would never hide. Start runs after every Awake, so the singleton always exists by then.
    /// </summary>
    void Start() {
        if (DirectionSystem.I == null) return;
        DirectionSystem.I.OnLost      += RefreshFin;
        DirectionSystem.I.OnRestored  += RefreshFin;
        DirectionSystem.I.OnPermanent += RefreshFin;
        for (int i = 0; i < Dir.Count; i++) RefreshFin((Direction)i);   // seed initial state
    }

    void OnDisable() {
        if (DirectionSystem.I == null) return;
        DirectionSystem.I.OnLost      -= RefreshFin;
        DirectionSystem.I.OnRestored  -= RefreshFin;
        DirectionSystem.I.OnPermanent -= RefreshFin;
    }

    void Update() {
#if UNITY_EDITOR
        // Runs FIRST, ahead of the early returns below. These keys are how a tester removes a
        // direction, so they must work while the player is still unarmed at full mobility.
        DebugDirectionKeys();
#endif

        if (aimDot == null) return;

        // The aim dot doubles as the ARMED light. Firing is locked until a direction is
        // lost, and a click that silently does nothing reads as broken input, so the dot
        // only appears once the gun is live.
        var shooting = GetComponent<PlayerShooting>();
        bool armed = shooting == null || shooting.Armed;
        if (_aimDotSr == null) _aimDotSr = aimDot.GetComponent<SpriteRenderer>();
        if (_aimDotSr != null) _aimDotSr.enabled = armed;
        if (!armed) return;

        // Offsets toward the mouse - a centred dot cannot show a direction.
        Vector3 m = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        Vector2 dir = ((Vector2)(m - transform.position)).normalized;
        aimDot.localPosition = dir * aimDotRadius;
    }

    SpriteRenderer _aimDotSr;

    void FixedUpdate() {
        if (LevelManager.I != null && LevelManager.I.Frozen) {
            _rb.linearVelocity = _lastVelocity = Vector2.zero;
            _bx = _by = 0;
            return;
        }

        Vector2 input = TestInput ?? new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));

        var ds = DirectionSystem.I;
        if (ds != null) {
            if (!ds.IsActive(Direction.Left)  && input.x < 0) input.x = 0;
            if (!ds.IsActive(Direction.Right) && input.x > 0) input.x = 0;
            if (!ds.IsActive(Direction.Down)  && input.y < 0) input.y = 0;
            if (!ds.IsActive(Direction.Up)    && input.y > 0) input.y = 0;
        }

        // CRITICAL: without this, diagonals run at 7.07 u/s and
        // losing a direction makes you FASTER, inverting the entire mechanic.
        input = Vector2.ClampMagnitude(input, 1f);

        Vector2 v = input * speed;
        _lastInput = input;

        // Any new key press hands control straight back - a bounce you can't break out of
        // carries you into bullets you saw coming.
        if (Bouncing && PressedNewKey(input, ref _heldAtBounce)) EndBounce();

        // Applied AFTER the direction lock on purpose: a lost key stops the player choosing
        // that way, it doesn't stop the wall throwing them that way.
        if (Bouncing) v = BounceStep(v);
        _rb.linearVelocity = _lastVelocity = v;
    }

    // ---------------------------------------------------------------- wall bounce

    void OnCollisionEnter2D(Collision2D c) {
        if (LevelManager.I != null && LevelManager.I.Frozen) return;

        if (!IsArenaWall(c.collider)) {
            // Carried into a pillar or a turret: the bounce ends there instead of grinding.
            if (Bouncing) EndBounce();
            return;
        }
        if (!Bouncing && Time.time < _bounceReadyAt) return;

        bool wasBouncing = Bouncing;
        Vector2 n = InwardNormal(c);
        bool started = TryStartAxis(ref _bx, ref _bsx, n.x, _lastVelocity.x)
                     | TryStartAxis(ref _by, ref _bsy, n.y, _lastVelocity.y);
        if (!started) return;
        BounceCount++;
        // A corner's second wall joins the running bounce; only a fresh bounce resets
        // which keys count as "already held".
        if (!wasBouncing) _heldAtBounce = _lastInput;
    }

    /// <summary>Starts a bounce on one axis if this wall faces along it and the player was
    /// actually moving into it. Speed out = speed in (times the factor).</summary>
    bool TryStartAxis(ref int sign, ref float bounceSpeed, float normal, float velocity) {
        if (Mathf.Abs(normal) < 0.5f) return false;          // wall doesn't face this axis
        int away = normal > 0f ? 1 : -1;
        float approach = -velocity * away;                   // speed INTO the wall
        if (approach < minBounceSpeed) return false;
        sign = away;
        bounceSpeed = approach * bounceSpeedFactor;
        return true;
    }

    /// <summary>
    /// True if the player pressed a key that wasn't held when the bounce started. Letting go
    /// isn't a new press - the bounce carries on - but a released key stops counting as held,
    /// so pressing it again is. Signs only: a held D is the same key at any magnitude.
    /// </summary>
    public static bool PressedNewKey(Vector2 input, ref Vector2 held) {
        int ix = Sign(input.x), iy = Sign(input.y);
        if (ix == 0) held.x = 0f;
        if (iy == 0) held.y = 0f;
        return (ix != 0 && ix != Sign(held.x)) || (iy != 0 && iy != Sign(held.y));
    }

    static int Sign(float f) => f > 0.01f ? 1 : f < -0.01f ? -1 : 0;

    /// <summary>
    /// One physics step of an active bounce. Each bouncing axis is driven toward the centre
    /// line (or the crawler stop point), easing out over the last stretch; a key still held
    /// from before the bounce is overridden on that axis. The other axis keeps the input.
    /// </summary>
    Vector2 BounceStep(Vector2 v) {
        Vector2 pos = _rb.position;
        Vector2 target = new(_bx != 0 ? arenaCentre.x : pos.x, _by != 0 ? arenaCentre.y : pos.y);

        // Every crawler's CURRENT position, re-checked each step as they move. Not their
        // paths: the S-curve covers most of the arena (and its start-to-end line runs flat
        // across the middle), so guarding the path stopped most bounces almost instantly.
        // Late-phase crawlers appear mid-run, hence no caching.
        foreach (var cr in FindObjectsByType<Crawler>(FindObjectsSortMode.None)) {
            if (!cr.isActiveAndEnabled) continue;
            Vector2 at = cr.transform.position;
            target = StopShortOf(pos, target, at, at, crawlerClearance);
        }

        if (_bx != 0) {
            float left = (target.x - pos.x) * _bx;
            if (left <= 0.02f) _bx = 0;
            else v.x = _bx * EasedSpeed(_bsx, left, bounceEaseDistance);
        }
        if (_by != 0) {
            float left = (target.y - pos.y) * _by;
            if (left <= 0.02f) _by = 0;
            else v.y = _by * EasedSpeed(_bsy, left, bounceEaseDistance);
        }
        if (!Bouncing) _bounceReadyAt = Time.time + bounceCooldown;

        // A key held from before (e.g. W while sliding up the wall) shares the budget with
        // the bounce rather than adding to it.
        return Vector2.ClampMagnitude(v, Mathf.Max(speed, _bsx, _bsy));
    }

    void EndBounce() {
        _bx = _by = 0;
        _bounceReadyAt = Time.time + bounceCooldown;
    }

    /// <summary>Full speed until the last <paramref name="easeDistance"/>, then slows in
    /// proportion to what's left. Floored so it always arrives.</summary>
    public static float EasedSpeed(float bounceSpeed, float remaining, float easeDistance) {
        if (easeDistance <= 0f || remaining >= easeDistance) return bounceSpeed;
        return Mathf.Max(bounceSpeed * remaining / easeDistance, 0.6f);
    }

    /// <summary>The first point on from->to that comes within <paramref name="clearance"/> of
    /// the segment a-b, backed off one step; or <paramref name="to"/> if the line stays clear.</summary>
    public static Vector2 StopShortOf(Vector2 from, Vector2 to, Vector2 a, Vector2 b, float clearance) {
        const float Step = 0.05f;
        Vector2 d = to - from;
        float len = d.magnitude;
        if (len < 1e-4f) return to;
        Vector2 dir = d / len;
        for (float s = 0f; s <= len; s += Step)
            if (OrbPlacement.DistToSegment(from + dir * s, a, b) < clearance)
                return from + dir * Mathf.Max(0f, s - Step);
        return to;
    }

    /// <summary>The four boundary walls live under "Walls" (SceneBuilder.BuildWalls). Pillars
    /// share the Wall layer but sit under "Pillars", so they stay plain solid cover.</summary>
    static bool IsArenaWall(Collider2D col) =>
        col.gameObject.layer == Layers.Wall && col.transform.parent != null && col.transform.parent.name == "Walls";

    /// <summary>Average contact normal, flipped if needed so it points from the wall toward
    /// the player - Unity's 2D contact normal sign depends on which body reports it.</summary>
    Vector2 InwardNormal(Collision2D c) {
        Vector2 sum = Vector2.zero;
        for (int i = 0; i < c.contactCount; i++) {
            var cp = c.GetContact(i);
            Vector2 n = cp.normal;
            if (Vector2.Dot(n, (Vector2)transform.position - cp.point) < 0f) n = -n;
            sum += n;
        }
        return sum.sqrMagnitude < 1e-6f ? Vector2.zero : sum.normalized;
    }

    // ---------------------------------------------------------------- visuals

    void RefreshFin(Direction d) {
        if (fins == null || (int)d < 0 || (int)d >= fins.Length) return;
        var sr = fins[(int)d];
        if (sr != null) sr.enabled = DirectionSystem.I.IsActive(d);
    }

#if UNITY_EDITOR
    /// <summary>
    /// Editor-only playtest shortcut, never compiled into a build: 1/2/3/4 toggle
    /// Up/Down/Left/Right. Losing one goes through the real path (orb spawn, relocation of
    /// stranded orbs, mercy rule) - it just skips getting shot.
    /// </summary>
    void DebugDirectionKeys() {
        var ds = DirectionSystem.I;
        if (ds == null || (LevelManager.I != null && LevelManager.I.Frozen)) return;

        for (int i = 0; i < Dir.Count; i++) {
            if (!Input.GetKeyDown(KeyCode.Alpha1 + i)) continue;
            var d = (Direction)i;
            if (ds.IsActive(d)) {
                ds.Lose(d);
                if (OrbSpawner.I != null) OrbSpawner.I.SpawnFor(d);
            } else {
                ds.Restore(d);
                if (OrbSpawner.I != null) OrbSpawner.I.Retire(d);
            }
        }
    }
#endif
}
}
