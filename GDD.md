# Game Design Document

Working title: **Magic Game** (the final title is TBD and should name something that matters inside the game; decide after the story is written).
Engine: Unity 6.5, URP, new Input System. Target: Windows PC only (keyboard + mouse). Mobile is **removed** from the plan.
Status: draft 2, 2026-11-15. Anything marked **TBD** is not decided yet.

---

## 1. Vision

A third-person magic action game in a bright, medieval-fantasy open region. You are a witch who flies a broom and casts spells that can be tapped, chanted, and fused together. The world is explored freely, but story progress opens new parts of the map. New wands and spells are earned through separate quests, where the more powerful the reward, the harder or more secret the quest.

**The feeling that makes it worth finishing:** powerful, cinematic spellcasting **and** the freedom of flight and exploration.

**Pillars** (every feature must serve at least one):
1. **Spells are the game.** Tap, chant, multicast and fuse. Chanting is both a mechanic and a performance (voice lines, circles, camera).
2. **The broom is freedom.** Flight is fast and fun, and the world is built to be flown over. Places where flying is forbidden feel like a deliberate change of pace.
3. **Explore, don't farm.** No grind is ever required. Quests are not limited to the quest board, and the story can be finished without any specific side quest (harder, never impossible).
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
- **Never required:** no farming, no mandatory side quest. A quest board exists, but quests are not limited to it. Skipping optional content makes the game harder, not blocked.

**Quest types:** story missions, spell and wand quests, broom trials, and NPC favor quests (which can make NPCs into helpers).
**Finding quests:** the quest board, NPC conversations, exploration and secrets, and rumors and clues collected in the spellbook. Some quests have a map marker and some deliberately have none.

**Moment-to-moment loop:** explore (broom) > find a quest or threat > fight (spells, mana, barrier) > reward (story, upgrade, wand, spell, broom upgrade, cosmetic) > a new part of the map or a new option in the spellbook.

## 5. Story

The owner will write the story. This document only fixes the structure:
- Premise, characters, and ending: **TBD (owner writes)**.
- **Cast to plan for:** a **mentor/guide**, a **main rival witch** (an early opponent who is a rival, **not entirely an enemy**), a **main villain who appears later** in the story, and town leaders and merchants (who also judge crime).
- **Opening:** an **interactive tutorial where the player must survive a situation**; details follow the story.
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
- **Spell themes:** the ~40 spells are **not fixed to elements**; what exists depends on what the player learns, in the spirit of *Frieren*. Themes to draw from: fire, frost, lightning, wind, gravity, space, time, nature, earth, poison, light, shadow and summoning.
- **Spell redesign (next work):** the current spells are disliked because they are not unique enough, tap and hold read as two different spells, charge tiers just give a bigger version, fusions stack the ingredients' effects, and the visuals and scale look bad. The new rules:
  - **Each spell is unique** in shape and motion, cast feel (animation, camera, sound), use in play, and theme or identity.
  - **A spell can have any number of forms.** Holding to charge can **transform** it into something different, not only a stronger version (for example a lightning ball becomes a lightning beam). Each form has its own shape and look.
  - **A fusion is an entirely new spell**, picked by a **hand-designed recipe table**; it does not have to combine its ingredients' properties.
  - **Pre-fused spells can be equipped in a slot like any normal spell**, but casting one this way **costs more mana than fusing it live**. Live fusing with `B` stays.
  - **Scale:** spells are **too small for the world and bosses** and should be bigger. The **casting circle is too big** and should be **smaller, but scale with the spell's size** (small for taps, large only for siege spells).
  - **Start from scratch:** the current spell list is discarded. New spells are designed together (the owner gives references and ideas, Claude proposes concepts), and the M1 slice needs only **3 to 4 very polished spells**.
  - **Spell scale follows the player's mana and mana output**, and there is a separate **mana output** control (own key) the player uses, in the spirit of *Frieren*:
    - The player sets a **power level before casting**, which **multiplies how much each spell costs**, and the output **sets the spell's size continuously**.
    - While casting, the player can **hold to pour in even more mana**. Drawbacks so it isn't free power: past a safe level the **cost rises much faster**, and **mana regeneration pauses for a few seconds afterward**.
    - **Every spell has its own output limit.** Pushing extra output past that point makes the spell **backfire and damage the caster**. The limit differs per spell and is part of its design.
    - Output is shown as a **visible mana aura** on the witch, and the player can **suppress mana** (stealth) so enemies and NPCs notice less.
    - Normal tap and hold casting stays the base, and output is the extra layer.
  - M1's first spells should cover: a basic attack with a signature motion, a big charge spell that transforms (**not just ball to beam**), the movement spell (blink or dash), and a defensive or counter spell.
  - **First concepts (approved, working names):**
    - **Weaver** (basic attack): tap fires a glowing thread that snakes in a lazy S-curve to the target and stitches to it; hold stitches several threads between you and enemies, and on release they snap tight and yank the targets together.
    - **Hollow Moon** (big charge spell): a dark disc grows over the wand while chanting; tap-release is a spinning crescent blade; held longer it becomes a moon hanging in the sky, and on release it collapses into a huge ring that shrinks and crushes everything inside.
    - **Mirror Bloom** (defense and counter): glass petals open in front of the witch; a well-timed tap reflects an incoming projectile back as a shard storm; held, it becomes a slow dome that bends projectiles around you.
    - **Veil Step** (movement): a petal blink spell with a **toggleable passive that changes the dash into a blink**; details in the next bullets.
    - **Lantern** (multipurpose): a floating light you place or throw. It lights dark places such as caves, marks enemies for homing, lures enemies when thrown, and held it grows into a beacon that others can teleport to.
    - **Spells with a passive part:** a spell in the list can have an **active part** (what it does when cast) and a separate **passive part** that is **toggled on and off**. Equipping a spell keeps its active part usable; the toggle only controls the passive. The passive **costs mana when it activates**, or **continuously takes up mana while toggled on**.
    - **Mana style is decided per spell**: each spell chooses whether its passive costs mana per activation or keeps mana tied up while on (Veil Step uses per activation, since constant drain would cost too much). **Any kind of passive is allowed**, for example passive healing, trap sense or eagle eye.
    - **Veil Step** is the example: its **active part is unchanged when equipped** (holding it still opens the star gate portal ahead of you, with the afterimage), and its **toggleable passive part turns the dash into a blink** (petals dissolve and re-form at the aimed spot). So it is an ordinary castable spell and a movement modifier at once. **Tapping Veil Step** makes a short blink toward where you aim; holding opens the star gate.
  - The remaining spell designs: **TBD** (spell changes come before M1 content).
