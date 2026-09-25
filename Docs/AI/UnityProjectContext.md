# Unity project context

Updated: 2026-09-25. Implementation baseline commit: 8707898.

## Environment
- Project: `D:/test_65/test_65/test_65`.
- Unity 6000.3.23f1, URP 17.3.0 using the 2D template renderer.
- Input System 1.20.0; Unity Test Framework 1.6.0.
- Official Unity MCP can execute commands and read Console.
- SampleScene and the Lit 2D scene template were removed during the requested cleanup.

## Current user scope
Keep horizontal acceleration/braking, variable jump, coyote time and jump input buffering. Units also support optional FSMs and enemy combat.
The current request adds four-direction glitch relocation, logical underground/emergence, player attacks and cross-target combos, with simple diagnostic visuals.
Do not reintroduce dash, wire/grapple, checkpoints, respawn, pause, HUD, sounds or building artwork without a new request.
The lab uses plain rectangles for the floor, boundary walls and three ledges. Camera is static.
The player now uses rvros's Animated Pixel Adventurer with movement and combat animations. Player.prefab owns physics/input/movement on the root, with one direct Visual child holding SpriteRenderer, Animator and PlayerVisual. Root motion is disabled. Edit the existing prefab; temporary art authoring tools are not part of Assets.

## Architecture
All first-party code/assets live in `Assets/_Game`, namespace `ActionPlatformer`.
Runtime / Editor / EditModeTests / PlayModeTests are separate assemblies.
Runtime depends only on Input System.
- PlayerInput owns action lifetime; PlayerInputReader reads Move/Jump/Aim/Glitch/Attack without cloning or enabling actions.
- Keep both input action assets: the player uses _Game/Data/PrototypeControls.inputactions; Settings/InputSystem_Actions.inputactions remains registered as the project-wide asset at the user's request.
- PlayerUnit : Unit owns jump buffering, coyote time and the plain C# PlayerGlitch/PlayerCombat objects. GlitchUtility contains geometry/targeting functions. Unit and FsmRuntime have no player physics responsibilities.
- CharacterMotor2D owns all player Rigidbody2D velocity/position writes.
- An above-target glitch arrival enables one airborne slam until landing, another arrival or interruption. PlayerCombat owns this follow-up state; PlayerUnit supplies arrival/grounding facts. Ordinary air attacks use the basic forward attack and retain normal movement/gravity. CharacterMotor2D sweeps a slam downward to the first floor, and PlayerCombat applies one terrain-occluded radial hit on landing. PlayerVisual draws the expanding shockwave. Physics2D layer 2 ignores itself so units pass through each other; terrain collision and explicit hit queries remain enabled.
- Slam has an independent 0.5-second hover windup: PlayerCombat owns its timer, and CharacterMotor2D zeroes velocity while PlayerUnit skips normal movement. Body type and damage eligibility stay unchanged; interruption/disable/death cancels the hover. Ground attack windup remains 0.12 seconds.
- During slam descent, the forward Reach/Width box damages enemies and GroundHitReaction launches them downward at Slam Knockdown Speed (36). Landing shockwave and forward hits share the per-attack hit set. Shockwave hit metadata uses PlayerAttack.Shockwave so the damage-only landing reaction stays distinct from Slam knockdown. Hover has no hitbox.
- Glitch and combat tuning are independently optional. PlayerCombat owns combo state, attack timing and damage; it does not reference PlayerGlitch or GlitchUtility. Stateless IPlayerComboRule, IPlayerAttackSelector and IPlayerHitReaction are constructor-injected by PlayerUnit, with single-hit/side-attack/no-reaction fallbacks. Inspector link switches apply at initialization. Shared collider and sight queries live in UnitPhysics2D.
- PlayerVisual reads motor velocity/grounding to set Animator bools Moving/Grounded/Rising and SpriteRenderer.flipX. It never writes physics state.
- PlayerTuning is definition-only ScriptableObject data.
- Overlapping bodies use the basic side attack; positional Lift requires target bottom >= player top minus 0.02 contact tolerance. Emergence uses the fixed burrow marker's body-center X and a centered hit area, never current target-side candidates. A blocked column cancels and restores the parked body; normal target/floor validity checks still apply.
- No session manager, camera controller, UI, global event bus or singleton. Combat uses UnitHealth and GroundMovement2D forced motion, with explicit invulnerability during underground.
- Art: Assets/_Game/Art/Characters/Adventurer (rvros, creator custom license; no standalone redistribution); 50x37 frames, 18 PPU, pivot (25/50,1/37), Point filter, uncompressed. Visual local position (0,-0.8,0), unit scale, white tint. Physics collider remains 0.65x1.6. Previous VirtualGuy art is retained unused.
- Animator: Assets/_Game/Animations/Player/Player.controller; Idle 4 frames, Run 6, Jump 4, Fall 2. Ground combo Attack1/2/3, ordinary air AirAttack1/2/3 (third reuses air1), Lift/Emergence upward swing, SlamHover/SlamFall/SlamLand use original air-attack3 ready/loop/end. PlayerCombat exposes read-only PlayerAttackPresentation via PlayerUnit/IPlayerActionState; only PlayerVisual writes ActionOverride/ActionTime and plays states. No animation events or root motion. Show Attack Area defaults off on the prefab and can be enabled for debugging.
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
Release platforms, save requirements, localization and performance budget. Initial action settings and supported underground terrain are documented in Docs/PrototypeGuide.md; moving/sloped/layered underground terrain is excluded.
