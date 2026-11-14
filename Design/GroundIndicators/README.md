# Ground indicators: circle, cone and line (design pack)

Design only. Nothing here is imported by the game: this folder sits outside `Assets/`, so Unity ignores it. To try a design, copy the PNGs from `textures/` into `Assets/_Assets/Images/Circles/`, let Unity import them, and build the layers below as a Casting Circle asset anchored on the ground (Feet or Aim Point).

All textures are 512 x 512 RGBA, white with alpha, meant to be tinted by the spell's colour (`useSpellColor` on) except where stated. The previews in `previews/` were drawn outside Unity with the same layer values, so they are close but not exact.

Previews: `previews/GroundIndicators_states.mp4` (aiming, charging, locked), `..._states_strip.png`, `..._readability_pale_floor.png` (outline off vs on).

## Design language (shared by all three)
- Glyph track along the border, soft translucent segmented band, a broken inner ring that moves, a spinning star emblem.
- Blade horns: each corner / divider flares into a thin swept blade with a slight tip curl, a fine inner line and a bright dot at the tip. The blade's top edge carries on the main curve, its underside leaves the straight edge tangentially a little below the corner, and the two never meet at the same point on the edge.
- Brightness lives in the rim and ornaments; the fill stays soft.
- One white texture set, tinted per spell. No per-spell shapes (kept unique per spell in colour only).

## Circle (diameter 3.0 m, Aim Point or Feet, spins while indicating)
| Layer | Texture | Size m | Spin deg/s | Start | Draw | Appear | Opacity |
|---|---|---|---|---|---|---|---|
| Glow | existing `Glow.png`, tint = 0.8 x spell colour | 3.3 | 0 | 0 | 0.5 | FadeIn | 0.18 |
| Band | GroundCircle_Band | 3.0 | -45 | 0 | 0.7 | RadialWipe | 0.9 |
| Outer Track | GroundCircle_Outer | 3.0 | +22 | 0 | 0.6 | RadialWipe | 0.9 |
| Inner Ring | GroundCircle_Ring | 3.0 | +75 | 0.2 | 0.6 | RadialWipe | 1 |
| Core | GroundCircle_Core | 3.0 | -28 | 0.4 | 0.6 | Burst | 1 |
| Charge Fill | GroundCircle_ChargeFill | 3.0 | 0 | see Charge | | | 0.7 |
| Echo Outer (depth) | GroundCircle_Outer | 3.14 | -30 | 0 | 0.6 | RadialWipe | 0.45 |
| Echo Ring (depth) | GroundCircle_Ring | 2.95 | -95 | 0.2 | 0.6 | RadialWipe | 0.4 |

The two Echo layers sit slightly higher than the rest (depth) and turn the other way.

## Cone (4.0 m wide, 68 degrees, apex at the caster, points forward)
The apex is the centre of every cone texture, so the layers all share one pivot at the caster's end.
| Layer | Texture | Size m | Motion | Start | Draw | Appear | Opacity |
|---|---|---|---|---|---|---|---|
| Glow | `Glow.png` | 2.8, offset (0, +1.3) up the cone | none | 0 | 0.5 | FadeIn | 0.18 |
| Track | GroundCone_Track | 4.0 | none | 0 | 0.6 | RadialOut | 0.9 |
| Band | GroundCone_Band | 4.0 | none | 0.1 | 0.6 | RadialOut | 0.9 |
| Inner Arcs | GroundCone_Ring | 4.0 | swing +/-6 deg, 3.2 s period | 0.25 | 0.5 | RadialOut | 1 |
| Core | GroundCircle_Core (reused) | 0.8 | spin -28 deg/s | 0.4 | 0.6 | Burst | 1 |
| Charge Fill | GroundCone_ChargeFill | 4.0 | none | see Charge | | | 0.7 |

## Line (4.6 m texture; strip about 0.9 m wide; near end at the caster, far end forward)
| Layer | Texture | Size m | Motion | Start | Draw | Appear | Opacity |
|---|---|---|---|---|---|---|---|
| Track | GroundLine_Track | 4.6 | none | 0 | 0.6 | wipe from the near end up | 0.9 |
| Band | GroundLine_Band | 4.6 | none | 0.1 | 0.6 | wipe from the near end up | 0.9 |
| Arcs | GroundLine_Ring | 4.6 | slides toward the far end, 2.4 s loop, fades in and out | 0.9 | 0.1 | FadeIn | 0.9 |
| Arcs 2 | GroundLine_Ring | 4.6 | same, half a loop later | 0.9 | 0.1 | FadeIn | 0.5 |
| Star | GroundLine_Star | 0.85, offset (0, +1.0) toward the far end | spin -28 deg/s | 0.4 | 0.6 | Burst | 1 |
| Charge Fill | GroundLine_ChargeFill | 4.6 | none | see Charge | | | 0.7 |

The Star is a star only (no circle around it). There is no ring at the near end.

## States
- Aiming: the layers above run on their own. At the maximum range the indicator simply stops there (existing behaviour); there is no out-of-range look.
- Charging: the Charge Fill is revealed as the charge goes 0 to 1 (smoothstep). Reveal type: Circle = clockwise from the top; Cone = outward from the apex with a bright leading edge; Line = from the near end to the far end.
- Locked: for 0.4 s everything flashes toward white (alpha x 1.5 falling back to 1, tint 80 percent to white) and swells 5 percent, then holds in the filled state.
- Full-charge brightness is capped: the fill textures peak at about 60 percent alpha and the Charge Fill layer opacity is 0.7.

## Readability: dark outline
On pale ground an additive glow washes out. Under every layer except Glow and Charge Fill, draw a copy of the same texture in black, alpha blended (not additive), slightly blurred, at about 0.6 opacity. See `previews/..._readability_pale_floor.png`.

## What the current circle code does and does not support
Checked against `CastingCircle.cs` / `CastingCircleData.cs`. The older version on `main-bwmpsm` has: size, rotation speed, offset, start, draw time, appear (RadialWipe, ScaleUp, FadeIn), ready flash. The `spell-changes` branch adds more appear modes (Burst, RadialOut, Bloom, Flash, Pop), reverse spin, spin speed and curves, and exit styles.

Not available in either, so they need new code or shader work before the previews can be matched exactly:
1. Charge Fill reveal driven by the charge amount (clockwise / outward from the apex / near end to far end).
2. Wipe from the near end up (for the line).
3. Swing (back-and-forth rotation) for the cone's Inner Arcs.
4. Slide-and-fade along the line's axis for the Arcs.
5. A per-layer offset along the layer's own axis (the Star; the cone's Glow). A plain Offset exists, but check it is in the right space.
6. The dark outline layer under each texture (a second, alpha-blended material).
7. The lock flash with a tint to white and a 5 percent swell (the existing ready flash is similar but not the same).
8. Per-layer exit: not specified here; use the circle's existing exit style.

## Not done yet
- No Casting Circle `.asset` files and no `.meta` files (Unity creates the meta files and GUIDs on import).
- Sizes assume the layer sizes above at circleScale 1. Cone angle, line width and line length are placeholders; in the game they should come from the spell's real area.
- Colours in the previews are examples only: Absolute Zero ice for the circle, Fire for the cone, Lightning for the line.
