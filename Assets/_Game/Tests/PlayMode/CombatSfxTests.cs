#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using ActionPlatformer.Player;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ActionPlatformer.Tests
{
    public sealed class CombatSfxTests
    {
        private sealed class State : IPlayerActionState
        {
            public PlayerAttackPresentation Attack;
            public PlayerGlitchPresentation Glitch;
            public PlayerAttackPhase Phase;
            public PlayerAttackPresentation AttackPresentation => Attack;
            public PlayerGlitchPresentation GlitchPresentation => Glitch;
            public PlayerAttackPhase AttackPhase => Phase;
            public PlayerReactionPresentation ReactionPresentation => default;
            public GlitchPreview GlitchPreview => default;
            public float GlitchMaxDistance => 0f;
            public bool IsUnderground => false;
            public bool CanEmerge => false;
            public Vector2 UndergroundPosition => default;
            public Vector2 GroundMarkerPosition => default;
            public float Facing => 1;
            public Bounds AttackBounds => default;
            public PlayerShockwave Shockwave => default;
        }
        private GameObject owner, target;
        private object sound;
        private State state;
        private bool paused;
        private AudioSource[] Sources => ((Transform)sound.GetType().GetProperty("SessionRoot").GetValue(sound)).GetComponentsInChildren<AudioSource>();
        private void Call(string name, params object[] args) => sound.GetType().GetMethod(name).Invoke(sound, args);
        [SetUp] public void Setup()
        {
            paused = AudioListener.pause; AudioListener.pause = false;
            owner = new GameObject("SFX test"); state = new State();
            var config = AssetDatabase.LoadAssetAtPath<PlayerSfxDefinition>("Assets/_Game/Audio/Combat/PlayerSfx.asset");
            sound = Activator.CreateInstance(typeof(PlayerVisual).Assembly.GetType("ActionPlatformer.Player.PlayerSoundEffects"),
                BindingFlags.Public | BindingFlags.Instance, null, new object[] { owner.transform, config }, null);
        }
        [TearDown] public void Cleanup()
        {
            Call("Release"); Object.DestroyImmediate(owner); if (target != null) Object.DestroyImmediate(target);
            AudioListener.pause = paused;
        }
        [Test] public void OnlyNewExecutedAttackAndGlitchVersionsProduceSound()
        {
            Call("Observe", state); Assert.That(Sources.All(s => s.clip == null), Is.True);
            state.Attack = new PlayerAttackPresentation { Version = 1, Kind = PlayerAttack.Side };
            state.Phase = PlayerAttackPhase.Windup;
            Call("Observe", state); Call("Observe", state);
            Assert.That(Sources.Count(s => s.clip != null), Is.EqualTo(1));
            Assert.That(Sources.First(s => s.clip != null).clip.name, Is.EqualTo("SwingA"));
            state.Glitch = new PlayerGlitchPresentation { Version = 1 };
            Call("Observe", state); Call("Observe", state);
            Assert.That(Sources.Count(s => s.clip != null && s.clip.name == "Glitch"), Is.EqualTo(1));
        }
        [Test] public void ResetAndListenerPauseDoNotReplayOldActions()
        {
            state.Attack = new PlayerAttackPresentation { Version = 7, Kind = PlayerAttack.Side };
            state.Glitch = new PlayerGlitchPresentation { Version = 5 }; state.Phase = PlayerAttackPhase.Active;
            Call("Reset", state); Call("Observe", state);
            Assert.That(Sources.All(s => s.clip == null), Is.True);
            AudioListener.pause = true; state.Glitch = new PlayerGlitchPresentation { Version = 6 };
            Call("Observe", state); Assert.That(Sources.All(s => s.clip == null), Is.True);
            AudioListener.pause = false; Call("Observe", state);
            Assert.That(Sources.All(s => s.clip == null), Is.True);
        }
        [Test] public void ContactSoundRequiresAcceptedDamageAndCoalescesSimultaneousVictims()
        {
            target = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Enemies/TrainingDummy.prefab"));
            var health = target.GetComponent<ActionPlatformer.Units.Features.UnitHealth>();
            Call("Hit", new PlayerHitPresentation(1, PlayerAttack.Side, health, Vector2.zero, Vector2.right, 0, 0, false));
            Assert.That(Sources.All(s => s.clip == null), Is.True);
            var hit = new PlayerHitPresentation(1, PlayerAttack.Side, health, Vector2.zero, Vector2.right, 10, 0, false);
            Call("Hit", hit); Call("Hit", hit);
            Assert.That(Sources.Count(s => s.clip != null), Is.EqualTo(1));
            Assert.That(Sources.First(s => s.clip != null).clip.name, Is.EqualTo("HitA"));
        }
        [Test] public void StrikesRiseInPitchAndEverySoundVariesSlightly()
        {
            uint version = 1;
            float Swing(PlayerAttack kind, int strike, bool finisher)
            {
                Call("Reset", state);
                state.Attack = new PlayerAttackPresentation { Version = ++version, Kind = kind, Strike = strike, Finisher = finisher };
                state.Phase = PlayerAttackPhase.Windup;
                Call("Observe", state);
                return Sources.Single(s => s.clip != null).pitch;
            }
            // Ranges mirror PlayerSoundEffects: +5% per strike, +-4% swing, +-6% hit, heavy 0.97 +-3%.
            var first = Enumerable.Range(0, 20).Select(_ => Swing(PlayerAttack.Side, 1, false)).ToArray();
            Assert.That(first, Has.All.InRange(.96f, 1.04f));
            Assert.That(first.Distinct().Count(), Is.GreaterThan(1), "Repeated swings must not share one pitch.");
            Assert.That(Swing(PlayerAttack.Side, 2, false), Is.InRange(1.05f * .96f, 1.05f * 1.04f));
            Assert.That(Swing(PlayerAttack.Air, 3, true), Is.InRange(.97f * .97f, .97f * 1.03f), "The strong strike sounds a touch lower.");
            target = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Enemies/TrainingDummy.prefab"));
            var health = target.GetComponent<ActionPlatformer.Units.Features.UnitHealth>();
            float Hit(bool killed)
            {
                Call("Reset", state);
                Call("Hit", new PlayerHitPresentation(1, PlayerAttack.Side, health, Vector2.zero, Vector2.right, 10, 0, killed));
                return Sources.Single(s => s.clip != null).pitch;
            }
            Assert.That(Enumerable.Range(0, 10).Select(_ => Hit(false)), Has.All.InRange(.94f, 1.06f));
            Assert.That(Hit(true), Is.InRange(.97f * .97f, .97f * 1.03f));
        }
        [Test] public void LandingVariantsUseBoundedVoicesAndResetStopsPlayback()
        {
            Call("Land", Vector2.zero, false);
            Assert.That(Sources.Any(s => s.clip != null && s.clip.name == "LandA"), Is.True);
            Call("Land", Vector2.zero, true);
            Assert.That(Sources.Any(s => s.clip != null && s.clip.name == "SlamLand"), Is.True);
            for (int i = 0; i < 30; i++) Call("PlayGlitch");
            Assert.That(Sources.Length, Is.EqualTo(6));
            Call("Reset", state); Assert.That(Sources.All(s => s.clip == null && !s.isPlaying), Is.True);
        }
        [Test] public void BothRangedEnemyPrefabsHaveDistinctShotClips()
        {
            foreach (var pair in new[] { ("SurveillanceTurret", "TurretShot"), ("FlyingDrone", "DroneShot") })
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Enemies/" + pair.Item1 + ".prefab");
                var data = new SerializedObject(prefab.GetComponentInChildren<ActionPlatformer.Units.Features.EnemyVisual2D>());
                Assert.That(new SerializedObject(data.FindProperty("sfxProfile").objectReferenceValue).FindProperty("shotSound").objectReferenceValue.name, Is.EqualTo(pair.Item2));
            }
        }
    }
}
#endif
