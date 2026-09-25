using System.Collections.Generic;
using ActionPlatformer.Units;
using ActionPlatformer.Units.Features;
using UnityEngine;

namespace ActionPlatformer.Player
{
    public enum PlayerImpactStrength { Normal, Heavy, Slam }

    public readonly struct PlayerImpact
    {
        public readonly Vector2 Direction;
        public readonly PlayerImpactStrength Strength;
        public PlayerImpact(Vector2 direction, PlayerImpactStrength strength)
        { Direction = direction; Strength = strength; }
    }

    public sealed class PlayerCombat
    {
        private readonly Unit owner;
        private readonly Collider2D body;
        private readonly PlayerCombatTuning tuning;
        private readonly IPlayerComboRule comboRule;
        private readonly IPlayerAttackSelector attackSelector;
        private readonly IPlayerHitReaction hitReaction;
        private readonly List<Collider2D> candidates = new List<Collider2D>(32);
        private readonly List<RaycastHit2D> obstacles = new List<RaycastHit2D>(8);
        private readonly HashSet<UnitHealth> hit = new HashSet<UnitHealth>();
        private double phaseEndsAt;
        private double phaseStartedAt;
        private uint attackVersion;
        private bool airborneAttack;
        private double comboExpiresAt;
        private double stunnedUntil;
        private int nextStrike = 1;
        private UnitHealth pendingComboTarget, previousComboTarget;
        private PlayerComboStep combo;
        private PlayerAttackSelection selection;
        private Vector2 shockwaveCenter;
        private double shockwaveStartedAt = double.NegativeInfinity;
        private bool slamReady;
        private bool bufferedAttack;
        private bool hasImpact;
        private int emittedImpactStages;
        private PlayerImpact pendingImpact;
        public bool HasBufferedAttack => bufferedAttack;
        public int Strike => combo.Strike;
        public PlayerAttack Attack => selection.Kind;
        public PlayerAttackPhase Phase { get; private set; }
        public float Facing => selection.Facing == 0f ? 1f : selection.Facing;
        public float LaunchSpeed => selection.LaunchSpeed;
        public float SlamSpeed => tuning.SlamSpeed;
        public bool IsDescending => Phase == PlayerAttackPhase.Descending;
        public bool IsPreparingSlam => Attack == PlayerAttack.Slam && Phase == PlayerAttackPhase.Windup;
        public bool IsAttacking => Phase != PlayerAttackPhase.Ready;
        public bool CanReposition(double now) => now >= stunnedUntil &&
            (Phase == PlayerAttackPhase.Ready || Phase == PlayerAttackPhase.Recovery);
        public bool CanAttack(double now) => now >= stunnedUntil && Phase == PlayerAttackPhase.Ready;

        public bool BufferAttack(double pressedAt)
        {
            if (Phase != PlayerAttackPhase.Recovery || tuning.AttackBufferTime <= 0f ||
                pressedAt < phaseEndsAt - tuning.AttackBufferTime || pressedAt < phaseStartedAt) return false;
            bufferedAttack = true;
            return true;
        }

        public bool ConsumeBufferedAttack(double now)
        {
            if (!bufferedAttack || !CanAttack(now)) return false;
            bufferedAttack = false;
            return true;
        }

        public bool ConsumeImpact(out PlayerImpact impact)
        {
            impact = pendingImpact;
            if (!hasImpact) return false;
            hasImpact = false;
            return true;
        }

        // The coordinator supplies arrival facts; combat owns the one-use follow-up state.
        public void ObserveArrival(bool aboveTarget) => slamReady = aboveTarget;
        public void ObserveGrounded(bool grounded) { if (grounded) slamReady = false; }

        public PlayerCombat(Unit owner, Collider2D body, PlayerCombatTuning tuning,
            IPlayerComboRule comboRule = null, IPlayerAttackSelector attackSelector = null, IPlayerHitReaction hitReaction = null)
        {
            this.owner = owner; this.body = body; this.tuning = tuning;
            this.comboRule = comboRule; this.attackSelector = attackSelector; this.hitReaction = hitReaction;
        }

        public bool TryAttack(UnitHealth target, float facing, bool emergence, double now, bool grounded = true)
        {
            if (!CanAttack(now)) return false;
            ObserveGrounded(grounded);
            combo = comboRule?.Select(nextStrike, now > comboExpiresAt, target != previousComboTarget) ?? PlayerComboStep.Basic;
            pendingComboTarget = target;
            var forward = PlayerAttackSelection.Side(body.bounds, facing, tuning);
            if (!grounded)
                selection = slamReady ? new PlayerAttackSelection(PlayerAttack.Slam, facing, forward.HitOffset, forward.HitSize) : forward;
            else selection = attackSelector?.Select(body.bounds, target, facing, emergence) ?? forward;
            if (Attack == PlayerAttack.Slam) slamReady = false;
            hit.Clear();
            bufferedAttack = false;
            emittedImpactStages = 0;
            attackVersion++;
            airborneAttack = !grounded;
            Phase = PlayerAttackPhase.Windup;
            phaseStartedAt = now;
            phaseEndsAt = now + (Attack == PlayerAttack.Slam ? tuning.SlamHoverDuration : tuning.Windup);
            return true;
        }

        public Bounds AttackBounds => Attack == PlayerAttack.Slam && !IsDescending ? default :
            new Bounds((Vector2)body.bounds.center + selection.HitOffset, selection.HitSize);

