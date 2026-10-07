using UnityEngine;
using UnityEngine.UIElements;

namespace ActionPlatformer.Cinematics
{
    // Presentation only. The invisible full line reserves layout space during typing.
    public sealed class SpeechBubbleView
    {
        private readonly VisualElement layer, box, tail;
        private readonly Label name, measure, body;
        private Vector2 tailBase, tailTip;
        private string fullText;
        public bool IsVisible => box.style.display == DisplayStyle.Flex;
        public Rect Bounds => box.layout;

        public SpeechBubbleView(VisualElement layer)
        {
            this.layer = layer;
            tail = new VisualElement { name = "speech-tail", pickingMode = PickingMode.Ignore };
            tail.AddToClassList("speech-tail");
            tail.generateVisualContent += DrawTail;
            box = new VisualElement { name = "speech-bubble", pickingMode = PickingMode.Ignore };
            box.AddToClassList("speech-bubble");
            name = new Label { name = "speech-name", enableRichText = false };
            name.AddToClassList("speaker");
            var content = new VisualElement();
            content.AddToClassList("speech-content");
            measure = new Label { name = "speech-measure", enableRichText = false };
            measure.AddToClassList("speech-text"); measure.AddToClassList("speech-measure");
            body = new Label { name = "speech-body", enableRichText = false };
            body.AddToClassList("speech-text"); body.AddToClassList("speech-visible");
            content.Add(measure); content.Add(body);
            box.Add(name); box.Add(content);
            layer.Add(tail); layer.Add(box);
            foreach (var element in layer.Query<VisualElement>().ToList()) element.pickingMode = PickingMode.Ignore;
            Hide();
        }
        public void Show(string speaker, string text, string visible)
        {
            name.text = speaker;
            fullText = text ?? "";
            measure.text = fullText; body.text = visible;
            box.style.display = DisplayStyle.Flex;
            tail.style.display = DisplayStyle.Flex;
        }
        public void Hide() { box.style.display = DisplayStyle.None; tail.style.display = DisplayStyle.None; }
        public void Position(Camera camera, Transform anchor)
        {
            if (!IsVisible || camera == null || anchor == null || layer.panel == null) return;
            float width = layer.contentRect.width, height = layer.contentRect.height;
            if (width <= 32 || height <= 32 || float.IsNaN(width)) return;
            float maxWidth = Mathf.Min(420, width - 32);
            float textWidth = measure.MeasureTextSize(fullText, 0, VisualElement.MeasureMode.Undefined,
                0, VisualElement.MeasureMode.Undefined).x;
            float bubbleWidth = Mathf.Clamp(textWidth + 36, Mathf.Min(180, maxWidth), maxWidth);
            box.style.width = bubbleWidth;
            float textHeight = measure.MeasureTextSize(fullText, Mathf.Max(1, bubbleWidth - 36),
                VisualElement.MeasureMode.Exactly, 0, VisualElement.MeasureMode.Undefined).y;
            float nameHeight = name.MeasureTextSize(name.text, bubbleWidth - 36,
                VisualElement.MeasureMode.Exactly, 0, VisualElement.MeasureMode.Undefined).y;
            float bubbleHeight = Mathf.Min(textHeight + nameHeight + 40, height - 32);
            // Reserve the entire line before typing so the tail and box do not shift with each character.
            box.style.height = bubbleHeight;
            Vector2 point = RuntimePanelUtils.CameraTransformWorldToPanel(layer.panel, anchor.position, camera);
            point = layer.WorldToLocal(point);
            float x = Mathf.Clamp(point.x - bubbleWidth * .5f, 16, width - 16 - bubbleWidth);
            float y = Mathf.Clamp(point.y - bubbleHeight - 18, 16, height - 16 - bubbleHeight);
            box.style.left = x; box.style.top = y;
            var rect = new Rect(x, y, bubbleWidth, bubbleHeight);
            Vector2 delta = point - rect.center;
            if (delta.sqrMagnitude < .01f) delta = Vector2.down;
            float scale = Mathf.Min(bubbleWidth * .5f / Mathf.Max(.001f, Mathf.Abs(delta.x)),
                bubbleHeight * .5f / Mathf.Max(.001f, Mathf.Abs(delta.y)));
            tailBase = rect.center + delta * scale;
            Vector2 direction = delta.normalized;
            tailTip = tailBase + direction * Mathf.Min(20, Vector2.Distance(tailBase, point));
            tailTip.x = Mathf.Clamp(tailTip.x, 4, width - 4);
            tailTip.y = Mathf.Clamp(tailTip.y, 4, height - 4);
            tail.MarkDirtyRepaint();
        }
        private void DrawTail(MeshGenerationContext context)
        {
            Vector2 direction = tailTip - tailBase;
            if (direction.sqrMagnitude < .01f) return;
            Vector2 side = new Vector2(-direction.y, direction.x).normalized * 8;
            var painter = context.painter2D;
            painter.fillColor = new Color(12f / 255, 22f / 255, 32f / 255, .97f);
            painter.BeginPath(); painter.MoveTo(tailBase - side); painter.LineTo(tailTip);
            painter.LineTo(tailBase + side); painter.ClosePath(); painter.Fill();
        }
    }
}
