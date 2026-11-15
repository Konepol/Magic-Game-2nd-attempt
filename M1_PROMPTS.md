# M1 prompts

Paste **one part at a time** into a Claude Code session in this project. After each part: play-test it using that part's checklist, then tell Claude what to change or say "approved". Do not paste the next part until the current one is approved.

The source of truth for design is `GDD.md` (read the sections each part names). M1's quality bar is "feels good and looks like the target".

## Rules to paste at the start of every session

```
Read GDD.md first, especially the sections named in the task. Work on only this one task; do not start the next part. Stay in the existing project conventions (Unity 6.5, URP, new Input System, own code in Assets/_Assets). Do not re-run any builder that overwrites my Inspector tweaks (for example the casting circle builder) without asking. Keep performance healthy: hide or skip what the player cannot see. When finished, do not commit. Give me a short list of what changed, how to test it, and anything you could not verify. I will review and play-test, then tell you to commit.
```

## Order

| Part | What | Why here |
| --- | --- | --- |
| 1 | New spell foundation (forms, passives, output) | Every spell depends on it |
| 2 | Mana output and the casting circle rescale | Scale and the circle come before any spell looks right |
| 3 | Weaver | Proves visuals, scale and the new system |
| 4 | Hollow Moon | Big charge spell with transforms |
| 5 | Mirror Bloom | Counter and parry |
| 6 | Lantern | Multipurpose utility |
| 7 | Veil Step and the toggled passive | Movement and passives |
| 8 | Starter wand: held cast | The prepare-ahead wand |
| 9 | Destruction prototype | Biggest technical risk |
| 10 | Quest system | Everything quest-shaped uses it |
| 11 | New camera and immersive HUD | Feel |
| 12 | Spellbook menu | Loadout, quests, map, reputation |
| 13 | Forest rebuild (handcrafted, small) | The world |
| 14 | Placeholder town | Hub and test site |
| 15 | Reputation and crime | Needs town and destruction |
| 16 | Repair minigame | Needs destruction and reputation |
| 17 | Tutorial, teacher, wand and spell quests | Uses everything above |
| 18 | Broom trial 1 | Slalom |
| 19 | Polish against the feel checks | Quality bar |

---

## Part 1: New spell foundation

```
Task: replace the old spell structure with the new spell foundation described in GDD.md section 6.2 (spell redesign, forms, passives) and 6.3.

Do:
- Remove the old spell list from the game catalog (keep the old assets in the project but out of the catalog so nothing is lost).
- Design the data for a spell: any number of forms, where holding to charge can transform the spell into a different form (each form has its own shape, motion, visuals); an optional passive part that is toggled separately and costs mana either per activation or continuously, chosen per spell; a per-spell output limit past which it backfires and damages the caster; a size that follows mana output.
- Support fusions as hand-designed recipes producing entirely new spells, and support equipping an already-fused spell in a slot at a higher mana cost than fusing live. Keep live fusing with B.
- Add a throwaway test spell so the system can be exercised before real spells exist.

Do not build real spells, new VFX, or the output key yet.
Done when: the game compiles, the old spells are out of play, and the test spell can be equipped and cast with tap, a hold that transforms it, a passive toggle and a backfire.
Play-test: equip the test spell, tap it, hold it until it changes form, toggle its passive and see mana behave as described, push it past its limit and take damage.
```

## Part 2: Mana output and the casting circle rescale

```
Task: implement the mana output control (GDD.md section 6.2, "Spell scale follows the player's mana and mana output") and rescale the casting circle.

Do:
- Add a separate key for mana output: set a power level before casting that multiplies spell cost and sets spell size continuously; hold while casting to pour in more mana; cost rises much faster past a safe level and mana regeneration pauses for a few seconds afterward; each spell's output limit causes backfire.
- Show output as a visible mana aura on the witch (simple first version), and a suppress mode that lowers the aura.
- Make the casting circle much smaller and scale with the spell's size (small for taps, large only for siege-sized casts). Do NOT re-run the circle builder; change the circle through its scale or component values only, and tell me exactly which values you changed.
- Make spell scale overall bigger relative to the world and bosses so the biggest casts can be bigger than a boss's body.

Done when: changing output visibly changes the test spell's size and cost, and the circle matches it.
Play-test: cast at low, normal and high output; watch the aura; push past the limit; compare the circle size for a tap and for a large cast.
```

