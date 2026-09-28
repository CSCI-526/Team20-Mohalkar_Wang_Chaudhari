using System;
using UnityEngine;

namespace Lockdown {

/// <summary>
/// Owns which of the four movement directions the player currently has.
///
/// Three states per direction:
///   Active                  - usable
///   Lost, orb live          - recoverable by touching the orb
///   Lost past despawn       - "permanent", recoverable ONLY via the mercy rule
///
/// Hearts are health, directions are mobility. They are separate systems and are not
/// expected to agree.
/// </summary>
public class DirectionSystem : MonoBehaviour {
    public static DirectionSystem I { get; private set; }

    [Header("Mercy rule")]
    [Tooltip("Seconds of total immobility before one direction is awarded back. Unscaled.")]
    public float mercyDelay = 2.0f;

    readonly bool[] _active    = { true, true, true, true };
    readonly bool[] _permanent = { false, false, false, false };

    float _mercyAt = -1f;

    public event Action<Direction> OnLost;
    public event Action<Direction> OnRestored;
    public event Action<Direction> OnPermanent;
    /// <summary>Raised when the mercy rule hands a direction back. OrbSpawner listens so it
    /// can silently retire that direction's live orb - see MercyAward() for why.</summary>
    public event Action<Direction> OnMercyAward;
    public event Action<bool>      OnMercyWindowChanged;

    public bool InMercyWindow => _mercyAt >= 0f;
    /// <summary>0..1 fill for the on-player ring. Two seconds of total immobility with no
    /// on-screen explanation reads as a crash, not a mechanic.</summary>
    public float MercyProgress =>
        _mercyAt < 0f ? 0f : Mathf.Clamp01(1f - (_mercyAt - Time.unscaledTime) / mercyDelay);

    void Awake() {
        I = this;
        for (int i = 0; i < Dir.Count; i++) { _active[i] = true; _permanent[i] = false; }
        _mercyAt = -1f;
    }

    void OnDestroy() { if (I == this) I = null; }

    public bool IsActive(Direction d)    => d != Direction.None && _active[(int)d];
    public bool IsPermanent(Direction d) => d != Direction.None && _permanent[(int)d];
    /// <summary>What the mercy rule would hand back right now. OrbSpawner places orbs
    /// around it when all four are gone.</summary>
    public Direction NextMercyAward {
        get {
            foreach (Direction d in Dir.Priority) if (!_active[(int)d]) return d;
            return Direction.None;
        }
    }
    public bool AnyActive() { for (int i = 0; i < Dir.Count; i++) if (_active[i]) return true; return false; }

    /// <summary>True once the player has lost at least one direction. Gates firing: the gun
    /// is what a lost direction buys you (see PlayerShooting).</summary>
    public bool AnyLost() { for (int i = 0; i < Dir.Count; i++) if (!_active[i]) return true; return false; }

    // ---------------------------------------------------------------- quadrant rule

    /// <summary>
    /// Which direction a bullet travelling <paramref name="travel"/> takes.
    ///
    /// Takes the two cardinals of the opposite quadrant and PREFERS WHICHEVER IS STILL ALIVE.
    /// That preference buys three things for free:
    ///   1. Losses spread evenly. The arena is 20 wide x 12 tall with turrets at x = +/-8, so
    ///      the horizontal component dominates most shots - a plain "snap to dominant axis"
    ///      rule would take Left/Right over and over.
    ///   2. Every hit lands on something live whenever anything is live, so every hit removes
    ///      a direction and spawns an orb. No silent no-op hits.
    ///   3. It makes a separate "next available in priority order" fallback almost unnecessary.
    ///
    /// Returns Direction.None only when both candidates are already gone; the mercy rule
    /// covers that case, so the caller just charges a heart and removes nothing.
    /// </summary>
    public Direction ResolveLoss(Vector2 travel) {
        Dir.Candidates(travel, out Direction h, out Direction v);
        bool hLive = IsActive(h), vLive = IsActive(v);

        if (hLive && !vLive) return h;
        if (vLive && !hLive) return v;
        if (!hLive && !vLive) return Direction.None;

        // Both available: dominant axis decides, so early hits stay learnable.
        // >= sends an exact 45-degree tie to horizontal. Which way it breaks doesn't
        // matter; that it breaks CONSISTENTLY does.
        return Mathf.Abs(travel.x) >= Mathf.Abs(travel.y) ? h : v;
    }

    /// <summary>Applies a hit. Returns the direction removed, or None if nothing was left
    /// to take (the hit still costs a heart).</summary>
    public Direction ApplyHit(Vector2 travel) {
        Direction d = ResolveLoss(travel);
        if (d == Direction.None) return Direction.None;
        Lose(d);
        return d;
    }

    /// <summary>Removes one specific direction. ApplyHit's back half; also the editor-only
    /// debug keys in PlayerController.</summary>
    public void Lose(Direction d) {
        if (d == Direction.None || !_active[(int)d]) return;
        _active[(int)d] = false;
        _permanent[(int)d] = false;
        OnLost?.Invoke(d);
        EvaluateMercy();
    }

    // ---------------------------------------------------------------- recovery

    public void Restore(Direction d) {
        if (d == Direction.None || _active[(int)d]) return;
        _active[(int)d] = true;
        _permanent[(int)d] = false;
        CancelMercyIfMobile();
        OnRestored?.Invoke(d);
    }

    /// <summary>Called by Orb when its 15s timer expires uncollected. "Permanent" means
    /// permanent unless the mercy rule restores it.</summary>
    public void MarkPermanent(Direction d) {
        if (d == Direction.None || _active[(int)d]) return;
        _permanent[(int)d] = true;
        OnPermanent?.Invoke(d);
    }

    // ---------------------------------------------------------------- mercy rule

    void EvaluateMercy() {
        if (!AnyActive() && _mercyAt < 0f) {
            _mercyAt = Time.unscaledTime + mercyDelay;   // unscaled: it's a promise to the
            OnMercyWindowChanged?.Invoke(true);          // player, not part of the sim
        }
    }

    void CancelMercyIfMobile() {
        if (_mercyAt >= 0f && AnyActive()) {
            _mercyAt = -1f;
            OnMercyWindowChanged?.Invoke(false);
        }
    }

    void Update() {
        if (_mercyAt < 0f) return;
        if (Time.unscaledTime < _mercyAt) return;
        MercyAward();
    }

    /// <summary>
    /// Hand one direction back so the player can never softlock.
    /// It REPEATS - lose the awarded direction again and another window starts. That makes
    /// hearts the sole death clock, which is the point.
    /// </summary>
    void MercyAward() {
        Direction award = NextMercyAward;

        _mercyAt = -1f;
        OnMercyWindowChanged?.Invoke(false);
        if (award == Direction.None) return;

        _active[(int)award] = true;
        _permanent[(int)award] = false;

        // Sub-rule 2: if that direction has a live orb, it is retired SILENTLY - no dark
        // particles, no permanent-loss penalty. It has been redeemed. Without this the
        // player gets the direction free AND can still collect its orb for a 2nd restore.
        OnMercyAward?.Invoke(award);
        OnRestored?.Invoke(award);
    }

    /// <summary>Test hook. Lets EditMode tests drive the quadrant rule without a scene.</summary>
    public void TestSetActive(Direction d, bool active) => _active[(int)d] = active;
}
}
