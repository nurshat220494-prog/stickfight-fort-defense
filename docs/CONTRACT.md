# Fort Defense local co-op contract (v1)

## Scope
- The target is local co-op in one Stick Fight process. All local players share the same Unity scene and simulation.
- The Stick Fight game process owns all wave, enemy, and fort state. No external process, relay, or network protocol is needed for this scope.
- Online Steam lobbies are out of scope for this first version.

## Gameplay state
- Players use Stick Fight's native movement, weapons, ragdoll physics, and damage.
- Stick Fight's existing `HoardHandler` owns wave progression and creates its configured AI enemy prefab.
- The Fort Defense plugin owns only mode activation, the fort objective's health/failure state, and the shared HUD.
- The source game is used as a design reference. No executable code or extracted assets are loaded at runtime.

## First proof
1. A selected map has a configured `HoardHandler` and enemy prefab.
2. F8 activates waves once a local match is running.
3. Native AI enemies appear and attack nearby players.
4. The fort is visible, AI attacks near it reduce its health, and all local players see the same HUD.
5. When fort health reaches zero, the mode stops starting new waves and reports defeat.

## Open questions
- Which map contains a configured `HoardHandler` with an enemy prefab.
- Whether that handler's enemy prefab and spawn range work in local co-op.
- Whether enemy attacks can be counted reliably near a separate fort object.
- Whether mode activation belongs in a map selection or a start button after the prototype is proven.
