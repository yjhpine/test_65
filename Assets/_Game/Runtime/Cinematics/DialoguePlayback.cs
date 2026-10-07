using System;
using System.Globalization;
using UnityEngine.Timeline;

namespace ActionPlatformer.Cinematics
{
    // Per-runner state. Clip placement, not its reusable asset, identifies a line.
    public sealed class DialoguePlayback
    {
        private TimelineClip waiting;
        private double waitStarted;
        private int[] boundaries = Array.Empty<int>();
        private bool revealAll;
        private string sourceText;
        public TimelineClip Current { get; private set; }
        public DialogueClip Data => Current?.asset as DialogueClip;
        public int VisibleCharacters { get; private set; }
        public string VisibleText { get; private set; } = "";
        public bool IsFullyRevealed => Current == null || VisibleCharacters >= boundaries.Length;
        public bool IsWaiting => Current != null && waiting == Current;

        public void BeginWait(TimelineClip clip, double now)
        { waiting = clip; waitStarted = now; revealAll = false; }
        public void EndWait() { waiting = null; revealAll = false; }
        public void Evaluate(TimelineClip clip, double localTime, double now, bool preview)
        {
            if (clip == null || !(clip.asset is DialogueClip data)) { Hide(); return; }
            if (Current != clip || sourceText != (data.Text ?? ""))
            {
                Current = clip;
                sourceText = data.Text ?? "";
                boundaries = StringInfo.ParseCombiningCharacters(sourceText);
                VisibleCharacters = -1;
                revealAll = false;
            }
            // The clock is supplied by the runner; the UI never advances a dialogue.
            double elapsed = waiting == clip ? now - waitStarted : localTime;
            int count = preview || revealAll || data.CharactersPerSecond <= 0 ? boundaries.Length :
                (int)Math.Min(boundaries.Length, Math.Max(0, elapsed * data.CharactersPerSecond));
            SetVisible(count);
        }
        public void Reveal() { revealAll = true; if (Current != null) SetVisible(boundaries.Length); }
        private void SetVisible(int count)
        {
            if (VisibleCharacters == count) return;
            VisibleCharacters = count;
            string text = Data?.Text ?? "";
            VisibleText = count >= boundaries.Length ? text : text.Substring(0, boundaries[count]);
        }
        public void Hide() { Current = null; VisibleCharacters = 0; VisibleText = ""; revealAll = false; }
        public void Clear() { Hide(); EndWait(); }
    }
}
