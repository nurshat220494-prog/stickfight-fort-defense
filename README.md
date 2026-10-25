# Stick Fight Fort Defense

Local co-op PvE survival prototype for Stick Fight: The Game. It uses the game's existing maps, `HoardHandler`, weapons and stickman AI. Fort Defense starts on the built-in `castle2` map with one to four local players, spawns enemies in growing waves, and returns the team to the lobby after defeat. Enemies spawn on marked solid platforms, have 30 HP, use the game's damage values, and drop no items or currency.

## Play

- Join one to four local players in the lobby and press **F8** or click **START PvE**.
- In the lobby, press **C**, **S**, or **Down Arrow** (keyboard), or **Down** (controller) to open the color palette from anywhere. Choose a color with arrows and Enter or **1–9 / 0** (keyboard), or controller navigation; the menu shows the selected ability description. Each local player changes only their own color and ability. During a run, press **E** for the keyboard player's ability or **Up + Block** on a controller. The bottom cooldown HUD shows the controls.
- When a wave is cleared, each living player chooses one of up to three perks. Click an offer or press **1**, **2**, or **3**. The next wave starts after everyone has chosen.
- Perks last for the run and reset after defeat: +20% maximum health, 15% faster attacks, 10% reduced incoming damage, or 15 HP healing after each wave. A player cannot choose the same perk twice.
- Press **F1** or click **CONSOLE** for developer commands: `help`, `status`, `join`, `start`, `starttest`, `startpurpletest`, `testpurple`, `clearwave`, `testabilities`, and `sprayinfo`. The commands marked test are development aids.

## Implemented prototype content

Nineteen colors have first-pass abilities: red rage, pink healing aura, green roots, purple SPRAY beam, blue impulse, yellow barrier, orange detonation, cyan air step, brown stone skin, gray Storm Discharge, snow ice grip, dark-blue shadow form, lilac gravity sphere, gold crush, turquoise reload, lime parasitic seeds, raspberry rescue oath, steel hunter mark, and lemon greed. Red rage doubles punch damage, halves the time between punches, and protects from knockback for five seconds (15-second cooldown), with Claude's red ring and character flash plus the supplied red activation sound; pink healing restores up to 20 HP per second for four seconds (28-second cooldown); green roots keeps the growing root animation and freezes nearby enemies within 5 units for three seconds; purple SPRAY aims from the mouse, pierces enemies, deals 30 HP and has a three-layer violet beam. Blue impulse applies distance-scaled knockback with 100 close-range velocity-change strength in a 7 m radius (14-second cooldown). Yellow creates an 8-second forward barrier that blocks colliding projectiles/enemies and reduces frontal non-punch damage by 35% (25-second cooldown). Orange uses a cloned native Explosion visual and enforces 50 HP damage with 40 knockback strength within 5 m, plus nearby bomb chain reactions (30-second cooldown). Cyan air step is restricted to airborne use and sets upward velocity to at least 65 for the torso and 62 for other body parts, plus forward air control (5-second cooldown). Brown Stone Skin lasts 5 seconds: it reduces incoming damage and knockback by 90%, reflects projectiles, halves movement force and shows pulsing silver-gray outlines (25-second cooldown). Player-targeted abilities and effect markers use the character's moving main rigidbody position. Gray Storm Discharge fires toward the keyboard player mouse aim (or nearest visible enemy for other players), deals two 20 HP ticks at impact, then chains up to twice to enemies within 4 m for two ticks of 10 HP and then 5 HP; a visible 4 m pulse ring and bright lightning arc show each transfer, with electrified sparks around each target (22-second cooldown). Snow Ice Grip targets a 5.5 m area at the keyboard player's mouse cursor (controller players target ahead), freezes normal enemies there for 2 seconds, interrupts their AI attack state, and shows a rotating snowflake above each target; afterwards their AI movement scalar remains at 30% until they die or the run ends (20-second cooldown). The perk system reapplies selected max-health and damage-resistance modifiers while the run is active; Fast Hands modifies the game's punch timer reset. Build 0.10.11 compiles; fresh in-game visual and gameplay verification is pending.

Co-op downing and revival are implemented as a prototype. A teammate holds **F10** (keyboard) or **Block** (controller) near a downed player for six seconds; this needs a two-player live test. The perk intermission UI and wave transition were verified in a solo game. The selected defensive perk, perk stacking, medic healing and run-end stat reset still need live balance/edge-case checks.

## Build

Build `FortDefense.csproj` with the game's C# 5 compiler and BepInEx assemblies. The current source is `FortDefensePlugin.cs`; the build output is `bin/FortDefense.dll`. Install the DLL under `BepInEx/plugins/` and keep `FortDefenseAssets/` beside it; this folder contains the red rage activation WAV and two Gray Storm Discharge WAV clips.

## Scope

This phase excludes bosses, custom locations, campaigns, shop, currency and enemy drops. Enemy drops are disabled. Remaining work includes the other five planned colors, two-player live verification of the raspberry link and revive system, additional perks and balancing, weapons, specialized enemies, map modifiers and broader arena testing.





Keyboard color selection uses arrows/WASD + Enter or 1–9 / 0 for the first ten colors, then arrow navigation for the remaining options. The picker and developer console block the local player's movement while open.


### Controller and castle bridge notes (build 0.10.12)
- The controller color picker and perk selection use each local player's controller: move with D-pad/stick and confirm with Block. Abilities use R1 by default; change `Controls/AbilityButton` in `BepInEx/config/local.stickfight.fortdefense.cfg` to `Triangle` or `Both` if preferred. Keyboard E remains enabled.
- Bot pathfinding includes jointed moving bridges. If a bot stays stuck for 6 seconds, the safety recovery relocates it onto a nearby platform; it can do this at most three times for one route.

Build 0.10.13: controller ability input now defaults to both R1 and Triangle. The color picker ignores keyboard devices in its gamepad input path and debounces repeated keyboard movement events.

Build 0.10.13 also increases only route-triggered enemy jumps: the plugin temporarily scales the game's native jump force by 1.75x and allows navigation jumps toward waypoints up to 5.5 units higher. Player jump force is unchanged.

Build 0.10.14 adds a three-candidate platform planner (medium, high, and super-high traversal limits). It scores complete and partial routes, selects the cheapest feasible one, reevaluates moving-platform paths every 2 seconds, and gives route jumps three force tiers based on the climb required. Player jump physics are unchanged.
