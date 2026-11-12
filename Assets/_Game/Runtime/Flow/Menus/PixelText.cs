using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.UIElements;

namespace ActionPlatformer.Flow
{
    // Galmuri11 is a 12px pixel font drawn from a point-filtered bitmap atlas (2026-11-12). Its pixels land on whole screen
    // pixels only when the text is drawn at 12, 24, 36... screen pixels, so every UI text keeps its authored size in panel
    // units and is drawn at the nearest whole multiple of 12 screen pixels for the panel's current scale.
    public static class PixelText
    {
        public const float NativeSize = 12f;

        private sealed class Entry { public float Design; public float AppliedScale; }
        private static readonly ConditionalWeakTable<TextElement, Entry> texts = new ConditionalWeakTable<TextElement, Entry>();
        private static readonly ConditionalWeakTable<VisualElement, Entry> roots = new ConditionalWeakTable<VisualElement, Entry>();

        // The panel-unit font size that draws designSize at the nearest whole multiple of 12 screen pixels (at least 12).
        public static float Snap(float designSize, float pixelsPerPoint)
        {
            if (pixelsPerPoint <= 0f) return designSize;
            float multiple = Mathf.Max(1f, Mathf.Floor(designSize * pixelsPerPoint / NativeSize + .5f));
            return NativeSize * multiple / pixelsPerPoint;
        }

        // Sets a text's authored size; it is drawn snapped as soon as it is on a panel.
        public static void SetSize(TextElement text, float designSize)
        {
            if (!texts.TryGetValue(text, out var entry))
            {
                entry = new Entry();
                texts.Add(text, entry);
                text.RegisterCallback<AttachToPanelEvent>(OnAttach);
            }
            entry.Design = designSize;
            entry.AppliedScale = 0f;
            if (!Apply(text, entry)) text.style.fontSize = designSize;
        }

        public static float DesignSize(TextElement text) =>
            texts.TryGetValue(text, out var entry) ? entry.Design : text.resolvedStyle.fontSize;

        // Re-snaps the texts under root. Without adoptStyled it only works when the panel scale changed (a resolution
        // change) and only on texts sized through SetSize. With adoptStyled, texts sized by a style sheet alone (the
        // cutscene UI) are taken in with their styled size the first time they are seen.
        public static void Refresh(VisualElement root, bool adoptStyled = false)
        {
            if (root == null || root.panel == null) return;
            float scale = root.scaledPixelsPerPoint;
            if (!roots.TryGetValue(root, out var last)) { last = new Entry(); roots.Add(root, last); }
            if (!adoptStyled && Mathf.Approximately(last.AppliedScale, scale)) return;
            last.AppliedScale = scale;
            root.Query<TextElement>().ForEach(text =>
            {
                if (!texts.TryGetValue(text, out var entry))
                {
                    if (!adoptStyled) return;
                    entry = new Entry { Design = text.resolvedStyle.fontSize };
                    texts.Add(text, entry);
                }
                Apply(text, entry);
            });
        }

        private static void OnAttach(AttachToPanelEvent evt)
        {
            if (evt.currentTarget is TextElement text && texts.TryGetValue(text, out var entry))
            {
                entry.AppliedScale = 0f;
                Apply(text, entry);
            }
        }

        private static bool Apply(TextElement text, Entry entry)
        {
            if (text.panel == null) return false;
            float scale = text.scaledPixelsPerPoint;
            if (scale <= 0f) return false;
            if (!Mathf.Approximately(scale, entry.AppliedScale))
            {
                entry.AppliedScale = scale;
                text.style.fontSize = Snap(entry.Design, scale);
            }
            return true;
        }
    }
}
