using ActionPlatformer.Cinematics;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Timeline;

namespace ActionPlatformer.Tests
{
    public sealed class DialoguePlaybackTests
    {
        [Test] public void TypingPreservesCombiningCharactersAndResetsForAnotherPlacementOfSameAsset()
        {
            var timeline = ScriptableObject.CreateInstance<TimelineAsset>();
            var track = timeline.CreateTrack<DialogueTrack>();
            var a = track.CreateClip<DialogueClip>();
            var b = track.CreateClip<DialogueClip>();
            var unused = b.asset;
            b.asset = a.asset;
            var data = (DialogueClip)a.asset;
            data.Text = "가e\u0301나"; data.CharactersPerSecond = 1;
            var playback = new DialoguePlayback();
            try
            {
                playback.Evaluate(a, 2, 0, false);
                Assert.That(playback.VisibleText, Is.EqualTo("가e\u0301"));
                playback.Reveal();
                Assert.That(playback.IsFullyRevealed, Is.True);
                playback.Evaluate(b, 0, 0, false);
                Assert.That(playback.VisibleText, Is.Empty);
                Assert.That(playback.IsFullyRevealed, Is.False);
            }
            finally { Object.DestroyImmediate(unused); Object.DestroyImmediate(a.asset); Object.DestroyImmediate(track); Object.DestroyImmediate(timeline); }
        }
        [Test] public void ConfirmationUsesUnscaledClockAndPreviewDoesNotConsumeWait()
        {
            var timeline = ScriptableObject.CreateInstance<TimelineAsset>();
            var track = timeline.CreateTrack<DialogueTrack>();
            var clip = track.CreateClip<DialogueClip>();
            var data = (DialogueClip)clip.asset;
            data.Text = "한글대사"; data.CharactersPerSecond = 1;
            var playback = new DialoguePlayback();
            try
            {
                playback.BeginWait(clip, 10);
                playback.Evaluate(clip, 0, 12, false);
                Assert.That(playback.VisibleText, Is.EqualTo("한글"));
                playback.Evaluate(clip, 0, 12, true);
                Assert.That(playback.VisibleText, Is.EqualTo(data.Text));
                playback.Evaluate(clip, 0, 12, false);
                Assert.That(playback.VisibleText, Is.EqualTo("한글"));
                playback.Reveal(); Assert.That(playback.IsFullyRevealed, Is.True);
                playback.Clear(); Assert.That(playback.IsWaiting, Is.False);
                playback.Evaluate(clip, 0, 0, true);
                data.Text = "수정한 대사";
                playback.Evaluate(clip, 0, 0, true);
                Assert.That(playback.VisibleText, Is.EqualTo("수정한 대사"));
                data.Text = ""; data.CharactersPerSecond = 0;
                playback.Evaluate(clip, 0, 0, false);
                Assert.That(playback.IsFullyRevealed, Is.True);
            }
            finally { Object.DestroyImmediate(clip.asset); Object.DestroyImmediate(track); Object.DestroyImmediate(timeline); }
        }
    }
}
