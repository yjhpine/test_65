using UnityEngine;

namespace ActionPlatformer.Player
{
    public struct PlayerCommand
    {
        public Vector2 Move;
        public bool JumpPressed;
        public bool JumpHeld;
        public bool JumpReleased;
        public Vector2 AimScreenPosition;
        public bool HasAim;
        public bool GlitchPressed;
        public bool AttackPressed;
    }

    public interface IPlayerInputSource
    {
        PlayerCommand Sample();
    }
}
