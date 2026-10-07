using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace ActionPlatformer.Cinematics
{
    [TrackColor(.4f, .75f, .95f), TrackBindingType(typeof(DialogueSpeaker)), TrackClipType(typeof(DialogueClip))]
    public sealed class SpeechBubbleTrack : TrackAsset
    {
        public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
            => DialogueMixer.Create(graph, go, inputCount, this, true);
    }
}
