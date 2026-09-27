using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Lockdown {

public enum GameState { Playing, Won, Dead }

/// <summary>
/// Plan v2 section 10 (state machine) and section 2 (difficulty ramp).
/// Single level, no progression. Lives on the persistent Systems object.
/// </summary>
public class LevelManager : MonoBehaviour {
    public static LevelManager I { get; private set; }

    [Header("Run")]
    public float runDuration = 60f;

    [Header("Difficulty ramp (plan v2 section 2)")]
    public float tierLength = 10f;

    public GameState State { get; private set; } = GameState.Playing;

    /// <summary>UNSCALED, so hitstop frames don't quietly extend the round
    /// (plan v2 section 12).</summary>
    public float Elapsed   { get; private set; }
    public float Remaining => Mathf.Max(0f, runDuration - Elapsed);

    /// <summary>True while the arena is frozen. Turret, Crawler and Bullet all check it.</summary>
    public bool Frozen => State != GameState.Playing;

    public event Action<GameState> OnStateChanged;
    /// <summary>Fires once at each 10s boundary. With no audio, the HUD pulse and the crawler
    /// tint are the ONLY cues the player gets that the game just got harder.</summary>
    public event Action<int> OnTierChanged;

    int _tier = 0;

    // ------------------------------------------------------------------ the ramp
    // Six tiers over 60s. This is the only escalation in the game - in v1 nothing changed
    // between second 5 and second 55 and the 60-second arc table was aspirational.

    public int Tier => _tier;
    public float CrawlerSpeed  => Mathf.Min(2.0f + 0.5f * _tier, 4.5f);   // capped under player's 5.0
    public float TurretFireGap => Mathf.Max(2.0f - 0.3f * _tier, 0.5f);   // 2.0 -> 0.5 across the run
    public float TurretRespawn => Mathf.Max(4.0f - 0.4f * _tier, 2.0f);   // any faster and it glows nonstop

    void Awake() {
        I = this;
        Hitstop.Reset();
        Elapsed = 0f;
        _tier = 0;
        State = GameState.Playing;
    }

    void OnDestroy() { if (I == this) I = null; }

    void Update() {
        Hitstop.Tick();   // the single place timeScale is restored

        // Plan v2 section 10, rule 3: R reloads from any state. You will press this several
        // hundred times while tuning.
        if (Input.GetKeyDown(KeyCode.R)) { Restart(); return; }

        // Won and Dead both just hold. Restarting is the player's call now, via the centred
        // RESTART button - previously a win restarted on ANY key (including the mouse click
        // that would press that button) and a death restarted itself on a timer, neither of
        // which leaves room for a button.
        if (State == GameState.Playing) TickPlaying();
    }

    void TickPlaying() {
        Elapsed += Time.unscaledDeltaTime;

        int t = Mathf.Clamp(Mathf.FloorToInt(Elapsed / tierLength), 0, 5);
        if (t != _tier) { _tier = t; OnTierChanged?.Invoke(_tier); }

        // Plan v2 section 10, rule 2: the WIN CHECK RUNS FIRST. Taking the 5th hit at
        // t=59.98 makes both conditions true on the same frame, and being killed by
        // evaluation order is the worst possible way to lose a run you survived.
        if (Elapsed >= runDuration) Win();
    }

    void Win() {
        if (State != GameState.Playing) return;
        State = GameState.Won;

        // Plan v2 section 10, rule 1: bullets are mid-flight when the timer hits 0.
        // Without this, your best run ends with "YOU WIN" on screen and a death
        // animation playing underneath it.
        foreach (Bullet b in FindObjectsByType<Bullet>(FindObjectsSortMode.None))
            if (b.IsEnemyBullet) Destroy(b.gameObject);

        Hitstop.Reset();
        OnStateChanged?.Invoke(State);
        StartCoroutine(Celebrate());
    }

    /// <summary>
    /// Fireworks. Only on a win - a death gets the banner and nothing else, because
    /// celebrating a loss undercuts it.
    /// </summary>
    IEnumerator Celebrate() {
        Color[] palette = {
            new(1.00f, 0.85f, 0.30f),   // gold
            new(0.20f, 0.87f, 1.00f),   // cyan
            new(0.20f, 1.00f, 0.53f),   // green
            new(1.00f, 0.55f, 0.90f),   // pink
        };

        for (int i = 0; i < 9; i++) {
            Vector2 at = new(UnityEngine.Random.Range(-8f, 8f), UnityEngine.Random.Range(-4f, 5f));
            Burst.Play(at, palette[i % palette.Length], count: 45, speed: 7f, size: 0.26f, life: 1.3f);
            if (CameraShake.I != null) CameraShake.I.Shake(0.12f, 0.12f);
            // Realtime: the arena is frozen on a win, so scaled waits would stall.
            yield return new WaitForSecondsRealtime(0.16f);
        }
    }

    /// <summary>Called by PlayerHealth on the 5th heart, and by Crawler on contact.</summary>
    public void Die() {
        if (State != GameState.Playing) return;   // cannot die after winning
        State = GameState.Dead;
        Hitstop.Reset();
        OnStateChanged?.Invoke(State);
    }

    public void Restart() {
        Hitstop.Reset();
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
}
