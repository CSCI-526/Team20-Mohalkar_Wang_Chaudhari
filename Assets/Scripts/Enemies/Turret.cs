using UnityEngine;

namespace Lockdown {

// Red square turret: locks aim, glows 0.5s, then fires. Dies in one hit, respawns later.
public class Turret : MonoBehaviour {
    [Header("Refs")]
    public Bullet bulletPrefab;
    public Transform barrel, barrelTip;
    public SpriteRenderer core, body;

    [Header("Tuning")]
    public float range = 15f;            // fire range
    public float telegraph = 0.5f;       // glow time before the shot (dodge window)
    public float spawnProtection = 1.0f; // invulnerable time after respawn

    float _fireAt = -1f;    // >0 while telegraphing; fires at that time
    float _nextAimAt = 0f;  // next time we try to aim at the player
    float _respawnAt = -1f; // when to respawn
    float _safeUntil = -1f; // invulnerable until this time
    Vector2 _aim;           // fire direction (locked when telegraph starts)
    bool _alive = true;
    Collider2D _col;
    PlayerController _player;

    void Awake() { _col = GetComponent<Collider2D>(); }

    void Update() {
        if (LevelManager.I == null || LevelManager.I.Frozen) return;

        // Dead: wait to respawn
        if (!_alive) {
            if (Time.time >= _respawnAt) Respawn();
            return;
        }

        // Telegraphing: barrel stays locked, core turns white, then fire
        if (_fireAt > 0f) {
            if (barrel != null) barrel.right = _aim;
            float t = 1f - Mathf.Max(0f, (_fireAt - Time.time) / telegraph);
            if (core != null)
                core.color = Color.Lerp(new Color(1f, 0.53f, 0.33f), Color.white, t);
            if (Time.time >= _fireAt) Fire();
            return;
        }

        // Not locked: barrel tracks the player
        TrackBarrel();

        // Idle: try to aim when ready
        if (Time.time >= _nextAimAt) TryAim();
    }

    // Keep the barrel pointed at the player
    void TrackBarrel() {
        if (barrel == null) return;
        if (_player == null) _player = FindFirstObjectByType<PlayerController>();
        if (_player == null) return;

        Vector2 to = (Vector2)_player.transform.position - (Vector2)transform.position;
        if (to.sqrMagnitude < 0.0001f) return;
        barrel.right = to.normalized;
    }

    void TryAim() {
        if (_player == null) _player = FindFirstObjectByType<PlayerController>();
        if (_player == null) return;

        Vector2 to = (Vector2)_player.transform.position - (Vector2)transform.position;
        if (to.magnitude > range) return; // out of range
        if (Physics2D.Raycast(transform.position, to.normalized, to.magnitude, Layers.WallMask))
            return; // wall in the way

        // Lock aim here. Barrel freezes until the shot is fired.
        _aim = to.normalized;
        _fireAt = Time.time + telegraph;
        if (barrel != null) barrel.right = _aim;
    }

    void Fire() {
        _fireAt = -1f;
        _nextAimAt = Time.time + LevelManager.I.TurretFireGap;
        if (core != null) core.color = new Color(1f, 0.53f, 0.33f);
        if (bulletPrefab == null) return;

        Vector3 pos = barrelTip != null
            ? barrelTip.position
            : transform.position + (Vector3)(_aim * 0.6f);
        Instantiate(bulletPrefab, pos, Quaternion.identity).Fire(_aim, true);
    }

    // Called when a player bullet hits the turret
    public void TakeHit() {
        if (!_alive || Time.time < _safeUntil) {
            Flash(); // just flash while invulnerable
            return;
        }
        Die();
    }

    void Flash() {
        if (body != null) body.color = Color.white;
        Invoke(nameof(ResetTint), 0.06f);
    }

    void ResetTint() {
        if (body != null) body.color = new Color(1f, 0.33f, 0.2f);
    }

    void Die() {
        _alive = false;
        _fireAt = -1f;
        _respawnAt = Time.time + LevelManager.I.TurretRespawn;
        SetVisible(false);
        Hitstop.Freeze(0.05f);
    }

    void Respawn() {
        _alive = true;
        _safeUntil = Time.time + spawnProtection;
        _nextAimAt = Time.time;
        SetVisible(true);
    }

    void SetVisible(bool on) {
        foreach (SpriteRenderer sr in GetComponentsInChildren<SpriteRenderer>(true))
            sr.enabled = on;
        if (_col != null) _col.enabled = on;
    }
}
}
