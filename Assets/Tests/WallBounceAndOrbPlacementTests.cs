using NUnit.Framework;
using UnityEngine;
using Lockdown;

namespace Lockdown.Tests {

/// <summary>The maths behind the wall bounce and reachable-orb placement. The same rules are
/// exercised against the real scene in PlayMode/Level01PlayTests.</summary>
public class WallBounceAndOrbPlacementTests {
    // ------------------------------------------------------------ bounce

    static readonly Vector2 CrawlA = new(-3, 0), CrawlB = new(3, 0);

    [Test]
    public void BounceKeepsFullSpeedThenEasesOut() {
        Assert.AreEqual(5f, PlayerController.EasedSpeed(5f, 6f, 1f), 1e-4f);
        Assert.AreEqual(2.5f, PlayerController.EasedSpeed(5f, 0.5f, 1f), 1e-4f);
        Assert.AreEqual(0.6f, PlayerController.EasedSpeed(5f, 0.01f, 1f), 1e-4f, "floored so it arrives");
    }

    [Test]
    public void OnlyANewKeyPressCancelsTheBounce() {
        Vector2 held = Vector2.right;                        // hit the right wall holding D
        Assert.IsFalse(PlayerController.PressedNewKey(Vector2.right, ref held), "still holding D");
        Assert.IsTrue (PlayerController.PressedNewKey(new Vector2(1, 1), ref held), "W is new");

        held = Vector2.right;
        Assert.IsTrue(PlayerController.PressedNewKey(Vector2.left, ref held), "A is new");

        held = Vector2.right;
        Assert.IsFalse(PlayerController.PressedNewKey(Vector2.zero, ref held), "letting go isn't a press");
        Assert.IsTrue (PlayerController.PressedNewKey(Vector2.right, ref held), "D again is a new choice");
    }

    [Test]
    public void SideWallBounceStopsShortOfTheCrawler() {
        // Right wall at mid-height, heading for the centre: crawler path ends at x = 3,
        // so with 1.5 clearance the bounce must stop at about x = 4.5.
        Vector2 stop = PlayerController.StopShortOf(new Vector2(9.6f, 0), Vector2.zero, CrawlA, CrawlB, 1.5f);
        Assert.AreEqual(4.5f, stop.x, 0.06f);
        Assert.GreaterOrEqual(OrbPlacement.DistToSegment(stop, CrawlA, CrawlB), 1.5f - 1e-3f);
    }

    [Test]
    public void BounceAwayFromTheCrawlerGoesAllTheWayToCentre() {
        Vector2 stop = PlayerController.StopShortOf(new Vector2(9.6f, 4f), new Vector2(0, 4f), CrawlA, CrawlB, 1.5f);
        Assert.AreEqual(new Vector2(0, 4f), stop);
    }

    [Test]
    public void CornerBounceNeverEndsOnTheCrawler() {
        Vector2 stop = PlayerController.StopShortOf(new Vector2(9.6f, 5.6f), Vector2.zero, CrawlA, CrawlB, 1.5f);
        Assert.GreaterOrEqual(OrbPlacement.DistToSegment(stop, CrawlA, CrawlB), 1.5f - 1e-3f);
        Assert.Less(stop.x, 6f, "still travels well back toward the centre");
    }

    // ------------------------------------------------------------ reachability

    static bool[] Only(params Direction[] ds) {
        var can = new bool[4];
        foreach (var d in ds) can[(int)d] = true;
        return can;
    }

    [Test]
    public void OrbAboveIsUnreachableWithoutUp() {
        var can = Only(Direction.Down, Direction.Left, Direction.Right);
        Assert.IsFalse(OrbPlacement.CanReach(Vector2.zero, new Vector2(3, 4), can, 0.7f, null));
        Assert.IsTrue (OrbPlacement.CanReach(Vector2.zero, new Vector2(3, -4), can, 0.7f, null));
    }

    [Test]
    public void SmallOffsetOnAnAxisNeedsNoKey() {
        // Level with the player (within pickup reach): Right alone is enough.
        var can = Only(Direction.Right);
        Assert.IsTrue(OrbPlacement.CanReach(Vector2.zero, new Vector2(6, 0.5f), can, 0.7f, null));
        Assert.IsFalse(OrbPlacement.CanReach(Vector2.zero, new Vector2(6, 1.0f), can, 0.7f, null));
    }

    [Test]
    public void BlockedPathMakesASpotUnreachable() {
        // Wall across x = 2: the straight line is blocked and there is no way round.
        bool Clear(Vector2 a, Vector2 b) => !(Mathf.Min(a.x, b.x) < 2f && Mathf.Max(a.x, b.x) > 2f);
        Assert.IsFalse(OrbPlacement.CanReach(Vector2.zero, new Vector2(5, 0), Only(Direction.Right), 0.7f, Clear));
    }

    // ------------------------------------------------------------ placement

    static OrbPlacement.Query Arena(Vector2 player, bool[] can) => new() {
        player    = player,
        canMove   = can,
        arena     = new Rect(-9.3f, -5.3f, 18.6f, 10.6f),
        preferred = new Rect(-8f, -4.8f, 16f, 9.6f),
    };

    static readonly System.Collections.Generic.List<Vector2> GridPts =
        OrbPlacement.Grid(new Rect(-9.3f, -5.3f, 18.6f, 10.6f), 0.5f);

