using ActionPlatformer.Units.Bosses;
using System.Collections.Generic;

namespace ActionPlatformer.Editor
{
    // Text shown by UnitEditor for a running boss. Kept apart from the GUI code so it can be tested.
    public static class BossDebugText
    {
        public const string NoTrace = "(아직 선택 기록이 없습니다)";

        public static IReadOnlyList<(string label, string value)> Rows(IBossActionState state)
        {
            if (state == null) return new (string, string)[0];
            return new[]
            {
                ("Boss State", state.State.ToString()),
                ("Boss Pattern", state.Pattern == null ? "None" : state.Pattern.DisplayName),
                ("Boss Phase", state.Phase.ToString()),
            };
        }

        public static string Format(IBossActionState state)
        {
            var lines = new List<string>();
            foreach (var (label, value) in Rows(state)) lines.Add(label + ": " + value);
            return string.Join("\n", lines);
        }

        public static string Trace(IBossActionState state) =>
            state == null || string.IsNullOrEmpty(state.LastSelectionTrace) ? NoTrace : state.LastSelectionTrace;
    }
}
