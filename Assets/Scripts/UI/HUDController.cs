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

    void OnEnable() {
        if (LevelManager.I != null) LevelManager.I.OnStateChanged += ShowBanner;
        var hp = Object.FindFirstObjectByType<PlayerHealth>();
        if (hp != null) hp.OnHeartsChanged += SetHearts;
    }

    void Start() { if (banner != null) banner.text = ""; }

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
        if (banner == null) return;
        banner.text = s == GameState.Won ? "YOU WIN" : s == GameState.Dead ? "YOU DIED" : "";
        banner.color = s == GameState.Won ? new Color(1f, 0.85f, 0.3f) : Color.red;
    }
}
}
