using System;
using UnityEngine;

namespace Lockdown {

/// <summary>
/// Hearts are health; directions are mobility. They are separate systems, so the two
/// counts drift apart during a run.
/// </summary>
public class PlayerHealth : MonoBehaviour {
    public int maxHearts = 5;
    public float invulnDuration = 1.0f;

    [Header("Feedback (no audio, no slow-mo - shake carries it)")]
    public float hitstopOnHit = 0.10f;
    public float shakeOnHit   = 0.30f;

    public int Hearts { get; private set; }
    /// <summary>Bullets only. Explicitly does NOT cover the crawler.</summary>
    public bool Invulnerable => Time.time < _invulnUntil;

    public event Action<int> OnHeartsChanged;

    float _invulnUntil = -1f;
    SpriteRenderer _sr;

    void Awake() { Hearts = maxHearts; _sr = GetComponent<SpriteRenderer>(); }
    void Start()  { OnHeartsChanged?.Invoke(Hearts); }

    void Update() {
        if (_sr == null) return;
        // Flash white during i-frames.
        _sr.color = Invulnerable && Mathf.FloorToInt(Time.time * 20f) % 2 == 0
            ? new Color(1f, 1f, 1f, 0.35f) : Color.white;
    }

    /// <summary>A bullet landed. <paramref name="travel"/> is the bullet's POST-BOUNCE
    /// velocity - that is what the quadrant rule reads.</summary>
    public void TakeHit(Vector2 travel) {
        if (LevelManager.I != null && LevelManager.I.Frozen) return;
        if (Invulnerable) return;

        _invulnUntil = Time.time + invulnDuration;

        Hearts = Mathf.Max(0, Hearts - 1);
        OnHeartsChanged?.Invoke(Hearts);

        // If nothing is left to take, the hit STILL costs a heart and
        // removes nothing. The cooldown keeps running.
        Direction lost = DirectionSystem.I != null ? DirectionSystem.I.ApplyHit(travel) : Direction.None;
        if (lost != Direction.None && OrbSpawner.I != null) OrbSpawner.I.SpawnFor(lost);

        Hitstop.Freeze(hitstopOnHit);
        if (CameraShake.I != null) CameraShake.I.Shake(shakeOnHit, 0.25f);

        if (Hearts <= 0 && LevelManager.I != null) LevelManager.I.Die();
    }

    /// <summary>
    /// Crawler contact. Instant, regardless of hearts.
    ///
    /// The rule: i-frames NEVER save you from the crawler while you can
    /// move; the mercy window DOES, because you can't. I-frames follow a hit you could have
    /// dodged; the mercy window covers a state with no counterplay at all - and at tier 5 the
    /// crawler crosses its whole path in 1.33s, well inside the 2s freeze.
    /// </summary>
    public void KillByCrawler() {
        if (LevelManager.I != null && LevelManager.I.Frozen) return;
        if (DirectionSystem.I != null && DirectionSystem.I.InMercyWindow) return;

        Hearts = 0;
        OnHeartsChanged?.Invoke(Hearts);
        if (CameraShake.I != null) CameraShake.I.Shake(0.5f, 0.3f);
        if (LevelManager.I != null) LevelManager.I.Die();
    }
}
}
