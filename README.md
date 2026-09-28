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

## Running it

Open the project in Unity **6000.3.22f1** or newer, load `Assets/Scenes/Level01.unity`, and
press Play.
