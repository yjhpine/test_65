# Unity project context

Updated: 2026-09-11. Baseline commit: 696d498.

## Environment
- Project: `D:/test_65/test_65/test_65`.
- Unity 6000.3.23f1, URP 17.3.0 using the 2D template renderer.
- Input System 1.20.0; Unity Test Framework 1.6.0.
- Official Unity MCP can execute commands and read Console.
- SampleScene and the Lit 2D scene template were removed during the requested cleanup.

## Current user scope
Keep ONLY horizontal acceleration/braking, variable jump, coyote time and jump input buffering.
The user requested removal of all other prototype features and building/environment artwork.
Do not reintroduce dash, combat, wire/grapple, checkpoints, respawn, pause, HUD, sounds or presentation systems without a new request.
The lab uses plain rectangles for the floor, boundary walls and three ledges. Camera is static.
The user requested a pixel-art player asset. Player.prefab owns physics/input/movement on the root, with one direct Visual child holding SpriteRenderer, Animator and PlayerVisual. Pixel Frog's Virtual Guy (Pixel Adventure, CC0) uses Idle/Run/Jump/Fall states. Root motion is disabled. Edit the existing prefab; the initial lab and player-art generation tools have been removed.

## Architecture
All first-party code/assets live in `Assets/_Game`, namespace `ActionPlatformer`.
Runtime / Editor / EditModeTests / PlayModeTests are separate assemblies.
Runtime depends only on Input System.
- PlayerInputReader clones the Move/Jump action asset per player.
- Keep both input action assets: the player uses _Game/Data/PrototypeControls.inputactions; Settings/InputSystem_Actions.inputactions remains registered as the project-wide asset at the user's request.
- PlayerController owns jump buffering and coyote time.
- CharacterMotor2D owns all player Rigidbody2D velocity/position writes.
- PlayerVisual reads motor velocity/grounding to set Animator bools Moving/Grounded/Rising and SpriteRenderer.flipX. It never writes physics state.
- PlayerTuning is definition-only ScriptableObject data.
- No session manager, combat, camera controller, UI, global event bus or singleton.
- Art: Assets/_Game/Art/Characters/VirtualGuy; 32x32 frames, 16 PPU, bottom-center pivot, Point filter, uncompressed. Visual local position (0,-0.8,0), unit scale, white tint. Physics collider remains 0.65x1.6.
- Animator: Assets/_Game/Animations/Player/Player.controller; Idle 11 frames and Run 12 frames at 20 FPS, Jump/Fall one pose each.
- Provenance and redistribution license: Docs/ThirdPartyNotices.md.

## Entry points
- Scene: `Assets/_Game/Scenes/MovementLab.unity`.
- Player prefab: `Assets/_Game/Prefabs/Player.prefab`.
- Open the existing MovementLab scene directly from the Project window.
- Tests: `Game > Prototype > Validate EditMode / Validate PlayMode`.
- Guide: `Docs/PrototypeGuide.md`.
- Validation: `Docs/PrototypeValidation.md`.
- Generated test results: `Library/PrototypeValidation/` (ignored by Git).

## Settings retained from initial implementation
MovementLab is the only build scene and is also the templateDefaultScene path in Player Settings.
The running Unity Editor registered an App UI config object, added APP_UI_EDITOR_ONLY to Standalone defines and generated SceneTemplateSettings.json. Those existing editor-generated settings were retained.
Cleanup removed six unused direct packages: com.unity.visualscripting, com.unity.timeline, com.unity.multiplayer.center, com.unity.2d.aseprite, com.unity.2d.psdimporter and com.unity.2d.spriteshape. Input System, URP, Sprite, Unity MCP and test dependencies are retained. Both input action assets and the fixed timestep are unchanged.
Use local files for inspection. Use Unity MCP only for necessary scene changes, compilation, console and runtime validation.

## Unknowns
Final art direction, combat design, release platforms, save requirements, localization and performance budget.
