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
        private VisualElement stageSection, completion;
        private Label goalHint, glitchHint, comboReady;
        private VisualElement comboBar;
        private VisualElement[] comboPips = Array.Empty<VisualElement>();
        private Button restart;
        private bool transferHintVisible;
        public event Action RestartRequested;
        public bool IsConfigured => document != null && document.panelSettings != null && completion != null;
        public bool IsCompletedVisible => completion != null && completion.style.display == DisplayStyle.Flex;
        public bool IsTransferHintVisible => transferHintVisible;
        public bool IsGlitchHintVisible => glitchHint != null && glitchHint.style.display == DisplayStyle.Flex;
        public string GlitchHintText => glitchHint?.text;
        public bool IsComboVisible => comboBar != null && comboBar.style.display == DisplayStyle.Flex;
        public int ComboPipsLit { get; private set; }
        public bool IsStrongNextVisible => comboReady != null && comboReady.style.display == DisplayStyle.Flex;
        public bool IsStageVisible => stageSection != null && stageSection.style.display == DisplayStyle.Flex;

        private void OnEnable()
        {
            if (document == null || document.panelSettings == null) return;
            var root = document.rootVisualElement;
            // Replace only this view's section so sibling sections under the same root survive a re-enable.
            stageSection?.RemoveFromHierarchy();
            root.pickingMode = PickingMode.Ignore;
            root.style.flexGrow = 1;
            // Everything below belongs to the stage section; other sections can sit beside it under the root.
            stageSection = new VisualElement { name = "stage-hud", pickingMode = PickingMode.Ignore };
            stageSection.style.position = Position.Absolute;
            stageSection.style.left = stageSection.style.right = stageSection.style.top = stageSection.style.bottom = 0;
            root.Add(stageSection);

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
            stageSection.Add(goalHint);

            glitchHint = new Label { name = "glitch-hint" };
            glitchHint.pickingMode = PickingMode.Ignore;
            glitchHint.style.position = Position.Absolute;
            glitchHint.style.paddingLeft = glitchHint.style.paddingRight = 10;
            glitchHint.style.paddingTop = glitchHint.style.paddingBottom = 4;
            glitchHint.style.fontSize = 16;
            glitchHint.style.color = new Color(1f, 0.8f, 0.8f);
            glitchHint.style.backgroundColor = new Color(0.12f, 0.06f, 0.08f, 0.85f);
            glitchHint.style.display = DisplayStyle.None;
            stageSection.Add(glitchHint);

            comboBar = new VisualElement { name = "combo" };
            comboBar.pickingMode = PickingMode.Ignore;
            comboBar.style.position = Position.Absolute;
            comboBar.style.left = 32;
            comboBar.style.bottom = 32;
            comboBar.style.flexDirection = FlexDirection.Row;
            comboBar.style.alignItems = Align.Center;
            var comboTitle = new Label("COMBO");
            comboTitle.style.fontSize = 16;
            comboTitle.style.color = Color.white;
            comboTitle.style.marginRight = 6;
            comboBar.Add(comboTitle);
            comboReady = new Label("STRONG NEXT");
            comboReady.style.fontSize = 16;
            comboReady.style.color = ComboReadyColor;
            comboReady.style.marginLeft = 8;
            comboBar.Add(comboReady);
            comboBar.style.display = DisplayStyle.None;
            stageSection.Add(comboBar);
            comboPips = Array.Empty<VisualElement>();

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
            stageSection.Add(completion);
            completion.style.display = DisplayStyle.None;
            ShowGoalHint(false);
            ShowStage(false);
        }

        // The stage section follows stage registration in the GameManager.
        public void ShowStage(bool visible)
        {
            if (stageSection != null) stageSection.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        // Clears what a previous stage left on screen, so a new run starts without its hints or clear screen.
        public void ResetStage()
        {
            ShowGoalHint(false);
            if (glitchHint != null) glitchHint.style.display = DisplayStyle.None;
            ShowCombo(0, 0);
            if (completion != null) completion.style.display = DisplayStyle.None;
            SetRestartEnabled(true);
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

        // Explains a red glitch marker just above it. Busy and blocked-space destinations keep only the red marker.
        public void ShowGlitchHint(GlitchPreview preview, Camera camera)
        {
            if (glitchHint == null) return;
            string text = !preview.Visible ? null : !preview.CanExecute ? HintText(preview.Reason) : preview.Lethal ? "LETHAL ZONE" : null;
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

        private static readonly Color ComboLitColor = new Color(0.47f, 0.85f, 0.87f);
        private static readonly Color ComboReadyColor = new Color(1f, 0.72f, 0.25f);
        private static readonly Color ComboEmptyColor = new Color(1f, 1f, 1f, 0.18f);

        // One pip per strike of the shared combo; the last pip and label light up when the next strike is the strong one.
        public void ShowCombo(int count, int length)
        {
            if (comboBar == null) return;
            if (length <= 1)
            {
                comboBar.style.display = DisplayStyle.None;
                ComboPipsLit = 0;
                return;
            }
            if (comboPips.Length != length)
            {
                foreach (var pip in comboPips) pip.RemoveFromHierarchy();
                comboPips = new VisualElement[length];
                for (int i = 0; i < length; i++)
                {
                    var pip = new VisualElement { pickingMode = PickingMode.Ignore };
                    pip.style.width = pip.style.height = 14;
                    pip.style.marginLeft = pip.style.marginRight = 3;
                    pip.style.borderTopLeftRadius = pip.style.borderTopRightRadius =
                        pip.style.borderBottomLeftRadius = pip.style.borderBottomRightRadius = 2;
                    comboBar.Insert(1 + i, pip);
                    comboPips[i] = pip;
                }
            }
            count = Mathf.Clamp(count, 0, length - 1);
            bool strongNext = count == length - 1;
            for (int i = 0; i < length; i++)
                comboPips[i].style.backgroundColor = i < count ? ComboLitColor : strongNext ? ComboReadyColor : ComboEmptyColor;
            comboReady.style.display = strongNext ? DisplayStyle.Flex : DisplayStyle.None;
            ComboPipsLit = count;
            comboBar.style.display = DisplayStyle.Flex;
        }

        private static string HintText(GlitchBlockReason reason)
        {
            switch (reason)
            {
                case GlitchBlockReason.OutOfRange: return "OUT OF RANGE";
                case GlitchBlockReason.OffScreen: return "OFF SCREEN";
                case GlitchBlockReason.GateClosed: return "GATE CLOSED";
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
