# Sky-combat spell system

The spell list was redesigned for fast broom fights. The old ground-combat spells are still in `Assets/_Assets/Weapons` as
reference, but they are no longer in the game catalog. The new spells live in `Assets/_Assets/Spells` (fusions in `Spells/Fusions`)
and are ordinary `SpellData` assets: every number below can be changed in the Inspector.

## Rules the spells follow

1. **Everything reaches a flier.** Shots have a tall hit shape, burst near a target, or curve toward it. Area spells are pillars
   from the ground to above the flight ceiling (60 to 70 m; the broom tops out at 40 m). Beams are instant and thick.
2. **Quick spells do not slow the fight.** Taps never restrict movement. Short holds use the *Slow* rule, which on the broom is
   now 80% speed and keeps the boost. Only siege spells use *Stand*: 40% speed, no boost.
3. **Siege spells are a commitment.** Seconds of cast time, most of the mana bar, broken by a solid hit, and the blast is big
   enough to cover a whole arena section and break scenery.

Locked onto a moving target, shots are aimed at where the target will be, and area spells land where it is heading.

## Spells

| Spell | Tap | Hold (cast time) | Role |
| --- | --- | --- | --- |
| **Star Bolt** | 110 m/s bolt, slight curve to target | *Comet Lance* (0.45 s): piercing lance | Rapid fire, free movement |
| **Seeker Swarm** | 3 homing darts | *Seeker Storm* (0.7 s): 9 homing darts that burst near targets | Fire and forget while dodging |
| **Flak Burst** | Shell that bursts 3 m from an enemy, 5 m blast, burn | *Skyburner* (0.8 s): 5 m fuse, 9 m blast | Groups of fliers |
| **Gale Crescent** | Piercing blade, 8 m tall | *Tempest Wall* (0.8 s): 20 m tall, slows | Enemies above and below you |
| **Thunderclap** | Instant beam, 140 m | *Chain Lightning* (0.6 s): jumps to 4 more enemies, stuns | Fast targets, no travel time |
| **Frost Spire** | 60 m tall pillar, 4.5 m wide, slows | *Glacier Spire* (0.9 s): 8 m wide, stays 4 s | Area denial at any altitude |
| **Gravity Well** | Vortex that drags enemies in for 2.5 s | *Singularity* (1.0 s): 11 m, 5 s, stronger pull | Sets up the siege spells |
| **Starfall** | (hold only) | *Starfall* (4.5 s) > *Cataclysm* (7.5 s) > *World Ender* (11 s) | Siege spell |

Starfall: 18 m radius and 260 damage, growing to 25 m / 416 and 34 m / 676 as it is charged. The pillar is 70 m tall, the crater
burns for 5 s, it destroys barriers and breakable scenery. It costs 90 / 126 / 180 mana, cannot be multicast, and a hit of 12 or
more damage breaks the cast.

## Fusions

| Fusion | Recipe | What it does |
| --- | --- | --- |
| **Nova Shell** | Star Bolt + Flak Burst | Fast shell, 6 m fuse, 12 m blast |
| **Firefly Barrage** | Seeker Swarm + Flak Burst | 14 homing darts that burst and burn |
| **Storm Front** | Gale Crescent + Thunderclap | 28 m tall piercing wave that stuns |
| **Absolute Zero** | Frost Spire + Gravity Well | 13 m pillar that freezes for 3.5 s |
| **Heaven Piercer** | Star Bolt + Thunderclap + Gale Crescent | 5 m thick piercing beam, 220 m, chains 3 times |
| **Judgement** | Thunderclap + Starfall | Siege spell (5 s cast): 22 m, 330 damage, 2.5 s stun |

## New fields on a spell variant

| Field | Meaning |
| --- | --- |
| `hitHeight` | Extra reach above and below a shot (metres). Scenery ignores it. A faint standing streak shows the reach. |
| `proximityFuse` | The shot goes off this close to an enemy. |
| `homingTurnRate` | Degrees per second the shot turns toward the locked enemy, or the nearest one ahead of it. |
| `chainCount`, `chainRange`, `chainFalloff` | Beam jumps to more enemies. |
| `aoeHeight` | Already existed; set it above the flight ceiling for a pillar. |
| `lingerDuration`, `lingerTickDamage`, `lingerTickInterval`, `lingerPull` | The area stays, keeps hurting and can drag enemies in. |

On the spell itself: `fxStyle` picks which existing effect family it uses (model, trail, impact, sound, cast pose), and
`roleLabel` is the short label in the spell book.

## Breakable scenery

Add the `Destructible` component to a map prop (pillar, wall, tower). It needs colliders but no tag. Hits weaker than
`Min Hit Damage` do nothing, so with the default of 40 only charged spells, fusions and siege spells break it. When it breaks,
each child mesh becomes a falling chunk. `Collapse Damage` optionally hurts whatever is standing next to it.
