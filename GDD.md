# Game Design Document

Working title: **Magic Game** (the final title is TBD and should name something that matters inside the game; decide after the story is written).
Engine: Unity 6.5, URP, new Input System. Target: Windows PC only (keyboard + mouse). Mobile is **removed** from the plan.
Status: draft 2, 2026-11-15. Anything marked **TBD** is not decided yet.

---

## 1. Vision

A third-person magic action game in a bright, medieval-fantasy open region. You are a witch who flies a broom and casts spells that can be tapped, chanted, and fused together. The world is explored freely, but story progress opens new parts of the map. New wands and spells are earned through separate quests, where the more powerful the reward, the harder or more secret the quest.

**Pillars** (every feature must serve at least one):
1. **Spells are the game.** Tap, chant, multicast and fuse. Chanting is both a mechanic and a performance (voice lines, circles, camera).
2. **The broom is freedom.** Flight is fast and fun, and the world is built to be flown over. Places where flying is forbidden feel like a deliberate change of pace.
3. **Explore, don't farm.** No grind is ever required. Quests are not locked behind a quest board, and the story can be finished without any specific side quest (harder, never impossible).
4. **Immersion over interface.** The HUD stays out of the way, and sound, camera and the witch herself carry the feedback.

## 2. Finish line

This is a **personal project with no release date and no store**. It is "done" when the owner is happy with it. Because the scope is large, the finish line is split into **milestones**, and each one is a complete place to stop (see section 13). Milestone 3 is the recommended definition of "finished".

## 3. Audience and platform
- Windows PC, keyboard and mouse. Mobile is cut, so heavier effects such as runtime fracture are allowed (still keep performance healthy for co-op).
- Co-op (quests only) after the single-player game works. PvP arena after that.
- Controller support: **not planned**.

## 4. Structure and game loop

**Shape:** story / mission campaign inside one large open region (5+ zones), similar in spirit to Genshin Impact or Wuthering Waves.

- **Main story:** a chain of missions. Finishing a story beat opens more of the map (zones, gates, or sealed areas). Some zones open through story, others need a **broom upgrade** (the "mix of story and abilities" rule).
- **Open world:** drop-in play. Roam, fly, find things, do a quest, save and quit at any time.
- **Short runs:** most quests and missions are short, about 10 to 15 minutes. Secret quests that give powerful spells can be much longer.
- **Replay:** quests do not need to be repeatable. Replay value comes from repeatable challenge content (boss fights, trials, arenas) and the multiplayer modes, not from re-running quests.
- **Never required:** no farming, no mandatory side quest, no quest-board dependency. Skipping optional content makes the game harder, not blocked.

**Moment-to-moment loop:** explore (broom) > find a quest or threat > fight (spells, mana, barrier) > reward (story, level, wand, spell, broom upgrade, cosmetic) > a new part of the map or a new option in the spellbook.

## 5. Story

The owner will write the story. This document only fixes the structure:
- Premise, characters, and ending: **TBD (owner writes)**.
- Story is delivered as missions that unlock zones. Placeholder chapter list in section 8.
- Tone: grounded medieval fantasy with a brighter, more hopeful feel than classic "dark" fantasy; each zone sets its own mood (see section 9).

## 6. Core gameplay systems

Most of these already exist in the project (details in `HANDOFF_project.md` and `SPELLS.md`).

### 6.1 Player and controls
Move WASD, look mouse, jump Space, sprint Shift. Spell slots: LMB, RMB, 1 to 5 (7 slots, rebindable). Combine `B`, barrier `F`, broom `X`, lock-on `V`, mana charge `C`. Esc pauses. Movement is not rebindable for now.

### 6.2 Spells
- Each spell has a **tap** version and a **hold (chant)** version; some have charge tiers.
- Tap is instant. Hold shows a casting circle and drains mana gradually; releasing early fizzles.
- **Multicast:** holding several spells casts them together.
- **Fusions:** press `B`, press the spells to join, press `B` again to ready the fusion (15 s timer), hold `B` to cast it. 2 to 4 ingredient recipes.
- **Sky-combat rules** (from `SPELLS.md`): everything reaches a flier; quick spells never restrict movement; siege spells (Starfall and others) are a big commitment.
- Current spell list: Star Bolt, Seeker Swarm, Flak Burst, Gale Crescent, Thunderclap, Frost Spire, Gravity Well, Starfall, plus fusions (Nova Shell, Firefly Barrage, Storm Front, Absolute Zero, Heaven Piercer, Judgement). The finished game targets about **40 spells** (and about **12 wands**), which grows through quests.
- **Chants:** voiced lines per spell, text above the head today; the finished game adds **chanting voice lines** (see section 10).

### 6.3 Mana, health, barrier
Mana (max 200, regen, manual charge) pays for spells, the barrier (its health), and broom flight. Health is separate. These three share one resource, so choices matter.

### 6.4 Broom flight
Costs mana, has boost and a quick dash. The broom is upgraded through its own **trial quests** (broom control challenges). Some story chapters require a certain broom upgrade before they can begin.

