using UnityEngine;

namespace ActionPlatformer.Player
{
    public enum GlitchDirection { Left, Right, Up, Down }
    public enum PlayerAttack { Side, Lift, Slam, Emergence, Shockwave, Air }
    public enum PlayerAttackPhase { Ready, Windup, Active, Recovery, Descending }

    public enum PlayerReaction { None, Hit, Die }

    public struct PlayerReactionPresentation
    {
        public PlayerReaction Kind;
        public uint Version;
        public float Progress;
        public bool HasDirection;
        public Vector2 Direction;
        // Enemy hits are ignored for a while after a hit; the sprite blinks meanwhile. Set even when Kind is None.
        public bool Invulnerable;
    }

    // Presentation facts only. Animation never advances combat or applies damage.
    public struct PlayerAttackPresentation
    {
        public PlayerAttack Kind;
        public int Strike;
        // The combo's strong strike, whatever attack kind carries it.
        public bool Finisher;
        public uint Version;
        public double StartedAt;
        public bool Airborne;
        public float PhaseProgress;
    }

    public struct PlayerShockwave
    {
        public uint Version;
        public bool Visible;
        public Vector2 Center;
        public float Radius;
        public float Progress;
    }

    public struct PlayerGlitchPresentation
    {
        public uint Version;
        public Vector2 From;
        public Vector2 To;
    }

    // Why an aimed glitch cannot run now. None means the preview is usable.
    public enum GlitchBlockReason { None, OutOfRange, OffScreen, GateClosed, NoSpace, Cooldown, Busy }

    public struct GlitchPreview
    {
        public bool Visible;
        public bool CanExecute;
        public GlitchBlockReason Reason;
        public bool IsUnderground;
        // Usable, but the start or the arrival is inside an active domain: carrying it out kills the player.
        public bool Lethal;
        public Vector2 Destination;
        public Bounds Bounds;
    }

    public interface IPlayerActionState
    {
        GlitchPreview GlitchPreview { get; }
        // After a glitch the player stays locked on to its target until it dies or gets too far away (world-space body bounds).
        bool HasLockOn => false;
        Bounds LockOnBounds => default;
        // Maximum glitch distance for the optional range display; zero without glitch tuning.
        float GlitchMaxDistance { get; }
        bool IsUnderground { get; }
        bool CanEmerge { get; }
        Vector2 UndergroundPosition { get; }
        Vector2 GroundMarkerPosition { get; }
        float Facing { get; }
        PlayerAttackPhase AttackPhase { get; }
        PlayerAttackPresentation AttackPresentation { get; }
        PlayerReactionPresentation ReactionPresentation { get; }
        PlayerGlitchPresentation GlitchPresentation { get; }
        Bounds AttackBounds { get; }
        PlayerShockwave Shockwave { get; }
    }
}
