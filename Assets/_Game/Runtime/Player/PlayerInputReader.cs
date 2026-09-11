using UnityEngine;
using UnityEngine.InputSystem;

namespace ActionPlatformer.Player
{
    public sealed class PlayerInputReader : MonoBehaviour, IPlayerInputSource
    {
        [SerializeField] private InputActionAsset actions;
        private InputActionAsset instance;
        private InputActionMap playerMap;
        private InputAction move, jump;

        private void Awake()
        {
            if (actions == null) { Debug.LogError("Player input action asset is missing.", this); enabled = false; return; }
            // Each player owns enabled state; the project action asset remains authoring data.
            instance = Instantiate(actions);
            playerMap = instance.FindActionMap("Player", true);
            move = playerMap.FindAction("Move", true);
            jump = playerMap.FindAction("Jump", true);
        }

        private void OnEnable() => playerMap?.Enable();
        private void OnDisable() => playerMap?.Disable();
        private void OnDestroy() { if (instance != null) Destroy(instance); }

        public PlayerCommand Sample()
        {
            if (!isActiveAndEnabled || playerMap == null) return default;
            return new PlayerCommand
            {
                Move = move.ReadValue<Vector2>(),
                JumpPressed = jump.WasPressedThisFrame(),
                JumpHeld = jump.IsPressed(),
                JumpReleased = jump.WasReleasedThisFrame()
            };
        }
    }
}
