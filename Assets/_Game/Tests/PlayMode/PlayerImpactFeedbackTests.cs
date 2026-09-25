#if UNITY_EDITOR
using ActionPlatformer.Player;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ActionPlatformer.Tests
{
    public sealed class PlayerImpactFeedbackTests
    {
        private PlayerCombatTuning tuning;
        private Camera camera;
        private PlayerImpactFeedback feedback;
        private float initialScale;
        [SetUp] public void Setup()
        {
            initialScale = Time.timeScale;
            Time.timeScale = 1f;
            tuning = Object.Instantiate(AssetDatabase.LoadAssetAtPath<PlayerCombatTuning>("Assets/_Game/Data/PlayerCombatTuning.asset"));
            camera = new GameObject("Impact camera").AddComponent<Camera>();
            camera.enabled = false; camera.orthographic = true;
            camera.transform.position = new Vector3(4f, 6f, -10f);
            feedback = new PlayerImpactFeedback(tuning, camera);
        }
        [TearDown] public void Cleanup()
        {
            feedback.Reset();
            Time.timeScale = initialScale;
            Object.DestroyImmediate(camera.gameObject); Object.DestroyImmediate(tuning);
        }
        private void Set(string name, float value)
        {
            var data = new SerializedObject(tuning); data.FindProperty(name).floatValue = value;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        [Test] public void HitStopUsesUnscaledDeadlineStrongestRequestAndRestoresPreviousSpeed()
        {
            Time.timeScale = .5f;
            feedback.Play(new PlayerImpact(Vector2.right, PlayerImpactStrength.Normal), 10);
            Assert.That(Time.timeScale, Is.Zero);
            for (int i=0;i<20;i++) feedback.Play(new PlayerImpact(Vector2.up, PlayerImpactStrength.Slam), 10);
            feedback.Tick(10 + tuning.SlamHitStopDuration - .001);
            Assert.That(feedback.IsHitStopped, Is.True);
            feedback.Tick(10 + tuning.SlamHitStopDuration + .001);
            Assert.That(Time.timeScale, Is.EqualTo(.5f), "Simultaneous hits use maximum duration, not their sum.");
            Assert.That(feedback.IsHitStopped, Is.False);
        }

        [Test] public void FeedbackDoesNotUnpauseAnExistingPauseOrOverwriteAnExternalSpeedChange()
        {
            Time.timeScale = 0f;
            feedback.Play(new PlayerImpact(Vector2.right, PlayerImpactStrength.Normal), 0);
            feedback.Tick(1); feedback.Reset();
            Assert.That(Time.timeScale, Is.Zero);
            Time.timeScale = .5f;
            feedback.Play(new PlayerImpact(Vector2.right, PlayerImpactStrength.Normal), 2);
            Time.timeScale = .25f;
            feedback.Tick(3); feedback.Reset();
            Assert.That(Time.timeScale, Is.EqualTo(.25f));
        }

        [TestCase(-1f, 0f)]
        [TestCase(1f, 0f)]
        [TestCase(0f, 1f)]
        [TestCase(0f, -1f)]
        public void CameraShakeUsesImpactAxisAndRestoresAimWithoutDrift(float x, float y)
        {
            Vector3 origin = camera.transform.position;
            Vector2 screenPoint = new Vector2(200, 180);
            Ray aim = camera.ScreenPointToRay(screenPoint);
            var direction = new Vector2(x,y);
            feedback.Play(new PlayerImpact(direction, PlayerImpactStrength.Heavy), 0);
            feedback.LateTick(0);
            Assert.That(Vector3.Distance(camera.transform.position, origin + (Vector3)(direction*tuning.HeavyShakeAmplitude)), Is.LessThan(.00001f));
            feedback.Tick(.01); // Runs before PlayerUnit samples its aim.
            Assert.That(Vector3.Distance(camera.ScreenPointToRay(screenPoint).origin, aim.origin), Is.LessThan(.00001f));
            for(int i=1;i<20;i++) { feedback.LateTick(i*.01); feedback.Tick(i*.01); }
            Assert.That(Vector3.Distance(camera.transform.position, origin), Is.LessThan(.00001f));
        }

        [Test] public void CameraUsesStrongestImpactAndResetRestoresCameraAndTimeWithoutACamera()
        {
            Vector3 origin = camera.transform.position;
            feedback.Play(new PlayerImpact(Vector2.down, PlayerImpactStrength.Slam), 0);
            for(int i=0;i<20;i++) feedback.Play(new PlayerImpact(Vector2.right, PlayerImpactStrength.Normal), 0);
            feedback.LateTick(0);
            Assert.That(camera.transform.position.y, Is.EqualTo(origin.y-tuning.SlamShakeAmplitude).Within(.00001f));
            Assert.That(camera.transform.position.x, Is.EqualTo(origin.x).Within(.00001f));
            feedback.SetCamera(null);
            Assert.That(Vector3.Distance(camera.transform.position, origin), Is.LessThan(.00001f));
            feedback.Reset(); Assert.That(Time.timeScale, Is.EqualTo(1f));
            feedback.Play(new PlayerImpact(Vector2.up, PlayerImpactStrength.Normal), 2);
            feedback.LateTick(2); feedback.Tick(3);
            Assert.That(Time.timeScale, Is.EqualTo(1f));
        }

        [Test] public void ZeroSettingsDisableEffectsAndInvalidValuesFailValidation()
        {
            foreach(string name in new[]{"hitStopDuration","heavyHitStopDuration","slamHitStopDuration","shakeAmplitude","heavyShakeAmplitude","slamShakeAmplitude","shakeDuration","attackBufferTime"})
            {
                Set(name,-1f); Assert.That(tuning.TryValidate(), Is.False, name);
                Set(name,float.NaN); Assert.That(tuning.TryValidate(), Is.False, name);
                Set(name,0f); Assert.That(tuning.TryValidate(), Is.True, name);
            }
            Vector3 origin=camera.transform.position;
            foreach(PlayerImpactStrength strength in System.Enum.GetValues(typeof(PlayerImpactStrength)))
                feedback.Play(new PlayerImpact(Vector2.right,strength), 0);
            feedback.LateTick(0); feedback.Tick(1);
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            Assert.That(camera.transform.position, Is.EqualTo(origin));
        }
    }
}
#endif
