using System;
using ActionPlatformer.Player;
using UnityEngine;
using UnityEngine.UIElements;

namespace ActionPlatformer.Flow
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    public sealed class StageFlowView : MonoBehaviour
    {
        [SerializeField] private UIDocument document;
        private VisualElement completion;
        private Label goalHint, glitchHint;
        private Button restart;
        private bool transferHintVisible;
        public event Action RestartRequested;
        public bool IsConfigured => document != null && document.panelSettings != null && completion != null;
        public bool IsCompletedVisible => completion != null && completion.style.display == DisplayStyle.Flex;
        public bool IsTransferHintVisible => transferHintVisible;
        public bool IsGlitchHintVisible => glitchHint != null && glitchHint.style.display == DisplayStyle.Flex;
        public string GlitchHintText => glitchHint?.text;

        private void OnEnable()
        {
            if (document == null || document.panelSettings == null) return;
            var root = document.rootVisualElement;
            root.Clear();
            root.pickingMode = PickingMode.Ignore;
            root.style.flexGrow = 1;

            goalHint = new Label("DEFEAT ALL ENEMIES TO OPEN THE EXIT");
            goalHint.pickingMode = PickingMode.Ignore;
            goalHint.style.position = Position.Absolute;
            goalHint.style.top = 32;
            goalHint.style.alignSelf = Align.Center;
            goalHint.style.paddingLeft = goalHint.style.paddingRight = 24;
            goalHint.style.paddingTop = goalHint.style.paddingBottom = 12;
            goalHint.style.fontSize = 22;
            goalHint.style.color = Color.white;
            goalHint.style.backgroundColor = new Color(0.12f, 0.06f, 0.08f, 0.92f);
            root.Add(goalHint);

            glitchHint = new Label { name = "glitch-hint" };
            glitchHint.pickingMode = PickingMode.Ignore;
            glitchHint.style.position = Position.Absolute;
            glitchHint.style.paddingLeft = glitchHint.style.paddingRight = 10;
            glitchHint.style.paddingTop = glitchHint.style.paddingBottom = 4;
            glitchHint.style.fontSize = 16;
            glitchHint.style.color = new Color(1f, 0.8f, 0.8f);
            glitchHint.style.backgroundColor = new Color(0.12f, 0.06f, 0.08f, 0.85f);
            glitchHint.style.display = DisplayStyle.None;
            root.Add(glitchHint);

            completion = new VisualElement { name = "stage-complete" };
            completion.style.position = Position.Absolute;
            completion.style.left = completion.style.right = completion.style.top = completion.style.bottom = 0;
            completion.style.alignItems = Align.Center;
            completion.style.justifyContent = Justify.Center;
            completion.style.backgroundColor = new Color(0.025f, 0.045f, 0.065f, 0.9f);
            var title = new Label("STAGE CLEAR");
            title.style.fontSize = 56;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = new Color(0.2f, 0.94f, 0.82f);
            title.style.marginBottom = 12;
            completion.Add(title);
            var subtitle = new Label("All enemies defeated. Destination reached.");
            subtitle.style.fontSize = 20;
            subtitle.style.color = Color.white;
            subtitle.style.marginBottom = 32;
            completion.Add(subtitle);
            restart = new Button(RequestRestart) { name = "restart", text = "RESTART" };
            restart.style.width = 220;
            restart.style.height = 56;
            restart.style.fontSize = 24;
            completion.Add(restart);
            root.Add(completion);
            completion.style.display = DisplayStyle.None;
            ShowGoalHint(false);
        }

        public void ShowGoalHint(bool show)
        {
            transferHintVisible = false;
            if (goalHint == null) return;
            goalHint.text = "DEFEAT ALL ENEMIES TO OPEN THE EXIT";
            goalHint.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void ShowTransferHint(bool show)
        {
            if (goalHint == null || (!show && !transferHintVisible)) return;
            transferHintVisible = show;
            goalHint.text = "REST AREA OPEN  >  WALK OR SHIFT";
            goalHint.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
        }

        // Explains a red glitch marker just above it. Brief Busy states keep only the red marker.
        public void ShowGlitchHint(GlitchPreview preview, Camera camera)
        {
            if (glitchHint == null) return;
            string text = preview.Visible && !preview.CanExecute ? HintText(preview.Reason) : null;
            var layer = glitchHint.parent;
            float width = layer.contentRect.width, height = layer.contentRect.height;
            if (text == null || camera == null || glitchHint.panel == null || !(width > 32f) || !(height > 32f))
            {
                glitchHint.style.display = DisplayStyle.None;
                return;
            }
            glitchHint.text = text;
            Vector2 size = glitchHint.MeasureTextSize(text, 0, VisualElement.MeasureMode.Undefined,
                0, VisualElement.MeasureMode.Undefined) + new Vector2(20f, 8f);
            Vector2 point = layer.WorldToLocal(RuntimePanelUtils.CameraTransformWorldToPanel(glitchHint.panel,
                preview.Bounds.center + Vector3.up * 0.55f, camera));
            glitchHint.style.left = Mathf.Clamp(point.x - size.x * 0.5f, 16f, width - 16f - size.x);
            glitchHint.style.top = Mathf.Clamp(point.y - size.y, 16f, height - 16f - size.y);
            glitchHint.style.display = DisplayStyle.Flex;
        }

        private static string HintText(GlitchBlockReason reason)
        {
            switch (reason)
            {
                case GlitchBlockReason.OutOfRange: return "OUT OF RANGE";
                case GlitchBlockReason.OffScreen: return "OFF SCREEN";
                case GlitchBlockReason.GateClosed: return "GATE CLOSED";
                case GlitchBlockReason.NoSpace: return "NO SPACE";
                case GlitchBlockReason.Cooldown: return "COOLDOWN";
                default: return null;
            }
        }

        public void ShowCompleted()
        {
            ShowGoalHint(false);
            completion.style.display = DisplayStyle.Flex;
            restart.Focus();
        }

        public void SetRestartEnabled(bool value) => restart?.SetEnabled(value);
        private void RequestRestart() => RestartRequested?.Invoke();

        private void OnDisable()
        {
            if (restart != null) restart.clicked -= RequestRestart;
        }
    }
}
