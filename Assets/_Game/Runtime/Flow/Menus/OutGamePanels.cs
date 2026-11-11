using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace ActionPlatformer.Flow
{
    // A question with a few answers. The cancelling answer is selected first and Esc chooses it (planning doc 3.1).
    public sealed class ConfirmDialog : MenuPanel
    {
        public const string SectionName = "confirm-dialog";
        public struct Choice
        {
            public string Name, Label; public Action Action; public bool Cancels;
            public Choice(string name, string label, Action action, bool cancels = false) { Name = name; Label = label; Action = action; Cancels = cancels; }
        }

        private readonly Label title, message;
        private readonly VisualElement row;
        private readonly List<(MenuEntry item, Choice choice)> buttons = new List<(MenuEntry, Choice)>();
        private MenuEntry cancel;
        public override bool IsDialog => true;
        public string Title => title.text;
        public string Message => message.text;
        public IEnumerable<string> ChoiceNames { get { foreach (var b in buttons) yield return b.item.name; } }

        public ConfirmDialog() : base(SectionName, new Color(0, 0, 0, .55f))
        {
            var box = new VisualElement();
            box.style.backgroundColor = MenuKit.Panel;
            MenuKit.Border(box, 2, MenuKit.AccentRed);
            MenuKit.Pad(box, 36, 26);
            box.style.maxWidth = 760;
            box.style.alignItems = Align.Center;
            title = MenuKit.Label("", 28, MenuKit.Text, "confirm-title");
            title.style.marginBottom = 12;
            box.Add(title);
            message = MenuKit.Label("", 20, MenuKit.Text, "confirm-message");
            message.style.marginBottom = 20;
            box.Add(message);
            row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.justifyContent = Justify.Center;
            box.Add(row);
            root.Add(box);
        }

        public void Setup(string heading, string text, params Choice[] choices)
        {
            foreach (var b in buttons) b.item.RemoveFromHierarchy();
            buttons.Clear();
            cancel = null;
            title.text = heading ?? "";
            title.style.display = string.IsNullOrEmpty(heading) ? DisplayStyle.None : DisplayStyle.Flex;
            message.text = text ?? "";
            foreach (var choice in choices)
            {
                var c = choice;
                var item = new MenuEntry(c.Name, c.Label, () => Choose(c), 220);
                item.style.marginLeft = item.style.marginRight = 8;
                row.Add(item);
                buttons.Add((Track(item), c));
                if (c.Cancels) cancel = item;
            }
        }

        public void SetMessage(string text) => message.text = text ?? "";

        public MenuEntry Item(string name)
        {
            foreach (var b in buttons) if (b.item.name == name) return b.item;
            return null;
        }

        protected override MenuEntry DefaultFocus => cancel ?? FirstVisible();
        protected override void Sideways(bool right) => Move(right ? 1 : -1);

        private void Choose(Choice choice)
        {
            CloseSelf();
            choice.Action?.Invoke();
        }

        public override bool Back()
        {
            foreach (var b in buttons)
                if (b.choice.Cancels) { Choose(b.choice); return false; }
            return false;
        }
    }

    // Title and main menu (planning doc 4.1).
    public sealed class TitlePanel : MenuPanel
    {
        public const string SectionName = "title-menu";
        public const string Logo = "치지직";
        public readonly MenuEntry NewGame, Continue, StageSelect, Settings, Controls, Quit;
        public override bool ClosesOnBack => false;

        public TitlePanel() : base(SectionName, new Color(.02f, .03f, .045f, .55f))
        {
            var logo = new VisualElement { name = "title-logo", pickingMode = PickingMode.Ignore };
            logo.style.marginBottom = 36;
            // A fixed two-colour offset gives the glitch look while the white text stays readable.
            logo.Add(LogoLayer(new Color(.85f, .26f, .33f, .85f), -4, 2));
            logo.Add(LogoLayer(new Color(.2f, .94f, .82f, .85f), 4, -2));
            var main = MenuKit.Label(Logo, 112, Color.white, "title-logo-text");
            main.style.unityFontStyleAndWeight = FontStyle.Bold;
            logo.Add(main);
            root.Add(logo);
            NewGame = Track(Add(new MenuEntry("title-new-game", "새 게임", null)));
            Continue = Track(Add(new MenuEntry("title-continue", "이어하기", null)));
            StageSelect = Track(Add(new MenuEntry("title-stage-select", "스테이지 선택", null)));
            Settings = Track(Add(new MenuEntry("title-settings", "설정", null)));
            Controls = Track(Add(new MenuEntry("title-controls", "조작 안내", null)));
            Quit = Track(Add(new MenuEntry("title-quit", "게임 종료", null)));
            root.Add(CreateHint());
        }

        private static Label LogoLayer(Color color, float x, float y)
        {
            var layer = MenuKit.Label(Logo, 112, color);
            layer.style.unityFontStyleAndWeight = FontStyle.Bold;
            layer.style.position = Position.Absolute;
            layer.style.left = x; layer.style.top = y; layer.style.right = -x;
            layer.pickingMode = PickingMode.Ignore;
            return layer;
        }

        private MenuEntry Add(MenuEntry item) { root.Add(item); return item; }
        protected override MenuEntry DefaultFocus => Continue.Available && MenuKit.IsShown(Continue) ? Continue : NewGame;
    }

    // Unlocked, cleared and locked stages (planning doc 4.4).
    public sealed class StageSelectPanel : MenuPanel
    {
        public const string SectionName = "stage-select";
        public enum Availability { Locked, Open, Cleared }
        private readonly VisualElement list;
        private readonly List<MenuEntry> entries = new List<MenuEntry>();
        public readonly MenuEntry BackItem;
        public Action<string> Entered;
        public IReadOnlyList<MenuEntry> Entries => entries;

        public StageSelectPanel() : base(SectionName, MenuKit.Backdrop)
        {
            root.Add(MenuKit.Label("스테이지 선택", 40, MenuKit.Accent));
            list = new VisualElement();
            list.style.marginTop = 20;
            root.Add(list);
            BackItem = Track(new MenuEntry("stage-select-back", "뒤로", () => CloseSelf()));
            BackItem.style.marginTop = 18;
            root.Add(BackItem);
            root.Add(CreateHint());
        }

        public void Rebuild(IEnumerable<(string id, string name, Availability state)> stages)
        {
            foreach (var e in entries) e.RemoveFromHierarchy();
            entries.Clear();
            foreach (var stage in stages)
            {
                var id = stage.id;
                var item = new MenuEntry("stage-" + id, stage.name, () => Entered?.Invoke(id), 420);
                item.Detail = stage.state == Availability.Cleared ? "클리어" : stage.state == Availability.Locked ? "잠김" : "입장 가능";
                if (stage.state == Availability.Locked) item.SetAvailable(false, "이전 스테이지를 클리어하면 열립니다");
                list.Add(item);
                entries.Add(Track(item));
            }
        }
    }

    // The current controls, read from the live bindings (planning doc 5.3).
    public sealed class ControlsGuidePanel : MenuPanel
    {
        public const string SectionName = "controls-guide";
        private readonly VisualElement table;
        public readonly MenuEntry BackItem;
        public IReadOnlyList<(string action, string keys)> Rows { get; private set; } = Array.Empty<(string, string)>();

        public ControlsGuidePanel() : base(SectionName, MenuKit.Backdrop)
        {
            root.Add(MenuKit.Label("조작 안내", 40, MenuKit.Accent));
            table = new VisualElement { name = "controls-table" };
            table.style.marginTop = 20;
            table.style.width = 640;
            root.Add(table);
            BackItem = Track(new MenuEntry("controls-back", "뒤로", () => CloseSelf()));
            BackItem.style.marginTop = 20;
            root.Add(BackItem);
        }

        public void Rebuild(IReadOnlyList<(string action, string keys)> rows)
        {
            Rows = rows;
            table.Clear();
            foreach (var row in rows)
            {
                var line = new VisualElement();
                line.style.flexDirection = FlexDirection.Row;
                line.style.justifyContent = Justify.SpaceBetween;
                line.style.borderBottomWidth = 1;
                line.style.borderBottomColor = new Color(1, 1, 1, .1f);
                MenuKit.Pad(line, 8, 7);
                var action = MenuKit.Label(row.action, 20, MenuKit.Text);
                var keys = MenuKit.Label(row.keys, 20, MenuKit.Accent);
                action.style.unityTextAlign = TextAnchor.MiddleLeft;
                keys.style.unityTextAlign = TextAnchor.MiddleRight;
                line.Add(action);
                line.Add(keys);
                table.Add(line);
            }
        }
    }
}
