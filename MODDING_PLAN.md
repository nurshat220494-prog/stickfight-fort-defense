# Stick Fight: Fort Defense modding plan

- Host: `Stick Fight: The Game`, Steam AppID 674940, `D:\SteamLibrary\steamapps\common\StickFightTheGame`, Unity 5.6.7f1, Mono x64. Scan found no anti-cheat and no loader.
- Reference: `Stick It to the Stickman`, Steam AppID 2085540, `D:\SteamLibrary\steamapps\common\Stick It to the Stickman`, Unity 2022.3.4f1, Mono x64. Scan found no anti-cheat and no loader.
- User goal: local co-op PvE mode where players defend a fort against waves.
- Route: transplant selected gameplay ideas into a BepInEx 5 plugin for Stick Fight. The game installs remain at their Steam paths; this project contains only original plugin code and notes, not game binaries, assets, or decompiled code.
- Host implementation: Stick Fight already contains `HoardHandler` wave and AI systems. Reuse the configured handler and enemy prefab; add a simple objective with health and a HUD. The reference game's executable/assets are not needed in the runtime loop.
- Prototype input: press F8 during a local match. Later, replace this temporary trigger with a proper mode/map selection if the prototype works.
- Multiplayer: local co-op uses one game process and one shared simulation, so no network replication is needed for this target. Online play is out of scope for this prototype.
- Loader: BepInEx 5 x64 is the likely route. Current official release observed: 5.4.23.5. It is not installed. Do not modify the game folder until the user approves loader installation.
- First proof: on a map with a configured `HoardHandler`, start one local match, activate waves with F8, confirm enemies spawn, and check that the fort loses health when an attacking enemy reaches it. Then repeat with multiple local players.
- Known limitation: no map has yet been inspected in a live run. The current prototype may report that its map lacks a configured wave handler or enemy prefab. Fort position, damage radius, enemy wave setup, and failure behavior need in-game verification.