### 6.5 Flight-restricted areas
Some fights happen underground or in places where flying is forbidden or limited (caves, ruins, towns, sealed halls). Those fights are ground-based, use cover and the barrier, and favour spell choices that do not rely on height. There are three kinds of restriction: a **hard ban** underground and in sealed halls (no broom at all), a **low ceiling** in tight spaces (you can fly low but not climb), and **law-based** bans in towns (flying is possible but a crime, tied to the wanted level). Which specific areas use which: **TBD**.

### 6.6 Enemies
- Ground and air enemies both matter (existing: Rusher, Charger, Heavy, Seeker, flying shooter).
- **Rival witches on brooms** are elite enemies who cast spells like the player. They are also the foundation for PvP, so build their AI and spell use to share the player's casting code.
- **Bosses** are set pieces with a gimmick (existing: The Warden of Ruins, three phases).
- Enemy and boss stat values stay in code for now and are reworked when the new structure is in place.

### 6.7 Destruction, crime and repair

Spells can destroy scenery, and destroying things has consequences.

- **What breaks:** props (crates, barrels, fences, trees) and buildings and walls. Terrain deformation is not planned.
- **How:** runtime physics fracture, with chunks flying apart. Terrain-wide spectacle is limited by a pooled, capped number of live pieces so combat and co-op stay smooth.
- **Why it matters:**
  - spectacle and feedback for spells,
  - puzzles and secrets (break a seal or wall to find a hidden quest, spell or loot),
  - a combat tool (collapse things on enemies, remove their cover; enemies and bosses can wreck yours),
  - lasting story and world changes (broken gates, ruined buildings).
- **Repair:** destroyed things are never simply reset. The player or NPCs have to **fix** them, and whoever breaks them can be committing a **crime**.
- **Choice when you break something:** the player picks how to make it right:
  - pay a **fine or repair bill**,
  - repair it **themselves** by a **minigame**, or
  - repair it with **repair magic**, unlocked by a quest or by a specific spell.
- **Consequences:** a **wanted level** (guards hunt you, including witch guards on brooms) and **reputation** with towns and factions, which affects shops, quests and story options.
- **Where crime applies depends on the zone**: each zone has its own law (a lawless wilderness versus a strict sky city). Table of zone laws: **TBD**.
- Exact rules for what the wanted levels do, reputation tiers, and the repair minigame design: **TBD**.

### 6.8 Progression
| What | How it grows |
| --- | --- |
| Player level and stats | Quests and story |
| Wands (about 12) | Separate wand quests; stronger wands come from harder or more secret quests. A wand changes stats and mana modifiers, has its own unique ability, and sets the number of spell slots in the loadout |
| Spells | Separate spell quests with the same rule |
| Broom | Its own trial quests (broom control); also gates some story |
| Cosmetics | Rewards from some quests |

**Quest difficulty rule:** the reward's power sets the quest's difficulty and secrecy. Easy common spells and wands are obtained simply. The most powerful ones combine **hard fights, hidden secrets, puzzles, and exploration**, and may be long.

## 7. Co-op, NPC helpers and PvP (later milestones)
- **Co-op (first):** players help each other with quests.
- **Player count follows boss scale.** Each boss gets a **recommended range**, for example 5 to 10, but the number is **not fixed**: you can enter with fewer or more. Bosses **also scale live** (health, phases and mechanics) with how many are fighting.
- **Networking:** Steam-style peer-to-peer with a host; others join by invite. Keep spells, quests and world state data-driven so they can be synchronised.
- **NPC helpers when playing alone:** which NPCs help is decided by the quests the player has completed (a character you helped can join later fights). **Some NPCs help and some will not**, each has a personality and their own voice lines, and there is no limit on how many you can recruit. When playing solo, NPCs count toward boss scaling like players. Full roster and rules: **TBD**.
- **PvP arena (after co-op):** built after the rival-witch enemy exists, since that enemy reuses the same casting and flight logic. Balance and matchmaking: **TBD**.
- Single-player must stay fully playable without any of this.

## 8. World and zones

One large region with at least five zones, plus the hub. The owner wants zones that are more distinct than a standard list; each needs a unique look, mood, enemy mix, and music.

| Zone | Role | Notes |
| --- | --- | --- |
| Castle town (hub) | Home base: shops, spellbook access, co-op lobby, flight restricted | Medieval village or town |
| Forest | Starting zone | Exists today (runtime-generated) |
| Floating islands / sky city | Late-game broom showcase | Needs top broom upgrades |
| Zone 4 | **TBD**, make it unique | Idea: flight-restricted underground |
| Zone 5 | **TBD**, make it unique | |
| More zones | **TBD** | Candidates previously considered: ruined kingdom (existing Warden boss), snow peaks, desert/canyon, cursed marsh |

Zone gating: story chapters, sealed gates or fog that story events remove, and broom-upgrade requirements. Exact chapter plan: **TBD** after the story is written.

## 9. Art direction

