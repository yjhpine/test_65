using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace ActionPlatformer.Cinematics
{
    [TrackColor(.4f, .4f, .55f), TrackBindingType(typeof(CutsceneView)), TrackClipType(typeof(FadeClip))]
    public sealed class FadeTrack : TrackAsset
    {
        public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
            => ScriptPlayable<FadeMixer>.Create(graph, inputCount);
    }
    public sealed class FadeMixer : PlayableBehaviour
    {
        private CutsceneView view;
        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            view = playerData as CutsceneView;
            if (view == null) return;
            for (int i = 0; i < playable.GetInputCount(); i++)
            {
                if (playable.GetInputWeight(i) <= 0f) continue;
                var input = (ScriptPlayable<FadeBehaviour>)playable.GetInput(i);
                var data = input.GetBehaviour().Data;
                float progress = input.GetDuration() > 0 ? Mathf.Clamp01((float)(input.GetTime() / input.GetDuration())) : 1f;
                view.ShowFade(data.Color, Mathf.Lerp(data.From, data.To, Mathf.SmoothStep(0, 1, progress)));
                return;
            }
            view.ShowFade(Color.black, 0f);
        }
        public override void OnGraphStop(Playable playable) { if (view != null) view.ShowFade(Color.black, 0); }
        public override void OnPlayableDestroy(Playable playable) { if (view != null) view.ShowFade(Color.black, 0); }
    }
}