## Part 3: Weaver

```
Task: build the spell Weaver (GDD.md section 6.2, first concepts) as the first real spell, using the new foundation and output system.

Weaver: tap fires a glowing thread that snakes in a lazy S-curve to the target and stitches to it. Hold stitches several threads between the witch and enemies; on release they snap tight and yank the targets together.

Do: the thread visuals (anime-style, glowing, a distinct look from all other spells), the S-curve motion, the stitch and the snap, cast animation and camera feel, sound placeholders, and the casting circle for it. It should read clearly at flight speed and at distance.

Done when: it looks and feels unique, scales with output, and works against the existing test enemies.
Play-test: tap at a near and far target, hold with several targets, cast on the ground and on the broom, at low and high output. Tell me what looks wrong.
```

## Part 4: Hollow Moon

```
Task: build Hollow Moon (GDD.md section 6.2): a dark disc grows over the wand while chanting; tap-release is a spinning crescent blade; held longer it becomes a moon hanging in the sky and on release collapses into a huge ring that shrinks and crushes everything inside.

Do: all three forms with distinct visuals, the transform timing, scale tied to output, an output limit with backfire, chant timing hooks for the voice lines later, cast animation and camera pull-out for the big form.

Play-test: tap, medium hold, full hold; do it on a boss-sized target; push past the output limit.
```

## Part 5: Mirror Bloom

```
Task: build Mirror Bloom (GDD.md section 6.2): glass petals open in front of the witch; a well-timed tap reflects an incoming projectile back as a shard storm; held, it becomes a slow dome that bends projectiles around you.

Do: the petal visuals, a clear timing window with feedback, reflecting enemy projectiles, the dome form, and mana costs. If the test enemies do not shoot enough to test it, add a simple projectile-firing test dummy.

Play-test: parry at the right and wrong time, hold the dome against several projectiles, stand in it while moving.
```

## Part 6: Lantern

```
Task: build Lantern (GDD.md section 6.2): a floating light you place or throw. It lights dark places, marks enemies for homing, lures enemies when thrown, and held it grows into a beacon that others can teleport to.

Do: the light object, marking and homing support for other spells, lure behavior, and the beacon (teleport can be a placeholder until multiplayer, so use a self-teleport for now). Add a dark test area so lighting can be judged.

Play-test: place it, throw it, mark an enemy and fire Weaver at it, hold for the beacon and teleport.
```

## Part 7: Veil Step and the toggled passive

```
Task: build Veil Step (GDD.md section 6.2): tap makes a short blink toward the aim in petals; hold opens a star gate ahead that you can fly through at speed, leaving a fading afterimage that enemies target. It also has a separately toggled passive part that changes the dash into a blink, costing mana per activation.

Do: both parts, the toggle in the spell list, the passive's mana cost, and the visuals. Make sure equipping it keeps its active part usable.

Play-test: tap and hold it, toggle the passive and dash, toggle it off and dash.
```

## Part 8: Starter wand, held cast

```
Task: build the starter wand and held cast (GDD.md section 6.2, starter wand): pause a chant halfway and keep the spell ready, release it later; the bank starts at one paused spell and can grow later; held spells can be fused; a held spell scales with output; held cast is unique to this wand.

Do: the wand model placeholder with a distinctive glow, the held-cast mechanic and its HUD indication, bank size as a value that quests can raise later, and held fusion.

Play-test: hold a spell then release it, hold two after raising the bank, fuse held spells, pour output into a held spell.
```

## Part 9: Destruction prototype

```
Task: runtime physics fracture of props and buildings or walls (GDD.md section 6.7).

Do: a fracture system with a capped, pooled piece count of about 150 live pieces where the oldest fade out first; props (crates, barrels, fences, trees) and a wall or building piece that can be destroyed by the new spells and by enemies; a durability value; debris that is cheap to simulate. Build a small test arena. Do not build repair or crime yet.

Done when: destroying many objects at once keeps frame rate healthy.
Play-test: destroy one object, then a whole wall, then spam spells at a cluster; watch the frame rate and the piece cap.
```