- **Chant length scales with cast time:** short for taps and quick holds, long verses for siege spells and fusions.
- **Learning spells:** from people (teachers, rivals, NPC mages), from books and ruins, and by practice and use.
- **Getting spells:** quests give spells, and **fusion recipes are discovered** by experimenting or following clues (they then appear in the spellbook).
- **Loadout size depends on the wand.** Some wands hold many spells, and some hold **only one and cannot fuse, which makes that wand extremely strong**. Slot numbers per wand: **TBD**. A single-spell wand has no multicast and no fusion, its spell is boosted far above normal and costs a lot of mana or cast time, and it is **tied to the hardest secret quests** (though not every one of them has to be the hardest).
- **Fusion recipes are shown but locked until learned.** You see the recipe, and you learn it by doing; some recipes come from quests or quest rewards.
- **Chants:** voiced lines per spell, text above the head today; the finished game adds **chanting voice lines** (see section 10).

### 6.3 Mana, health, barrier
Mana (max 200, regen, manual charge) pays for spells, the barrier (its health), and broom flight. Health is separate. These three share one resource, so choices matter.

### 6.3b Defense
There is no single defense. The player avoids damage by **dodging and dashing on the broom**, by **ground movement using magic**, by the **mana barrier**, by using **terrain and destructible cover** (which breaks), and by **counter or parry magic**. **Ground movement magic** includes blink or short teleport, a magic dash or slide, and spell-powered jumps and climbs, and some spells double as movement. **Counter and parry magic** includes timed parries and interrupting enemy casts; some spells exist **only as counter spells**, and some are combined spells like projectile versus projectile. Exact timings and which spells: **TBD**.

### 6.4 Broom flight
**Flight is free outside combat**; in combat it costs mana (the current game drains mana the whole time, which will change). It has boost and a quick dash. The broom is upgraded through its own **trial quests** (broom control challenges). Some story chapters require a certain broom upgrade before they can begin.

### 6.5 Flight-restricted areas
Some fights happen underground or in places where flying is forbidden or limited (caves, ruins, towns, sealed halls). Those fights are ground-based, use cover and the barrier, and favour spell choices that do not rely on height. There are three kinds of restriction: a **hard ban** underground and in sealed halls (no broom at all), a **low ceiling** in tight spaces (you can fly low but not climb), and **law-based** bans in towns (flying is possible but a crime, costs reputation with that town). Which specific areas use which: **TBD**.