- **Style:** anime cel-shading in the vein of Genshin Impact and Wuthering Waves: clear cel bands, outlines, richer colour, more realistic proportions than the current chibi.
- **Mood:** grounded medieval fantasy, a little brighter than classic medieval. Each zone sets its own palette and mood (for example, a bright forest, a gloomy cave, a luminous sky city).
- **Characters:** the witch goes **full anime** (realistic anime proportions, no chibi). The character design happens in a later milestone. The final model is made from AI image-to-3D and cleaned up in Maya, or built entirely by the owner. The current chibi is a placeholder.
- **Spell effects:** 3D gold stars, glowing cores, crescent streaks and star trails (the "star synthesis" reference), one distinct look per spell (see `VFX_SPEC.md`).
- **Camera:** third-person, wider and more cinematic than the current one. It pulls back for flight and big spells, and gives short cinematic moments for bosses and zone reveals.
- **Asset sources:** mostly the owner with AI and code-built assets (editor tools under Tools > Magic). Keep toon shaders (`ToonCharacter`, `ToonGround`) as the shared look and upgrade them toward cel shading (branch `stylized-toon-shader`).
- **Performance:** priority from the start. Hide or skip what the player cannot see (the forest already skips chunks outside the camera).

## 10. UI and immersion

- **In play:** a clean fantasy HUD, minimal gold trim, small icons, and it **fades out unless needed**. Combat state is shown on the witch and in the world where possible (wand glow, aura, barrier, circle) instead of bars.
- **Menus:** diegetic **spellbook**. Loadout, fusion recipes, quests and map live in a grimoire. Opening it shows the book in the witch's hands with the world dimmed behind it.
- **Feedback:** chants, wand hum and ambient sound carry state and mood.
- **Cinematic camera:** big spells, bosses, and zone reveals.
- Immersion applies to the other parts of the game too (quests, shops, hub), not only combat. The detailed design of each is **TBD**.

## 11. Audio

- **Ambient music per zone**, **SFX** for spells, movement and enemies, and **chanting voice lines** per spell (the chant is spoken or sung as it is cast). NPC helpers have their own voice lines.
- **Voice lines are made with the owner's local voice AI** and **pre-generated** as audio files, then imported. Connecting it (how the AI is called, naming, an editor tool that batches lines from spell data and assigns clips) is a task for a later milestone; the owner already has the voice AI ready.
- The Songs slider already exists and is inert until music is added. Music sources: **TBD**.

## 12. Technical notes

- Unity 6.5, URP, new Input System. Rig parts, forest, and shared assets are scene objects or catalog assets, not runtime-built where possible.
- Player data lives in a `PlayerSave` ScriptableObject, and it is wiped before every build.
- Performance targets: **TBD** (PC only; runtime fracture and co-op set the budget). Windows build only after the owner says polishing is finished.
- Open technical decisions: networking solution for co-op, save format for a long open-world campaign, quest system design (data-driven recommended, since quests are not tied to a board).

## 13. Milestones

Each milestone ends in something complete. Stopping at any of them still leaves a game.

| # | Name | Contents | Done when |
| --- | --- | --- | --- |
| M0 | Core combat (current) | Casting, fusions, broom, mana, barrier, base enemies, one boss, menus | Existing features feel good in play-testing; remaining VFX and cast animations done per `VFX_SPEC.md` |
| M1 | Open-world slice | Forest zone as free roam, quest system, first main-story mission, one spell quest, one wand quest, broom trial 1, hub placeholder, new camera, immersive HUD, spellbook menu | Playable from start to a story gate that opens a second area |
| M2 | Campaign core | 3 zones, 5 to 8 story missions, flight-restricted fight type, rival witch enemy, cel-shaded look, zone music, chant voices | Beginning to a zone-3 boss, with the story written |
| M3 | Full single-player (**recommended finish line**) | 5+ zones including hub and sky city, full story, all spell, wand and broom quests, all bosses, full audio, cosmetics | Complete story and all unlockables work end to end |
| M4 | Co-op | Quest co-op for 2 to 4 players | Two players finish a quest together |
| M5 | PvP arena | Duels using the rival-witch systems | A balanced match runs start to finish |
| M6 | Final polish and build | Full anime witch, QA, Windows build | Shippable to friends on PC |

(Mobile was removed. The witch redesign and its model can move earlier if the owner wants.)

## 14. Open questions and next steps

**Title:** TBD. It should be something that matters inside the game (a place, artifact or concept from the story), so choose it once the story is written.

**Open questions to answer:**
1. Story premise, cast, and ending (owner writes).
2. Zones 4 and later: themes and unique hooks, and the law of each zone.
3. Which specific areas use a hard ban, a low ceiling, or a law-based ban on flight.
4. Spell counts per zone and per rarity, and how many of the ~40 are fusions.
5. What each of the ~12 wands does (stats, unique ability, slot count).
6. Crime details: wanted levels, reputation tiers, repair minigame, which spell unlocks repair magic.
7. NPC helper roster: who helps, who refuses, and what each needs.
8. Boss scaling numbers for each boss.

**Suggested next steps:**
1. Answer the open questions above (they can be answered a few at a time).
2. Write the story outline and chapter-to-zone map.
3. Plan M1 in detail: quest system data model, spellbook menu, new camera, immersive HUD.
