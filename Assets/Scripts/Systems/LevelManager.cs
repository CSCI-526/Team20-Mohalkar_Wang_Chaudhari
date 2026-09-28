using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Lockdown {

public enum GameState { Playing, Won, Dead }

/// <summary>
/// Run state machine and difficulty ramp. Single level, no progression.
/// Lives on the persistent Systems object.
/// </summary>
public class LevelManager : MonoBehaviour {
    public static LevelManager I { get; private set; }

    [Header("Run")]
    public float runDuration = 60f;

    [Header("Difficulty ramp")]
    public float tierLength = 10f;
    /// <summary>When Remaining hits this, the late center-cross crawlers spawn.</summary>
    public float latePhaseLead = 20f;

    public GameState State { get; private set; } = GameState.Playing;

    /// <summary>UNSCALED, so hitstop frames don't quietly extend the round
    ///.</summary>
    public float Elapsed   { get; private set; }
    public float Remaining => Mathf.Max(0f, runDuration - Elapsed);

    /// <summary>True while the arena is frozen. Turret, Crawler and Bullet all check it.</summary>
    public bool Frozen => State != GameState.Playing;

    public event Action<GameState> OnStateChanged;
    /// <summary>Fires once at each 10s boundary. With no audio, the HUD pulse and the crawler
    /// tint are the ONLY cues the player gets that the game just got harder.</summary>
    public event Action<int> OnTierChanged;

    int _tier = 0;
    bool _lateCrawlersSpawned;   // late-phase crawlers spawn once

    // ------------------------------------------------------------------ the ramp
    // Six tiers over 60s: the only escalation in the game.

    public int Tier => _tier;
    public float CrawlerSpeed  => Mathf.Min(1.5f + 0.5f * _tier, 4.0f);   // capped under the player's speed of 5.0
    public float TurretFireGap => Mathf.Max(2.5f - 0.2f * _tier, 1.5f);   // floor: the telegraph is 0.5s,
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

        // R reloads from any state. You will press this several
        // hundred times while tuning.
        if (Input.GetKeyDown(KeyCode.R)) { Restart(); return; }

        // Won and Dead both just hold until the player restarts, with the centred RESTART button
        // or the R key. Restarting on any key would count the mouse click that presses the
        // button, and an automatic restart would leave no time to use it.
        if (State == GameState.Playing) TickPlaying();
    }

    void TickPlaying() {
        Elapsed += Time.unscaledDeltaTime;

        int t = Mathf.Clamp(Mathf.FloorToInt(Elapsed / tierLength), 0, 5);
        if (t != _tier) { _tier = t; OnTierChanged?.Invoke(_tier); }

        // Late phase: one extra vertical crawler on the midline.
        if (!_lateCrawlersSpawned && Remaining <= latePhaseLead) SpawnLateCrawlers();

        // The WIN CHECK RUNS FIRST. Taking the 5th hit at
        // t=59.98 makes both conditions true on the same frame, and being killed by
        // evaluation order is the worst possible way to lose a run you survived.
        if (Elapsed >= runDuration) Win();
    }

    // Last 20s: spawn one extra crawler (vertical midline)
    void SpawnLateCrawlers() {
        _lateCrawlersSpawned = true;

        // Clone from the scene's S-path crawler
        Crawler proto = null;
        foreach (Crawler c in FindObjectsByType<Crawler>(FindObjectsSortMode.None))
            if (c.pathMode == Crawler.PathMode.SCurve) { proto = c; break; }
        if (proto == null) return;

        MakeCrawler(proto, Crawler.PathMode.Vertical);
    }

    void MakeCrawler(Crawler proto, Crawler.PathMode mode) {
        GameObject go = Instantiate(proto.gameObject);
        go.name = "Crawler_" + mode;

        Crawler c = go.GetComponent<Crawler>();
        c.pathMode = mode;
        c.laneX = 0f;
        c.topY = 5f;
        c.bottomY = -5f;

        // Vertical starts at the top
        go.transform.position = new Vector3(0f, 5f, 0f);
    }

    void Win() {
        if (State != GameState.Playing) return;
        State = GameState.Won;

        // Bullets are mid-flight when the timer hits 0.
        // Without this, your best run ends with "YOU WIN" on screen and a death
        // animation playing underneath it.
        foreach (Bullet b in FindObjectsByType<Bullet>(FindObjectsSortMode.None))
            if (b.IsEnemyBullet) Destroy(b.gameObject);

        Hitstop.Reset();
        OnStateChanged?.Invoke(State);
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