### 6.6 Enemies and bosses
- **The game is boss-focused.** Bosses are the main fights and regular enemies are fewer and serve as supporting encounters, not the bulk of play. Effort goes into boss design, boss scaling and boss spectacle first.
- Enemy factions tie directly to the story and are **not set yet** (candidate groups: rival witches, creatures and monsters per zone, corrupted or cursed beings, human soldiers and guards). Boss count and structure: **TBD** with the story.
- Ground and air enemies both matter (existing: Rusher, Charger, Heavy, Seeker, flying shooter).
- **Rival witches on brooms** are elite enemies who cast spells like the player. They serve as mini-boss duels, as law guards who chase you when a town or faction's reputation toward you is low, and as recurring characters with their own stories. They are also the foundation for PvP, so build their AI and spell use to share the player's casting code.
- **Bosses** are set pieces (existing: The Warden of Ruins, three phases). It depends on the boss: many are multi-phase, some have a gimmick or puzzle, some are huge and fought in the air on the broom, many have destructible arenas, and **some have no gimmick at all**.
- **Crime during boss fights:** it depends on the zone, but destruction is **mostly ignored while a boss fight is going**.
- **Rematches:** a boss you beat is placed in a **simulation area** where you can fight it again. Rematch rewards: **TBD**.
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
  - repair it **themselves** by a **minigame** (a repair center appears near the damaged place; you carry the materials that thing needs and fix it yourself; a **perfection meter** decides if the result is good or bad, and **your repair stays permanent**), or
  - repair it with **repair magic**, unlocked by a quest or by a specific spell.
