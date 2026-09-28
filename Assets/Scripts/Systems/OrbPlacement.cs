using System;
using System.Collections.Generic;
using UnityEngine;

namespace Lockdown {

/// <summary>
/// Where a recovery orb is allowed to go. Pure maths - physics comes in through the two
/// delegates on Query - so the rules are unit tested without a scene.
///
/// The one rule that NEVER relaxes: the player must be able to reach the spot using only the
/// directions they still have. An orb above a player who has lost Up is not a challenge, it
/// is a softlock. Everything else relaxes in tiers (see Passes).
/// </summary>
public static class OrbPlacement {
    /// <summary>A place orbs keep away from. A point hazard has a == b.</summary>
    public struct Hazard {
        public Vector2 a, b;
        public float radius;
        public Hazard(Vector2 a, Vector2 b, float radius) { this.a = a; this.b = b; this.radius = radius; }
    }

    public class Query {
        public Vector2 player;
        /// <summary>Indexed by Direction: which directions the player can walk in.</summary>
        public bool[] canMove = { true, true, true, true };
        /// <summary>Where an orb centre can physically sit.</summary>
        public Rect arena;
        /// <summary>The arena minus the edge margin - an orb hugging the wall has the player
        /// grinding into it trying to line up the pickup.</summary>
        public Rect preferred;
        /// <summary>Tier 0 distance band: far enough that recovering takes real movement,
        /// close enough that it isn't a cross-map trek.</summary>
        public float minTravel = 5f, maxTravel = 8f;
        /// <summary>An axis offset smaller than this needs no key - the pickup overlap
        /// covers it (orb radius 0.4 + player radius 0.4, minus slack).</summary>
        public float pickupReach = 0.7f;
        public List<Hazard> hazards = new();
        public List<Vector2> taken = new();
        public float takenSpacing = 1.5f;
        public Func<Vector2, bool> solidAt = _ => false;
        public Func<Vector2, Vector2, bool> pathClear = (_, _) => true;
    }

    /// <summary>
    /// Tier 0: every rule.
    /// Tier 1: drop the distance band (keep half of minTravel so it isn't a free pickup).
    /// Tier 2: also ignore hazards.
    /// Tier 3: also drop the edge margin and keep only "not on top of the player".
    /// Reachability, solidity and orb spacing hold at every tier.
    /// </summary>
    public const int Tiers = 4;

    /// <summary>Which keys walking from here to there takes. None on an axis inside pickup reach.</summary>
    public static void Needed(Vector2 delta, float reach, out Direction h, out Direction v) {
        h = delta.x >  reach ? Direction.Right : delta.x < -reach ? Direction.Left : Direction.None;
        v = delta.y >  reach ? Direction.Up    : delta.y < -reach ? Direction.Down : Direction.None;
    }

    /// <summary>
    /// Reachable = every key the trip needs is live, AND an axis-aligned L-path (either
    /// corner) is clear of pillars. The L is conservative: diagonals only make it easier.
    /// </summary>
    public static bool CanReach(Vector2 from, Vector2 to, bool[] canMove, float reach,
                                Func<Vector2, Vector2, bool> pathClear) {
        Needed(to - from, reach, out Direction h, out Direction v);
        if (h != Direction.None && !canMove[(int)h]) return false;
        if (v != Direction.None && !canMove[(int)v]) return false;
        if (pathClear == null) return true;

        // Only the axes that need a key get walked; the rest is already inside pickup range.
        Vector2 end = new(h != Direction.None ? to.x : from.x, v != Direction.None ? to.y : from.y);
        if (h == Direction.None || v == Direction.None) return pathClear(from, end);

        Vector2 viaX = new(end.x, from.y), viaY = new(from.x, end.y);
        return (pathClear(from, viaX) && pathClear(viaX, end))
            || (pathClear(from, viaY) && pathClear(viaY, end));
    }

    public static bool Passes(Query q, Vector2 c, int tier) {
        if (!Contains(tier < 3 ? q.preferred : q.arena, c)) return false;

        float d = Vector2.Distance(c, q.player);
        if (tier == 0 && (d < q.minTravel || d > q.maxTravel)) return false;
        if (tier is 1 or 2 && d < q.minTravel * 0.5f) return false;
        if (tier == 3 && d < q.pickupReach * 2f) return false;

        if (tier < 2)
            foreach (var hz in q.hazards)
                if (DistToSegment(c, hz.a, hz.b) < hz.radius) return false;

        foreach (var t in q.taken)
            if (Vector2.Distance(c, t) < q.takenSpacing) return false;

        // Physics last - it's the only expensive check.
        if (q.solidAt != null && q.solidAt(c)) return false;
        return CanReach(q.player, c, q.canMove, q.pickupReach, q.pathClear);
    }

    /// <summary>
    /// Lowest tier wins; inside a tier the hand-placed <paramref name="preferred"/> spots beat
    /// the generated <paramref name="fill"/> grid. <paramref name="pick"/> chooses among the
    /// passing spots (Random.Range in play, deterministic in tests).
    /// </summary>
    public static bool TryPick(Query q, IList<Vector2> preferred, IList<Vector2> fill,
                               Func<int, int> pick, out Vector2 spot, out int tier) {
        var ok = new List<Vector2>();
        for (tier = 0; tier < Tiers; tier++) {
            foreach (var list in new[] { preferred, fill }) {
                if (list == null) continue;
                ok.Clear();
                foreach (var c in list) if (Passes(q, c, tier)) ok.Add(c);
                if (ok.Count > 0) { spot = ok[pick(ok.Count)]; return true; }
            }
        }
        spot = default;
        tier = -1;
        return false;
    }

    public static List<Vector2> Grid(Rect r, float step) {
        var pts = new List<Vector2>();
        for (float x = r.xMin; x <= r.xMax + 1e-4f; x += step)
            for (float y = r.yMin; y <= r.yMax + 1e-4f; y += step)
                pts.Add(new Vector2(x, y));
        return pts;
    }

    /// <summary>Inclusive on every edge - Rect.Contains drops xMax/yMax, which silently
    /// rejected the hand-placed zones sitting exactly on the margin line.</summary>
    public static bool Contains(Rect r, Vector2 p) =>
        p.x >= r.xMin - 1e-4f && p.x <= r.xMax + 1e-4f && p.y >= r.yMin - 1e-4f && p.y <= r.yMax + 1e-4f;

    public static float DistToSegment(Vector2 p, Vector2 a, Vector2 b) {
        Vector2 ab = b - a;
        float len2 = ab.sqrMagnitude;
        if (len2 < 1e-8f) return Vector2.Distance(p, a);
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
        return Vector2.Distance(p, a + ab * t);
    }
}
}
