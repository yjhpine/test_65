using System.Collections;
using ActionPlatformer.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ActionPlatformer.Tests
{
    public sealed class PlayerCourseTests
    {
        private PlayerController player;
        private Driver input;
        private Keyboard testKeyboard;
        private Gamepad testGamepad;
        private static readonly WaitForFixedUpdate FixedStep = new WaitForFixedUpdate();

        private sealed class Driver : IPlayerInputSource
        {
            public PlayerCommand Frame;
            public PlayerCommand Sample()
            {
                var command = Frame;
                Frame.JumpPressed = Frame.JumpReleased = false;
                return command;
            }
        }

        [UnitySetUp] public IEnumerator LoadLab()
        {
            yield return SceneManager.LoadSceneAsync("MovementLab");
            yield return null;
            player = Object.FindFirstObjectByType<PlayerController>();
            input = new Driver();
            player.SetInputSource(input);
            yield return Steps(12);
            Assert.That(player.Motor.IsGrounded, Is.True);
        }

        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (testKeyboard != null) InputSystem.RemoveDevice(testKeyboard);
            if (testGamepad != null) InputSystem.RemoveDevice(testGamepad);
            testKeyboard = null; testGamepad = null;
            yield return null;
        }

        private static IEnumerator Steps(int count) { for (int i = 0; i < count; i++) yield return FixedStep; }

        private static IEnumerator WaitForVisualState(Animator animator, string state)
        {
            float deadline = Time.realtimeSinceStartup + 2f;
            while (!animator.GetCurrentAnimatorStateInfo(0).IsName(state) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName(state), Is.True, "Expected player animation: " + state);
        }

        [UnityTest] public IEnumerator VisualFollowsMovementAndJumpWithoutMovingPhysicsRoot()
        {
            var visual = player.transform.Find("Visual");
            var animator = visual.GetComponent<Animator>();
            var renderer = visual.GetComponent<SpriteRenderer>();
            Vector3 visualPosition = visual.localPosition;
            Vector2 idlePosition = player.Motor.Position;
            yield return WaitForVisualState(animator, "Idle");
            Sprite first = renderer.sprite;
            bool advanced = false;
            for (int i = 0; i < 12; i++)
            {
                yield return FixedStep;
                advanced |= renderer.sprite != first;
            }
            Assert.That(advanced, Is.True, "Idle animation must advance sprite frames.");
            Assert.That(Vector2.Distance(player.Motor.Position, idlePosition), Is.LessThan(0.01f));

            input.Frame.Move = Vector2.right; yield return Steps(10);
            yield return WaitForVisualState(animator, "Run");
            Assert.That(renderer.flipX, Is.False);
            input.Frame.Move = Vector2.left; yield return Steps(15);
            Assert.That(renderer.flipX, Is.True);
            input.Frame.Move = Vector2.zero; yield return Steps(10);
            yield return WaitForVisualState(animator, "Idle");
            Assert.That(renderer.flipX, Is.True, "Stopping should preserve the facing direction.");

            input.Frame.JumpPressed = true; input.Frame.JumpHeld = true;
            yield return Steps(3);
            yield return WaitForVisualState(animator, "Jump");
            for (int i = 0; i < 60 && player.Motor.Velocity.y >= 0f; i++) yield return FixedStep;
            yield return WaitForVisualState(animator, "Fall");
            yield return Steps(40);
            yield return WaitForVisualState(animator, "Idle");
            Assert.That(visual.localPosition, Is.EqualTo(visualPosition));
            Assert.That(player.transform.localScale, Is.EqualTo(Vector3.one));
            Assert.That(animator.applyRootMotion, Is.False);
        }

        [UnityTest] public IEnumerator RunningReversesAndStopsResponsively()
        {
            input.Frame.Move = Vector2.right; yield return Steps(15);
            Assert.That(player.Motor.Velocity.x, Is.GreaterThan(9f));
            input.Frame.Move = Vector2.left; yield return Steps(15);
            Assert.That(player.Motor.Velocity.x, Is.LessThan(-9f));
            input.Frame.Move = Vector2.zero; yield return Steps(10);
            Assert.That(Mathf.Abs(player.Motor.Velocity.x), Is.LessThan(0.1f));
        }

        [UnityTest] public IEnumerator HoldJumpRisesHigherThanTapJump()
        {
            float floorY = player.Motor.Position.y;
            input.Frame.JumpPressed = true; input.Frame.JumpHeld = true;
            float high = floorY;
            for (int i = 0; i < 45; i++) { yield return FixedStep; high = Mathf.Max(high, player.Motor.Position.y); }
            input.Frame = default; player.Motor.Teleport(new Vector2(-10f, 1f)); yield return Steps(12);
            input.Frame.JumpPressed = true; input.Frame.JumpHeld = true; yield return Steps(3);
            input.Frame.JumpHeld = false; input.Frame.JumpReleased = true;
            float low = player.Motor.Position.y;
            for (int i = 0; i < 35; i++) { yield return FixedStep; low = Mathf.Max(low, player.Motor.Position.y); }
            Assert.That(high - floorY, Is.InRange(2.7f, 3.5f));
            Assert.That(high - low, Is.GreaterThan(1f));
        }

        [UnityTest] public IEnumerator JumpPressedJustBeforeLandingIsBuffered()
        {
            player.Motor.Teleport(new Vector2(-10f, 2.5f));
            for (int i = 0; i < 40 && player.Motor.Position.y > 1.35f; i++) yield return FixedStep;
            input.Frame.JumpPressed = true; input.Frame.JumpHeld = true;
            yield return Steps(10);
            Assert.That(player.Motor.Position.y, Is.GreaterThan(1.4f));
            Assert.That(player.Motor.Velocity.y, Is.GreaterThan(0f));
        }

        private IEnumerator WalkOffMiddleLedge()
        {
            player.Motor.Teleport(new Vector2(3f, 4.1f)); yield return Steps(15);
            Assert.That(player.Motor.IsGrounded, Is.True);
            input.Frame.Move = Vector2.right;
            for (int i = 0; i < 60; i++)
            {
                yield return FixedStep;
                if (!player.Motor.IsGrounded) break;
            }
            Assert.That(player.Motor.IsGrounded, Is.False);
            input.Frame.Move = Vector2.zero;
        }

        [UnityTest] public IEnumerator CoyoteTimeAllowsJumpJustAfterLeavingLedge()
        {
            yield return WalkOffMiddleLedge();
            input.Frame.JumpPressed = true; input.Frame.JumpHeld = true;
            yield return Steps(3);
            Assert.That(player.Motor.Velocity.y, Is.GreaterThan(10f));
        }

        [UnityTest] public IEnumerator CoyoteTimeExpiresBeforeALateAirbornePress()
        {
            yield return WalkOffMiddleLedge();
            yield return Steps(9);
            input.Frame.JumpPressed = true; input.Frame.JumpHeld = true;
            yield return Steps(2);
            Assert.That(player.Motor.IsGrounded, Is.False);
            Assert.That(player.Motor.Velocity.y, Is.LessThan(0f));
        }

        [UnityTest] public IEnumerator AnotherPressDoesNotAddAnAirJump()
        {
            input.Frame.JumpPressed = true; input.Frame.JumpHeld = true;
            yield return Steps(10);
            float velocityBefore = player.Motor.Velocity.y;
            input.Frame.JumpPressed = true;
            yield return Steps(3);
            Assert.That(player.Motor.Velocity.y, Is.LessThan(velocityBefore));
        }

        [UnityTest] public IEnumerator KeyboardAndGamepadBindingsProduceMovementAndJump()
        {
#if UNITY_EDITOR
            var previousInputBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            var previousBackgroundBehavior = InputSystem.settings.backgroundBehavior;
            // Synthetic input must not depend on which Editor window currently has focus.
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode =
                InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            try
            {
                var reader = player.GetComponent<PlayerInputReader>();
                testKeyboard = InputSystem.AddDevice<Keyboard>();
                InputSystem.QueueStateEvent(testKeyboard, new KeyboardState(Key.D, Key.Space));
                InputSystem.Update();
                PlayerCommand keyboard = reader.Sample();
                Assert.That(keyboard.Move.x, Is.GreaterThan(0.9f));
                Assert.That(keyboard.JumpPressed, Is.True);
                InputSystem.QueueStateEvent(testKeyboard, new KeyboardState()); InputSystem.Update();
                testGamepad = InputSystem.AddDevice<Gamepad>();
                InputSystem.QueueStateEvent(testGamepad, new GamepadState { leftStick = Vector2.right }.WithButton(GamepadButton.South));
                InputSystem.Update();
                PlayerCommand gamepad = reader.Sample();
                Assert.That(gamepad.Move.x, Is.GreaterThan(0.9f));
                Assert.That(gamepad.JumpPressed, Is.True);
                yield return null;
            }
            finally
            {
#if UNITY_EDITOR
                InputSystem.settings.editorInputBehaviorInPlayMode = previousInputBehavior;
                InputSystem.settings.backgroundBehavior = previousBackgroundBehavior;
#endif
            }
        }
    }
}
