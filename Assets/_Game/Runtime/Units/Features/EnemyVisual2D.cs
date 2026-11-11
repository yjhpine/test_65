using UnityEngine;
using ActionPlatformer.Presentation;
using ActionPlatformer.Units.Fsm;

namespace ActionPlatformer.Units.Features
{
    [DisallowMultipleComponent, RequireComponent(typeof(SpriteRenderer), typeof(Animator))]
    public sealed class EnemyVisual2D : MonoBehaviour
    {
        [SerializeField] private EnemyCombat2D stateSource;
        [SerializeField] private bool spriteFacesRight = true;
        private VfxManager effects;
        private SfxManager audio;
        private EffectOwnerHandle effectOwner,audioOwner;
        public EnemyVfxProfile EffectsProfile => vfxProfile;
        public EffectOwnerHandle EffectOwner => effectOwner;
        public VfxManager Effects => effects;
        [SerializeField] private ActionPlatformer.Presentation.EnemyVfxProfile vfxProfile;
        [SerializeField] private ActionPlatformer.Presentation.EnemySfxProfile sfxProfile;
        [Tooltip("Shared red danger zone stretched over the coming attack (Data/Presentation/AttackZone).")]
        [SerializeField] private AttackZoneProfile attackZone;
        private readonly AttackZoneOwner zone = new AttackZoneOwner();
        public Transform AttackZoneRoot => zone.SessionRoot;
        private float preparationSeen;
        private SpriteRenderer spriteRenderer;
        private Animator animator;
        private UnitHealth health;
        private Rigidbody2D body;
        private uint damageVersion, blockVersion, shotVersion;
        private float blockUntil, shotUntil;
        private string animationName;
        private Color originalColor;
        private SurveillanceTurretVisual turretVisual;
        private ShieldSoldierVisual shieldVisual;
        private EnemyLaserVisual laserVisual;
        private Vector2 shotEmitter;
        private bool shotVertical;
        private Quaternion restingRotation;
        private double droneHitAt = double.NegativeInfinity;
        private float droneHitLean;
        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>(); animator = GetComponent<Animator>();
            health = stateSource.GetComponent<UnitHealth>(); body = stateSource.GetComponent<Rigidbody2D>();
            originalColor = spriteRenderer.color;
            restingRotation = transform.localRotation;
            turretVisual = GetComponent<SurveillanceTurretVisual>();
            if (turretVisual != null) turretVisual.Initialize(spriteRenderer, animator);
            shieldVisual = GetComponent<ShieldSoldierVisual>();
            if (shieldVisual != null) shieldVisual.Initialize(animator, health.DamageVersion, stateSource.BlockVersion);
            laserVisual = GetComponent<EnemyLaserVisual>();
            if (laserVisual != null) laserVisual.Initialize(spriteRenderer);
            EnsureEffects();
        }
        private void EnsureEffects()
        {
            if(effects==null || !effects.IsValid(effectOwner)) {
                effects=EffectsRuntime.Vfx;
                if(effects!=null)effectOwner=effects.RegisterOwner(transform,vfxProfile,spriteRenderer);
            }
            if(audio==null || !audio.IsValid(audioOwner)) {
                audio=EffectsRuntime.Sfx;
                if(audio!=null)audioOwner=audio.RegisterOwner(transform,sfxProfile);
            }
            zone.Ensure(transform,attackZone,spriteRenderer);
        }
        // One ramp over the whole warning: preparation, then (ranged enemies) the aim lock that follows it.
        private float Urgency(IEnemyActionState state, EnemyFsmDefinition settings)
        {
            float lockTime = settings.Pattern == EnemyPattern.Shield ? 0f : settings.LockDuration;
            if (state.Phase == EnemyPhase.Preparing)
            {
                // The preparation's length is read back from the state (elapsed / progress) so each wind-up uses its own.
                float preparation = state.Progress > 0f ? state.Elapsed / state.Progress : preparationSeen;
                if (state.Progress > 0f && state.Progress < 1f) preparationSeen = preparation;
                return preparation <= 0f ? 0f : Mathf.Clamp01(state.Elapsed / (preparation + lockTime));
            }
            // Locked without a seen preparation (bound straight into the lock) starts the second half of the ramp.
            float before = preparationSeen > 0f ? preparationSeen : lockTime;
            return before + lockTime <= 0f ? 1f : Mathf.Clamp01((before + state.Elapsed) / (before + lockTime));
        }
        private void LateUpdate()
        {
            EnsureEffects();
            var state = stateSource.State; var settings = stateSource.Settings;
            if (state == null || settings == null || !stateSource.isActiveAndEnabled || !health.Owner.isActiveAndEnabled) { Hide(); return; }
            bool dead = !health.IsAlive || state.Phase == EnemyPhase.Death;
            if (damageVersion != health.DamageVersion)
            {
                damageVersion = health.DamageVersion;
                if (settings.Pattern == EnemyPattern.Drone)
                {
                    droneHitAt = Time.unscaledTimeAsDouble;
                    droneHitLean = health.LastDamage.HasDirection && Mathf.Abs(health.LastDamage.Direction.x) > .01f
                        ? -Mathf.Sign(health.LastDamage.Direction.x) : -state.Facing;
                }
            }
            if (settings.Pattern == EnemyPattern.Drone)
            {
                // Visual recoil only: leave the body, FSM and firing animation/timing untouched.
                float recoil = dead ? 0f : Mathf.Clamp01(1f - (float)(Time.unscaledTimeAsDouble - droneHitAt) / .12f);
                transform.localRotation = restingRotation * Quaternion.Euler(0, 0, droneHitLean * 8f * recoil * recoil);
            }
            if (blockVersion != stateSource.BlockVersion) { blockVersion = stateSource.BlockVersion; blockUntil = Time.time + .12f; }
            if (shotVersion != stateSource.ShotVersion)
            {
                shotVersion = stateSource.ShotVersion; shotUntil = Time.time + settings.BeamDisplayDuration;
                audio?.PlayShot(audioOwner);
                shotEmitter = stateSource.Center; shotVertical = state.Vertical;
            }
            spriteRenderer.flipX = (state.Facing > 0f) != spriteFacesRight;
            spriteRenderer.color = originalColor; // UnitDamageFlash owns the short, unscaled hit brightness.
            string desired = dead ? "Death" : state.Phase == EnemyPhase.Dash || Time.time < shotUntil ? "Attack" :
                state.Phase == EnemyPhase.Idle && body.linearVelocity.sqrMagnitude > .04f ? "Run" : "Idle";
            float actionTime = dead ? (settings.DeathDuration <= 0f ? 1f : state.Elapsed / settings.DeathDuration) :
                state.Phase == EnemyPhase.Dash ? state.Progress : 1f - (shotUntil - Time.time) / settings.BeamDisplayDuration;
            if (turretVisual != null && settings.Pattern == EnemyPattern.Surveillance)
                turretVisual.Render(state, Time.time < shotUntil, dead, actionTime,
                    stateSource.LastShot.End - stateSource.LastShot.Start);
            else if (shieldVisual != null && settings.Pattern == EnemyPattern.Shield)
                shieldVisual.Render(state, dead, actionTime, body.linearVelocity.sqrMagnitude > .04f,
                    health.DamageVersion, stateSource.BlockVersion);
            else
            {
                if (desired != animationName) { animationName = desired; animator.Play(desired, 0, 0f); }
                animator.SetFloat("ActionTime", Mathf.Clamp01(actionTime));
            }
            Hide(); if (dead) return;
            Vector2 center = stateSource.Center;
            if (settings.Pattern == EnemyPattern.Shield)
            {
                bool warning = state.Phase == EnemyPhase.Preparing || state.Phase == EnemyPhase.Dash || state.Phase == EnemyPhase.Strike;
                // No shield bar while it is down (launched/slammed).
                if (stateSource.Guarding)
                    Draw(EnemyEffectShape.Shield, center + Vector2.right * state.Facing * .58f, new Vector2(.09f, 1.5f), 0,
                        Time.time < blockUntil ? Color.white : warning ? Color.Lerp(Color.cyan, new Color(1f,.35f,.05f), state.Progress) : new Color(.12f,.6f,1f,.8f));
                // The danger zone covers what is coming: the strike reach or the fixed rush path.
                if (state.Phase == EnemyPhase.Preparing)
                {
                    stateSource.ShieldAttackArea(state.Facing, out var area, out var size);
                    zone.Show(area, size, 0f, Urgency(state, settings));
                }
            }
            if (settings.Pattern == EnemyPattern.Surveillance && state.Phase == EnemyPhase.Preparing)
                Draw(EnemyEffectShape.Scan, center + new Vector2(-.5f + state.Progress * .5f, .82f), new Vector2(Mathf.Max(.015f,state.Progress), .07f), 0, Color.yellow);
            if (Time.time < shotUntil)
            {
                if (laserVisual == null || !laserVisual.ShowShot(stateSource.LastShot, shotEmitter, shotVertical, actionTime))
                    DrawBeam(stateSource.LastShot, new Color(1f,.85f,.25f));
            }
            else if (settings.Pattern != EnemyPattern.Shield && (state.Phase == EnemyPhase.Preparing || state.Phase == EnemyPhase.Locked))
            {
                var preview = stateSource.CalculateBeam(state.Aim, state.Vertical);
                bool locked = state.Phase == EnemyPhase.Locked;
                // The danger zone is the beam's own strip (same EnemyBeam the hit uses), so it ends where the beam is clipped.
                Vector2 delta = preview.End - preview.Start;
                zone.Show((preview.Start + preview.End) * .5f, new Vector2(delta.magnitude, preview.Width),
                    Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg, Urgency(state, settings));
                laserVisual?.ShowCharge(preview, stateSource.Center, state.Vertical, locked ? 1 : state.Progress);
            }
        }
        private void DrawBeam(EnemyBeam value, Color color)
        {
            Vector2 delta = value.End - value.Start;
            Draw(EnemyEffectShape.Beam, (value.Start + value.End) * .5f, new Vector2(delta.magnitude, value.Width), Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg, color);
        }
        private void Draw(EnemyEffectShape shape,Vector2 position,Vector2 size,float angle,Color color)
            => effects?.SubmitShape(effectOwner,shape,position,size,angle,color);
        private void Hide() { effects?.HideEnemy(effectOwner); zone.Hide(); }
        private void OnDisable()
        {
            audio?.ReleaseOwner(audioOwner); audioOwner=default;
            droneHitAt = double.NegativeInfinity;
            transform.localRotation = restingRotation;
            Hide(); effects?.ReleaseOwner(effectOwner); effectOwner=default; blockUntil = shotUntil = 0f; animationName = null;
            zone.Release(); preparationSeen = 0f;
            if (stateSource != null) { blockVersion = stateSource.BlockVersion; shotVersion = stateSource.ShotVersion; }
            if (health != null) damageVersion = health.DamageVersion;
            if (shieldVisual != null) shieldVisual.ResetPresentation(damageVersion, blockVersion);
            if (spriteRenderer != null) { spriteRenderer.color = originalColor; spriteRenderer.flipX = !spriteFacesRight; }
        }
    }
}
