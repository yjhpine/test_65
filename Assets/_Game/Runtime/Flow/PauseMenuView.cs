using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace ActionPlatformer.Flow
{
    // The pause menu section beside the stage HUD under the shared root. Only 계속하기 does anything yet.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    public sealed class PauseMenuView : MonoBehaviour
    {
        public const string SectionName = "pause-menu";
        [SerializeField] private UIDocument document;
        [Tooltip("Needs Korean glyphs; the HUD's default font has none.")]
        [SerializeField] private Font font;
        private VisualElement section;
        private Button continueButton;
        private bool focusPending;
        public event Action ContinueRequested;
        public bool IsVisible => section != null && section.style.display == DisplayStyle.Flex;

        private void OnEnable()
        {
            if (document == null || document.panelSettings == null) return;
            // Replace only this section; the root and its other sections belong to their own views.
            section?.RemoveFromHierarchy();
            section = new VisualElement { name = SectionName };
            section.style.position = Position.Absolute;
            section.style.left = section.style.right = section.style.top = section.style.bottom = 0;
            section.style.alignItems = Align.Center;
            section.style.justifyContent = Justify.Center;
            section.style.backgroundColor = new Color(0.025f, 0.045f, 0.065f, 0.72f);
            if (font != null) section.style.unityFontDefinition = FontDefinition.FromFont(font);
            continueButton = AddButton("pause-continue", "계속하기", RequestContinue);
            AddButton("pause-options", "옵션", null);
            AddButton("pause-save-quit", "저장 후 종료", null);
            // A section shown this frame cannot take focus until its style is resolved; retry after layout.
            continueButton.RegisterCallback<GeometryChangedEvent>(OnContinueLayout);
            document.rootVisualElement.Add(section);
            Show(false);
        }

        private Button AddButton(string name, string text, Action clicked)
        {
            var button = clicked != null ? new Button(clicked) : new Button();
            button.name = name;
            button.text = text;
            button.style.width = 260;
            button.style.height = 56;
            button.style.fontSize = 24;
            button.style.marginTop = button.style.marginBottom = 8;
            section.Add(button);
            return button;
        }

        public void Show(bool visible)
        {
            if (section == null) return;
            section.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            focusPending = visible;
            if (visible)
            {
                // Sections added later (the stage HUD on re-enable) must not draw over the menu.
                section.BringToFront();
                FocusContinue();
            }
            else if (section.focusController?.focusedElement is VisualElement focused && section.Contains(focused)) focused.Blur();
        }

        private void FocusContinue()
        {
            continueButton.Focus();
            if (continueButton.focusController?.focusedElement == continueButton) focusPending = false;
        }

        private void OnContinueLayout(GeometryChangedEvent _)
        {
            if (focusPending && IsVisible) FocusContinue();
        }

        private void RequestContinue() => ContinueRequested?.Invoke();

        private void OnDisable()
        {
            continueButton?.UnregisterCallback<GeometryChangedEvent>(OnContinueLayout);
            section?.RemoveFromHierarchy();
            section = null;
            continueButton = null;
            focusPending = false;
        }
    }
}
