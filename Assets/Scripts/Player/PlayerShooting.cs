using UnityEngine;

namespace Lockdown {

/// <summary>Plan v2 section 8/9. Mouse aim, independent of movement - you can always shoot
/// in a direction you can no longer move in.</summary>
public class PlayerShooting : MonoBehaviour {
    public Bullet bulletPrefab;
    public Transform firePoint;
    [Tooltip("Seconds between shots. 0.25 = 4 shots/sec.")]
    public float fireRate = 0.25f;

    float _next;

    /// <summary>
    /// The gun is LOCKED at full mobility and unlocks the moment a direction is lost.
    ///
    /// It inverts the loop: you open the run unable to fight back at all, so the first
    /// stretch is pure dodging and the movement mechanic gets taught before the shooting
    /// one. Getting hit is what arms you.
    ///
    /// It also creates the interesting decision on the other side. Collecting every orb
    /// restores full mobility - and takes the gun away again. Staying one direction down is
    /// staying armed, so the player has to weigh mobility against firepower every time an
    /// orb appears, instead of always grabbing it.
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
