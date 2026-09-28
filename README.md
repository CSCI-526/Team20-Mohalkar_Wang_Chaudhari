# LOCKDOWN

**A 60-second arena shooter where getting shot doesn't cost you health — it costs you a direction.**

Take a hit and one of your four movement directions is taken away. Lose *Left* and the A key
simply stops working. The bullet that hits you decides which one you lose, so you don't get to
choose. Survive a full minute to win.

---

## Controls

| | |
|---|---|
| **WASD** / **Arrow keys** | Move |
| **Mouse** | Aim |
| **Left click** | Shoot |
| **R** | Restart |

Aiming is independent of movement — you can always shoot in a direction you can no longer
walk in.

---

## Losing a direction

Every bullet that lands takes one heart **and** one direction. Which direction depends on the
angle the bullet was travelling: you lose the one pointing back toward where it came from, so
getting shot from the left takes away your ability to move left, into the shot.

Your four directions are the coloured fins on your ship, and the arrows in the top-left
corner:

| Direction | Colour |
|---|---|
| Up | Blue |
| Down | Red |
| Left | Yellow |
| Right | Green |

When an arrow greys out, that key is dead.

## Getting it back

Losing a direction drops a **coloured orb** somewhere in the arena. Touch it and that
direction comes straight back.

You have **15 seconds** before the orb burns out — watch the ring around it shrink. Let it
expire and that direction is gone for the rest of the run.

The orb always lands somewhere you **can** reach with the directions you have left — never
behind a key you've lost — but far enough away (roughly a third of the arena) that getting
there means crossing open ground with three turrets watching. If a later hit cuts you off
from an orb that's already out, it jumps to a new reachable spot and keeps its timer.

**If all four directions go at once**, you're frozen solid. Hold on for two seconds and the
game hands one back. It's the only thing standing between you and a soft lock, and it will
keep doing it — but the turrets don't stop firing while you wait.

## Walls

The outer walls throw you back. Hit one and you rebound at the speed you hit it with —
head-on comes back hard, a glancing angle comes back gentler — and the bounce carries you
all the way back to the middle of the arena. Press any new key mid-bounce and you take
control back instantly — stop, reverse or dodge. Keys you were already holding when you hit
the wall don't cancel it, and letting go doesn't either. The bounce ignores your lost keys: losing
*Left* stops you choosing to move left, it doesn't stop the right-hand wall throwing you
left.

The bounce stops short of the crawler's patrol line — a wall should never throw you into
an instant kill — and it ends early if it carries you into a pillar. The pillars
themselves don't bounce; they're solid cover.

---

## What's shooting at you

**Turrets** — three of them, bolted to the corners of the arena. Each one glows for half a
second before it fires, and it commits to where you were standing when the glow started.
**That glow is your dodge window.** Step aside in time and the shot sails past.

They die in a single hit, but they come back, and they come back faster as the clock runs
down.

**Bullets** bounce once off a wall before they die, so a shot that misses you can still swing
round and catch you on the way back. Watch for the ones that turn hollow — those have already
bounced and they're coming from a direction you weren't looking.

You can **shoot bullets out of the air.** When you're down a direction and can't dodge, that's
often the only answer left.

**The crawler** patrols the middle of the arena. It cannot be killed and it does not take
hearts — it just kills you, instantly, on contact. Treat it as a moving wall, not an enemy.

---

## The clock is the difficulty

Nothing gets easier. Every ten seconds the arena tightens:

- turrets fire faster
- turrets respawn faster
- the crawler gets quicker — it starts slow enough to ignore and ends up nearly as fast as you

The last ten seconds are meant to be survived, not controlled.

---

## Winning and losing

**Survive 60 seconds** and you win. Take **five hits** and you're dead — or touch the crawler
once, which skips straight to the end no matter how many hearts you had left.

Hearts and directions are different things. Collecting orbs will keep you moving, but nothing
gives your hearts back.

---

## Changes in this branch

### A. Orbs (recovery keys) always spawn somewhere you can reach

- **Before:** the orb picked one of 8 fixed spots at random. It could land above you after
  you'd lost Up, where you could never collect it.
- **Now:** the orb only spawns where your remaining keys can take you. It's placed a fair
  distance away, clear of walls, turrets and the crawler.
- If a later hit cuts you off from an orb that's already out, that orb jumps to a new
  reachable spot and keeps its timer.

### B. Walls bounce you back to the centre

- **Before:** hitting a wall just stopped you dead.
- **Now:** the outer walls throw you back at the speed you hit them, all the way to the
  middle of the arena.
  - Press any new key mid-bounce to take control back instantly (e.g. to dodge a bullet).
  - A lost key doesn't block the bounce.
  - It stops short of the crawler's patrol path.
  - It ends early if you hit a pillar or turret.
  - Pillars themselves don't bounce.

---

## How orb reachability works

Code: `Assets/Scripts/Systems/OrbPlacement.cs`, used by `OrbSpawner.cs`.

**Rule 1: which keys does the trip need?** Compare the orb's position with yours on each axis:

- more than 0.7 units to the right → needs **Right**; to the left → needs **Left**
- more than 0.7 units above → needs **Up**; below → needs **Down**
- within 0.7 on an axis → no key needed on that axis, because the pickup circle already
  reaches it (orb radius 0.4 + ship radius 0.4)

A spot is reachable only if **every key the trip needs still works**. For example, if you've
lost Up, nothing above you qualifies.

**Rule 2: is the path clear?** The code checks two L-shaped routes, "sideways then up/down"
and "up/down then sideways", using a physics sweep the width of your ship. If both routes are
blocked by pillars, the spot is rejected.

**Rule 3: a fair spot, with rules relaxed step by step.** It checks candidate spots (the 8
hand-placed spots first, then a grid covering the whole arena) in tiers:

| Tier | Rules |
|---|---|
| 0 (ideal) | 5–8 units away (25–40% of the arena width), away from the edges, 3+ units from turrets, 1.5+ units from the crawler's path |
| 1 | drop the 5–8 distance rule, but stay at least 2.5 units away |
| 2 | also ignore turret and crawler distance |
| 3 | also allow spots near the edges |

Reachability (rules 1 and 2), not being inside a pillar, and staying 1.5 units from other orbs
**never relax**. The first tier that has any valid spot wins, and it picks randomly among
those spots.

**Rule 4: special cases**

- **All 4 keys lost:** the mercy rule gives back one key after 2 seconds (Up first), so the
  orb is placed for that key.
- **Stranded orbs:** on every new loss, each existing orb is re-checked. If it's now
  unreachable, it's moved to a new valid spot with a small "pop" animation. Its timer keeps
  running.
- **Truly nowhere to go** (for example, pressed against the left wall with only Left
  working): the orb spawns on you, so you get the key straight back. This is very rare.

---

## Running it

Open the project in Unity **6000.3.22f1** or newer, load `Assets/Scenes/Level01.unity`, and
press Play.
