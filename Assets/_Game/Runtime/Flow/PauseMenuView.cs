using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace ActionPlatformer.Flow
{
    // The pause menu section beside the stage HUD under the shared root (planning doc 6.1):
    // 계속하기, 설정, 조작 안내, 현재 스테이지 재시작, 타이틀로.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    public sealed class PauseMenuView : MonoBehaviour
    {
        public const string SectionName = "pause-menu";
        public const string ContinueName = "pause-continue", SettingsName = "pause-settings", ControlsName = "pause-controls",
            RestartName = "pause-restart", TitleName = "pause-title";
        [SerializeField] private UIDocument document;
        [Tooltip("SDF font asset with Korean glyphs, so text stays sharp at any scale.")]
        [SerializeField] private UnityEngine.TextCore.Text.FontAsset fontAsset;
        private PausePanel panel;
        public event Action ContinueRequested, SettingsRequested, ControlsRequested, RestartRequested, TitleRequested;
        public bool IsVisible => panel != null && panel.Root.style.display == DisplayStyle.Flex;
        public MenuEntry Item(string name) => panel?.Root.Q<MenuEntry>(name);

        private sealed class PausePanel : MenuPanel
        {
            public readonly MenuEntry Continue;
            public PausePanel(PauseMenuView owner) : base(SectionName, new Color(0.025f, 0.045f, 0.065f, 0.72f))
            {
                root.Add(MenuKit.Label("일시정지", 36, MenuKit.Accent));
                Continue = Add(ContinueName, "계속하기", () => owner.ContinueRequested?.Invoke());
                Add(SettingsName, "설정", () => owner.SettingsRequested?.Invoke());
                Add(ControlsName, "조작 안내", () => owner.ControlsRequested?.Invoke());
                Add(RestartName, "현재 스테이지 재시작", () => owner.RestartRequested?.Invoke());
                Add(TitleName, "타이틀로", () => owner.TitleRequested?.Invoke());
            }

            private MenuEntry Add(string name, string text, Action action)
            {
                var item = new MenuEntry(name, text, action, 300);
                root.Add(item);
                return Track(item);
            }

            protected override MenuEntry DefaultFocus => Continue;
            public void SetOpen(bool open) => IsOpen = open;
        }

        private void OnEnable()
        {
            if (document == null || document.panelSettings == null) return;
            // Replace only this section; the root and its other sections belong to their own views.
            panel?.Root.RemoveFromHierarchy();
            panel = new PausePanel(this);
            if (fontAsset != null) panel.Root.style.unityFontDefinition = FontDefinition.FromSDFFont(fontAsset);
            document.rootVisualElement.Add(panel.Root);
            Show(false);
        }

        public void Show(bool visible) => Show(visible, false);

        // keepFocus returns to the item that opened a sub-page, as the planning doc asks for a return to the caller.
        public void Show(bool visible, bool keepFocus)
        {
            if (panel == null) return;
            var section = panel.Root;
            section.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            panel.SetOpen(visible);
            if (visible)
            {
                // Sections added later (the stage HUD on re-enable) must not draw over the menu.
                section.BringToFront();
                panel.FocusDefault(keepFocus);
            }
            else if (section.focusController?.focusedElement is VisualElement focused && section.Contains(focused)) focused.Blur();
        }

        private void OnDisable()
        {
            panel?.Root.RemoveFromHierarchy();
            panel = null;
        }
    }
}
