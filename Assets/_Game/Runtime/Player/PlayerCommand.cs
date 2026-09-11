using UnityEngine;

namespace ActionPlatformer.Player
{
    public struct PlayerCommand
    {
        public Vector2 Move;
        public bool JumpPressed;
        public bool JumpHeld;
        public bool JumpReleased;
    }

    public interface IPlayerInputSource
    {
        PlayerCommand Sample();
    }
}
