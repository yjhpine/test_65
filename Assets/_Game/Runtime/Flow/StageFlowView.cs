using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace ActionPlatformer.Flow
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    public sealed class StageFlowView : MonoBehaviour
    {
        [SerializeField] private UIDocument document;
        private VisualElement completion;
        private Label goalHint;
        private Button restart;
        public event Action RestartRequested;
        public bool IsConfigured => document != null && document.panelSettings != null && completion != null;
        public bool IsCompletedVisible => completion != null && completion.style.display == DisplayStyle.Flex;

        private void OnEnable()
        {
            if (document == null || document.panelSettings == null) return;
            var root = document.rootVisualElement;
            root.Clear();
            root.pickingMode = PickingMode.Ignore;
            root.style.flexGrow = 1;

            goalHint = new Label("DEFEAT ALL ENEMIES TO OPEN THE EXIT");
            goalHint.pickingMode = PickingMode.Ignore;
            goalHint.style.position = Position.Absolute;
            goalHint.style.top = 32;
            goalHint.style.alignSelf = Align.Center;
            goalHint.style.paddingLeft = goalHint.style.paddingRight = 24;
            goalHint.style.paddingTop = goalHint.style.paddingBottom = 12;
            goalHint.style.fontSize = 22;
            goalHint.style.color = Color.white;
            goalHint.style.backgroundColor = new Color(0.12f, 0.06f, 0.08f, 0.92f);
            root.Add(goalHint);

            completion = new VisualElement { name = "stage-complete" };
            completion.style.position = Position.Absolute;
            completion.style.left = completion.style.right = completion.style.top = completion.style.bottom = 0;
            completion.style.alignItems = Align.Center;
            completion.style.justifyContent = Justify.Center;
            completion.style.backgroundColor = new Color(0.025f, 0.045f, 0.065f, 0.9f);
            var title = new Label("STAGE CLEAR");
            title.style.fontSize = 56;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = new Color(0.2f, 0.94f, 0.82f);
            title.style.marginBottom = 12;
            completion.Add(title);
            var subtitle = new Label("All enemies defeated. Destination reached.");
            subtitle.style.fontSize = 20;
            subtitle.style.color = Color.white;
            subtitle.style.marginBottom = 32;
            completion.Add(subtitle);
            restart = new Button(RequestRestart) { name = "restart", text = "RESTART" };
            restart.style.width = 220;
            restart.style.height = 56;
            restart.style.fontSize = 24;
            completion.Add(restart);
            root.Add(completion);
            completion.style.display = DisplayStyle.None;
            ShowGoalHint(false);
        }

        public void ShowGoalHint(bool show)
        {
            if (goalHint != null) goalHint.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void ShowCompleted()
        {
            ShowGoalHint(false);
            completion.style.display = DisplayStyle.Flex;
            restart.Focus();
        }

        public void SetRestartEnabled(bool value) => restart?.SetEnabled(value);
        private void RequestRestart() => RestartRequested?.Invoke();

        private void OnDisable()
        {
            if (restart != null) restart.clicked -= RequestRestart;
        }
    }
}
