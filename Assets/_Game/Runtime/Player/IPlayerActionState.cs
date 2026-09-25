using UnityEngine;

namespace ActionPlatformer.Player
{
    public enum GlitchDirection { Left, Right, Up, Down }
    public enum PlayerAttack { Side, Lift, Slam, Emergence, Shockwave }
    public enum PlayerAttackPhase { Ready, Windup, Active, Recovery, Descending }

    public enum PlayerReaction { None, Hit, Die }

    public struct PlayerReactionPresentation
    {
        public PlayerReaction Kind;
        public uint Version;
        public float Progress;
    }

    // Presentation facts only. Animation never advances combat or applies damage.
    public struct PlayerAttackPresentation
    {
        public PlayerAttack Kind;
        public int Strike;
        public uint Version;
        public bool Airborne;
        public float PhaseProgress;
    }

    public struct PlayerShockwave
    {
        public bool Visible;
        public Vector2 Center;
        public float Radius;
        public float Progress;
    }

    public struct GlitchPreview
    {
        public bool Visible;
        public bool CanExecute;
        public bool IsUnderground;
        public Vector2 Destination;
        public Bounds Bounds;
    }

    public interface IPlayerActionState
    {
        GlitchPreview GlitchPreview { get; }
        bool IsUnderground { get; }
        bool CanEmerge { get; }
        Vector2 UndergroundPosition { get; }
        Vector2 GroundMarkerPosition { get; }
        float Facing { get; }
        PlayerAttackPhase AttackPhase { get; }
        PlayerAttackPresentation AttackPresentation { get; }
        PlayerReactionPresentation ReactionPresentation { get; }
        Bounds AttackBounds { get; }
        PlayerShockwave Shockwave { get; }
    }
}
