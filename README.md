# Stick Fight: Fort Defense

An experimental local-co-op PvE mod prototype for **Stick Fight: The Game**. It adds enemy waves and player abilities/perks using the game's existing systems. The build is under active development; several ability effects and multiplayer edge cases still need in-game verification.

## Current prototype

- Start the mode from a local match with **F8** or the in-game **START PvE** control.
- Supports one to four local players in one game process.
- Uses the game's existing maps, wave handler, weapons and stickman AI.
- Enemies spawn in waves, have 30 HP, and drop no items or currency.
- On team defeat, the run ends and returns players to the lobby.
- Includes a color picker, first-pass color abilities, wave perks, and a developer console (**F1**).

This is not a finished or balanced release. In particular, ability visuals/behavior, enemy navigation, combat damage, and co-op edge cases may be incomplete or broken. See [MODDING_PLAN.md](MODDING_PLAN.md) for intended scope.

## Latest source update: 0.8.96

- Red Rage and Fast Hands now scale the game's actual punch recovery subtraction, instead of multiplying a timer value that may already be zero.
- Rooted enemies skip both the game's AI update and the mod's post-update attack/jump logic; their dynamic body parts are held still for the effect duration.
- Ability positions and movement impulses prefer the live torso rigidbody, with the game's main rig as a fallback.
- The purple shot searches for the stock BEAM trail or line material, passes through enemies and moving props, and stops at static arena geometry.

The 0.8.96 source compiles against the game's .NET 3.5-era references, BepInEx and Unity assemblies. This update has **not** been launched for a live gameplay check yet; BEAM prefab matching and movement/ability behavior still need in-game verification.

## Requirements

- Stick Fight: The Game (Steam, Windows, 64-bit)
- BepInEx 5 for Unity Mono x64
- .NET Framework targeting pack/toolchain compatible with the game's Unity-era assemblies

## Build

Pass the directory containing `BepInEx` and `StickFight_Data` as `GameDir`:

```powershell
msbuild FortDefense.csproj /p:GameDir="C:\Path\To\StickFightTheGame"
```

The output is `bin/FortDefense.dll`. The project references the game's local managed assemblies and BepInEx; those files are not included here.

## Install

Copy `FortDefense.dll` to `BepInEx/plugins/` in the game installation. To uninstall, remove that DLL. Back up the folder before modifying a game installation.

## Testing and compatibility

Designed for local co-op in one process. Online multiplayer compatibility is not claimed. Test on a local match and check the BepInEx log if startup fails. The source has been build-checked, but this public snapshot has not had a complete live regression pass; consult the project issues before relying on any specific feature.

## Credits

- Stick Fight: The Game and its authors; BepInEx and its contributors.
- Gameplay inspiration: Stick It to the Stickman. This project does not include that game's files or assets.
- Mod code and documentation prepared with AI assistance and reviewed against the source project.

