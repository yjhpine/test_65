using UnityEngine;

namespace ActionPlatformer.Units.Features
{
    [DisallowMultipleComponent, RequireComponent(typeof(SpriteRenderer), typeof(Animator))]
    public sealed class EnemyVisual2D : MonoBehaviour
    {
        [SerializeField] private EnemyCombat2D stateSource;
        [SerializeField] private Sprite shapeSprite;
        [SerializeField] private bool spriteFacesRight = true;
        private SpriteRenderer spriteRenderer, shield, scan, beam;
        private Animator animator;
        private UnitHealth health;
        private Rigidbody2D body;
        private uint damageVersion, blockVersion, shotVersion;
        private float hurtUntil, blockUntil, shotUntil;
        private string animationName;
        private Color originalColor;
        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>(); animator = GetComponent<Animator>();
            health = stateSource.GetComponent<UnitHealth>(); body = stateSource.GetComponent<Rigidbody2D>();
            originalColor = spriteRenderer.color;
            shield = Shape("Energy shield"); scan = Shape("Scan progress"); beam = Shape("Attack telegraph");
        }
        private SpriteRenderer Shape(string label)
        {
            var child = new GameObject(label); child.transform.SetParent(transform, false);
            var renderer = child.AddComponent<SpriteRenderer>(); renderer.sprite = shapeSprite;
            renderer.sharedMaterial = spriteRenderer.sharedMaterial; renderer.sortingOrder = spriteRenderer.sortingOrder + 2;
            renderer.enabled = false; return renderer;
        }
        private void LateUpdate()
        {
            var state = stateSource.State; var settings = stateSource.Settings;
            if (state == null || settings == null || !stateSource.isActiveAndEnabled || !health.Owner.isActiveAndEnabled) { Hide(); return; }
            bool dead = !health.IsAlive || state.Phase == EnemyPhase.Death;
            if (damageVersion != health.DamageVersion) { damageVersion = health.DamageVersion; hurtUntil = Time.time + .09f; }
            if (blockVersion != stateSource.BlockVersion) { blockVersion = stateSource.BlockVersion; blockUntil = Time.time + .12f; }
            if (shotVersion != stateSource.ShotVersion) { shotVersion = stateSource.ShotVersion; shotUntil = Time.time + settings.BeamDisplayDuration; }
            spriteRenderer.flipX = (state.Facing > 0f) != spriteFacesRight;
            spriteRenderer.color = !dead && Time.time < hurtUntil ? new Color(1f, .45f, .45f) : originalColor;
            string desired = dead ? "Death" : state.Phase == EnemyPhase.Dash || Time.time < shotUntil ? "Attack" :
                state.Phase == EnemyPhase.Idle && body.linearVelocity.sqrMagnitude > .04f ? "Run" : "Idle";
            if (desired != animationName) { animationName = desired; animator.Play(desired, 0, 0f); }
            float actionTime = dead ? (settings.DeathDuration <= 0f ? 1f : state.Elapsed / settings.DeathDuration) :
                state.Phase == EnemyPhase.Dash ? state.Progress : 1f - (shotUntil - Time.time) / settings.BeamDisplayDuration;
            animator.SetFloat("ActionTime", Mathf.Clamp01(actionTime));
            Hide(); if (dead) return;
            Vector2 center = stateSource.Center;
            if (settings.Pattern == EnemyPattern.Shield)
            {
                bool warning = state.Phase == EnemyPhase.Preparing || state.Phase == EnemyPhase.Dash;
                Draw(shield, center + Vector2.right * state.Facing * .58f, new Vector2(.09f, 1.5f), 0,
                    Time.time < blockUntil ? Color.white : warning ? Color.Lerp(Color.cyan, new Color(1f,.35f,.05f), state.Progress) : new Color(.12f,.6f,1f,.8f));
            }
            if (settings.Pattern == EnemyPattern.Surveillance && state.Phase == EnemyPhase.Preparing)
                Draw(scan, center + new Vector2(-.5f + state.Progress * .5f, .82f), new Vector2(Mathf.Max(.015f,state.Progress), .07f), 0, Color.yellow);
            if (Time.time < shotUntil) DrawBeam(stateSource.LastShot, new Color(1f,.85f,.25f));
            else if (settings.Pattern != EnemyPattern.Shield && (state.Phase == EnemyPhase.Preparing || state.Phase == EnemyPhase.Locked))
                DrawBeam(stateSource.CalculateBeam(state.Aim, state.Vertical), state.Phase == EnemyPhase.Locked ? new Color(1f,.2f,.12f,.75f) : new Color(1f,.8f,.15f,.28f));
        }
        private void DrawBeam(EnemyBeam value, Color color)
        {
            Vector2 delta = value.End - value.Start;
            Draw(beam, (value.Start + value.End) * .5f, new Vector2(delta.magnitude, value.Width), Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg, color);
        }
        private void Draw(SpriteRenderer renderer, Vector2 position, Vector2 size, float angle, Color color)
        {
            if (shapeSprite == null) return;
            renderer.enabled = true; renderer.color = color; renderer.transform.position = new Vector3(position.x, position.y, transform.position.z);
            renderer.transform.rotation = Quaternion.Euler(0,0,angle);
            Vector3 source = shapeSprite.bounds.size;
            renderer.transform.localScale = new Vector3(size.x/source.x, size.y/source.y, 1f);
        }
        private void Hide() { if (shield != null) shield.enabled = false; if (scan != null) scan.enabled = false; if (beam != null) beam.enabled = false; }
        private void OnDisable()
        {
            Hide(); hurtUntil = blockUntil = shotUntil = 0f; animationName = null;
            if (stateSource != null) { blockVersion = stateSource.BlockVersion; shotVersion = stateSource.ShotVersion; }
            if (health != null) damageVersion = health.DamageVersion;
            if (spriteRenderer != null) { spriteRenderer.color = originalColor; spriteRenderer.flipX = !spriteFacesRight; }
        }
    }
}
