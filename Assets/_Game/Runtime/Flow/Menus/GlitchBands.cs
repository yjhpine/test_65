using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace ActionPlatformer.Flow
{
    // Torn horizontal bands in the teal/red glitch colours (planning doc 3.2: the glitch mood goes on backgrounds and
    // transitions). A new pattern every 1/30 s; nothing is drawn over text that must stay readable, the owner decides
    // where the layer sits and how strong it is. Intensity 0 hides every band.
    public sealed class GlitchBands
    {
        public const float PatternRate = 30f;
        private static readonly Color Teal = new Color(.2f, .94f, .82f), Red = new Color(.85f, .26f, .33f), White = new Color(.93f, .96f, .97f);
        private readonly VisualElement layer;
        private readonly List<VisualElement> bands = new List<VisualElement>();
        private readonly float maxAlpha;
        private long shownPattern = long.MinValue;

        public GlitchBands(string name, int count, float maxAlpha)
        {
            this.maxAlpha = maxAlpha;
            layer = new VisualElement { name = name, pickingMode = PickingMode.Ignore };
            layer.style.position = Position.Absolute;
            layer.style.left = layer.style.right = layer.style.top = layer.style.bottom = 0;
            layer.style.overflow = Overflow.Hidden;
            for (int i = 0; i < count; i++)
            {
                var band = new VisualElement { pickingMode = PickingMode.Ignore };
                band.style.position = Position.Absolute;
                band.style.display = DisplayStyle.None;
                layer.Add(band);
                bands.Add(band);
            }
        }

        public VisualElement Layer => layer;
        public int VisibleBands { get; private set; }

        // intensity 0..1 picks how many bands show and how opaque they are; time is real (unscaled) seconds.
        public int Show(float intensity, double time)
        {
            intensity = Mathf.Clamp01(intensity);
            if (intensity <= 0f)
            {
                if (VisibleBands > 0) foreach (var band in bands) band.style.display = DisplayStyle.None;
                VisibleBands = 0;
                shownPattern = long.MinValue;
                return 0;
            }
            long pattern = (long)System.Math.Floor(time * PatternRate);
            if (pattern == shownPattern) return VisibleBands;
            shownPattern = pattern;
            var random = new System.Random(unchecked((int)(pattern * 7919L)));
            int wanted = Mathf.Max(1, Mathf.RoundToInt(bands.Count * intensity));
            VisibleBands = 0;
            for (int i = 0; i < bands.Count; i++)
            {
                var band = bands[i];
                if (i >= wanted) { band.style.display = DisplayStyle.None; continue; }
                float roll = (float)random.NextDouble();
                var color = roll < .45f ? Teal : roll < .9f ? Red : White;
                color.a = maxAlpha * intensity * (.35f + .65f * (float)random.NextDouble());
                band.style.backgroundColor = color;
                band.style.top = Length.Percent((float)random.NextDouble() * 98f);
                band.style.height = 2f + (float)random.NextDouble() * 22f;
                float shift = ((float)random.NextDouble() - .5f) * 30f;
                band.style.left = Length.Percent(shift);
                band.style.width = Length.Percent(40f + (float)random.NextDouble() * 80f);
                band.style.display = DisplayStyle.Flex;
                VisibleBands++;
            }
            return VisibleBands;
        }
    }
}
