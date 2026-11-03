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
        // Presentation facts of the same swing: a target died, or the combo's strong strike landed.
        public readonly bool Killed, Finisher;
        public PlayerImpact(Vector2 direction, PlayerImpactStrength strength, bool killed = false, bool finisher = false)
        { Direction = direction; Strength = strength; Killed = killed; Finisher = finisher; }
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
        private double attackStartedAt;
        private uint landingVersion;
        private readonly Queue<PlayerHitPresentation> presentationHits = new Queue<PlayerHitPresentation>();
        private bool airborneAttack;
        private double comboExpiresAt;
        private double stunnedUntil;
        private int nextStrike = 1;
        private UnitHealth pendingComboTarget, previousComboTarget;
        private PlayerComboStep combo;
        private PlayerAttackSelection selection;
        private Vector2 shockwaveCenter;
        private double shockwaveStartedAt = double.NegativeInfinity;
        private bool bufferedAttack;
        private bool launched, launchPending;
        private bool hasImpact;
        private int emittedImpactStages;
        private PlayerImpact pendingImpact;
        private bool hasRecoil;
        private float recoilVelocity, recoilDuration;
        public bool HasBufferedAttack => bufferedAttack;
        public int Strike => combo.Strike;
        public PlayerAttack Attack => selection.Kind;
        public PlayerAttackPhase Phase { get; private set; }
        public float Facing => selection.Facing == 0f ? 1f : selection.Facing;
        public float LaunchSpeed => selection.LaunchSpeed *
            (Attack == PlayerAttack.Lift && combo.IsFinisher ? tuning.FinisherLaunchMultiplier : 1f);
        public int ComboLength => comboRule == null ? 1 : tuning.ComboLength;
        // Strikes executed in the live combo chain; zero once it expires or after the finisher.
        public int ComboCount(double now) => nextStrike > 1 && (IsAttacking || now <= comboExpiresAt) ? nextStrike - 1 : 0;
        public float SlamSpeed => tuning.SlamSpeed;
        public bool IsDescending => Phase == PlayerAttackPhase.Descending;
        public bool IsPreparingSlam => Attack == PlayerAttack.Slam && Phase == PlayerAttackPhase.Windup;
        public bool IsAttacking => Phase != PlayerAttackPhase.Ready;
        // Grounded basic swings hold the player in place; airborne swings keep their momentum.
        // Special attacks retain their authored motion.
        public bool BlocksMovement(bool grounded) => IsAttacking &&
            (Attack == PlayerAttack.Side ? grounded : Attack != PlayerAttack.Air);
        public bool CanReposition(double now) => now >= stunnedUntil &&
            (Phase == PlayerAttackPhase.Ready || Phase == PlayerAttackPhase.Recovery);
        public bool CanAttack(double now) => now >= stunnedUntil && Phase == PlayerAttackPhase.Ready;

        // Any press during an attack keeps one follow-up for the next Ready step. Zero buffer time disables it.
        public bool BufferAttack(double pressedAt)
        {
            if (!IsAttacking || tuning.AttackBufferTime <= 0f || pressedAt < attackStartedAt) return false;
            bufferedAttack = true;
            return true;
        }

        public bool ConsumeBufferedAttack(double now)
        {
            if (!bufferedAttack || !CanAttack(now)) return false;
            bufferedAttack = false;
            return true;
        }

        // The swing's reaction force for PlayerUnit to apply: horizontal start speed (away from the struck side) and duration.
        public bool ConsumeRecoil(out float velocity, out float duration)
        {
            velocity = recoilVelocity; duration = recoilDuration;
            if (!hasRecoil) return false;
            hasRecoil = false;
            return true;
        }

        public bool ConsumeImpact(out PlayerImpact impact)
        {
            impact = pendingImpact;
            if (!hasImpact) return false;
            hasImpact = false;
            return true;
        }

        public bool ConsumeHit(out PlayerHitPresentation result)
        {
            if (presentationHits.Count == 0) { result = default; return false; }
            result = presentationHits.Dequeue(); return true;
        }

        public PlayerCombat(Unit owner, Collider2D body, PlayerCombatTuning tuning,
            IPlayerComboRule comboRule = null, IPlayerAttackSelector attackSelector = null, IPlayerHitReaction hitReaction = null)
        {
            this.owner = owner; this.body = body; this.tuning = tuning;
            this.comboRule = comboRule; this.attackSelector = attackSelector; this.hitReaction = hitReaction;
        }

        // Emergence keeps its glitch target. Other attacks keep a supplied target only while it is within reach,
        // otherwise the nearest enemy when the attack starts decides the attack kind.
        public bool TryAttack(UnitHealth target, float facing, bool emergence, double now, bool grounded = true)
        {
            if (!CanAttack(now)) return false;
            var targetBody = UnitPhysics2D.Body(target);
            bool emerging = emergence && grounded;
            if (!emerging && (!Damageable(target) || targetBody == null ||
                !PlayerAttackSelection.WithinReach(body.bounds, targetBody.bounds, tuning))) target = NearestTarget();
            combo = comboRule?.Select(nextStrike, now > comboExpiresAt, target != previousComboTarget) ?? PlayerComboStep.Basic;
            pendingComboTarget = target;
            var forward = PlayerAttackSelection.Side(body.bounds, facing, tuning);
            selection = attackSelector?.Select(body.bounds, target, facing, emergence, grounded) ?? (grounded ? forward :
                new PlayerAttackSelection(PlayerAttack.Air, facing, forward.HitOffset, forward.HitSize));
            hit.Clear();
            bufferedAttack = launched = launchPending = false;
            emittedImpactStages = 0;
            attackVersion++;
            attackStartedAt = now;
            airborneAttack = !grounded;
            Phase = PlayerAttackPhase.Windup;
            phaseStartedAt = now;
            phaseEndsAt = now + (Attack == PlayerAttack.Slam ? tuning.SlamHoverDuration : tuning.Windup);
            return true;
        }

        // Candidates extend past Reach so the selector can measure large bodies; it applies the exact range.
        private const float TargetSearchMargin = 2f;
        private UnitHealth NearestTarget()
        {
            var bounds = body.bounds;
            Physics2D.OverlapCircle(bounds.center, tuning.Reach + bounds.extents.magnitude + TargetSearchMargin,
                UnitPhysics2D.Filter(tuning.TargetMask), candidates);
            UnitHealth best = null;
            float bestDistance = float.PositiveInfinity;
            for (int i = 0; i < candidates.Count; i++)
            {
                var candidate = candidates[i].GetComponentInParent<UnitHealth>();
                var candidateBody = UnitPhysics2D.Body(candidate);
                if (!Damageable(candidate) || candidateBody == null) continue;
                float distance = ((Vector2)candidateBody.bounds.center - (Vector2)bounds.center).sqrMagnitude;
                if (distance >= bestDistance || !UnitPhysics2D.HasSight(bounds.center, candidateBody.bounds.center,
                    tuning.ObstacleMask, obstacles)) continue;
                best = candidate;
                bestDistance = distance;
            }
            return best;
        }

        private bool Damageable(UnitHealth target) => UnitPhysics2D.IsAvailable(target) && target.Owner != owner &&
            target.Owner.Kind == UnitKind.Monster && target.CanReceiveDamage;

        public Bounds AttackBounds => Attack == PlayerAttack.Slam && !IsDescending ? default :
            new Bounds((Vector2)body.bounds.center + selection.HitOffset, selection.HitSize);

        public PlayerAttackPresentation GetPresentation(double now) => new PlayerAttackPresentation
        {
            Kind = Attack, Strike = Strike, Finisher = combo.IsFinisher, Version = attackVersion, StartedAt = attackStartedAt,
            Airborne = airborneAttack,
            PhaseProgress = phaseEndsAt > phaseStartedAt
                ? Mathf.Clamp01((float)((now - phaseStartedAt) / (phaseEndsAt - phaseStartedAt))) : 1f
        };

        public PlayerShockwave GetShockwave(double now)
        {
            float progress = (float)((now - shockwaveStartedAt) / tuning.ShockwaveDuration);
            return new PlayerShockwave { Version = landingVersion, Visible = progress >= 0f && progress < 1f,
                Center = shockwaveCenter, Radius = tuning.ShockwaveRadius, Progress = Mathf.Clamp01(progress) };
        }

        public void LandSlam(Vector2 contact, double now)
        {
            if (!IsDescending) return;
            Phase = PlayerAttackPhase.Active;
            phaseStartedAt = now;
            phaseEndsAt = now + tuning.ActiveDuration;
            // A basic slam ends on landing; only the combo's finishing slam adds the landing shockwave.
            if (!combo.IsFinisher) return;
            shockwaveCenter = contact + Vector2.up * 0.08f;
            shockwaveStartedAt = now;
            landingVersion++;
            HitTargets(true);
        }

        // Repositioning keeps a live combo going so the player can choose where the next strike lands.
        public void ExtendCombo(double now)
        {
            if (nextStrike > 1 && now <= comboExpiresAt) comboExpiresAt = now + tuning.ComboWindow;
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
                // Every executed attack advances the shared combo, whether or not it lands.
                nextStrike = combo.NextStrike;
                previousComboTarget = pendingComboTarget;
                // Emergence rises as it starts; a lift rises only with a target it actually hit.
                launch = Attack == PlayerAttack.Emergence && selection.LaunchSpeed > 0f;
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
            if (launchPending) { launchPending = false; launch = true; }
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
            bool dealtDamage = false, killed = false;
            if (shockwave)
                Physics2D.OverlapCircle(shockwaveCenter, tuning.ShockwaveRadius, UnitPhysics2D.Filter(tuning.TargetMask), candidates);
            else Physics2D.OverlapBox(area.center, area.size, 0f, UnitPhysics2D.Filter(tuning.TargetMask), candidates);
            for (int i = 0; i < candidates.Count; i++)
            {
                var target = candidates[i].GetComponentInParent<UnitHealth>();
                if (!Damageable(target) || hit.Contains(target) ||
                    !UnitPhysics2D.HasSight(shockwave ? shockwaveCenter : (Vector2)body.bounds.center,
                        candidates[i].bounds.center, tuning.ObstacleMask, obstacles)) continue;
                hit.Add(target);
                bool bypassGuard = shockwave || Attack == PlayerAttack.Emergence || Attack == PlayerAttack.Lift || Attack == PlayerAttack.Slam;
                Vector2 origin = shockwave ? shockwaveCenter : (Vector2)body.bounds.center;
                Vector2 point = candidates[i].ClosestPoint(origin);
                int healthBefore = target.CurrentHealth;
                int power = owner.Definition.AttackPower;
                if (combo.IsFinisher) power = Mathf.RoundToInt(power * tuning.FinisherDamageMultiplier);
                // Basic swings leave the target's attack running; launches, slams and emergence stop it.
                bool interrupts = shockwave || (Attack != PlayerAttack.Side && Attack != PlayerAttack.Air);
                int damage = target.ApplyDamage(power, body.bounds.center, bypassGuard, interrupts);
                if (damage <= 0) continue;
                if (Attack == PlayerAttack.Lift && !shockwave && !launched) launched = launchPending = true;
                var kind = shockwave ? PlayerAttack.Shockwave : Attack;
                Vector2 hitDirection = Attack == PlayerAttack.Slam ? Vector2.down :
                    Attack == PlayerAttack.Lift || Attack == PlayerAttack.Emergence ? Vector2.up : Vector2.right * Facing;
                presentationHits.Enqueue(new PlayerHitPresentation(attackVersion, kind, target, point, hitDirection,
                    damage, healthBefore - target.CurrentHealth, !target.IsAlive));
                dealtDamage = true; // Lethal hits also produce feedback.
                killed |= !target.IsAlive;
                if (target.IsAlive)
                    hitReaction?.Apply(target, shockwave ? new PlayerAttackSelection(PlayerAttack.Shockwave,
                        Facing, Vector2.zero, Vector2.one * tuning.ShockwaveRadius * 2f) : selection, combo);
            }
            // One request per swing; the slam's descending hit and landing are separate impact stages.
            int stage = shockwave ? 2 : 1;
            if (!dealtDamage || (emittedImpactStages & stage) != 0) return;
            emittedImpactStages |= stage;
            // Only basic swings recoil: lifts rise with their target, slams and emergence own their motion.
            if (!shockwave && (Attack == PlayerAttack.Side || Attack == PlayerAttack.Air))
            {
                float speed = combo.IsFinisher ? tuning.FinisherRecoilSpeed : tuning.RecoilSpeed;
                float duration = combo.IsFinisher ? tuning.FinisherRecoilDuration : tuning.RecoilDuration;
                if (Attack == PlayerAttack.Air) speed *= tuning.AirRecoilMultiplier;
                hasRecoil = speed > 0f && duration > 0f;
                recoilVelocity = -Facing * speed; recoilDuration = duration;
            }
            var strength = Attack == PlayerAttack.Slam ? PlayerImpactStrength.Slam :
                combo.IsFinisher || Attack == PlayerAttack.Lift || Attack == PlayerAttack.Emergence
                    ? PlayerImpactStrength.Heavy : PlayerImpactStrength.Normal;
            Vector2 direction = Attack == PlayerAttack.Slam ? Vector2.down :
                (Attack == PlayerAttack.Side || Attack == PlayerAttack.Air) ? Vector2.right * Facing : Vector2.up;
            if (hasImpact && strength <= pendingImpact.Strength) direction = pendingImpact.Direction;
            pendingImpact = new PlayerImpact(direction, hasImpact && pendingImpact.Strength > strength ? pendingImpact.Strength : strength,
                killed || (hasImpact && pendingImpact.Killed), combo.IsFinisher || (hasImpact && pendingImpact.Finisher));
            hasImpact = true;
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
            bufferedAttack = launched = launchPending = hasImpact = hasRecoil = false;
            emittedImpactStages = 0;
            pendingImpact = default;
            presentationHits.Clear();
            hit.Clear();
        }
    }
}
