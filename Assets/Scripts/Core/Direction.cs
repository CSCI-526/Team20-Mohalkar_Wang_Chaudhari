using UnityEngine;

namespace Lockdown {

public enum Direction { Up = 0, Down = 1, Left = 2, Right = 3, None = -1 }

/// <summary>
/// Direction maths, including the quadrant rule that maps any bullet angle, not just the four
/// axis-aligned ones, to the direction the player loses.
/// </summary>
public static class Dir {
    public const int Count = 4;

    // Priority order used for fallbacks and for the mercy award.
    public static readonly Direction[] Priority =
        { Direction.Up, Direction.Down, Direction.Left, Direction.Right };

    public static Vector2 ToVector(Direction d) => d switch {
        Direction.Up    => Vector2.up,
        Direction.Down  => Vector2.down,
        Direction.Left  => Vector2.left,
        Direction.Right => Vector2.right,
        _               => Vector2.zero,
    };

    public static Direction Opposite(Direction d) => d switch {
        Direction.Up    => Direction.Down,
        Direction.Down  => Direction.Up,
        Direction.Left  => Direction.Right,
        Direction.Right => Direction.Left,
        _               => Direction.None,
    };

    /// <summary>
    /// The two cardinals of the quadrant OPPOSITE the bullet's travel - i.e. the two
    /// directions the player is a candidate to lose. A zero component counts as positive.
    ///
    ///   travels Up-Right   -> Down or Left
    ///   travels Down-Right -> Up   or Left
    ///   travels Down-Left  -> Up   or Right
    ///   travels Up-Left    -> Down or Right
    /// </summary>
    public static void Candidates(Vector2 travel, out Direction horizontal, out Direction vertical) {
        horizontal = travel.x >= 0f ? Direction.Left : Direction.Right;
        vertical   = travel.y >= 0f ? Direction.Down : Direction.Up;
    }
}
}
