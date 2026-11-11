using ActionPlatformer.Presentation;
using UnityEngine;

namespace ActionPlatformer.Units.Features
{
    // Placeholder body plus every telegraph of the barrage, laser and domain enemies. All renderers belong to the
    // global VFX manager; this only says, once per frame, where the coming attack lands and how close it is.
    [DisallowMultipleComponent, RequireComponent(typeof(SpriteRenderer))]
    public sealed class RangedVisual2D : MonoBehaviour
    {
        private const int TelegraphZones = 7;
        [SerializeField] private RangedCombat2D stateSource;
        [SerializeField] private bool spriteFacesRight = true;
        [Tooltip("Shared red danger zone (Data/Presentation/AttackZone) laid over every telegraph.")]
        [SerializeField] private AttackZoneProfile attackZone;
        [Tooltip("Active domain field (Data/Presentation/DomainField): a glitch started or ended inside it kills.")]
        [SerializeField] private AttackZoneProfile domainField;
        [Tooltip("Sweep laser beam sprites (the drone's red ARCBIT laser).")]
        [SerializeField] private EnemyVfxProfile laserLook;
        [SerializeField] private EnemySfxProfile sfxProfile;
        [Tooltip("Shot, blast and pop sprites shared by every barrage enemy (Data/Presentation/EnemyProjectiles).")]
        [SerializeField] private EnemyProjectileProfile projectileLook;
        private readonly AttackZoneOwner[] telegraph = new AttackZoneOwner[TelegraphZones];
        private readonly AttackZoneOwner field = new AttackZoneOwner(), link = new AttackZoneOwner();
        private SpriteRenderer spriteRenderer;
        private UnitHealth health;
        private VfxManager effects;
        private SfxManager audio;
        private EffectOwnerHandle laserOwner, audioOwner;
        private Color originalColor;
        private uint shotVersion;
        public Transform TelegraphRoot(int index) => telegraph[index].SessionRoot;
        public Transform FieldRoot => field.SessionRoot;
        public Transform LinkRoot => link.SessionRoot;
        public EffectOwnerHandle LaserOwner => laserOwner;
        public VfxManager Effects => effects;

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>(); originalColor = spriteRenderer.color;
            health = stateSource.GetComponent<UnitHealth>(); shotVersion = stateSource.ShotVersion;
            for (int i = 0; i < TelegraphZones; i++) telegraph[i] = new AttackZoneOwner();
            EnemyProjectileView.Use(projectileLook, spriteRenderer);
            EnsureEffects();
        }
        private void EnsureEffects()
        {
            // Sessions follow the unscaled unit root: a rotated strip under a non-uniformly scaled placeholder body
            // would otherwise be sheared (the zone renderer cancels scale on its own axes only).
            var owner = stateSource.transform;
            foreach (var zone in telegraph) zone.Ensure(owner, attackZone, spriteRenderer);
            field.Ensure(owner, domainField, spriteRenderer); link.Ensure(owner, domainField, spriteRenderer);
            if (laserLook != null && (effects == null || !effects.IsValid(laserOwner)))
            {
                effects = EffectsRuntime.Vfx;
                if (effects != null) laserOwner = effects.RegisterOwner(owner, laserLook, spriteRenderer);
            }
            if (sfxProfile != null && (audio == null || !audio.IsValid(audioOwner)))
            {
                audio = EffectsRuntime.Sfx;
                if (audio != null) audioOwner = audio.RegisterOwner(transform, sfxProfile);
            }
        }
        private void Hide()
        {
            foreach (var zone in telegraph) zone.Hide();
            field.Hide(); link.Hide();
            effects?.HideEnemy(laserOwner);
        }
        private void LateUpdate()
        {
            EnsureEffects();
            Hide();
            var state = stateSource.State; var settings = stateSource.Settings;
            if (state == null || settings == null || !stateSource.isActiveAndEnabled) return;
            if (shotVersion != stateSource.ShotVersion) { shotVersion = stateSource.ShotVersion; audio?.PlayShot(audioOwner); }
            bool dead = !health.IsAlive || state.Phase == RangedPhase.Death;
            spriteRenderer.flipX = (state.Facing > 0f) != spriteFacesRight;
            // Placeholder death: the body fades out over the death delay (the player's kill burst plays on top).
            Color color = originalColor;
            if (dead) color.a *= 1f - (settings.DeathDuration <= 0f ? 1f : Mathf.Clamp01(state.Elapsed / settings.DeathDuration));
            spriteRenderer.color = color;
            if (dead) return;
            // An open field stays for its time whatever the owner does (self-centered follows the owner, fixed stays put).
            if (stateSource.DomainActive)
            {
                field.Show(stateSource.DomainCenterNow, stateSource.DomainSize, 0f, 1f);
                if (settings.Attack == RangedAttack.FixedDomain) Strip(link, stateSource.Center, stateSource.DomainCenterNow, .08f, 1f);
            }
            switch (state.Phase)
            {
                case RangedPhase.Preparing: DrawTelegraph(state, settings, state.Progress); break;
                case RangedPhase.Sweeping:
                {
                    // On, then the looping body frames for as long as the sweep lasts.
                    float t = state.Elapsed, p = t < .08f ? .25f * t / .08f : .25f + Mathf.Repeat(t - .08f, .3f) / .3f * .449f;
                    effects?.SubmitEnemyBeam(laserOwner, stateSource.CurrentBeam, stateSource.Center, false, p, true);
                    break;
                }
                case RangedPhase.Afterimage:
                    // Harmless afterimage: the beam's off frames at the last angle.
                    effects?.SubmitEnemyBeam(laserOwner, stateSource.CurrentBeam, stateSource.Center, false, .7f + .3f * state.Progress, true);
                    break;
            }
        }
        private void DrawTelegraph(IRangedActionState state, Fsm.RangedFsmDefinition settings, float urgency)
        {
            Vector2 center = stateSource.Center;
            switch (settings.Attack)
            {
                case RangedAttack.Straight: case RangedAttack.Shotgun: case RangedAttack.Heavy:
                    for (int i = 0; i < stateSource.ShotCount; i++)
                    {
                        var path = stateSource.ShotPath(stateSource.ShotDirection(state.Aim, i));
                        Strip(telegraph[i], path.Start, path.End, path.Width, urgency);
                    }
                    break;
                case RangedAttack.Mortar:
                {
                    // The drop column and the blast's above-ground half at the fixed landing point.
                    Vector2 land = state.LandingPoint;
                    if (float.IsNaN(land.y)) land = center + Vector2.down * settings.DetectionRange;
                    Strip(telegraph[0], center, land, settings.ShotRadius * 2f, urgency);
                    if (!float.IsNaN(state.LandingPoint.y))
                        telegraph[1].Show(land + Vector2.up * settings.BlastRadius * .5f, new Vector2(settings.BlastRadius * 2f, settings.BlastRadius), 0f, urgency);
                    break;
                }
                case RangedAttack.SweepLaser:
                    // The fan as seven rays, the first (where the sweep starts) drawn wider.
                    for (int i = 0; i < TelegraphZones; i++)
                    {
                        float angle = Mathf.Lerp(state.SweepFrom, state.SweepTo, i / (float)(TelegraphZones - 1));
                        var beam = stateSource.BeamAt(angle);
                        Strip(telegraph[i], beam.Start, beam.End, beam.Width * (i == 0 ? 2.5f : 1f), urgency);
                    }
                    break;
                case RangedAttack.SelfDomain: case RangedAttack.FixedDomain:
                    telegraph[0].Show(stateSource.DomainCenterNow, stateSource.DomainSize, 0f, urgency);
                    if (settings.Attack == RangedAttack.FixedDomain) Strip(telegraph[1], center, stateSource.DomainCenterNow, .08f, urgency);
                    break;
            }
        }
        private static void Strip(AttackZoneOwner zone, Vector2 from, Vector2 to, float width, float urgency)
        {
            Vector2 delta = to - from;
            if (delta.sqrMagnitude < .0001f) return;
            zone.Show((from + to) * .5f, new Vector2(delta.magnitude, width), Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg, urgency);
        }
        private void OnDisable()
        {
            Hide();
            foreach (var zone in telegraph) zone?.Release();
            field.Release(); link.Release();
            effects?.ReleaseOwner(laserOwner); laserOwner = default;
            audio?.ReleaseOwner(audioOwner); audioOwner = default;
            if (spriteRenderer != null) { spriteRenderer.color = originalColor; spriteRenderer.flipX = !spriteFacesRight; }
            if (stateSource != null) shotVersion = stateSource.ShotVersion;
        }
    }
}