## Part 10: Quest system

```
Task: build the data-driven quest system (GDD.md sections 4 and 6.8): story missions, spell and wand quests, broom trials and NPC favor quests; quests are not limited to a quest board; some have map markers and some do not; completed quests stay completed; completing a quest can give a spell, a wand, a stat upgrade, reputation or a cosmetic.

Do: the quest data, objectives, rewards, save and load into the save system (auto-save anywhere), a minimal debug UI, and three test quests. Keep it simple enough that co-op can sync it later.

Play-test: start, progress and complete a test quest, quit and reload, confirm it stays complete.
```

## Part 11: New camera and immersive HUD

```
Task: the new camera and HUD (GDD.md sections 9 and 10): a wider ground follow, a dynamic flight camera (pulls out with speed), cast zoom and framing for big chants, and scripted cinematic moments. The HUD hides unless needed, shows combat state on the witch (aura, wand glow, barrier), and stays clean fantasy style with thin gold trim.

Play-test: walk, fly fast, cast a big spell, check the HUD fading in and out.
```

## Part 12: Spellbook menu

```
Task: the diegetic spellbook (GDD.md section 10): a book in the witch's hands with the world dimmed behind it, with tabs for loadout and fusion recipes (locked recipes shown), quest log with rumors and clues, map (fog of war, bought maps greyed, fast travel), and reputation, NPC helpers and settings. The game pauses while it is open in single player.

Play-test: open it, equip spells, read the quest log, open the map and fast travel.
```

## Part 13: Forest rebuild

```
Task: rebuild the forest as a handcrafted world, small (about 2 minutes of flight across), dense and polished, with AI assistance where useful (GDD.md sections 8 and 9, M1 detail). Keep the existing edge and culling work. Build quest sites as handmade points of interest, with day and night and weather as a first simple version.

Play-test: fly across, check frame rate, look at the anime-style lighting and mood.
```

## Part 14: Placeholder town

```
Task: a generated placeholder town (M1 detail): houses and walls that can be destroyed, a quest board and a merchant with the barter economy (anything of equal or higher value), a guard NPC and a repair center. Flying inside town costs reputation.

Play-test: walk the town, trade, read the quest board, talk to the guard.
```

## Part 15: Reputation and crime

```
Task: reputation per town and per faction (GDD.md section 6.7): crime includes destruction and harming NPCs; consequences are guard behavior, shop prices and which quests and helpers appear; making amends by repair, fines or favor quests; reputation is per player.

Play-test: break a wall in town and watch the guard and prices react; pay a fine.
```

## Part 16: Repair minigame

```
Task: the repair minigame (GDD.md section 6.7): a repair center appears near the damage; you carry the materials (basic from the center, special from the world; dropping or crashing damages the load) and fix it yourself; a perfection meter decides the result; the repair is permanent; a better-than-original repair earns more reputation; a bad repair looks shabby and is weaker; redo by destroying your own work.

Play-test: repair well, repair badly, drop a load.
```

## Part 17: Tutorial, teacher and quests

```
Task: the tutorial and first quests (M1 detail): an interactive survival tutorial (generic placeholder situation, no boss) that ends by opening the first gate; the teacher NPC (older, dry-humoured, impatient) with the spell quest that teaches Veil Step and the wand quest that gives the starter wand; chant voice lines hooked up from the pre-generated audio (placeholder files are fine).

Play-test: play from a new save through the tutorial and both quests.
```

## Part 18: Broom trial 1

```
Task: the obstacle slalom through trees and ruins (M1 detail), the first broom upgrade, with destructible gaps where it fits and a clear reward on completion.

Play-test: fly it, crash into things, finish it, check the upgrade.
```

## Part 19: Polish against the feel checks

```
Task: go through the M1 feel checks in GDD.md and report where each one fails: flying across the forest feels free; a chant makes the player feel powerful; breaking a wall feels satisfying; the HUD stays out of the way; each of the four spells looks different at a glance. Fix the worst items. Do not add features.
```
