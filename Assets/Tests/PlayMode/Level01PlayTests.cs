using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Lockdown;

namespace Lockdown.Tests {

/// <summary>
/// Wall bounce and orb placement against the real Level01: real walls, real pillars, real
/// physics. Turrets and the crawler are switched off for the movement tests so nothing
/// interferes with the player's path.
/// </summary>
public class Level01PlayTests {
    PlayerController _pc;
    Rigidbody2D _rb;

    [UnitySetUp]
    public IEnumerator LoadLevel() {
        PlayerController.TestInput = null;
        SceneManager.LoadScene("Level01");
        yield return null;
        yield return null;
        _pc = Object.FindFirstObjectByType<PlayerController>();
        _rb = _pc.GetComponent<Rigidbody2D>();
    }

    [TearDown]
    public void Cleanup() { PlayerController.TestInput = null; }

    void QuietArena() {
        GameObject.Find("Turrets")?.SetActive(false);
        GameObject.Find("Crawler")?.SetActive(false);
    }

    void Place(Vector2 p) {
        _rb.position = p;
        _rb.linearVelocity = Vector2.zero;
        _pc.transform.position = p;
        Physics2D.SyncTransforms();
    }

    // ------------------------------------------------------------ wall bounce

    /// <summary>Holds <paramref name="input"/> for <paramref name="seconds"/>, tracking the
    /// lowest x reached and the fastest leftward speed seen while bouncing.</summary>
    IEnumerator Hold(Vector2 input, float seconds, System.Action<Vector2, Vector2> each) {
        PlayerController.TestInput = input;
        for (float t = 0; t < seconds; t += Time.fixedDeltaTime) {
            yield return new WaitForFixedUpdate();
            each(_rb.position, _rb.linearVelocity);
        }
    }

    [UnityTest]
    public IEnumerator HeadOn_BouncesBackAtFullSpeedAllTheWayToCentre() {
        QuietArena();                     // crawler off: nothing to stop short of
        Place(new Vector2(8f, 3f));       // y = 3 clears the pillars (y = +/-2)
        yield return new WaitForFixedUpdate();

        float minX = float.MaxValue, fastest = 0f;
        yield return Hold(Vector2.right, 3f, (p, v) => {
            minX = Mathf.Min(minX, p.x);
            if (_pc.Bouncing) fastest = Mathf.Max(fastest, -v.x);
        });

        Assert.GreaterOrEqual(_pc.BounceCount, 1);
        Assert.AreEqual(5f, fastest, 0.05f, "comes back as fast as it went in");
        Assert.That(minX, Is.InRange(-0.3f, 0.3f), "carried back to the centre line, even with D held");
    }

    [UnityTest]
    public IEnumerator Diagonal_BouncesBackSlowerThanHeadOn() {
        QuietArena();
        Place(new Vector2(8.5f, 3f));
        yield return new WaitForFixedUpdate();

        float fastestX = 0f;
        yield return Hold(new Vector2(1f, 0.3f), 1.5f, (p, v) => {
            if (_pc.Bouncing) fastestX = Mathf.Max(fastestX, -v.x);
        });
        // Input (1, 0.3) normalised: 4.79 u/s of it heads into the wall.
        Assert.AreEqual(5f / Mathf.Sqrt(1.09f), fastestX, 0.1f);
        Assert.Less(fastestX, 4.9f);
    }

    [UnityTest]
    public IEnumerator SideWallBounceStopsShortOfTheCrawler() {
        GameObject.Find("Turrets")?.SetActive(false);   // crawler stays ON for this one
        Place(new Vector2(8f, 0f));
        yield return new WaitForFixedUpdate();

        float minX = float.MaxValue;
        bool ended = false;
        PlayerController.TestInput = Vector2.right;
        for (float t = 0; t < 3f && !ended; t += Time.fixedDeltaTime) {
            yield return new WaitForFixedUpdate();
            minX = Mathf.Min(minX, _rb.position.x);
            if (_pc.BounceCount > 0 && !_pc.Bouncing) ended = true;
        }
        PlayerController.TestInput = Vector2.zero;
        Assert.IsTrue(ended);
        Assert.That(minX, Is.InRange(4.3f, 4.8f), "stops ~1.5 short of the crawler's path (ends at x = 3)");
        Assert.AreEqual(GameState.Playing, LevelManager.I.State, "the bounce must not kill the player");
    }

    [UnityTest]
    public IEnumerator BounceCarriesThePlayerInALostDirection() {
        QuietArena();
        DirectionSystem.I.Lose(Direction.Left);
        Place(new Vector2(8f, 3f));
        yield return new WaitForFixedUpdate();

        float minX = float.MaxValue;
        yield return Hold(Vector2.right, 3f, (p, v) => minX = Mathf.Min(minX, p.x));
        Assert.Less(minX, 0.5f, "thrown left across the arena although Left is lost");
    }

    IEnumerator BounceOffRightWall() {
        QuietArena();
        Place(new Vector2(8.5f, 3f));
        yield return new WaitForFixedUpdate();
        PlayerController.TestInput = Vector2.right;
        while (_pc.BounceCount == 0) yield return new WaitForFixedUpdate();
        for (int i = 0; i < 5; i++) yield return new WaitForFixedUpdate();   // well under way
        Assert.IsTrue(_pc.Bouncing);
    }

