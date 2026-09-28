using UnityEngine;

namespace Lockdown {

/// <summary>Mouse aim, independent of movement - you can always shoot
/// in a direction you can no longer move in.</summary>
public class PlayerShooting : MonoBehaviour {
    public Bullet bulletPrefab;
    public Transform firePoint;
    [Tooltip("Seconds between shots. 0.25 = 4 shots/sec.")]
    public float fireRate = 0.25f;

    float _next;

    /// <summary>
    /// The gun is locked at full mobility and unlocks as soon as any direction is lost.
    /// Collecting every orb restores full mobility and locks it again, so staying one
    /// direction down keeps the gun armed.
    /// </summary>
    public bool Armed => DirectionSystem.I != null && DirectionSystem.I.AnyLost();

    void Update() {
        if (LevelManager.I != null && LevelManager.I.Frozen) return;
        if (!Armed) return;
        if (!Input.GetMouseButton(0) || Time.time < _next) return;
        if (bulletPrefab == null) return;

        _next = Time.time + fireRate;

        Vector3 m = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        Vector2 dir = ((Vector2)(m - transform.position)).normalized;
        if (dir.sqrMagnitude < 0.0001f) dir = Vector2.right;

        Vector3 spawn = firePoint != null ? firePoint.position : transform.position;
        Bullet b = Instantiate(bulletPrefab, spawn + (Vector3)(dir * 0.45f), Quaternion.identity);
        b.Fire(dir, enemy: false);

        if (CameraShake.I != null) CameraShake.I.Shake(0.06f, 0.05f);
    }
}
}
