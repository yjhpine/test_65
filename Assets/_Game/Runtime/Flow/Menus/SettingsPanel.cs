using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace ActionPlatformer.Flow
{
    // Settings pages (planning doc 5): environment, presentation, keys and, from the title only, data.
    // It only shows and reports values; the owner applies and saves them.
    public sealed class SettingsPanel : MenuPanel
    {
        public const string SectionName = "settings";
        public enum Tab { Environment, Presentation, Keys, Data }

        private readonly Dictionary<Tab, VisualElement> pages = new Dictionary<Tab, VisualElement>();
        private readonly Dictionary<Tab, MenuEntry> tabs = new Dictionary<Tab, MenuEntry>();
        private readonly VisualElement keyList;
        private readonly List<MenuEntry> keyItems = new List<MenuEntry>();
        public readonly ValueRow Master, Music, Sfx, Shake, Distortion, Flash;
        public readonly ChoiceRow ScreenMode, Resolution;
        public readonly MenuEntry ApplyDisplay, ResetEnvironment, ResetPresentation, ResetKeys, DeleteProgress, BackItem;
        public Tab Current { get; private set; }
        public IReadOnlyList<MenuEntry> KeyItems => keyItems;
        public Action<int> KeySelected;

        public SettingsPanel() : base(SectionName, MenuKit.Backdrop)
        {
            root.Add(MenuKit.Label("설정", 40, MenuKit.Accent));
            var tabRow = new VisualElement();
            tabRow.style.flexDirection = FlexDirection.Row;
            tabRow.style.marginTop = 14; tabRow.style.marginBottom = 12;
            root.Add(tabRow);
            AddTab(tabRow, Tab.Environment, "환경");
            AddTab(tabRow, Tab.Presentation, "연출");
            AddTab(tabRow, Tab.Keys, "조작 키");
            AddTab(tabRow, Tab.Data, "데이터");

            var env = Page(Tab.Environment);
            Master = Row(env, new ValueRow("settings-master", "전체 음량"));
            Music = Row(env, new ValueRow("settings-music", "배경음 음량"));
            Sfx = Row(env, new ValueRow("settings-sfx", "효과음 음량"));
            ScreenMode = Row(env, new ChoiceRow("settings-screen-mode", "화면 모드"));
            Resolution = Row(env, new ChoiceRow("settings-resolution", "해상도"));
            ApplyDisplay = Row(env, new MenuEntry("settings-apply-display", "화면 설정 적용", null, 560));
            ResetEnvironment = Row(env, new MenuEntry("settings-reset-environment", "환경 설정 기본값 복원", null, 560));

            var presentation = Page(Tab.Presentation);
            Shake = Row(presentation, new ValueRow("settings-shake", "화면 흔들림"));
            Distortion = Row(presentation, new ValueRow("settings-distortion", "글리치 화면 왜곡"));
            Flash = Row(presentation, new ValueRow("settings-flash", "섬광 강도"));
            var note = MenuKit.Label("0이면 해당 장식 연출을 끕니다. 글리치 사거리·충돌·공격 판정·쿨타임은 바뀌지 않습니다.", 15, MenuKit.DimText);
            note.style.width = 560; note.style.marginTop = 6; note.style.marginBottom = 6;
            presentation.Add(note);
            ResetPresentation = Row(presentation, new MenuEntry("settings-reset-presentation", "연출 설정 기본값 복원", null, 560));

            var keys = Page(Tab.Keys);
            keyList = new VisualElement { name = "settings-key-list" };
            keys.Add(keyList);
            ResetKeys = Row(keys, new MenuEntry("settings-reset-keys", "기본 키 복원", null, 560));

            var data = Page(Tab.Data);
            DeleteProgress = Row(data, new MenuEntry("settings-delete-progress", "진행도 삭제", null, 560));

            BackItem = Track(new MenuEntry("settings-back", "뒤로", () => CloseSelf()));
            BackItem.style.marginTop = 16;
            root.Add(BackItem);
            root.Add(CreateHint());
            Select(Tab.Environment, false);
        }

        private void AddTab(VisualElement row, Tab tab, string text)
        {
            var item = Track(new MenuEntry("settings-tab-" + tab.ToString().ToLowerInvariant(), text, () => Select(tab, false), 130));
            item.style.marginLeft = item.style.marginRight = 4;
            item.style.minHeight = 42;
            tabs[tab] = item;
            row.Add(item);
        }

        private VisualElement Page(Tab tab)
        {
            var page = new VisualElement { name = "settings-page-" + tab.ToString().ToLowerInvariant() };
            page.style.alignItems = Align.Center;
            page.style.minHeight = 330;
            pages[tab] = page;
            root.Add(page);
            return page;
        }

        private T Row<T>(VisualElement page, T item) where T : MenuEntry
        {
            page.Add(item);
            return Track(item);
        }

        public bool DataTabVisible { get; private set; }

        public void SetDataTabVisible(bool visible)
        {
            DataTabVisible = visible;
            tabs[Tab.Data].style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (!visible && Current == Tab.Data) Select(Tab.Environment, false);
        }

        public void SetMusicVisible(bool visible) => Music.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;

        public void Select(Tab tab, bool focusTab)
        {
            if (tab == Tab.Data && !DataTabVisible && tabs[Tab.Data].style.display == DisplayStyle.None) tab = Tab.Environment;
            Current = tab;
            foreach (var pair in pages) pair.Value.style.display = pair.Key == tab ? DisplayStyle.Flex : DisplayStyle.None;
            foreach (var pair in tabs) pair.Value.Selected = pair.Key == tab;
            if (focusTab) tabs[tab].Focus();
        }

        public MenuEntry TabItem(Tab tab) => tabs[tab];

        // Left/Right on a tab switches pages.
        protected override void Sideways(bool right)
        {
            foreach (var pair in tabs)
                if (Focused == pair.Value)
                {
                    var order = new List<Tab>();
                    foreach (var t in tabs.Keys) if (MenuKit.IsShown(tabs[t])) order.Add(t);
                    int i = order.IndexOf(pair.Key);
                    Select(order[(i + (right ? 1 : -1) + order.Count) % order.Count], true);
                    return;
                }
        }

        public void RebuildKeys(IReadOnlyList<(string label, string key)> rows)
        {
            foreach (var item in keyItems) item.RemoveFromHierarchy();
            keyItems.Clear();
            for (int i = 0; i < rows.Count; i++)
            {
                int index = i;
                var item = new MenuEntry("settings-key-" + i, rows[i].label, () => KeySelected?.Invoke(index), 560);
                item.style.flexDirection = FlexDirection.Row;
                item.style.justifyContent = Justify.SpaceBetween;
                item.SetDetailStyle(20, MenuKit.Accent);
                item.Detail = rows[i].key;
                keyList.Add(item);
                keyItems.Add(Track(item));
            }
        }

        public void SetKeyText(int index, string label, string key)
        {
            if (index < 0 || index >= keyItems.Count) return;
            keyItems[index].Text = label;
            keyItems[index].Detail = key;
        }

        public void Notify(string text) => ShowHint(text);

        protected override MenuEntry DefaultFocus => tabs[Current];
    }
}
