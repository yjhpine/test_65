using UnityEngine;
using UnityEngine.UIElements;

namespace ActionPlatformer.Cinematics
{
    [ExecuteAlways, RequireComponent(typeof(UIDocument))]
    public sealed class CutsceneView : MonoBehaviour
    {
        [SerializeField] private UIDocument document;
        private VisualElement root, panel, portrait, fade;
        private Label speaker, body, hint, bubbleHint;
        private SpeechBubbleView bubble;
        public bool ShowSkipHint { get; set; } = true;
        public string VisibleText { get; private set; } = "";
        public bool IsDialogueVisible => (panel != null && panel.style.display == DisplayStyle.Flex) || IsBubbleVisible;
        public bool IsBubbleVisible => bubble != null && bubble.IsVisible;
        public Rect BubbleBounds => bubble != null ? bubble.Bounds : default;
        public bool IsConfigured => document != null && document.panelSettings != null && document.visualTreeAsset != null;
        private bool Bind()
        {
            if (document == null) document = GetComponent<UIDocument>();
            if (!IsConfigured) return false;
            if (root == document.rootVisualElement && panel != null) return true;
            root = document.rootVisualElement;
            panel = root.Q("dialogue"); portrait = root.Q("portrait"); fade = root.Q("fade");
            speaker = root.Q<Label>("speaker"); body = root.Q<Label>("body"); hint = root.Q<Label>("hint");
            if (panel == null || portrait == null || fade == null || speaker == null || body == null || hint == null) return false;
            var layer = root.Q("speech-layer");
            if (layer == null)
            {
                layer = new VisualElement { name = "speech-layer" }; layer.AddToClassList("speech-layer");
                fade.parent.Insert(fade.parent.IndexOf(fade), layer);
            }
            layer.Clear();
            bubble = new SpeechBubbleView(layer);
            bubbleHint = new Label { name = "speech-hint" }; bubbleHint.AddToClassList("speech-hint");
            layer.Add(bubbleHint);
            root.pickingMode = PickingMode.Ignore;
            foreach (var element in root.Query<VisualElement>().ToList()) element.pickingMode = PickingMode.Ignore;
            return true;
        }
        private void OnEnable() { Bind(); Clear(); }
        private void OnDisable() => Clear();
        public void RenderDialogue(DialoguePlayback playback, DialogueSpeaker actor, bool asBubble, bool preview)
        {
            if (!Bind()) return;
            var data = playback.Data;
            if (data == null) { HideDialogue(); return; }
            VisibleText = playback.VisibleText;
            string instructions = preview ? "미리보기" : playback.IsWaiting
                ? (ShowSkipHint ? "Enter / Space / 클릭: 계속    Esc: 건너뛰기" : "Enter / Space / 클릭: 계속")
                : (ShowSkipHint ? "Esc: 건너뛰기" : "");
            if (asBubble)
            {
                panel.style.display = DisplayStyle.None;
                bubble.Show(actor.DisplayName, data.Text, VisibleText);
                bubbleHint.text = instructions; bubbleHint.style.display = DisplayStyle.Flex;
            }
            else
            {
                bubble.Hide(); bubbleHint.style.display = DisplayStyle.None;
                panel.style.display = DisplayStyle.Flex;
                speaker.text = data.Speaker; body.text = VisibleText; hint.text = instructions;
                portrait.style.backgroundImage = data.Portrait == null ? StyleKeyword.None : new StyleBackground(data.Portrait);
                portrait.style.display = data.Portrait == null ? DisplayStyle.None : DisplayStyle.Flex;
            }
        }
        public void UpdateBubblePosition(Camera camera, DialogueSpeaker actor)
        { if (actor != null) bubble?.Position(camera, actor.Anchor); }
        public void HideDialogue()
        {
            if (panel != null) panel.style.display = DisplayStyle.None;
            bubble?.Hide();
            if (bubbleHint != null) bubbleHint.style.display = DisplayStyle.None;
            VisibleText = "";
        }
        public void ShowFade(Color color, float opacity)
        {
            if (!Bind()) return;
            color.a *= Mathf.Clamp01(opacity); fade.style.backgroundColor = color;
        }
        public void Clear() { HideDialogue(); if (fade != null) fade.style.backgroundColor = Color.clear; }
    }
}
