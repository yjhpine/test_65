using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ActionPlatformer.Flow
{
    public enum RebindOutcome { Applied, Cancelled, Conflict }

    // Key changes for the player's keyboard and mouse controls (planning doc 5.4). Each slot is the primary key of one
    // action; secondary keys, aiming, menu keys and Esc stay fixed. The overrides live on the actions asset itself,
    // which a single PlayerInput uses directly, and are saved as JSON with the settings.
    public sealed class KeyBindings
    {
        public const string MapName = "Player";
        public const string EscapePath = "<Keyboard>/escape";

        public sealed class Slot
        {
            public readonly string Label, Action, Part, DefaultPath;
            public int BindingIndex = -1;
            public Slot(string label, string action, string part, string defaultPath) { Label = label; Action = action; Part = part; DefaultPath = defaultPath; }
        }

        private static readonly string[] MouseButtons =
            { "<Mouse>/leftButton", "<Mouse>/rightButton", "<Mouse>/middleButton", "<Mouse>/forwardButton", "<Mouse>/backButton" };

        private readonly InputActionAsset asset;
        private readonly InputActionMap map;
        private readonly List<Slot> slots = new List<Slot>();
        private InputActionRebindingExtensions.RebindingOperation operation;

        public KeyBindings(InputActionAsset asset)
        {
            this.asset = asset;
            map = asset != null ? asset.FindActionMap(MapName) : null;
            Define("왼쪽 이동", "Move", "left", "<Keyboard>/a");
            Define("오른쪽 이동", "Move", "right", "<Keyboard>/d");
            Define("점프", "Jump", null, "<Keyboard>/space");
            Define("공격", "Attack", null, "<Mouse>/leftButton");
            Define("글리치", "Glitch", null, "<Keyboard>/leftShift");
        }

        public InputActionAsset Asset => asset;
        public IReadOnlyList<Slot> Slots => slots;
        public bool IsConfigured => map != null && slots.TrueForAll(s => s.BindingIndex >= 0);
        public bool IsRebinding => operation != null;
        public int RebindingSlot { get; private set; } = -1;
        // The frame a rebind ended: its Esc or click must not also reach the menus.
        public int EndedFrame { get; private set; } = -1;
        public double EndedAt { get; private set; } = double.NegativeInfinity;

        private void Define(string label, string action, string part, string defaultPath)
        {
            var slot = new Slot(label, action, part, defaultPath);
            var inputAction = map?.FindAction(action);
            if (inputAction != null)
                for (int i = 0; i < inputAction.bindings.Count; i++)
                {
                    var binding = inputAction.bindings[i];
                    if (binding.isComposite || binding.path != defaultPath) continue;
                    if (part != null && (!binding.isPartOfComposite || binding.name != part)) continue;
                    slot.BindingIndex = i;
                    break;
                }
            slots.Add(slot);
        }

        private InputAction ActionOf(Slot slot) => map?.FindAction(slot.Action);

        public string Display(int index)
        {
            if (index < 0 || index >= slots.Count) return "";
            var slot = slots[index];
            var action = ActionOf(slot);
            if (action == null || slot.BindingIndex < 0) return "";
            return Pretty(action.GetBindingDisplayString(slot.BindingIndex));
        }

        // Friendlier names for the few keys whose short display names read badly in Korean text.
        public static string Pretty(string display)
        {
            switch (display)
            {
                case "LMB": return "마우스 왼쪽";
                case "RMB": return "마우스 오른쪽";
                case "MMB": return "마우스 가운데";
                case "Left": return "←";
                case "Right": return "→";
                case "Up": return "↑";
                case "Down": return "↓";
                default: return display;
            }
        }

        public string EffectivePath(int index)
        {
            var slot = slots[index];
            var action = ActionOf(slot);
            return action == null || slot.BindingIndex < 0 ? null : action.bindings[slot.BindingIndex].effectivePath;
        }

        // Every keyboard/mouse key of an action, e.g. "A / ←" for one direction of Move.
        public string DisplayAll(string actionName, string part = null)
        {
            var action = map?.FindAction(actionName);
            if (action == null) return "";
            var names = new List<string>();
            for (int i = 0; i < action.bindings.Count; i++)
            {
                var binding = action.bindings[i];
                if (binding.isComposite || (part != null && binding.name != part) || (part == null && binding.isPartOfComposite)) continue;
                var path = binding.effectivePath;
                if (string.IsNullOrEmpty(path) || !(path.StartsWith("<Keyboard>") || path.StartsWith("<Mouse>"))) continue;
                if (path == "<Mouse>/position") continue;
                string text = Pretty(action.GetBindingDisplayString(i));
                if (!string.IsNullOrEmpty(text) && !names.Contains(text)) names.Add(text);
            }
            return string.Join(" / ", names);
        }

        public void Load(string json)
        {
            if (asset == null) return;
            asset.RemoveAllBindingOverrides();
            if (string.IsNullOrWhiteSpace(json)) return;
            try { asset.LoadBindingOverridesFromJson(json); }
            catch (Exception e)
            {
                asset.RemoveAllBindingOverrides();
                Debug.LogWarning("Saved key settings could not be read; default keys are used: " + e.Message);
            }
        }

        // Applies the same overrides to a copy of the actions (a second PlayerInput gets one).
        public void ApplyTo(InputActionAsset other)
        {
            if (other == null || other == asset || asset == null) return;
            other.RemoveAllBindingOverrides();
            string json = Save();
            if (!string.IsNullOrEmpty(json)) other.LoadBindingOverridesFromJson(json);
        }

        public string Save()
        {
            if (asset == null) return "";
            foreach (var action in asset)
                foreach (var binding in action.bindings)
                    if (binding.hasOverrides) return asset.SaveBindingOverridesAsJson();
            return "";
        }

        public void ResetAll() => asset?.RemoveAllBindingOverrides();

        // The action already using path in the player's controls, or null. The slot's own binding is skipped.
        public string FindConflict(string path, int exceptSlot)
        {
            if (map == null || string.IsNullOrEmpty(path)) return null;
            var except = exceptSlot >= 0 && exceptSlot < slots.Count ? slots[exceptSlot] : null;
            foreach (var action in map.actions)
                for (int i = 0; i < action.bindings.Count; i++)
                {
                    var binding = action.bindings[i];
                    if (binding.isComposite) continue;
                    if (except != null && action.name == except.Action && i == except.BindingIndex) continue;
                    if (string.Equals(binding.effectivePath, path, StringComparison.OrdinalIgnoreCase))
                        return LabelOf(action.name, binding.isPartOfComposite ? binding.name : null);
                }
            return null;
        }

        private string LabelOf(string action, string part)
        {
            foreach (var slot in slots) if (slot.Action == action && (part == null || slot.Part == part)) return slot.Label;
            switch (action)
            {
                case "Move": return part == "up" ? "위 이동" : part == "down" ? "아래 이동" : "이동";
                case "Aim": return "조준";
                default: return action;
            }
        }

        // Waits for one key or mouse button. Esc cancels; a key another action uses is refused and the old key stays.
        public bool StartRebind(int index, Action<RebindOutcome, string> finished)
        {
            if (IsRebinding || index < 0 || index >= slots.Count || slots[index].BindingIndex < 0) return false;
            var slot = slots[index];
            var action = ActionOf(slot);
            if (action == null) return false;
            bool wasEnabled = action.enabled;
            if (wasEnabled) action.Disable();
            string previous = action.bindings[slot.BindingIndex].overridePath;
            RebindingSlot = index;
            var rebind = action.PerformInteractiveRebinding(slot.BindingIndex)
                .WithControlsHavingToMatchPath("<Keyboard>")
                .WithControlsExcluding("<Keyboard>/anyKey")
                .WithCancelingThrough(EscapePath)
                .WithMatchingEventsBeingSuppressed();
            foreach (var button in MouseButtons) rebind.WithControlsHavingToMatchPath(button);
            rebind.OnComplete(op => End(op, action, slot, index, previous, wasEnabled, false, finished))
                .OnCancel(op => End(op, action, slot, index, previous, wasEnabled, true, finished));
            operation = rebind;
            rebind.Start();
            return true;
        }

        private void End(InputActionRebindingExtensions.RebindingOperation op, InputAction action, Slot slot, int index,
            string previous, bool wasEnabled, bool cancelled, Action<RebindOutcome, string> finished)
        {
            string conflict = null;
            if (!cancelled)
            {
                conflict = FindConflict(action.bindings[slot.BindingIndex].effectivePath, index);
                if (conflict != null) Restore(action, slot.BindingIndex, previous);
            }
            else Restore(action, slot.BindingIndex, previous);
            op.Dispose();
            if (operation == op) operation = null;
            RebindingSlot = -1;
            EndedFrame = Time.frameCount;
            EndedAt = Time.unscaledTimeAsDouble;
            if (wasEnabled) action.Enable();
            finished?.Invoke(cancelled ? RebindOutcome.Cancelled : conflict != null ? RebindOutcome.Conflict : RebindOutcome.Applied, conflict);
        }

        private static void Restore(InputAction action, int index, string previous)
        {
            if (string.IsNullOrEmpty(previous)) action.RemoveBindingOverride(index);
            else action.ApplyBindingOverride(index, previous);
        }

        public void Cancel() => operation?.Cancel();
    }
}
