using UnityEngine;

namespace Lockdown {

/// <summary>
/// One script for both teams: player bullets behave like enemy bullets at walls.
///
/// Walls are handled by a CircleCast rather than by physics collision. Two reasons:
///   1. A trigger gives no contact normal, and Vector2.Reflect needs one.
///   2. A non-trigger bullet with a Rigidbody2D physically SHOVES the Dynamic player
///      around. A raycast bullet touches nothing.
/// Entities (player, opposing bullets, turrets) still use trigger overlap.
/// </summary>
[RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
public class Bullet : MonoBehaviour {
    [Header("Team")]
    public bool isEnemyBullet = true;

    [Header("Speeds / lifetimes (Inspector knobs, tune to taste)")]
    public float freshSpeed      = 5.0f;
    public float bouncedSpeed    = 4.5f;
    public float freshLifetime   = 4.0f;
    /// <summary>At 4.5 u/s, 3.0s covers 13.5 units - enough to cross most of the 20-wide arena, so
    /// a bounced bullet usually reaches a second wall instead of expiring mid-air.</summary>
    public float bouncedLifetime = 3.0f;

    [Header("Bounced look (the 'ping' is gone - this carries it alone)")]
    public Color bouncedTint = Color.white;
    public float bouncedScale = 1.2f;

    public bool IsEnemyBullet => isEnemyBullet;
    public Vector2 Velocity => _dir * _speed;

    Vector2 _dir;
    float _speed, _life, _radius;
    bool _canBounce = true;
    SpriteRenderer _sr;

    public void Fire(Vector2 direction, bool enemy) {
        isEnemyBullet = enemy;
        _dir   = direction.normalized;
        _speed = freshSpeed;
        _life  = freshLifetime;
        _canBounce = true;
        gameObject.layer = enemy ? Layers.EnemyBullet : Layers.PlayerBullet;
    }

    void Awake() {
        _sr = GetComponentInChildren<SpriteRenderer>();
        _radius = GetComponent<CircleCollider2D>().radius * Mathf.Abs(transform.lossyScale.x);
        var rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.simulated = true;
        GetComponent<CircleCollider2D>().isTrigger = true;
        if (_speed == 0f) { _dir = Vector2.right; _speed = freshSpeed; _life = freshLifetime; }
    }

    void Update() {
        if (LevelManager.I != null && LevelManager.I.Frozen) return;

        _life -= Time.deltaTime;
        if (_life <= 0f) { Destroy(gameObject); return; }

        float step = _speed * Time.deltaTime;

        // Wall handling. CircleCast so a fast bullet cannot tunnel through a thin wall.
        RaycastHit2D hit = Physics2D.CircleCast(transform.position, _radius, _dir, step, Layers.WallMask);
        if (hit.collider != null) {
            if (!_canBounce) { Destroy(gameObject); return; }   // 2nd wall = gone
            transform.position = hit.point + hit.normal * (_radius + 0.001f);
            Bounce(hit.normal);
            return;
        }

        transform.position += (Vector3)(_dir * step);
    }

    void Bounce(Vector2 normal) {
        _dir       = Vector2.Reflect(_dir, normal).normalized;
        _speed     = bouncedSpeed;
        _life      = bouncedLifetime;
        _canBounce = false;

        // Nothing plays on a bounce, so make the visual swap stark.
        if (_sr != null) _sr.color = bouncedTint;
        transform.localScale *= bouncedScale;
    }

    void OnTriggerEnter2D(Collider2D other) {
        int layer = other.gameObject.layer;

        // Opposing bullet: both die. Player bullet is CONSUMED - a 1-for-1 trade.
        if (layer == (isEnemyBullet ? Layers.PlayerBullet : Layers.EnemyBullet)) {
            Destroy(other.gameObject);
            Destroy(gameObject);
            return;
        }

        if (isEnemyBullet && layer == Layers.Player) {
            var hp = other.GetComponentInParent<PlayerHealth>();
            // Damage decisions (i-frames, which direction is lost) live in PlayerHealth.
            // The bullet is destroyed either way: a bullet visibly passing through the
            // player reads as broken hit detection, not as mercy.
            if (hp != null) hp.TakeHit(Velocity);
            Destroy(gameObject);
            return;
        }

        if (!isEnemyBullet && layer == Layers.Enemy) {
            var t = other.GetComponentInParent<Turret>();
            if (t != null) t.TakeHit();       // a spawn-protected turret survives and flashes
            Destroy(gameObject);              // ...but the bullet dies regardless
        }
    }
}
}
