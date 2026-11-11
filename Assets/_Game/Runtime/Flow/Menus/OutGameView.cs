using UnityEngine;
using UnityEngine.UIElements;

namespace ActionPlatformer.Flow
{
    // The out-game sections beside the stage HUD and the pause menu under the shared root: title, stage list, settings,
    // controls guide, confirmation dialogs, the loading cover and the unsaved-progress notice.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    public sealed class OutGameView : MonoBehaviour
    {
        public const string LoadingName = "loading";
        public const string UnsavedName = "unsaved-notice";
        [SerializeField] private UIDocument document;
        [Tooltip("SDF font asset with Korean glyphs, so text stays sharp at any scale.")]
        [SerializeField] private UnityEngine.TextCore.Text.FontAsset fontAsset;
        private VisualElement section, loading, unsaved;
        private Label loadingText;

        public MenuStack Stack { get; } = new MenuStack();
        public TitlePanel Title { get; private set; }
        public StageSelectPanel StageSelect { get; private set; }
        public SettingsPanel Settings { get; private set; }
        public ControlsGuidePanel Controls { get; private set; }
        public ConfirmDialog Dialog { get; private set; }
        public bool IsConfigured => section != null;
        public bool IsLoadingVisible => loading != null && loading.style.display == DisplayStyle.Flex;
        public bool IsUnsavedVisible => unsaved != null && unsaved.style.display == DisplayStyle.Flex;
        public bool IsInteractable { get; private set; } = true;
        // Raised after the sections are (re)built, so the owner can wire the new pages.
        public event System.Action Built;

        private void OnEnable()
        {
            if (document == null || document.panelSettings == null) return;
            section?.RemoveFromHierarchy();
            Stack.CloseAll();
            section = new VisualElement { name = "out-game", pickingMode = PickingMode.Ignore };
            section.style.position = Position.Absolute;
            section.style.left = section.style.right = section.style.top = section.style.bottom = 0;
            if (fontAsset != null) section.style.unityFontDefinition = FontDefinition.FromSDFFont(fontAsset);
            Title = Add(new TitlePanel());
            StageSelect = Add(new StageSelectPanel());
            Settings = Add(new SettingsPanel());
            Controls = Add(new ControlsGuidePanel());
            Dialog = Add(new ConfirmDialog());

            unsaved = MenuKit.Label("저장되지 않은 진행이 있습니다", 16, new Color(1f, .78f, .5f), UnsavedName);
            unsaved.pickingMode = PickingMode.Ignore;
            unsaved.style.position = Position.Absolute;
            unsaved.style.right = 24; unsaved.style.top = 20;
            unsaved.style.backgroundColor = new Color(.12f, .06f, .08f, .9f);
            MenuKit.Pad(unsaved, 12, 6);
            unsaved.style.display = DisplayStyle.None;
            section.Add(unsaved);

            loading = MenuKit.Overlay(LoadingName, Color.black);
            loading.pickingMode = PickingMode.Position;
            loadingText = MenuKit.Label("불러오는 중", 22, MenuKit.Text, "loading-text");
            loadingText.style.position = Position.Absolute;
            loadingText.style.right = 40; loadingText.style.bottom = 32;
            loading.Add(loadingText);
            section.Add(loading);

            document.rootVisualElement.Add(section);
            Built?.Invoke();
        }

        private T Add<T>(T panel) where T : MenuPanel
        {
            section.Add(panel.Root);
            return panel;
        }

        // Esc: true when an out-game page used it.
        public bool HandleBack() => IsInteractable && Stack.Back();

        public void Open(MenuPanel panel, System.Action onClosed = null)
        {
            if (section == null) return;
            section.BringToFront();
            Stack.Push(panel, onClosed);
            loading.BringToFront();
        }

        public void Close(MenuPanel panel) => Stack.Close(panel);
        public bool IsOpen(MenuPanel panel) => panel != null && Stack.Contains(panel);
        public bool HasOpenPanel => Stack.Count > 0;

        public void ShowDialog(string title, string message, params ConfirmDialog.Choice[] choices) =>
            ShowDialog(title, message, null, choices);

        public void ShowDialog(string title, string message, System.Action onClosed, params ConfirmDialog.Choice[] choices)
        {
            if (Dialog == null) return;
            if (Stack.Contains(Dialog)) Stack.Close(Dialog);
            Dialog.Setup(title, message, choices);
            Open(Dialog, onClosed);
        }

        // The cover fades over everything and swallows clicks while a scene changes.
        public void SetCover(float alpha, bool showText)
        {
            if (loading == null) return;
            loading.style.display = alpha > 0f ? DisplayStyle.Flex : DisplayStyle.None;
            loading.style.backgroundColor = new Color(0, 0, 0, Mathf.Clamp01(alpha));
            loadingText.style.display = showText ? DisplayStyle.Flex : DisplayStyle.None;
            if (alpha > 0f) { section.BringToFront(); loading.BringToFront(); }
        }

        public void SetInteractable(bool value)
        {
            IsInteractable = value;
            section?.SetEnabled(value);
        }

        public void ShowUnsaved(bool visible)
        {
            if (unsaved != null) unsaved.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void OnDisable()
        {
            Stack.CloseAll();
            section?.RemoveFromHierarchy();
            section = null;
        }
    }
}