    [UnityTest]
    public IEnumerator PressingANewKeyTakesControlBackMidBounce() {
        yield return BounceOffRightWall();

        // Bullet incoming - dodge down. D is still held; S is the new key.
        PlayerController.TestInput = new Vector2(1f, -1f);
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        Assert.IsFalse(_pc.Bouncing, "a new key ends the bounce");
        Vector2 v = _rb.linearVelocity;
        Assert.Greater(v.x, 3f, "moving the way the player chose (right)...");
        Assert.Less(v.y, -3f, "...and down, at normal speed");
    }

    [UnityTest]
    public IEnumerator ReversingMidBounceStopsTheBounce() {
        yield return BounceOffRightWall();
        PlayerController.TestInput = Vector2.zero;           // let go of D: bounce carries on
        yield return new WaitForFixedUpdate();
        Assert.IsTrue(_pc.Bouncing, "letting go doesn't cancel");
        PlayerController.TestInput = Vector2.right;          // press D again: a new choice
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        Assert.IsFalse(_pc.Bouncing);
        Assert.Greater(_rb.linearVelocity.x, 4.9f, "heading back right at full speed");
    }

    [UnityTest]
    public IEnumerator BounceEndsAtAPillar() {
        QuietArena();
        Place(new Vector2(8f, -2f));      // pillar P4 spans x 4.5..5.5 at y = -2
        yield return new WaitForFixedUpdate();

        float minX = float.MaxValue;
        PlayerController.TestInput = Vector2.right;
        while (_pc.BounceCount == 0) yield return new WaitForFixedUpdate();
        PlayerController.TestInput = Vector2.zero;
        for (float t = 0; t < 1.5f; t += Time.fixedDeltaTime) {
            yield return new WaitForFixedUpdate();
            minX = Mathf.Min(minX, _rb.position.x);
        }
        Assert.IsFalse(_pc.Bouncing);
        Assert.That(minX, Is.InRange(5.8f, 6.1f), "stopped against the pillar, not through it");
    }

    [UnityTest]
    public IEnumerator PillarsDoNotBounce() {
        QuietArena();
        Place(new Vector2(3.3f, 2f));     // pillar P2 spans x 4.5..5.5 at y = 2
        yield return new WaitForFixedUpdate();
        yield return Hold(Vector2.right, 1f, (p, v) => { });
        Assert.AreEqual(0, _pc.BounceCount);
        Assert.Greater(_rb.position.x, 3.9f, "reached the pillar and stopped against it");
    }

    // ------------------------------------------------------------ orbs

    [UnityTest]
    public IEnumerator OrbIsReachable_EveryLossComboAcrossTheArena() {
        Random.InitState(526);
        var ds = DirectionSystem.I;
        var spawner = OrbSpawner.I;
        int spawned = 0;

        for (int mask = 0; mask < 15; mask++) {            // 15 = nothing lost, skipped
            for (float x = -9f; x <= 9f; x += 1.5f)
            for (float y = -5f; y <= 5f; y += 1.25f) {
                var p = new Vector2(x, y);
                if (Physics2D.OverlapCircle(p, 0.45f, Layers.WallMask) != null) continue;   // inside a pillar
                Place(p);

                Direction lost = Direction.None;
                for (int i = 0; i < 4; i++) {
                    bool live = (mask & (1 << i)) != 0;
                    ds.TestSetActive((Direction)i, live);
                    if (!live && lost == Direction.None) lost = (Direction)i;
                }

                spawner.SpawnFor(lost);
                Orb orb = spawner.LiveOrb(lost);
                Assert.IsNotNull(orb, $"no orb for {lost} at {p}");
                Vector2 at = orb.transform.position;
                Assert.IsTrue(spawner.IsReachable(at), $"orb at {at} unreachable from {p}, keys mask {mask}");
                Assert.IsNull(Physics2D.OverlapCircle(at, 0.4f, Layers.WallMask), $"orb at {at} is inside a wall");
                spawner.Retire(lost);
                spawned++;
            }
        }
        Assert.Greater(spawned, 1000);
        yield return null;
    }

    [UnityTest]
    public IEnumerator StrandedOrbIsRelocated() {
        var ds = DirectionSystem.I;
        var spawner = OrbSpawner.I;

        for (int seed = 0; seed < 25; seed++) {
            Random.InitState(seed);
            for (int i = 0; i < 4; i++) ds.TestSetActive((Direction)i, true);
            foreach (Direction d in Dir.Priority) spawner.Retire(d);
            Place(new Vector2(0f, -3.5f));

            ds.Lose(Direction.Left);
            spawner.SpawnFor(Direction.Left);
            Orb orb = spawner.LiveOrb(Direction.Left);
            Vector2 before = orb.transform.position;

            // Take away a key that the trip to that orb needs.
            OrbPlacement.Needed(before - (Vector2)_pc.transform.position, 0.7f, out var h, out var v);
            Direction strand = h != Direction.None ? h : v;
            Assert.AreNotEqual(Direction.None, strand);
            Assert.IsTrue(spawner.IsReachable(before));

            ds.Lose(strand);
            spawner.SpawnFor(strand);

            Vector2 after = orb.transform.position;
            Assert.AreNotEqual(before, after, $"seed {seed}: stranded orb was not moved");
            Assert.IsTrue(spawner.IsReachable(after), $"seed {seed}: relocated orb still unreachable");
            Assert.IsTrue(spawner.IsReachable(spawner.LiveOrb(strand).transform.position));
        }
        yield return null;
    }
}
}
