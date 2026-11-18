# UI mockups

Image mockups for the GDD (`GDD.md` on the `spell-changes` branch). They are concept images, not the game's UI. All numbers, names (Eclipse Thread, Oakheart Wand, Moonshard Wand, the helpers) and folk spell icons are placeholders.

Theme: **Starlit Grimoire**. Self-lit violet pages in a night sky, gold and lavender ink, cyan for mana, magenta to white-hot for extreme output. Reference image: `theme/reference-glowing-book.webp`.

## Decisions

| Area | Decision |
| --- | --- |
| Spellbook | Diegetic grimoire, tabs: Loadout, Quests, Map, Standing, Helpers, Settings |
| Quests | Show only the next step plus a "Percent of success" bar |
| Standing | Per town and faction, fines paid per faction. Repair-it-yourself appears in the moment a guard arrives, not in the book |
| Helpers | No toggles in the book, only location. The toggle panel appears at the boss gate, and only for helpers the player asked in person |
| HUD | Two selectable layouts: **Grimoire Glass** and **Anime Clean** |
| Crosshair | Six customizable parts. Default preset **Astrolabe**, also Starlight and Clean |
| Flip indicator | Two looks: **Edge Pointer** (default) and **Wing Bars** |
| Held cast | **Orbiting Star** and **Diamond Pips** |
| Broom status | **Ride Rune**, on its own row above the slots, themed to the HUD layout |
| Output level | One global value set while playing. The gauge shows level and cost only, colour escalates, no backfire marker. Backfire is shown per spell on its own casting circle and slot |
| Output looks | Zone Arc (default), Tick Ring, Orbit Beads, Tube, Gem |
| Health | Glass Vial (default) and Clean Bar, barrier shown as a cyan ring or line |
| Crime prompt | **Writ Scroll** (pay, mend, or flee) |
| Boss helper prompt | **Side Panel** |
| Mana sense | Parked for a later milestone (A+C idea kept in `parked/`) |

## Files

- `spellbook/` the eight spellbook screens. `00-loadout.png` is the Loadout page, `06` to `08` are Loadout sub-screens.
- `hud/` the two full-screen HUD layouts.
- `crosshair/`
  - `parts-chant-window-lockon.png` has the chant, window and lock-on looks. The flight-level row on it is superseded by the flip indicator.
  - `flip-indicator.png` shows four looks. Only A and B were kept.
  - `held-cast-and-customizer.png` shows the customizer.
- `gauges/`
  - `output-dials.png` shows three dials.
  - `output-crystals.png` shows three vessels. The Wand Orb row was dropped.
  - `health.png` shows four looks. Only the Vial and the Bar were kept.
- `prompts/` the crime and boss helper prompts, four styles each. Only D was kept.
- `parked/` mana sense.
- `theme/` the reference image and the first theme board.
- `source/` the HTML pages the images were rendered from (Playwright, 1600 px wide). Open with a hash to pick a board, for example `hud8.html#5`. They load Google Fonts.

## Not designed yet

Repair minigame, quest board and merchant barter, dialogue, boss bar, broom trial UI, carry-load indicator, notifications, death screen, main and pause menus, loading, tutorial prompts, jail screen, photo mode, casting circles, and spell concept boards. Co-op and PvP screens belong to later milestones.