- **Repair quality:** a bad repair **looks shabby, is weaker, and returns less reputation, or can even negate the reputation you would get back**. To redo it the player must **destroy their own work** and repair it again. A repair **better than the original** earns **more reputation** than simply paying back the damage.
- **Materials:** a **mix**: the repair center holds basic materials, and special materials come from the world.
- **Carrying loads:** the player can **carry loads and deliveries on the broom** (also used for delivery quests). **Dropping a load reduces that piece's durability until it is destroyed**, and **hitting something destructible damages the load too**.
- **Crime is wider than destruction.** Doing bad things in general counts, including hurting or killing NPCs and civilians, which is possible **with consequences**.
- **No wanted-level stages.** Consequences depend on **reputation**, tracked **per town and per faction**, separately. Towns and factions can be allies, and a faction can come from the same town while still being a separate standing. Reputation affects **prices and barter terms**, **which quests and NPC helpers appear** (some refuse you if you're hated), **guard behavior** (including witch guards on brooms hunting you), and **story options**.
- **Where crime applies depends on the zone**: each zone has its own law (a lawless wilderness versus a strict sky city). Table of zone laws: **TBD**.
- **Making amends:** repair and pay fines, do favor quests for that town, **capture a wanted criminal**, **revive NPCs you killed**, or **serve time in jail**. **Some crimes are permanent.**
- **Killing NPCs:** a dead NPC **stays dead until revived**. Important NPCs are **strong or protected by default but can still be killed**.
- **Allies:** whether allied towns and factions share reputation is **up to each faction**.
- **Co-op:** reputation is **per player**, even in a shared world, so a friend's crime in your world is on their own record.
- Exact reputation tiers and thresholds, the list of crimes, and the repair minigame design: **TBD**.

### 6.8 Progression
| What | How it grows |
| --- | --- |
| Player stats | **No levels, only upgrades**: quest and story rewards raise stats (health, mana and so on) |
| Wands (about 12) | Separate wand quests; stronger wands come from harder or more secret quests. A wand changes stats and mana modifiers, has its own unique ability, and sets the number of spell slots in the loadout |
| Spells | Separate spell quests with the same rule |
| Broom | Its own trial quests (broom control); also gates some story |
| Cosmetics | Rewards from some quests |

**Quest difficulty rule:** the reward's power sets the quest's difficulty and secrecy. Easy common spells and wands are obtained simply. The most powerful ones combine **hard fights, hidden secrets, puzzles, and exploration**, and may be long.

### 6.9 Economy, failure, difficulty and saving
- **Economy:** medieval-style trade. There is a **central money system**, but you don't have to pay with money: you can offer **anything of equal or higher value**, and what it can be traded for depends on the seller. Fines and repair bills use the same system. Value tables and which items are tradable: **TBD**.
- **Death:** you respawn at the **last checkpoint with no loss**.
- **Difficulty:** medium by default, with a **selectable difficulty** (easy, normal, hard). Exact numbers: **TBD**.
- **Saving:** **auto-save anywhere**, so drop-in play always keeps progress.

### 6.10 Extras
- In-engine **cinematic cutscenes**, a **photo mode**, **subtitles** for chants and dialogue, and the existing **rebindable keys**. You also asked for **more accessibility options** beyond subtitles and rebinding. Chosen options: **text size and UI scale**, **colour-blind modes**, **reduced shake, flash and motion**, and **hold-versus-toggle** for chants and aiming. Further options: **TBD**.

### 6.11 Completed quests stay completed
Most quests are **not repeatable**, so once finished they **stay finished** in the world and the save. What to do with them afterward (for example, whether their places change, give nothing, or offer a replay, and how a co-op player who already finished one is handled): **TBD, to be decided later**.

### 6.12 Map and navigation
The spellbook has a **map** that starts as fog of war and reveals as you explore. **Maps can be bought from shops**: if you bought a region's map, the unexplored parts show greyed out; if you haven't, they don't appear at all (plain fog). There are **fast travel points**. Marker rules follow section 4 (some quests marked, some not).

## 7. Co-op, NPC helpers and PvP (later milestones)
- **Co-op (first):** players help each other with quests. Players see each other **both** in the open world and in quests: a friend can roam your world, and you can invite each other into quests. A quest completed together counts as complete for **both players**, and both are rewarded.
- **Player count follows boss scale.** Each boss gets a **recommended range**, for example 5 to 10, but the number is **not fixed**: you can enter with fewer or more. Bosses **also scale live** (health, phases and mechanics) with how many are fighting.
- **Networking:** Steam-style peer-to-peer with a host; others join by invite. Keep spells, quests and world state data-driven so they can be synchronised.
- **NPC helpers when playing alone:** which NPCs help is decided by the quests the player has completed (a character you helped can join later fights). **Some NPCs help and some will not**, each has a personality and their own voice lines, and there is no limit on how many you can recruit. NPC helpers can be **switched off individually**, and they are **also allowed in co-op**, with limits: every NPC has a **value**, every boss has a **value limit**, and the NPCs brought in count toward that limit. The more NPC value you bring, the more the boss scales up. When playing solo, NPCs count toward boss scaling like players. An NPC's value comes from its **combat power** and its **support role**. Boss scaling changes the boss's **health** and **rewards**. **Rewards scale with everything**: more players and more NPC value both mean a stronger boss and better rewards. Boss arenas are a **mix**: some bosses roam the open world and some are in dedicated arenas. Value numbers, boss limits and the full roster: **TBD**.
- **PvP arena (after co-op):** built after the rival-witch enemy exists, since that enemy reuses the same casting and flight logic. There are **several modes** (candidates: 1v1 duel, team battle, free-for-all, and a co-op boss race; **what actually gets added is TBD**), and PvP uses **separate PvP numbers** for damage and cooldowns so PvE balance is untouched. Matchmaking: **TBD**.
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

**Build method:** the world is **mostly handcrafted with AI assistance**. **Time and weather:** the world has a **day and night cycle with weather**; Weather interacts with spells, and **some spells change the weather itself**. Day and night and weather affect **enemy and boss behavior**. The player can **skip to day or night from the menu**. Specific weather rules: **TBD**. **AI assistance** is used across the whole build, roughly **half and half** with handmade work: terrain and foliage layout, props and building placement, concept art and 3D model drafts, and editor tools and scripts.

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
- **Menus:** diegetic **spellbook**. Loadout, fusion recipes, quests and map live in a grimoire. Opening it shows the book in the witch's hands with the world dimmed behind it. Each part is its own **tab**: loadout and fusion recipes, quest log with rumors and clues, map, and reputation, NPC helpers and settings. In single player the game **pauses while the book is open**; in co-op the world keeps running.
- **Feedback:** chants, wand hum and ambient sound carry state and mood.
- **Cinematic camera:** big spells, bosses, and zone reveals.
- Immersion applies to the other parts of the game too (quests, shops, hub), not only combat. The detailed design of each is **TBD**.

## 11. Audio

- **Sound priorities:** wind and broom flight, and chants and spell charge-ups, matter most for immersion.
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

### M1 detail (current goal)

**Scope (nothing cut):** forest free roam, quest system, one story mission, one spell quest, one wand quest, broom trial 1, a hub (placeholder town), new camera, immersive HUD, spellbook menu, plus the destruction and crime pieces below.

**Done means:** it **feels good and looks like the target** (quality bar, not just a checklist). Because that is subjective, write a short list of "feel checks" when M1 starts (for example: flight feels free, a chant feels powerful, breaking a wall feels satisfying, the HUD stays out of the way).

**World:**
- The forest is **rebuilt handcrafted with AI assistance** (the runtime generator is not the final answer), and it is **small: about 2 minutes of flight across**, dense and polished.
- The hub is a **generated placeholder town** containing: houses and walls to break, a quest board and a merchant, a guard NPC, and a repair center.

**Destruction and crime in M1:** runtime fracture of props, destruction of buildings and walls, the repair minigame (repair center, carried materials, perfection meter), and reputation and crime for the one town.

**Story mission:** a **tutorial only, with no boss**. It is an interactive survival tutorial that ends by opening the first gate or area. Story content is a placeholder until written.

**Other M1 content:**
- **Spell quest:** learn a spell from a **teacher NPC** (the teacher sets a task, then teaches).
- **The spell taught:** a **movement spell (blink or dash)**.
- **Wand quest:** a **unique starter wand given by the teacher**, with a **unique ability** and a **distinctive look** (its own model and glow colour).
- **Camera in M1:** a wider ground follow, a dynamic flight camera, cast zoom and framing for big chants, and cinematic moments in the tutorial.
- **Existing content:** only a **small part** of the current spells and enemies carries into M1. **Spell changes come next** (to scope M1); which spells stay is decided then.
- **Broom trial 1:** an **obstacle slalom through trees and ruins** (the first broom upgrade), with destructible gaps where it fits.
- **Tutorial situation:** a **generic placeholder**, reworked when the story is written.

**Suggested build order** (riskiest and most foundational first):
1. Destruction prototype: runtime fracture with a capped, pooled piece count. It is the biggest technical risk.
2. Quest system data model: story, spell, wand and broom quests and NPC favors all use it.
3. New camera and immersive HUD (fading HUD, state shown on the witch).
4. Spellbook menu with tabs (loadout and fusions, quest log, map, reputation, helpers and settings).
5. Forest rebuild and the placeholder town.
6. Quest content: tutorial, spell quest, wand quest, broom trial 1.
7. Repair minigame and reputation, then a polish pass against the feel checks.

**Decided for M1:**
- **Feel checks:** flying across the forest feels free; a chant makes the player feel powerful; breaking a wall feels satisfying; the HUD stays out of the way; each of the 4 spells looks different at a glance.
- **Fracture budget:** start with about **150 live pieces** at once, with the oldest fading out first, and tune after testing.
- **Teacher:** an older, dry-humoured witch, slightly impatient, who teaches by giving tasks (placeholder until the story is written).
- **Spell build order:** **Weaver** first (it proves the casting circle, scale and visual style), then Hollow Moon, Mirror Bloom, Lantern and Veil Step.
- **Starter wand's unique ability:** **Held cast**: pause a chant halfway and keep the spell ready, then release it later. The starter wand must stay **viable late in the game and keep its uniqueness**, so players have a reason to keep using it even after finding stronger wands. It should offer **a variety of things to use** rather than one trick, and its ability should stay useful at every stage (how it grows or stays relevant: **TBD**). **Echo** (every spell leaves a weaker echo that repeats it once) and **Compass** (the wand points toward the nearest secret or quest site) are **set aside for other wands**, along with the unused ideas Sapling (the wand's glow and boost follow your most used spell), Gem reservoir (stored overflow mana released as a burst) and Chant partner (the wand hums half the chant).

**M1 open questions (remaining):** the feel-check list, the tutorial's content, Echo's tuning, the spell designs after the first four, and the fracture piece budget.

## 14. Open questions and next steps

**Title:** TBD. It should be something that matters inside the game (a place, artifact or concept from the story), so choose it once the story is written.

**Open questions to answer:**
1. Story premise, cast, and ending (owner writes).
2. Zones 4 and later: themes and unique hooks, and the law of each zone.
3. Which specific areas use a hard ban, a low ceiling, or a law-based ban on flight.
4. Spell counts per zone and per rarity, and how many of the ~40 are fusions.
5. What each of the ~12 wands does (stats, unique ability, slot count).
6. What happens with quests that are completed and cannot be repeated (also in co-op).
6b. Crime details: reputation tiers and thresholds, which towns and factions are allied, list of crimes, repair minigame, which spell unlocks repair magic.
7. NPC helper roster: who helps, who refuses, and what each needs.
8. Boss scaling numbers for each boss.

**Suggested next steps:**
1. Answer the open questions above (they can be answered a few at a time).
2. Write the story outline and chapter-to-zone map.
3. Plan M1 in detail: quest system data model, spellbook menu, new camera, immersive HUD.
