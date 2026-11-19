# UI mockups: handoff

Image mockups of the UI for the GDD (`GDD.md`). They are concept images, not the game's UI. All numbers, names (Eclipse Thread, Oakheart Wand, Moonshard Wand, the helpers) and folk spell icons are placeholders.

**Where they are:** folder `docs/mockups/` on this branch (a copy is also on branch `claude/gdd-mockup-design-j85dvv`). Its `README.md` lists every decision and file. Read that first.

## Theme: Starlit Grimoire
Self-lit violet pages in a night sky, gold and lavender ink, cyan for mana, magenta to white-hot for extreme output. Fonts: Almendra, Alegreya Sans, Cinzel, Nunito, Rajdhani. Reference image: `docs/mockups/theme/reference-glowing-book.webp`.

## Decided
- **Spellbook:** a diegetic grimoire with tabs Loadout, Quests, Map, Standing, Helpers and Settings, plus the sub-screens Change wand, Fusion recipes and Spell detail. All nine screens are done.
- **Quests:** show only the next step plus a "Percent of success" bar.
- **Standing:** tracked per town and faction, fines paid per faction. Repair-it-yourself appears in the moment a guard arrives, never in the book. If someone of a faction sees both the crime and you, standing drops at once. Refusing to talk to the guard costs more and raises the alert level. The fine stays on the record.
- **Helpers:** the book shows only location, no toggles. The toggle panel appears at the boss gate, and only for helpers asked in person.
- **HUD:** two selectable layouts, Grimoire Glass and Anime Clean.
- **Crosshair:** six customizable parts, each with several looks, plus presets. Default preset Astrolabe, others Starlight and Clean.
- **Flip indicator:** Edge Pointer (default) and Wing Bars. It is the game's `FlipIndicator.cs` horizon: it rolls with you, reacts to dive and climb, and turns red upside down.
- **Held cast:** Orbiting Star and Diamond Pips.
- **Broom status:** the Ride Rune, on its own row above the slots, styled to the HUD layout.
- **Output level:** one global value set while playing, with its own key. The gauge shows level and cost only, colour escalates, and there is no backfire marker. Backfire is shown per spell on its own casting circle and slot, and only for spells that have it. Looks: Zone Arc (default), Tick Ring, Orbit Beads, Tube, Gem.
- **Health:** Glass Vial (default) and Clean Bar, with the barrier as a cyan ring or line.
- **Crime prompt:** the Writ Scroll (pay, mend, or flee).
- **Boss helper prompt:** the Side Panel.
- **Mana sense:** parked for a later milestone. The idea (always-visible auras plus a focus lens) is kept in `docs/mockups/parked/`.

## Not designed yet
Repair minigame, quest board and merchant barter, dialogue system, boss bar, broom trial UI, carry-load indicator, notifications, death screen, main and pause menus, loading screen, tutorial prompts, jail screen, photo mode, casting circles, and the first spell concept boards. Co-op and PvP screens belong to later milestones.

## Open decisions
- The default HUD layout (Grimoire Glass or Anime Clean).
- The default output vessel (Tube or Gem).
- Whether to keep the small course ring beside the crosshair. It marks the broom's heading during free aim, and it was dropped from the newer designs.

## Working style
Brief notes first, then images. Several separate looks per element so the owner can choose. Image mockups only, not interactive pages. Plain language, no em-dashes.

## How the images are made
HTML pages rendered to PNG with Playwright at 1600 px wide. Sources are in `docs/mockups/source/`. Open them with a hash to pick a board, for example `hud8.html#5` for the crime prompt and `book.html#quests` for a spellbook page. They load Google Fonts.

## Gotchas
- Some saved images still contain looks that were dropped: the flight-level row on the crosshair parts image, the Wand Orb row on the output crystals image, and the unpicked looks on the health and prompt images. The mockups README names them.
- The PNGs were reduced to 256 colours to save space, so gradients are slightly less smooth.
- This GDD has not yet been updated with these UI decisions.
