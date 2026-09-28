using UnityEngine;

namespace Lockdown {

/// <summary>
/// Touch it to get the direction back; let it run out and the direction
/// is gone (recoverable only via the mercy rule).
/// The countdown ring is MANDATORY - with no audio it is the only warning before what is
/// the harshest event in the game.
/// </summary>
public class Orb : MonoBehaviour {
    public float lifetime = 15f;
    public Transform countdownRing;
    public SpriteRenderer ringRenderer, iconRenderer;

    [Header("Patrol")]
    [Tooltip("Units per second along its short path. Must stay well under the player's 5 " +
             "or the orb becomes impossible to intercept.")]
    public float patrolSpeed = 1.5f;

    public Direction Dir { get; private set; }

    const float PopTime = 0.25f;
    const float PulseAmount = 0.2f;
    /// <summary>Smallest the pulse makes the orb (and its pickup collider). OrbSpawner sizes
    /// its "close enough to touch" rule on this.</summary>
    public const float PulseMin = 1f - PulseAmount;

    float _dieAt;
    float _poppedAt = -1f;
    Vector3 _baseScale = Vector3.one;

    Vector2 _a, _b, _target;
    bool _patrols;

    /// <summary>Ends of the patrol sweep (both the orb's position if it sits still).</summary>
    public Vector2 PatrolA => _patrols ? _a : (Vector2)transform.position;
    public Vector2 PatrolB => _patrols ? _b : (Vector2)transform.position;

    void Awake() { _baseScale = transform.localScale; }

    public void Init(Direction d, Color c) {
        Dir = d;
        _dieAt = Time.unscaledTime + lifetime;   // unscaled: should feel like 15 real seconds
        if (ringRenderer != null) ringRenderer.color = c;
        if (iconRenderer != null) {
            iconRenderer.color = c;
            iconRenderer.transform.localRotation = Quaternion.Euler(0, 0, d switch {
                Direction.Up => 0f, Direction.Right => -90f, Direction.Down => 180f, _ => 90f });
        }
    }

    /// <summary>
    /// Give the orb a short back-and-forth path, crawler style.
    ///
    /// The axis matches the direction that was lost - horizontal for Left/Right, vertical for
    /// Up/Down - so the orb sweeps along the axis the player can no longer travel on. You
    /// cannot chase it, so you position yourself and intercept it on a pass.
    ///
    /// Both endpoints are chosen by OrbSpawner to sit INSIDE the player's reachable region.
    /// A patrol that wandered outside it would leave the orb somewhere the player can never go.
    /// </summary>
    public void SetPatrol(Vector2 a, Vector2 b) {
        _a = a; _b = b; _target = b;
        _patrols = (a - b).sqrMagnitude > 0.0001f;
        // Start at the middle of the path, which is the spot the spawner actually chose -
        // starting at an end would put the orb up to half a sweep away from it.
        transform.position = (a + b) * 0.5f;
    }

    void Update() {
        float left = _dieAt - Time.unscaledTime;
        if (left <= 0f) { Despawn(); return; }

        bool frozen = LevelManager.I != null && LevelManager.I.Frozen;
        if (_patrols && !frozen) {
            transform.position = Vector2.MoveTowards(transform.position, _target,
                                                     patrolSpeed * Time.deltaTime);
            if (((Vector2)transform.position - _target).sqrMagnitude < 0.0001f)
                _target = _target == _a ? _b : _a;
        }

        if (countdownRing != null) {
            float t = left / lifetime;
            countdownRing.localScale = Vector3.one * Mathf.Lerp(0.15f, 1.15f, t);
        }
        // Pulse AROUND the prefab's own scale. Assigning Vector3.one here threw away the
        // 0.8 the prefab was built at, so every orb rendered 25% oversized.
        float pulse = 1f + PulseAmount * Mathf.Sin(Time.unscaledTime * (Mathf.PI * 2f / 0.8f));
        // Grows back in after a relocation so the jump reads as deliberate, not a glitch.
        float pop = _poppedAt < 0f ? 1f : Mathf.Clamp01((Time.unscaledTime - _poppedAt) / PopTime);
        transform.localScale = _baseScale * pulse * pop;
    }

    /// <summary>Moved by OrbSpawner when a newer loss left this orb unreachable. The timer
    /// keeps running - relocation fixes fairness, it isn't a refund.</summary>
    public void Relocate(Vector2 to) {
        transform.position = to;
        // The old path belongs to the old spot. Drop it, or the orb would drift straight back
        // toward its previous sweep; the spawner gives it a fresh one via SetPatrol.
        _patrols = false;
        _poppedAt = Time.unscaledTime;
    }

    void Despawn() {
        if (DirectionSystem.I != null) DirectionSystem.I.MarkPermanent(Dir);
        Destroy(gameObject);
    }

    /// <summary>Retired silently when the mercy rule returns this orb's direction: no
    /// permanent-loss penalty, since the direction has already been given back.</summary>
    public void RetireSilently() => Destroy(gameObject);

    void OnTriggerEnter2D(Collider2D other) {
        if (other.gameObject.layer != Layers.Player) return;
        if (DirectionSystem.I != null) DirectionSystem.I.Restore(Dir);
        if (CameraShake.I != null) CameraShake.I.Shake(0.08f, 0.1f);
        Destroy(gameObject);
    }
}
}