        public PlayerAttackPresentation GetPresentation(double now) => new PlayerAttackPresentation
        {
            Kind = Attack, Strike = Strike, Version = attackVersion, Airborne = airborneAttack,
            PhaseProgress = phaseEndsAt > phaseStartedAt
                ? Mathf.Clamp01((float)((now - phaseStartedAt) / (phaseEndsAt - phaseStartedAt))) : 1f
        };

        public PlayerShockwave GetShockwave(double now)
        {
            float progress = (float)((now - shockwaveStartedAt) / tuning.ShockwaveDuration);
            return new PlayerShockwave { Visible = progress >= 0f && progress < 1f,
                Center = shockwaveCenter, Radius = tuning.ShockwaveRadius, Progress = Mathf.Clamp01(progress) };
        }

        public void LandSlam(Vector2 contact, double now)
        {
            if (!IsDescending) return;
            shockwaveCenter = contact + Vector2.up * 0.08f;
            shockwaveStartedAt = now;
            Phase = PlayerAttackPhase.Active;
            phaseStartedAt = now;
            phaseEndsAt = now + tuning.ActiveDuration;
            HitTargets(true);
        }

        // Returns a one-shot motion request from the selected attack; the player motor applies it.
        public bool Tick(double now)
        {
            bool launch = false;
            if (Phase == PlayerAttackPhase.Windup && now >= phaseEndsAt)
            {
                Phase = Attack == PlayerAttack.Slam ? PlayerAttackPhase.Descending : PlayerAttackPhase.Active;
                phaseStartedAt = now;
                phaseEndsAt = now + (IsDescending ? tuning.SlamTimeout : tuning.ActiveDuration);
                nextStrike = combo.NextStrike;
                previousComboTarget = pendingComboTarget;
                launch = selection.LaunchSpeed > 0f;
            }
            if (IsDescending && now >= phaseEndsAt) BeginRecovery(now);
            if (IsDescending) HitTargets(false);
            if (Phase == PlayerAttackPhase.Active)
            {
                if (now >= phaseEndsAt)
                {
                    BeginRecovery(now);
                }
                else if (Attack != PlayerAttack.Slam) HitTargets(false);
            }
            if (Phase == PlayerAttackPhase.Recovery && now >= phaseEndsAt) Phase = PlayerAttackPhase.Ready;
            return launch;
        }

        private void BeginRecovery(double now)
        {
            Phase = PlayerAttackPhase.Recovery;
            phaseStartedAt = now;
            comboExpiresAt = now + tuning.ComboWindow;
            phaseEndsAt = now + tuning.Recovery;
        }

        private void HitTargets(bool shockwave)
        {
            var area = AttackBounds;
            bool dealtDamage = false;
            if (shockwave)
                Physics2D.OverlapCircle(shockwaveCenter, tuning.ShockwaveRadius, UnitPhysics2D.Filter(tuning.TargetMask), candidates);
            else Physics2D.OverlapBox(area.center, area.size, 0f, UnitPhysics2D.Filter(tuning.TargetMask), candidates);
            for (int i = 0; i < candidates.Count; i++)
            {
                var target = candidates[i].GetComponentInParent<UnitHealth>();
                if (!UnitPhysics2D.IsAvailable(target) || target.Owner == owner || target.Owner.Kind != UnitKind.Monster ||
                    !target.CanReceiveDamage || hit.Contains(target) ||
                    !UnitPhysics2D.HasSight(shockwave ? shockwaveCenter : (Vector2)body.bounds.center,
                        candidates[i].bounds.center, tuning.ObstacleMask, obstacles)) continue;
                hit.Add(target);
                if (target.ApplyDamage(owner.Definition.AttackPower) <= 0) continue;
                dealtDamage = true; // Lethal hits also produce feedback.
                if (target.IsAlive)
                    hitReaction?.Apply(target, shockwave ? new PlayerAttackSelection(PlayerAttack.Shockwave,
                        Facing, Vector2.zero, Vector2.one * tuning.ShockwaveRadius * 2f) : selection, combo);
            }
            // One request per swing; the slam's descending hit and landing are separate impact stages.
            int stage = shockwave ? 2 : 1;
            if (!dealtDamage || (emittedImpactStages & stage) != 0) return;
            emittedImpactStages |= stage;
            var strength = Attack == PlayerAttack.Slam ? PlayerImpactStrength.Slam :
                combo.IsFinisher || Attack == PlayerAttack.Lift || Attack == PlayerAttack.Emergence
                    ? PlayerImpactStrength.Heavy : PlayerImpactStrength.Normal;
            Vector2 direction = Attack == PlayerAttack.Slam ? Vector2.down :
                Attack == PlayerAttack.Side ? Vector2.right * Facing : Vector2.up;
            if (!hasImpact || strength > pendingImpact.Strength) pendingImpact = new PlayerImpact(direction, strength);
            hasImpact = true;
        }

        public void CancelRecovery()
        {
            if (Phase == PlayerAttackPhase.Recovery) Phase = PlayerAttackPhase.Ready;
            bufferedAttack = false;
        }

        public void Interrupt(double now)
        {
            Reset();
            stunnedUntil = now + tuning.HitStun;
        }

        public void Reset()
        {
            Phase = PlayerAttackPhase.Ready;
            nextStrike = 1;
            combo = default;
            pendingComboTarget = previousComboTarget = null;
            comboExpiresAt = stunnedUntil = 0;
            shockwaveStartedAt = double.NegativeInfinity;
            slamReady = false;
            bufferedAttack = hasImpact = false;
            emittedImpactStages = 0;
            pendingImpact = default;
            hit.Clear();
        }
    }
}
