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
        public enum ResultKind { None, Clear, FinalClear, Failed }
        public const string ResultName = "stage-result";
        [SerializeField] private UIDocument document;
        [Tooltip("UI font asset (Galmuri11 12px bitmap since 2026-11-12); text sizes snap to whole multiples of 12 screen pixels.")]
        [SerializeField] private UnityEngine.TextCore.Text.FontAsset fontAsset;
        private VisualElement stageSection;
        private Label goalHint, glitchHint, comboReady;
        private VisualElement comboBar;
        private VisualElement[] comboPips = Array.Empty<VisualElement>();
        private ResultPanel result;
        private bool transferHintVisible;
        private string transferKey = "SHIFT";
        // 다시 플레이 (and the old RESTART): the current stage from its start.
        public event Action RestartRequested;
        public event Action RetryRequested, NextRequested, StageSelectRequested, TitleRequested;
        public bool IsConfigured => document != null && document.panelSettings != null && result != null;
        public UIDocument Document => document;
        public ResultKind Result { get; private set; }
        public bool IsCompletedVisible => Result == ResultKind.Clear || Result == ResultKind.FinalClear;
        public bool IsFailedVisible => Result == ResultKind.Failed;
        public MenuEntry ResultItem(string name) => result?.Root.Q<MenuEntry>(name);
        public string ResultHeading => result?.Heading;
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
            PixelText.SetSize(goalHint, 22);
            goalHint.style.color = Color.white;
            goalHint.style.backgroundColor = new Color(0.12f, 0.06f, 0.08f, 0.92f);
            stageSection.Add(goalHint);

            glitchHint = new Label { name = "glitch-hint" };
            glitchHint.pickingMode = PickingMode.Ignore;
            glitchHint.style.position = Position.Absolute;
            glitchHint.style.paddingLeft = glitchHint.style.paddingRight = 10;
            glitchHint.style.paddingTop = glitchHint.style.paddingBottom = 4;
            PixelText.SetSize(glitchHint, 16);
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
            PixelText.SetSize(comboTitle, 16);
            comboTitle.style.color = Color.white;
            comboTitle.style.marginRight = 6;
            comboBar.Add(comboTitle);
            comboReady = new Label("STRONG NEXT");
            PixelText.SetSize(comboReady, 16);
            comboReady.style.color = ComboReadyColor;
            comboReady.style.marginLeft = 8;
            comboBar.Add(comboReady);
            comboBar.style.display = DisplayStyle.None;
            stageSection.Add(comboBar);
            comboPips = Array.Empty<VisualElement>();

            result = new ResultPanel(this);
            if (fontAsset != null) result.Root.style.unityFontDefinition = FontDefinition.FromSDFFont(fontAsset);
            stageSection.Add(result.Root);
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
            HideResult();
        }

        public void ShowGoalHint(bool show)
        {
            transferHintVisible = false;
            if (goalHint == null) return;
            goalHint.text = "DEFEAT ALL ENEMIES TO OPEN THE EXIT";
            goalHint.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
        }

        // The transfer key follows the player's key settings.
        public void SetTransferKey(string key)
        {
            transferKey = string.IsNullOrEmpty(key) ? "SHIFT" : key.ToUpperInvariant();
            if (transferHintVisible && goalHint != null) goalHint.text = "REST AREA OPEN  >  WALK OR " + transferKey;
        }

        public string GoalHintText => goalHint?.text;

        public void ShowTransferHint(bool show)
        {
            if (goalHint == null || (!show && !transferHintVisible)) return;
            transferHintVisible = show;
            goalHint.text = "REST AREA OPEN  >  WALK OR " + transferKey;
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

        // Clear screen (planning doc 6.3) or the final clear screen (6.4). stageName may be null.
        public void ShowCompleted(bool finalClear = false, string stageName = null, bool hasNext = false, bool hasStageSelect = false)
        {
            ShowGoalHint(false);
            Result = finalClear ? ResultKind.FinalClear : ResultKind.Clear;
            result.ShowClear(finalClear, stageName, hasNext, hasStageSelect);
        }

        // Failure screen (planning doc 6.2): retry is selected first and needs no confirmation.
        public void ShowFailed(string retryDetail)
        {
            ShowGoalHint(false);
            Result = ResultKind.Failed;
            result.ShowFailed(retryDetail);
        }

        public void HideResult()
        {
            Result = ResultKind.None;
            result?.Hide();
            SetResultCovered(false);
            SetRestartEnabled(true);
        }

        // The stage list opens over the clear screen; the result heading and buttons step aside while it is open,
        // so the two menus never draw through each other.
        public void SetResultCovered(bool covered)
        {
            if (result == null) return;
            result.Root.style.visibility = covered ? Visibility.Hidden : StyleKeyword.Null;
        }

        public bool IsResultCovered => result != null && result.Root.resolvedStyle.visibility == Visibility.Hidden;

        // While a save or scene change runs, the result buttons wait.
        public void SetRestartEnabled(bool value) => result?.Root.SetEnabled(value);

        private sealed class ResultPanel : MenuPanel
        {
            private readonly Label heading, subtitle;
            private readonly MenuEntry next, again, select, title, retry, failTitle;
            private MenuEntry first;
            public string Heading => heading.text;

            public ResultPanel(StageFlowView owner) : base(ResultName, new Color(0.025f, 0.045f, 0.065f, 0.9f))
            {
                heading = MenuKit.Label("", 56, MenuKit.Accent, "result-heading");
                heading.style.unityFontStyleAndWeight = FontStyle.Bold;
                heading.style.marginBottom = 10;
                root.Add(heading);
                subtitle = MenuKit.Label("", 20, Color.white, "result-subtitle");
                subtitle.style.marginBottom = 26;
                root.Add(subtitle);
                retry = Add("fail-retry", "재도전", () => owner.RetryRequested?.Invoke());
                next = Add("result-next", "다음 스테이지", () => owner.NextRequested?.Invoke());
                again = Add("restart", "다시 플레이", () => owner.RestartRequested?.Invoke());
                select = Add("result-select", "스테이지 선택", () => owner.StageSelectRequested?.Invoke());
                title = Add("result-title", "타이틀로", () => owner.TitleRequested?.Invoke());
                failTitle = Add("fail-title", "타이틀로", () => owner.TitleRequested?.Invoke());
            }

            private MenuEntry Add(string name, string text, Action action)
            {
                var item = new MenuEntry(name, text, action, 340);
                root.Add(item);
                return Track(item);
            }

            private static void Visible(VisualElement e, bool visible) => e.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;

            public void ShowClear(bool finalClear, string stageName, bool hasNext, bool hasStageSelect)
            {
                heading.text = finalClear ? "ALL CLEAR" : "STAGE CLEAR";
                heading.style.color = MenuKit.Accent;
                subtitle.text = finalClear ? "모든 스테이지를 클리어했습니다" :
                    string.IsNullOrEmpty(stageName) ? "스테이지를 클리어했습니다" : stageName + " 클리어";
                again.Text = finalClear ? "마지막 스테이지 다시 플레이" : "다시 플레이";
                Visible(retry, false); Visible(failTitle, false);
                Visible(next, !finalClear && hasNext);
                Visible(again, true); Visible(select, hasStageSelect); Visible(title, true);
                Open(!finalClear && hasNext ? next : again);
            }

            public void ShowFailed(string retryDetail)
            {
                heading.text = "실패";
                heading.style.color = MenuKit.AccentRed;
                subtitle.text = "쓰러졌습니다";
                retry.Detail = retryDetail;
                Visible(retry, true); Visible(failTitle, true);
                Visible(next, false); Visible(again, false); Visible(select, false); Visible(title, false);
                Open(retry);
            }

            protected override MenuEntry DefaultFocus => first ?? FirstVisible();

            private void Open(MenuEntry focus)
            {
                first = focus;
                root.style.display = DisplayStyle.Flex;
                IsOpen = true;
                root.BringToFront();
                FocusDefault(false);
            }

            public void Hide()
            {
                IsOpen = false;
                root.style.display = DisplayStyle.None;
                if (root.focusController?.focusedElement is VisualElement focused && root.Contains(focused)) focused.Blur();
            }
        }
    }
}
