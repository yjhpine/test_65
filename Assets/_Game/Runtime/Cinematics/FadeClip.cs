using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace ActionPlatformer.Cinematics
{
    [Serializable]
    public sealed class FadeClip : PlayableAsset, ITimelineClipAsset
    {
        public Color Color = Color.black;
        [Range(0f, 1f)] public float From = 1f;
        [Range(0f, 1f)] public float To;
        public ClipCaps clipCaps => ClipCaps.None;
        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            var playable = ScriptPlayable<FadeBehaviour>.Create(graph);
            playable.GetBehaviour().Data = this;
            return playable;
        }
    }
    public sealed class FadeBehaviour : PlayableBehaviour { public FadeClip Data; }
}
