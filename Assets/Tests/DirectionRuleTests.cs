using NUnit.Framework;
using UnityEngine;
using Lockdown;

namespace Lockdown.Tests {

/// <summary>
/// The quadrant rule is the one piece of genuinely non-trivial logic in the project and the
/// hardest to eyeball in play, so it gets the check. Everything else is verified by playing.
/// </summary>
public class DirectionRuleTests {
    DirectionSystem _ds;

    [SetUp]    public void Setup()    { _ds = new GameObject("ds").AddComponent<DirectionSystem>(); }
    [TearDown] public void Teardown() { Object.DestroyImmediate(_ds.gameObject); }

    // The four axis-aligned shots are the degenerate cases of the quadrant rule.
    [TestCase( 1f,  0f, Direction.Left )]   // travels Right -> lose Left
    [TestCase(-1f,  0f, Direction.Right)]   // travels Left  -> lose Right
    [TestCase( 0f,  1f, Direction.Down )]   // travels Up    -> lose Down
    [TestCase( 0f, -1f, Direction.Up   )]   // travels Down  -> lose Up
    public void AxisAlignedMatchesOriginalTable(float x, float y, Direction expected) {
        Assert.AreEqual(expected, _ds.ResolveLoss(new Vector2(x, y)));
    }

    [Test]
    public void DominantAxisDecidesWhenBothCandidatesAlive() {
        // T1 (-8,5) shooting a player at (0,4): travel (8,-1), X dominates -> lose Left.
        Assert.AreEqual(Direction.Left, _ds.ResolveLoss(new Vector2(8f, -1f)));
        // Player near (-6,-4): travel (2,-9), Y dominates -> lose Up.
        Assert.AreEqual(Direction.Up, _ds.ResolveLoss(new Vector2(2f, -9f)));
    }

    [Test]
    public void FortyFiveDegreeTieResolvesConsistently() {
        // An exact diagonal has no dominant axis. Which way it breaks doesn't matter;
        // that it breaks the SAME way every time does.
        Direction first = _ds.ResolveLoss(new Vector2(5f, -5f));
        Assert.AreEqual(Direction.Left, first);
        Assert.AreEqual(first, _ds.ResolveLoss(new Vector2(50f, -50f)));
    }

    [Test]
    public void PrefersTheCandidateThatIsStillAlive() {
        // Travel (5,-5) -> candidates Left (horizontal) and Up (vertical).
        // With Left gone the same shot must take Up instead, so the hit is never a no-op.
        _ds.TestSetActive(Direction.Left, false);
        Assert.AreEqual(Direction.Up, _ds.ResolveLoss(new Vector2(5f, -5f)));
    }

    [Test]
    public void ReturnsNoneOnlyWhenBothCandidatesAreGone() {
        _ds.TestSetActive(Direction.Left, false);
        _ds.TestSetActive(Direction.Up, false);
        Assert.AreEqual(Direction.None, _ds.ResolveLoss(new Vector2(5f, -5f)));
        // ...but the orthogonal quadrant is still live, so other shots still land.
        Assert.AreNotEqual(Direction.None, _ds.ResolveLoss(new Vector2(-5f, 5f)));
    }

    [Test]
    public void EveryHitRemovesSomethingWhileAnythingIsLive() {
        var shots = new[] { new Vector2(8f,-1f), new Vector2(-8f,-5f), new Vector2(2f,9f), new Vector2(-3f,-7f) };
        int removed = 0;
        foreach (var s in shots) if (_ds.ApplyHit(s) != Direction.None) removed++;
        Assert.AreEqual(4, removed, "all four hits should have taken a direction");
        Assert.IsFalse(_ds.AnyActive());
    }
}
}
