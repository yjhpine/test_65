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
- All side hits call GroundMovement2D.ApplyKnockback: non-finishers (default strikes 1/2, including basic attacks with combo disabled) use LightKnockbackSpeed=3 / LightKnockbackDeceleration=30; finishers (default strike 3) use KnockbackSpeed=12 / KnockbackDeceleration=18 for longer travel. Set horizontal speed once, then decay the actual Rigidbody velocity, preserving vertical physics. A wall sweep stops horizontal motion permanently for that kick. ForcedDuration is a minimum action lock; IsForcedMoving also remains true while residual knockback settles, preventing AI/Stop overwrite. New hits replace the kick; death/disable clear it. Existing ApplyForcedMovement remains the vertical launch/slam path.
- PlayerCombat owns a one-slot late-Recovery attack buffer (0.1s) and emits at most one confirmed-damage impact per swing (slam descent/landing are separate stages). PlayerUnit consumes the buffer after combat ticks and forwards impact facts to plain C# PlayerImpactFeedback. Feedback uses unscaled deadlines to restore the prior Time.timeScale after hit stop, plus decaying directional offsets on the assigned aim camera. Offsets are removed before aim sampling and applied in LateUpdate; reset/hit/death/disable restore time and camera. No effects on misses/invulnerable targets, no multi-victim duration/amplitude sum. Existing singleton-free single-player fixed camera setup is preserved.
- An above-target glitch arrival enables one airborne slam until landing, another arrival or interruption. PlayerCombat owns this follow-up state; PlayerUnit supplies arrival/grounding facts. Ordinary air attacks use the basic forward attack and retain gravity. PlayerUnit gates locomotion on PlayerCombat.IsAttacking: horizontal velocity stops immediately and new jumps/buffers are blocked from windup through recovery; emergence launch and slam motion remain active. Held movement resumes on Ready, including damage cancellation. CharacterMotor2D sweeps a slam downward to the first floor, and PlayerCombat applies one terrain-occluded radial hit on landing. PlayerVisual draws the expanding shockwave. Physics2D layer 2 ignores itself so units pass through each other; terrain collision and explicit hit queries remain enabled.
- Slam has an independent 0.5-second hover windup: PlayerCombat owns its timer, and CharacterMotor2D zeroes velocity while PlayerUnit skips normal movement. Body type and damage eligibility stay unchanged; interruption/disable/death cancels the hover. Ground attack windup remains 0.12 seconds.
- During slam descent, the forward Reach/Width box damages enemies and GroundHitReaction launches them downward at Slam Knockdown Speed (36). Landing shockwave and forward hits share the per-attack hit set. Shockwave hit metadata uses PlayerAttack.Shockwave so the damage-only landing reaction stays distinct from Slam knockdown. Hover has no hitbox.
- Glitch and combat tuning are independently optional. PlayerCombat owns combo state, attack timing and damage; it does not reference PlayerGlitch or GlitchUtility. Stateless IPlayerComboRule, IPlayerAttackSelector and IPlayerHitReaction are constructor-injected by PlayerUnit, with single-hit/side-attack/no-reaction fallbacks. Inspector link switches apply at initialization. Shared collider and sight queries live in UnitPhysics2D.
- PlayerVisual reads motor velocity/grounding to set Animator bools Moving/Grounded/Rising and SpriteRenderer.flipX. It never writes physics state.
- PlayerTuning is definition-only ScriptableObject data.
- PlayerUnit exposes Hit/Die reaction presentation through IPlayerActionState. Hit uses the existing combat HitStun duration and restarts on damage, preserving walking; Die cancels actions, restores burrow physics, blocks input, retains gravity and deactivates after PlayerUnit.DeathDuration (0.7 seconds, zero supported). Player prefab UnitHealth.DeactivateOnDeath is false. Only PlayerVisual plays Hit (3 hurt frames)/Die (7 die frames) with higher priority than attacks. Re-enabling a finished dead player does not revive or restart it.
- Blocked emergence now searches both directions on the same floor only on the attack request. GlitchUtility.TryNearestEmergence samples at 0.05 units and refines the first free boundary with 6 binary steps; narrow free intervals smaller than the sample spacing may be missed. The nearer refined candidate wins; ties prefer the last actual burrow movement, otherwise the original body's side. CanEmerge/marker color still describe the exact selected column. No valid nearby space or invalid target/floor restores the parked body. Successful correction updates the logical marker and uses the existing emergence/combat flow without restarting cooldown.
- Overlapping bodies use the basic side attack; positional Lift requires target bottom >= player top minus 0.02 contact tolerance. Emergence uses the current burrow marker's body-center X and a centered hit area, never current target-side candidates. A/D and arrow Move input adjusts this logical marker in FixedUpdate via PlayerGlitch.MoveUnderground at GlitchTuning.UndergroundMoveSpeed (3 units/s), clamped inside the same floor with full-body edge clearance. The real body stays parked/Kinematic/immune. CanEmerge uses the execution checks and PlayerVisual colors the marker cyan/red. Space takes priority over movement/attack, moving does not extend duration or cooldown, and emergence restores the parked body only if the nearby fallback also fails. Normal target/floor validity checks still apply.
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
