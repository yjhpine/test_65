using System.Linq;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace ActionPlatformer.Cinematics
{
    [TrackColor(.15f, .65f, .65f), TrackBindingType(typeof(CutsceneView)), TrackClipType(typeof(DialogueClip))]
    public sealed class DialogueTrack : TrackAsset
    {
        public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
            => DialogueMixer.Create(graph, go, inputCount, this, false);
    }
    public sealed class DialogueMixer : PlayableBehaviour
    {
        private CutsceneRunner runner;
        private TimelineClip[] clips;
        private bool bubble;
        public static Playable Create(PlayableGraph graph, GameObject owner, int count, TrackAsset track, bool isBubble)
        {
            var playable = ScriptPlayable<DialogueMixer>.Create(graph, count);
            var mixer = playable.GetBehaviour();
            mixer.runner = owner.GetComponent<CutsceneRunner>();
            mixer.clips = track.GetClips().ToArray();
            mixer.bubble = isBubble;
            return playable;
        }
        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            if (runner == null) return;
            for (int i = 0; i < playable.GetInputCount(); i++)
            {
                if (playable.GetInputWeight(i) <= 0f) continue;
                runner.SubmitDialogue(clips[i], playable.GetInput(i).GetTime(), bubble, playerData as DialogueSpeaker);
            }
            // An empty track never hides another track. The runner commits once after evaluation.
        }
    }
}
