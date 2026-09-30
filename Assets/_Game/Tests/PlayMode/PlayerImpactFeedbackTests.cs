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
        private float initialFixedDeltaTime;
        [SetUp] public void Setup()
        {
            initialScale = Time.timeScale;
            initialFixedDeltaTime = Time.fixedDeltaTime;
            Time.timeScale = 1f;
            tuning = Object.Instantiate(AssetDatabase.LoadAssetAtPath<PlayerCombatTuning>("Assets/_Game/Data/PlayerCombatTuning.asset"));
            camera = new GameObject("Impact camera").AddComponent<Camera>();
            camera.enabled = false; camera.orthographic = true;
            camera.transform.position = new Vector3(4f, 6f, -10f);
            feedback = new PlayerImpactFeedback(tuning, camera);
        }
        [TearDown] public void Cleanup()
        {
            feedback.Dispose();
            Time.timeScale = initialScale;
            Time.fixedDeltaTime = initialFixedDeltaTime;
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

        [Test] public void DamageFeedbackRestoresZoomAimAndTimeAndCanRetrigger()
        {
            Vector3 position = camera.transform.position;
            float size = camera.orthographicSize;
            feedback.PlayDamage(Vector2.left, 10);
            feedback.LateTick(10);
            Assert.That(Time.timeScale, Is.EqualTo(tuning.DamageSlowTimeScale));
            Assert.That(camera.transform.position.x, Is.LessThan(position.x));
            Assert.That(camera.orthographicSize, Is.LessThan(size));
            Assert.That(feedback.IsDamageFeedbackActive(10), Is.True);
            feedback.Tick(10.01);
            Assert.That(camera.transform.position, Is.EqualTo(position));
            Assert.That(camera.orthographicSize, Is.EqualTo(size).Within(.00001f));
            feedback.Tick(10.3); Assert.That(Time.timeScale, Is.EqualTo(1));
            feedback.PlayDamage(Vector2.right, 10.4); feedback.LateTick(10.4);
            Assert.That(camera.transform.position.x, Is.GreaterThan(position.x));
            feedback.Tick(11); feedback.LateTick(11);
            Assert.That(feedback.IsDamageFeedbackActive(11), Is.False);
            Assert.That(camera.orthographicSize, Is.EqualTo(size).Within(.00001f));
            feedback.Reset(); Assert.That(Time.timeScale, Is.EqualTo(1));
        }

        [Test] public void DamageSurfaceIsReusedAndDestroyedWhenOwnerDisposes()
        {
            feedback.Dispose();
            var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Art/PlayerDamageScreen.mat");
            Assert.That(material, Is.Not.Null);
            feedback = new PlayerImpactFeedback(tuning, camera, material);
            feedback.PlayDamage(Vector2.left, 0); feedback.LateTick(0);
            var surface = camera.GetComponentInChildren<MeshRenderer>();
            Assert.That(surface, Is.Not.Null); Assert.That(surface.enabled, Is.True);
            feedback.Tick(1); feedback.LateTick(1); Assert.That(surface.enabled, Is.False);
            feedback.PlayDamage(Vector2.right, 2); feedback.LateTick(2);
            Assert.That(camera.GetComponentInChildren<MeshRenderer>(), Is.SameAs(surface));
            feedback.SetCamera(null); Assert.That(surface.enabled, Is.False);
            Assert.That(camera.orthographicSize, Is.EqualTo(5).Within(.00001f));
            feedback.Dispose(); Assert.That(surface.enabled, Is.False);
        }

        [Test] public void DamageEffectsCanBeDisabledIndividuallyAndValidateRanges()
        {
            foreach (string name in new[] { "damageSlowDuration", "damageSlowRecovery", "damageShakeAmplitude", "damageShakeDuration", "damageZoom", "damageZoomDuration", "damageFlashOpacity", "damageFlashDuration", "damageVignetteOpacity", "damageScreenDuration" })
            {
                Set(name, -1); Assert.That(tuning.TryValidate(), Is.False, name);
                Set(name, 0); Assert.That(tuning.TryValidate(), Is.True, name);
            }
            Vector3 position = camera.transform.position; float size = camera.orthographicSize;
            feedback.PlayDamage(Vector2.right, 0); feedback.LateTick(0);
            Assert.That(Time.timeScale, Is.EqualTo(1)); Assert.That(camera.transform.position, Is.EqualTo(position));
            Assert.That(camera.orthographicSize, Is.EqualTo(size));
        }

        [Test] public void DamageSlowHoldsRecoversAndRestoresPreviousTimeAndPhysicsCadence()
        {
            Time.timeScale = .5f;
            float step = Time.fixedDeltaTime;
            feedback.PlayDamage(Vector2.left, 10);
            Assert.That(feedback.IsHitStopped, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(.1f).Within(.00001f));
            Assert.That(Time.fixedDeltaTime, Is.EqualTo(step * .2f).Within(.00001f));
            feedback.Tick(10.14);
            Assert.That(Time.timeScale, Is.EqualTo(.1f).Within(.00001f));
            feedback.Tick(10.2);
            Assert.That(Time.timeScale, Is.EqualTo(.3f).Within(.00001f));
            feedback.Tick(10.251);
            Assert.That(Time.timeScale, Is.EqualTo(.5f));
            Assert.That(Time.fixedDeltaTime, Is.EqualTo(step));
        }

        [Test] public void DamageSlowOverridesHitStopAndRepeatDamageRefreshesWithoutCompounding()
        {
            feedback.Play(new PlayerImpact(Vector2.right, PlayerImpactStrength.Slam), 10);
            feedback.PlayDamage(Vector2.left, 10);
            Assert.That(Time.timeScale, Is.EqualTo(.2f));
            feedback.Play(new PlayerImpact(Vector2.right, PlayerImpactStrength.Slam), 10.01);
            Assert.That(Time.timeScale, Is.EqualTo(.2f));
            feedback.Tick(10.2);
            feedback.PlayDamage(Vector2.left, 10.2);
            Assert.That(Time.timeScale, Is.EqualTo(.2f));
            feedback.Tick(10.34);
            Assert.That(Time.timeScale, Is.EqualTo(.2f));
            feedback.Dispose();
            Assert.That(Time.timeScale, Is.EqualTo(1));
            Assert.That(Time.fixedDeltaTime, Is.EqualTo(initialFixedDeltaTime));
        }

        [Test] public void DamageSlowRespectsExternalPauseAndSpeedChanges()
        {
            Time.timeScale = 0;
            feedback.PlayDamage(Vector2.left, 0); feedback.Tick(1); feedback.Reset();
            Assert.That(Time.timeScale, Is.Zero);
            foreach (float external in new[] { 0f, .7f })
            {
                Time.timeScale = 1;
                feedback.PlayDamage(Vector2.left, 2);
                Time.timeScale = external;
                feedback.Tick(2.1); feedback.Reset();
                Assert.That(Time.timeScale, Is.EqualTo(external));
                Assert.That(Time.fixedDeltaTime, Is.EqualTo(initialFixedDeltaTime));
            }
        }

        [Test] public void DamageSlowScaleValidationAndInstantRecovery()
        {
            foreach (float invalid in new[] { 0f, -1f, 1.1f, float.NaN, float.PositiveInfinity })
            {
                Set("damageSlowTimeScale", invalid); Assert.That(tuning.TryValidate(), Is.False);
            }
            Set("damageSlowTimeScale", 1f); Assert.That(tuning.TryValidate(), Is.True);
            feedback.PlayDamage(Vector2.left, 0); Assert.That(Time.timeScale, Is.EqualTo(1));
            Set("damageSlowTimeScale", .2f); Set("damageSlowRecovery", 0);
            feedback.PlayDamage(Vector2.left, 2);
            feedback.Tick(2.151); Assert.That(Time.timeScale, Is.EqualTo(1));
            Assert.That(Time.fixedDeltaTime, Is.EqualTo(initialFixedDeltaTime));
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
