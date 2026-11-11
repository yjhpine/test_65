using ActionPlatformer.Units.Features;
using UnityEngine;

namespace ActionPlatformer.Units.Bosses
{
    // Debug overlay: the boss's current FSM state and pattern, drawn above its body in the Game view.
    // Drawn only in the Editor and development builds; it reads the brain and never changes it.
    [DisallowMultipleComponent, RequireComponent(typeof(BossCombat2D))]
    public sealed class BossDebugLabel : MonoBehaviour
    {
        [SerializeField] private bool visible = true;
        [Tooltip("World units above the top of the body collider.")]
        [SerializeField, Min(0)] private float heightOffset = .35f;
        [SerializeField, Min(8)] private int fontSize = 14;
        private BossCombat2D combat;
        private UnitHealth health;
        private Collider2D body;
        private GUIStyle style;

        public bool Visible { get => visible; set => visible = value; }

        private void Awake() { combat = GetComponent<BossCombat2D>(); health = GetComponent<UnitHealth>(); body = GetComponent<Collider2D>(); }

        // Line 1: what the boss is doing now; line 2: phase and health.
        public static string Describe(IBossActionState state, int currentHealth, int maxHealth)
        {
            if (state == null) return "(no brain)";
            string action = state.State.ToString();
            switch (state.State)
            {
                case BossState.Telegraph: case BossState.Execute: case BossState.Recover:
                    action += " · " + (state.Pattern == null ? "?" : state.Pattern.DisplayName) + Percent(state.Progress);
                    break;
                case BossState.Staggered: case BossState.Death:
                    action += Percent(state.Progress);
                    break;
                case BossState.PhaseShift:
                    action += " → P" + state.Phase + Percent(state.Progress);
                    break;
            }
            return action + "\nP" + state.Phase + " · HP " + currentHealth + "/" + maxHealth;
        }

        public static Color ColorFor(BossState state)
        {
            switch (state)
            {
                case BossState.Telegraph: return new Color(1f, .78f, .25f);
                case BossState.Execute: return new Color(1f, .35f, .3f);
                case BossState.Recover: return new Color(.75f, .75f, .75f);
                case BossState.Staggered: return new Color(.4f, .9f, 1f);
                case BossState.PhaseShift: return new Color(.9f, .5f, 1f);
                case BossState.Death: return new Color(.5f, .5f, .5f);
                default: return Color.white;
            }
        }

        private static string Percent(float progress) => " " + Mathf.RoundToInt(Mathf.Clamp01(progress) * 100f) + "%";

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void OnGUI()
        {
            if (!visible || Event.current.type != EventType.Repaint || combat == null) return;
            var view = Camera.main;
            if (view == null) return;
            Vector2 top = body == null ? (Vector2)transform.position : new Vector2(body.bounds.center.x, body.bounds.max.y);
            Vector3 screen = view.WorldToScreenPoint(top + Vector2.up * heightOffset);
            if (screen.z < 0f) return;

            if (style == null) style = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.MiddleCenter, richText = false, wordWrap = false };
            style.fontSize = fontSize;
            var state = combat.State;
            style.normal.textColor = state == null ? Color.white : ColorFor(state.State);
            var content = new GUIContent(Describe(state, health == null ? 0 : health.CurrentHealth, health == null ? 0 : health.MaxHealth));
            Vector2 size = style.CalcSize(content);
            // Screen y grows upward, GUI y downward; anchor the label's bottom edge at the point above the head.
            var rect = new Rect(screen.x - size.x * .5f, Screen.height - screen.y - size.y, size.x, size.y);
            style.Draw(rect, content, false, false, false, false);
        }
#endif
    }
}
