using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace ActionPlatformer.Flow
{
    // Shared look and behaviour of every out-game menu (planning doc 03): one highlighted item for mouse and keyboard,
    // unavailable items that explain why, arrow-key movement and readable text over the glitch styling.
    public static class MenuKit
    {
        public static readonly Color Accent = new Color(.2f, .94f, .82f);
        public static readonly Color AccentRed = new Color(.85f, .26f, .33f);
        public static readonly Color Text = new Color(.93f, .96f, .97f);
        public static readonly Color DimText = new Color(.62f, .68f, .72f);
        public static readonly Color Backdrop = new Color(.025f, .045f, .065f, .9f);
        public static readonly Color Panel = new Color(.06f, .09f, .12f, .96f);
        public static readonly Color ButtonIdle = new Color(.11f, .15f, .19f, 1f);
        public static readonly Color ButtonFocus = new Color(.12f, .3f, .3f, 1f);
        public static readonly Color ButtonSelected = new Color(.08f, .17f, .18f, 1f);

        public static VisualElement Overlay(string name, Color background)
        {
            var element = new VisualElement { name = name };
            element.style.position = Position.Absolute;
            element.style.left = element.style.right = element.style.top = element.style.bottom = 0;
            element.style.alignItems = Align.Center;
            element.style.justifyContent = Justify.Center;
            element.style.backgroundColor = background;
            element.style.display = DisplayStyle.None;
            return element;
        }

        public static Label Label(string text, int size, Color color, string name = null)
        {
            var label = new Label(text) { name = name };
            PixelText.SetSize(label, size);
            label.style.color = color;
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            return label;
        }

        public static void Pad(VisualElement element, float horizontal, float vertical)
        {
            element.style.paddingLeft = element.style.paddingRight = horizontal;
            element.style.paddingTop = element.style.paddingBottom = vertical;
        }

        public static void Border(VisualElement element, float width, Color color)
        {
            element.style.borderLeftWidth = element.style.borderRightWidth =
                element.style.borderTopWidth = element.style.borderBottomWidth = width;
            element.style.borderLeftColor = element.style.borderRightColor =
                element.style.borderTopColor = element.style.borderBottomColor = color;
        }

        public static bool IsShown(VisualElement element)
        {
            for (var e = element; e != null; e = e.parent)
                if (e.resolvedStyle.display == DisplayStyle.None || e.style.display == DisplayStyle.None) return false;
            return true;
        }
    }

    // A focusable menu item. An unavailable item stays selectable so it can say why; activating it does nothing else.
    public class MenuEntry : VisualElement
    {
        private readonly Label caption;
        private readonly Label detail;
        private Action activated;
        private bool focused, selected;
        public string UnavailableReason { get; private set; }
        public bool Available => UnavailableReason == null;
        public string Text { get => caption.text; set => caption.text = value; }
        public string Detail { get => detail.text; set { detail.text = value; detail.style.display = string.IsNullOrEmpty(value) ? DisplayStyle.None : DisplayStyle.Flex; } }
        public event Action<MenuEntry> Highlighted;
        public event Action<MenuEntry> Refused;

        public MenuEntry(string name, string text, Action activated, float width = 320)
        {
            this.name = name;
            this.activated = activated;
            focusable = true;
            pickingMode = PickingMode.Position;
            style.width = width;
            style.minHeight = 52;
            style.marginTop = style.marginBottom = 5;
            style.justifyContent = Justify.Center;
            style.alignItems = Align.Center;
            MenuKit.Pad(this, 18, 6);
            style.borderTopLeftRadius = style.borderTopRightRadius = style.borderBottomLeftRadius = style.borderBottomRightRadius = 3;
            caption = MenuKit.Label(text, 22, MenuKit.Text);
            caption.pickingMode = PickingMode.Ignore;
            Add(caption);
            detail = MenuKit.Label("", 14, MenuKit.DimText);
            detail.pickingMode = PickingMode.Ignore;
            detail.style.display = DisplayStyle.None;
            Add(detail);
            RegisterCallback<FocusInEvent>(_ => { focused = true; Restyle(); Highlighted?.Invoke(this); });
            RegisterCallback<FocusOutEvent>(_ => { focused = false; Restyle(); });
            // Hovering selects, so mouse and keyboard always share the one highlighted item.
            RegisterCallback<PointerEnterEvent>(_ => { if (focusController?.focusedElement != this) Focus(); });
            RegisterCallback<PointerUpEvent>(e => { if (e.button == 0) Activate(); });
            RegisterCallback<NavigationSubmitEvent>(e => { Activate(); e.StopPropagation(); });
            Restyle();
        }

        public void SetAction(Action value) => activated = value;

        // A chosen option (a settings tab) keeps a quiet highlight while another item has focus.
        public bool Selected { get => selected; set { selected = value; Restyle(); } }

        public void SetDetailStyle(int size, Color color)
        {
            PixelText.SetSize(detail, size);
            detail.style.color = color;
        }

        public void SetAvailable(bool available, string reason = null)
        {
            UnavailableReason = available ? null : (reason ?? "지금은 사용할 수 없습니다");
            Restyle();
        }

        // The entry point for clicks, Enter and tests alike.
        public void Activate()
        {
            if (!enabledInHierarchy || !MenuKit.IsShown(this)) return;
            if (!Available) { Refused?.Invoke(this); return; }
            activated?.Invoke();
        }

        private void Restyle()
        {
            style.backgroundColor = focused ? MenuKit.ButtonFocus : selected ? MenuKit.ButtonSelected : MenuKit.ButtonIdle;
            MenuKit.Border(this, 2, focused ? MenuKit.Accent : selected ? new Color(.2f, .94f, .82f, .35f) : new Color(1, 1, 1, .08f));
            caption.style.color = !Available ? MenuKit.DimText : selected && !focused ? MenuKit.Accent : MenuKit.Text;
            style.opacity = Available ? 1f : .7f;
        }
    }

    // A 0~100 value: Left/Right steps it, a click or drag on the bar sets it. Commit fires when an adjustment ends.
    public sealed class ValueRow : MenuEntry
    {
        public const int Step = 5;
        private readonly VisualElement bar, fill;
        private readonly Label valueLabel;
        private bool dragging;
        public int Value { get; private set; }
        public event Action<int> Changed;
        public event Action<int> Committed;

        public ValueRow(string name, string text) : base(name, text, null, 560)
        {
            style.flexDirection = FlexDirection.Row;
            style.justifyContent = Justify.SpaceBetween;
            var line = new VisualElement { pickingMode = PickingMode.Ignore };
            line.style.flexDirection = FlexDirection.Row;
            line.style.alignItems = Align.Center;
            bar = new VisualElement { name = name + "-bar" };
            bar.style.width = 200; bar.style.height = 12;
            bar.style.backgroundColor = new Color(1, 1, 1, .12f);
            bar.style.marginRight = 12;
            fill = new VisualElement { pickingMode = PickingMode.Ignore };
            fill.style.height = Length.Percent(100);
            fill.style.backgroundColor = MenuKit.Accent;
            bar.Add(fill);
            line.Add(bar);
            valueLabel = MenuKit.Label("100", 20, MenuKit.Text);
            valueLabel.style.width = 64; // "100" stays on one line when the pixel font rounds up to 24 screen pixels.
            valueLabel.style.whiteSpace = WhiteSpace.NoWrap;
            valueLabel.pickingMode = PickingMode.Ignore;
            line.Add(valueLabel);
            Add(line);
            RegisterCallback<NavigationMoveEvent>(OnMove);
            bar.RegisterCallback<PointerDownEvent>(OnPointerDown);
            bar.RegisterCallback<PointerMoveEvent>(OnPointerMove);
            bar.RegisterCallback<PointerUpEvent>(OnPointerUp);
            RegisterCallback<FocusOutEvent>(_ => { if (pendingCommit) Commit(); });
        }

        private bool pendingCommit;

        public void SetValue(int value, bool notify = false)
        {
            value = Mathf.Clamp(value, 0, 100);
            bool changed = value != Value;
            Value = value;
            fill.style.width = Length.Percent(value);
            valueLabel.text = value.ToString();
            if (notify && changed) { pendingCommit = true; Changed?.Invoke(value); }
        }

        public void Commit()
        {
            pendingCommit = false;
            Committed?.Invoke(Value);
        }

        private void OnMove(NavigationMoveEvent e)
        {
            if (e.direction != NavigationMoveEvent.Direction.Left && e.direction != NavigationMoveEvent.Direction.Right) return;
            SetValue(Value + (e.direction == NavigationMoveEvent.Direction.Right ? Step : -Step), true);
            e.StopPropagation();
            focusController?.IgnoreEvent(e);
        }

        private void OnPointerDown(PointerDownEvent e)
        {
            if (e.button != 0) return;
            Focus();
            dragging = true;
            bar.CapturePointer(e.pointerId);
            SetFromPointer(e.localPosition.x);
            e.StopPropagation();
        }

        private void OnPointerMove(PointerMoveEvent e) { if (dragging) SetFromPointer(e.localPosition.x); }

        private void OnPointerUp(PointerUpEvent e)
        {
            if (!dragging) return;
            dragging = false;
            bar.ReleasePointer(e.pointerId);
            Commit();
            e.StopPropagation();
        }

        private void SetFromPointer(float x)
        {
            float width = bar.resolvedStyle.width;
            if (!(width > 0)) return;
            SetValue(Mathf.RoundToInt(Mathf.Clamp01(x / width) * 100f), true);
        }
    }

    // One choice out of a short list: Left/Right or a click cycles it.
    public sealed class ChoiceRow : MenuEntry
    {
        private readonly Label valueLabel;
        private string[] options = Array.Empty<string>();
        public int Index { get; private set; }
        public event Action<int> Changed;
        // The two arrows are click targets of their own: a click on one steps that way (2026-11-12). A click anywhere
        // else on the row steps forward, like Enter; Left/Right keys step either way.
        public Label PreviousArrow { get; }
        public Label NextArrow { get; }
        public string ValueText => valueLabel.text;

        public ChoiceRow(string name, string text) : base(name, text, null, 560)
        {
            style.flexDirection = FlexDirection.Row;
            style.justifyContent = Justify.SpaceBetween;
            var value = new VisualElement { pickingMode = PickingMode.Ignore };
            value.style.flexDirection = FlexDirection.Row;
            value.style.alignItems = Align.Center;
            PreviousArrow = Arrow("◀", name + "-previous", -1);
            valueLabel = MenuKit.Label("", 20, MenuKit.Accent);
            valueLabel.pickingMode = PickingMode.Ignore;
            valueLabel.style.whiteSpace = WhiteSpace.NoWrap;
            NextArrow = Arrow("▶", name + "-next", 1);
            value.Add(PreviousArrow);
            value.Add(valueLabel);
            value.Add(NextArrow);
            Add(value);
            SetAction(() => Step(1));
            RegisterCallback<NavigationMoveEvent>(OnMove);
        }

        private Label Arrow(string glyph, string arrowName, int direction)
        {
            var arrow = MenuKit.Label(glyph, 20, MenuKit.Accent, arrowName);
            arrow.pickingMode = PickingMode.Position;
            arrow.style.paddingLeft = arrow.style.paddingRight = 10; // a wider target than the glyph alone
            arrow.RegisterCallback<PointerUpEvent>(e =>
            {
                if (e.button != 0) return;
                Step(direction);
                e.StopPropagation(); // the row's own click would step forward again
            });
            return arrow;
        }

        // One option left (-1) or right (+1), wrapping. An unavailable row explains why instead.
        public void Step(int direction)
        {
            if (!enabledInHierarchy || !MenuKit.IsShown(this)) return;
            if (!Available) { Activate(); return; }
            Select(Index + direction, true);
        }

        public void SetOptions(string[] values, int index)
        {
            options = values ?? Array.Empty<string>();
            Select(index, false);
        }

        public void Select(int index, bool notify)
        {
            if (options.Length == 0) { Index = 0; valueLabel.text = "-"; return; }
            index = ((index % options.Length) + options.Length) % options.Length;
            bool changed = index != Index;
            Index = index;
            valueLabel.text = options[index];
            if (notify && changed) Changed?.Invoke(index);
        }

        private void OnMove(NavigationMoveEvent e)
        {
            if (e.direction != NavigationMoveEvent.Direction.Left && e.direction != NavigationMoveEvent.Direction.Right) return;
            if (Available) Step(e.direction == NavigationMoveEvent.Direction.Right ? 1 : -1);
            e.StopPropagation();
            focusController?.IgnoreEvent(e);
        }
    }

    // A full-screen menu page or dialog. Up/Down moves between its visible items; Esc goes back through the stack.
    public abstract class MenuPanel
    {
        protected readonly VisualElement root;
        private readonly List<MenuEntry> items = new List<MenuEntry>();
        private MenuEntry lastFocused;
        private Label hint;
        public VisualElement Root => root;
        public bool IsOpen { get; internal set; }
        internal MenuStack Owner { get; set; }
        protected void CloseSelf() => Owner?.Close(this);
        public virtual bool ClosesOnBack => true;
        // Dialogs keep the page below them visible.
        public virtual bool IsDialog => false;
        internal Action onClosed;

        protected MenuPanel(string name, Color background)
        {
            root = MenuKit.Overlay(name, background);
            root.RegisterCallback<NavigationMoveEvent>(OnNavigate);
        }

        protected T Track<T>(T item) where T : MenuEntry
        {
            items.Add(item);
            item.Highlighted += i => { lastFocused = i; ShowHint(i.Available ? null : i.UnavailableReason); };
            item.Refused += i => ShowHint(i.UnavailableReason);
            return item;
        }

        protected Label CreateHint()
        {
            hint = MenuKit.Label("", 16, new Color(1f, .78f, .5f), root.name + "-hint");
            hint.style.minHeight = 24;
            hint.style.marginTop = 10;
            return hint;
        }

        public string HintText => hint?.text;
        protected void ShowHint(string text) { if (hint != null) hint.text = text ?? ""; }

        protected virtual MenuEntry DefaultFocus => FirstVisible();

        protected MenuEntry FirstVisible()
        {
            foreach (var item in items) if (MenuKit.IsShown(item)) return item;
            return null;
        }

        public void FocusDefault(bool keepLast)
        {
            var target = keepLast && lastFocused != null && MenuKit.IsShown(lastFocused) ? lastFocused : DefaultFocus;
            ShowHint(null);
            if (target == null) return;
            target.Focus();
            if (root.panel == null) return;
            // A page shown this frame cannot take focus until its style resolves; retry once after layout.
            if (target.focusController?.focusedElement != target)
                root.schedule.Execute(() => { if (IsOpen) target.Focus(); }).ExecuteLater(0);
        }

        public MenuEntry Focused => root.focusController?.focusedElement as MenuEntry is MenuEntry item && items.Contains(item) ? item : null;

        private void OnNavigate(NavigationMoveEvent e)
        {
            bool down = e.direction == NavigationMoveEvent.Direction.Down || e.direction == NavigationMoveEvent.Direction.Next;
            bool up = e.direction == NavigationMoveEvent.Direction.Up || e.direction == NavigationMoveEvent.Direction.Previous;
            root.focusController?.IgnoreEvent(e);
            e.StopPropagation();
            if (!down && !up) { Sideways(e.direction == NavigationMoveEvent.Direction.Right); return; }
            Move(down ? 1 : -1);
        }

        protected virtual void Sideways(bool right) { }

        public void Move(int step)
        {
            var visible = items.FindAll(MenuKit.IsShown);
            if (visible.Count == 0) return;
            int index = visible.IndexOf(Focused ?? lastFocused);
            index = index < 0 ? 0 : (index + step + visible.Count) % visible.Count;
            visible[index].Focus();
        }

        internal virtual void Shown() { }
        internal virtual void Hidden() { }
        // Esc on this panel. Returning false keeps it open.
        public virtual bool Back() => ClosesOnBack;
    }

    // The out-game pages open in order; Esc closes only the top one (planning doc 3.1).
    public sealed class MenuStack
    {
        private readonly List<MenuPanel> panels = new List<MenuPanel>();
        public int Count => panels.Count;
        public MenuPanel Top => panels.Count > 0 ? panels[panels.Count - 1] : null;
        public bool Contains(MenuPanel panel) => panels.Contains(panel);

        public void Push(MenuPanel panel, Action onClosed = null)
        {
            if (panel == null) return;
            if (panels.Contains(panel)) Remove(panel, false);
            if (!panel.IsDialog && Top != null) Top.Root.style.display = DisplayStyle.None;
            panels.Add(panel);
            panel.Owner = this;
            panel.onClosed = onClosed;
            panel.IsOpen = true;
            panel.Root.style.display = DisplayStyle.Flex;
            if (panel.Root.parent != null) panel.Root.BringToFront();
            panel.Shown();
            panel.FocusDefault(false);
        }

        public void Close(MenuPanel panel) => Remove(panel, true);

        private void Remove(MenuPanel panel, bool reveal)
        {
            int index = panels.IndexOf(panel);
            if (index < 0) return;
            panels.RemoveAt(index);
            panel.IsOpen = false;
            panel.Root.style.display = DisplayStyle.None;
            panel.Hidden();
            var closed = panel.onClosed;
            panel.onClosed = null;
            if (reveal && index == panels.Count && Top != null)
            {
                Top.Root.style.display = DisplayStyle.Flex;
                Top.FocusDefault(true);
            }
            closed?.Invoke();
        }

        public void CloseAll()
        {
            while (panels.Count > 0) Remove(panels[panels.Count - 1], false);
        }

        // True when the stack used the key; an empty stack leaves it to the pause logic.
        public bool Back()
        {
            var top = Top;
            if (top == null) return false;
            if (top.Back()) Close(top);
            return true;
        }
    }
}