    [Test]
    public void PlacedOrbIsAlwaysReachable_EveryLossComboEveryPosition() {
        int placed = 0;
        for (int mask = 1; mask < 16; mask++) {            // every non-empty set of live keys
            var can = new bool[4];
            for (int i = 0; i < 4; i++) can[i] = (mask & (1 << i)) != 0;

            for (float x = -9f; x <= 9f; x += 1.5f)
            for (float y = -5f; y <= 5f; y += 1.25f) {
                var q = Arena(new Vector2(x, y), can);
                if (!OrbPlacement.TryPick(q, null, GridPts, n => n / 2, out Vector2 spot, out _)) continue;
                placed++;
                Assert.IsTrue(OrbPlacement.CanReach(q.player, spot, can, q.pickupReach, null),
                    $"unreachable orb at {spot} for player {q.player}, keys mask {mask}");
                Assert.Greater(Vector2.Distance(spot, q.player), q.pickupReach * 2f, "not a free pickup");
            }
        }
        Assert.Greater(placed, 1000, "placement should almost always succeed");
    }

    [Test]
    public void TierZeroRespectsDistanceBandMarginAndHazards() {
        var q = Arena(new Vector2(0, 0), Only(Direction.Up, Direction.Down, Direction.Left, Direction.Right));
        q.hazards.Add(new OrbPlacement.Hazard(new Vector2(-8, 5), new Vector2(-8, 5), 3f));
        q.hazards.Add(new OrbPlacement.Hazard(new Vector2(-3, 0), new Vector2(3, 0), 1.5f));

        for (int seed = 0; seed < 50; seed++) {
            var rng = new System.Random(seed);
            Assert.IsTrue(OrbPlacement.TryPick(q, null, GridPts, n => rng.Next(n), out Vector2 s, out int tier));
            Assert.AreEqual(0, tier);
            float d = Vector2.Distance(s, q.player);
            Assert.That(d, Is.InRange(5f, 8f));
            Assert.IsTrue(OrbPlacement.Contains(q.preferred, s));
            Assert.GreaterOrEqual(Vector2.Distance(s, new Vector2(-8, 5)), 3f);
            Assert.GreaterOrEqual(OrbPlacement.DistToSegment(s, new Vector2(-3, 0), new Vector2(3, 0)), 1.5f);
        }
    }

    [Test]
    public void HandPlacedZonesWinWhenTheyQualify() {
        var q = Arena(new Vector2(0, 4), Only(Direction.Down, Direction.Left, Direction.Right));
        var zones = new[] { new Vector2(0, 5), new Vector2(3, -1) };    // (0,5) is above: unreachable
        Assert.IsTrue(OrbPlacement.TryPick(q, zones, GridPts, _ => 0, out Vector2 s, out _));
        Assert.AreEqual(new Vector2(3, -1), s);
    }

    [Test]
    public void RelaxesTiersButNeverReachability() {
        // Only Up, 2 units under the top: the only reachable strip is short and near the
        // wall, so the distance band and margin have to give - reachability doesn't.
        var q = Arena(new Vector2(0, 3f), Only(Direction.Up));
        Assert.IsTrue(OrbPlacement.TryPick(q, null, GridPts, _ => 0, out Vector2 s, out int tier));
        Assert.AreEqual(3, tier);
        Assert.Greater(s.y, 3.7f);
        Assert.LessOrEqual(Mathf.Abs(s.x), 0.71f);

        // Pinned against the left edge with only Left: nowhere to go, so no spot at all.
        q = Arena(new Vector2(-9.3f, 0), Only(Direction.Left));
        Assert.IsFalse(OrbPlacement.TryPick(q, null, GridPts, _ => 0, out _, out _));
    }

    [Test]
    public void ThinStripWithOrbsAlreadyInItStillGetsAVisibleOrb() {
        // The bug from play: only Up left, so the reachable ground is a thin column above the
        // player, and two older orbs already sit in it. It used to find nothing and drop the
        // orb on the player (instantly collected, never seen).
        var q = Arena(new Vector2(-3.67f, 3.11f), Only(Direction.Up));
        q.pickupReach = 0.57f;
        q.taken.Add(new Vector2(-3.8f, 4.7f));
        q.taken.Add(new Vector2(-3.3f, 5.2f));
        Assert.IsTrue(OrbPlacement.TryPick(q, null, GridPts, _ => 0, out Vector2 s, out int tier));
        Assert.GreaterOrEqual(tier, 4, "only possible once orb spacing relaxes");
        Assert.IsTrue(OrbPlacement.CanReach(q.player, s, q.canMove, q.pickupReach, null), "still reachable");
        Assert.Greater(Vector2.Distance(s, q.player), q.pickupReach * 2f, "not dropped on the player");
    }

    [Test]
    public void OrbsKeepApartFromEachOther() {
        var q = Arena(new Vector2(0, 0), Only(Direction.Right));
        q.taken.Add(new Vector2(6, 0));
        for (int seed = 0; seed < 20; seed++) {
            var rng = new System.Random(seed);
            OrbPlacement.TryPick(q, null, GridPts, n => rng.Next(n), out Vector2 s, out _);
            Assert.GreaterOrEqual(Vector2.Distance(s, new Vector2(6, 0)), q.takenSpacing);
        }
    }

    // ------------------------------------------------------------ direction system

    [Test]
    public void NextMercyAwardFollowsPriority() {
        var ds = new GameObject("ds").AddComponent<DirectionSystem>();
        try {
            Assert.AreEqual(Direction.None, ds.NextMercyAward);
            ds.Lose(Direction.Left);
            ds.Lose(Direction.Down);
            Assert.AreEqual(Direction.Down, ds.NextMercyAward);
            Assert.IsFalse(ds.IsActive(Direction.Left));
        } finally { Object.DestroyImmediate(ds.gameObject); }
    }
}
}
