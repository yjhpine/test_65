using UnityEngine;
using UnityEngine.InputSystem;

namespace ActionPlatformer.Player
{
    public sealed class PlayerInputReader : IPlayerInputSource
    {
        private readonly PlayerInput playerInput;
        private InputActionAsset cachedActions;
        private InputActionMap playerMap;
        private InputAction move, jump;
        private bool invalidConfiguration;

        public PlayerInputReader(PlayerInput playerInput)
        {
            if (playerInput == null) throw new System.ArgumentNullException(nameof(playerInput));
            this.playerInput = playerInput;
        }

        private bool ResolveActions()
        {
            // PlayerInput owns initialization, device pairing and the action asset lifetime.
            var actions = playerInput.actions;
            if (actions != null && actions == cachedActions) return true;
            playerMap = actions?.FindActionMap("Player");
            move = playerMap?.FindAction("Move");
            jump = playerMap?.FindAction("Jump");
            if (move == null || jump == null)
            {
                Debug.LogError("PlayerInput needs an action asset with Player/Move and Player/Jump.", playerInput);
                invalidConfiguration = true;
                return false;
            }
            cachedActions = actions;
            return true;
        }

        public PlayerCommand Sample()
        {
            if (invalidConfiguration || playerInput == null || !playerInput.isActiveAndEnabled || !playerInput.inputIsActive)
                return default;
            if (!ResolveActions() || !playerMap.enabled) return default;
            return new PlayerCommand
            {
                Move = move.enabled ? move.ReadValue<Vector2>() : Vector2.zero,
                JumpPressed = jump.enabled && jump.WasPressedThisFrame(),
                JumpHeld = jump.enabled && jump.IsPressed(),
                JumpReleased = jump.enabled && jump.WasReleasedThisFrame()
            };
        }
    }
}
