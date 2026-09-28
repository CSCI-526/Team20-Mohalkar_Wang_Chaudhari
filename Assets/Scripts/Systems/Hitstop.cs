using UnityEngine;

namespace Lockdown {

/// <summary>
/// The ONLY thing in the project allowed to write Time.timeScale.
///
/// Why this exists: two hitstops can overlap (you kill a turret 0.02s before a bullet
/// hits you). Written naively - one coroutine per effect, each restoring timeScale = 1 -
/// the first to finish cancels the second. Worse, if a coroutine's GameObject is destroyed
/// mid-effect (a turret dying during its own hitstop) the coroutine dies with it and the
/// game stays frozen for the rest of the run.
///
/// Longest freeze wins. Unscaled clock so a scale-0 freeze can still end.
/// Tick() is driven from LevelManager, which never dies.
/// </summary>
public static class Hitstop {
    static float _until;

    public static void Freeze(float duration) {
        _until = Mathf.Max(_until, Time.unscaledTime + duration);
        Time.timeScale = 0f;
    }

    public static void Tick() {
        if (Time.timeScale == 0f && Time.unscaledTime > _until) Time.timeScale = 1f;
    }

    public static void Reset() { _until = 0f; Time.timeScale = 1f; }
}
}
