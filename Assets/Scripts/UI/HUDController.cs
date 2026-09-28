using UnityEngine;
using UnityEngine.UI;

namespace Lockdown {

/// <summary>
/// Plan v2 section 11. Pure iconography, no labels.
///
/// Direction state is NOT shown here - the coloured fins on the ship carry it, and that is
/// where the player is already looking. Whether a lost direction is recoverable is told by
/// whether its orb is on the field, which is more direct than a greyed-out arrow.
/// </summary>
public class HUDController : MonoBehaviour {
    public Text hearts, timer, banner;
    public Button restartButton;
    public Image gameOverOverlay;

    /// <summary>
    /// Everything wires up in Start, NOT OnEnable.
    ///
    /// OnEnable ran before LevelManager.Awake had set its singleton, so the guarded
    /// subscription silently did nothing and the game-over banner never appeared. The hearts
    /// failed the same way: PlayerHealth raises its first OnHeartsChanged from Start, so a
    /// subscriber that arrives later never hears it.
    ///
    /// Start is guaranteed to run after every Awake, and the current values are PULLED here
    /// rather than waiting for an event that may already have been raised.
    /// </summary>
    void Start() {
        if (LevelManager.I != null) LevelManager.I.OnStateChanged += ShowBanner;

        var hp = Object.FindFirstObjectByType<PlayerHealth>();
        if (hp != null) {
            hp.OnHeartsChanged += SetHearts;
            SetHearts(hp.Hearts);                  // pull, don't wait
        }

        if (restartButton != null) {
            restartButton.onClick.AddListener(() => {
                if (LevelManager.I != null) LevelManager.I.Restart();
            });
        }
        ShowBanner(LevelManager.I != null ? LevelManager.I.State : GameState.Playing);
    }

    void OnDestroy() {
        if (LevelManager.I != null) LevelManager.I.OnStateChanged -= ShowBanner;
    }

    void Update() {
        if (timer == null || LevelManager.I == null) return;
        float r = LevelManager.I.Remaining;
        timer.text = $"{Mathf.FloorToInt(r / 60f)}:{Mathf.FloorToInt(r % 60f):00}";
        timer.color = r < 10f ? Color.red : Color.white;
    }

    void SetHearts(int n) {
        if (hearts != null) hearts.text = new string('♥', Mathf.Max(0, n));
    }

    void ShowBanner(GameState s) {
        bool over = s == GameState.Won || s == GameState.Dead;

        if (banner != null) {
            banner.text  = s == GameState.Won ? "YOU WIN!" : s == GameState.Dead ? "YOU DIED" : "";
            banner.color = s == GameState.Won ? new Color(1f, 0.85f, 0.3f) : Color.red;
        }
        // Dim the arena behind the result so the banner and button read as an overlay
        // rather than as text floating over live gameplay.
        if (gameOverOverlay != null) gameOverOverlay.gameObject.SetActive(over);
        // The run no longer restarts itself - the player decides when, via this button.
        if (restartButton != null) restartButton.gameObject.SetActive(over);
    }
}
}
