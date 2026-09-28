using UnityEngine;

namespace Lockdown {

/// <summary>Cached layer indices so hot paths avoid string lookups.</summary>
public static class Layers {
    public static readonly int Player       = LayerMask.NameToLayer("Player");
    public static readonly int Enemy        = LayerMask.NameToLayer("Enemy");
    public static readonly int EnemyBullet  = LayerMask.NameToLayer("EnemyBullet");
    public static readonly int PlayerBullet = LayerMask.NameToLayer("PlayerBullet");
    public static readonly int Wall         = LayerMask.NameToLayer("Wall");
    public static readonly int Orb          = LayerMask.NameToLayer("Orb");

    public static int WallMask => 1 << Wall;
}
}
