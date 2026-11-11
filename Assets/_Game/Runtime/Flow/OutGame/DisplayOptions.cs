using System;
using System.Collections.Generic;
using UnityEngine;

namespace ActionPlatformer.Flow
{
    // Screen mode and resolution choices (planning doc 5.1) and the timed keep-or-revert question after applying them.
    public static class DisplayOptions
    {
        public static readonly string[] ModeNames = { "창 모드", "전체 화면" };

        // Distinct sizes the monitor supports, largest last; the current size is always offered.
        public static List<Vector2Int> Resolutions()
        {
            var list = new List<Vector2Int>();
            foreach (var r in Screen.resolutions)
            {
                var size = new Vector2Int(r.width, r.height);
                if (size.x >= 640 && size.y >= 360 && !list.Contains(size)) list.Add(size);
            }
            var current = Current;
            if (!list.Contains(current)) list.Add(current);
            list.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
            return list;
        }

        public static Vector2Int Current => Screen.fullScreenMode == FullScreenMode.Windowed
            ? new Vector2Int(Screen.width, Screen.height)
            : new Vector2Int(Screen.currentResolution.width, Screen.currentResolution.height);

        public static bool CurrentFullScreen => Screen.fullScreenMode != FullScreenMode.Windowed;
        public static string Name(Vector2Int size) => size.x + " × " + size.y;

        public static void Apply(bool fullScreen, Vector2Int size)
        {
            if (Application.isEditor) return;
            if (size.x <= 0 || size.y <= 0) size = Current;
            Screen.SetResolution(size.x, size.y, fullScreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
        }

        // The project default: full-screen window at the monitor's own resolution.
        public static void ApplyProjectDefault()
        {
            if (Application.isEditor) return;
            Screen.SetResolution(Display.main.systemWidth, Display.main.systemHeight, FullScreenMode.FullScreenWindow);
        }

        // The saved display setting is applied once at start-up in a player build; the Editor's Game view keeps its own size.
        public static void ApplySaved(SettingsData settings)
        {
            if (Application.isEditor || settings == null || settings.ScreenMode == ScreenModeSetting.ProjectDefault) return;
            Apply(settings.ScreenMode == ScreenModeSetting.FullScreen,
                settings.HasResolution ? new Vector2Int(settings.resolutionWidth, settings.resolutionHeight) : Current);
        }
    }

    // Counts down after a display change; without "keep" in time the previous values return (planning doc 5.1).
    public sealed class DisplayConfirmation
    {
        public const float Seconds = 15f;
        private readonly Action keep, revert;
        private float remaining;
        public bool Active { get; private set; }
        public int SecondsLeft => Mathf.CeilToInt(remaining);

        public DisplayConfirmation(Action keep, Action revert) { this.keep = keep; this.revert = revert; }

        public void Begin() { remaining = Seconds; Active = true; }

        // Returns true on the tick that ran out and reverted.
        public bool Tick(float unscaledDeltaTime)
        {
            if (!Active) return false;
            remaining -= unscaledDeltaTime;
            if (remaining > 0f) return false;
            Revert();
            return true;
        }

        public void Keep() { if (!Active) return; Active = false; keep?.Invoke(); }
        public void Revert() { if (!Active) return; Active = false; revert?.Invoke(); }
    }
}
