using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace ActionPlatformer.Cinematics
{
    public enum DialogueProgress { Timed, Confirm }
    [Serializable]
    public sealed class DialogueClip : PlayableAsset, ITimelineClipAsset
    {
        public string Speaker = "안내";
        public Sprite Portrait;
        [TextArea(2, 8)] public string Text = "대사를 입력하세요.";
        [Min(0f)] public float CharactersPerSecond = 30f;
        public DialogueProgress Progress = DialogueProgress.Confirm;
        public ClipCaps clipCaps => ClipCaps.None;
        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            var playable = ScriptPlayable<DialogueBehaviour>.Create(graph);
            playable.GetBehaviour().Data = this;
            return playable;
        }
    }
    public sealed class DialogueBehaviour : PlayableBehaviour { public DialogueClip Data; }
}
