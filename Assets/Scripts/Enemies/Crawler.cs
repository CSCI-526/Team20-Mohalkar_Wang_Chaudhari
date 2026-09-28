using UnityEngine;

namespace Lockdown {

// Pink triangle: kills the player on contact. Two path modes.
//   SCurve   = full-arena S path
//   Vertical = up/down along the vertical midline
// Hidden before the run starts; only visible while the game is Playing.
public class Crawler : MonoBehaviour {
    public enum PathMode { SCurve, Vertical }

    public PathMode pathMode = PathMode.SCurve;
    public SpriteRenderer body;

    [Header("S-path")]
    public float halfWidth = 9f;   // left/right extent
    public float amplitude = 4.5f; // up/down height

    [Header("Vertical lane (last 20s)")]
    public float laneX = 0f;   // x of the vertical lane
    public float topY = 5f;
    public float bottomY = -5f;

    float _x = -9f, _dirX = 1f;   // S-path
    float _y = 5f, _dirY = -1f;   // vertical

    void Awake() {
        // Hide until the run is live
        SetVisible(false);
    }

    void Start() {
        if (pathMode == PathMode.Vertical) {
            _y = topY;
            _dirY = -1f;
            transform.position = new Vector2(laneX, _y);
        } else {
            _x = -halfWidth;
            _dirX = 1f;
            transform.position = new Vector2(_x, SineY(_x));
        }
    }

    void Update() {
        if (LevelManager.I == null) return;

        // Only show while the game is Playing
        bool show = LevelManager.I.State == GameState.Playing;
        SetVisible(show);
        if (!show || LevelManager.I.Frozen) return;

        float speed = LevelManager.I.CrawlerSpeed;

        if (pathMode == PathMode.Vertical) {
            _y = _y + _dirY * speed * Time.deltaTime;
            if (_y >= topY)       { _y = topY;    _dirY = -1f; }
            if (_y <= bottomY)    { _y = bottomY; _dirY = 1f;  }
            transform.position = new Vector2(laneX, _y);
            Face(0f, _dirY);
        } else {
            // S-path: move in x, y follows a sine curve
            _x = _x + _dirX * speed * Time.deltaTime;
            if (_x >= halfWidth)  { _x = halfWidth;  _dirX = -1f; }
            if (_x <= -halfWidth) { _x = -halfWidth; _dirX = 1f;  }
            transform.position = new Vector2(_x, SineY(_x));
            Face(_dirX, 0f);
        }

        // Tint brightens with difficulty (cue that it sped up)
        if (body != null)
            body.color = Color.Lerp(new Color(1f, 0.2f, 0.67f), Color.white,
                                    LevelManager.I.Tier / 10f);
    }

    // S-path y: one sine wave
    float SineY(float x) {
        return amplitude * Mathf.Sin(Mathf.PI * x / halfWidth);
    }

    // Path endpoints (used by OrbSpawner / PlayerController to stay clear of the patrol)
    public Vector2 PathStart() {
        if (pathMode == PathMode.Vertical) return new Vector2(laneX, bottomY);
        return new Vector2(-halfWidth, SineY(-halfWidth));
    }

    public Vector2 PathEnd() {
        if (pathMode == PathMode.Vertical) return new Vector2(laneX, topY);
        return new Vector2(halfWidth, SineY(halfWidth));
    }

    /// <summary>The whole path as points (straight lane: 2; S-curve: sampled along the
    /// sine). OrbSpawner keeps orbs off it - PathStart/PathEnd alone describe the S-curve as
    /// a flat line across the middle, when it really swings +/- amplitude.</summary>
    public void SamplePath(System.Collections.Generic.List<Vector2> into, int samples = 36) {
        if (pathMode != PathMode.SCurve) { into.Add(PathStart()); into.Add(PathEnd()); return; }
        for (int i = 0; i <= samples; i++) {
            float x = Mathf.Lerp(-halfWidth, halfWidth, i / (float)samples);
            into.Add(new Vector2(x, SineY(x)));
        }
    }

    // Point the triangle along travel (sprite apex is up, so subtract 90)
    void Face(float dirX, float dirY) {
        float angle = Mathf.Atan2(dirY, dirX) * Mathf.Rad2Deg - 90f;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    void SetVisible(bool on) {
        if (body == null) body = GetComponent<SpriteRenderer>();
        if (body != null) body.enabled = on;
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.enabled = on;
    }

    void OnTriggerEnter2D(Collider2D other) {
        if (other.gameObject.layer != Layers.Player) return;
        PlayerHealth hp = other.GetComponentInParent<PlayerHealth>();
        if (hp != null) hp.KillByCrawler();
    }
}
}
