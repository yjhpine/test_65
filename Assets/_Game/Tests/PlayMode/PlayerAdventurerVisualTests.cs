#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using ActionPlatformer.Player;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace ActionPlatformer.Tests
{
    public sealed class PlayerAdventurerVisualTests
    {
        private readonly List<Object> created = new List<Object>();
        private sealed class State : IPlayerActionState, ICharacterMotionState
        {
            public Vector2 Velocity { get; set; }
            public bool IsGrounded { get; set; } = true;
            public GlitchPreview GlitchPreview => default;
            public bool IsUnderground { get; set; }
            public Vector2 UndergroundPosition => Vector2.down;
            public Vector2 GroundMarkerPosition => Vector2.zero;
            public float Facing { get; set; } = 1f;
            public PlayerAttackPhase AttackPhase { get; set; }
            public PlayerAttackPresentation AttackPresentation { get; set; }
            public Bounds AttackBounds => new Bounds(Vector3.right, Vector3.one);
            public PlayerShockwave Shockwave => default;
        }

        private PlayerVisual Visual(State state, Vector3 position = default)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Player.prefab");
            var holder = new GameObject("Adventurer visual test"); holder.SetActive(false); created.Add(holder);
            var go = Object.Instantiate(prefab.transform.Find("Visual").gameObject, holder.transform);
            go.transform.localPosition = Vector3.zero;
            holder.transform.position = position;
            var visual = go.GetComponent<PlayerVisual>(); visual.Initialize(state, state);
            holder.SetActive(true);
            return visual;
        }
        private static void Attack(State state, PlayerAttack kind, int strike, bool airborne, PlayerAttackPhase phase, float progress = 0f)
        {
            state.IsGrounded = !airborne;
            state.AttackPhase = phase;
            state.AttackPresentation = new PlayerAttackPresentation { Kind = kind, Strike = strike,
                Airborne = airborne, PhaseProgress = progress, Version = state.AttackPresentation.Version + 1 };
        }
        private static IEnumerator RenderFrame() { yield return null; yield return null; }
        private static void AssertFrame(PlayerVisual visual, string animation, string sprite)
        {
            Assert.That(visual.GetComponent<Animator>().GetCurrentAnimatorStateInfo(0).IsName(animation), Is.True, animation);
            Assert.That(visual.GetComponent<SpriteRenderer>().sprite.name, Is.EqualTo("adventurer-" + sprite));
        }
        [TearDown] public void Cleanup()
        {
            for (int i = created.Count - 1; i >= 0; i--) if (created[i] != null) Object.DestroyImmediate(created[i]);
            created.Clear();
        }

        [UnityTest] public IEnumerator GroundComboAndOrdinaryAirAttacksSampleImpactAndRecovery()
        {
            var state = new State(); var visual = Visual(state);
            yield return RenderFrame();
            for (int strike = 1; strike <= 3; strike++)
            {
                Attack(state, PlayerAttack.Side, strike, false, PlayerAttackPhase.Windup);
                yield return RenderFrame(); AssertFrame(visual, "Attack" + strike, "attack" + strike + "-00");
                Attack(state, PlayerAttack.Side, strike, false, PlayerAttackPhase.Active);
                yield return RenderFrame(); AssertFrame(visual, "Attack" + strike, "attack" + strike + (strike == 2 ? "-03" : "-02"));
                Attack(state, PlayerAttack.Side, strike, false, PlayerAttackPhase.Recovery, .99f);
                yield return RenderFrame(); AssertFrame(visual, "Attack" + strike, "attack" + strike + (strike == 1 ? "-04" : "-05"));
                Attack(state, PlayerAttack.Side, strike, true, PlayerAttackPhase.Active);
                yield return RenderFrame(); AssertFrame(visual, "AirAttack" + strike, strike == 2 ? "air-attack2-00" : "air-attack1-01");
            }
            Assert.That(visual.transform.Find("Attack area").GetComponent<SpriteRenderer>().enabled, Is.False);
            Assert.That(visual.transform.localPosition, Is.EqualTo(Vector3.zero));
            Assert.That(visual.GetComponent<Animator>().applyRootMotion, Is.False);
        }

        [UnityTest] public IEnumerator SlamUsesHoldLoopLandingAndInterruptionsRestoreLocomotion()
        {
            var state = new State(); var visual = Visual(state); var animator = visual.GetComponent<Animator>();
            Attack(state, PlayerAttack.Slam, 1, true, PlayerAttackPhase.Windup, .8f);
            yield return RenderFrame(); AssertFrame(visual, "SlamHover", "air-attack3-rdy-00");
            Attack(state, PlayerAttack.Slam, 1, true, PlayerAttackPhase.Descending);
            yield return RenderFrame();
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("SlamFall"), Is.True);
            var first = visual.GetComponent<SpriteRenderer>().sprite;
            bool changed = false;
            for (int i=0; i<15; i++) { yield return new WaitForFixedUpdate(); changed |= visual.GetComponent<SpriteRenderer>().sprite != first; }
            Assert.That(changed, Is.True, "The descending pose should loop.");
            Attack(state, PlayerAttack.Slam, 1, false, PlayerAttackPhase.Active);
            yield return RenderFrame(); AssertFrame(visual, "SlamLand", "air-attack-3-end-00");
            Attack(state, PlayerAttack.Slam, 1, false, PlayerAttackPhase.Recovery, .8f);
            yield return RenderFrame(); AssertFrame(visual, "SlamLand", "air-attack-3-end-02");
            Attack(state, PlayerAttack.Slam, 1, true, PlayerAttackPhase.Recovery);
            yield return RenderFrame();
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Fall"), Is.True, "No landing pose when descent timed out in midair.");
            Assert.That(animator.GetBool("ActionOverride"), Is.False);
            Attack(state, PlayerAttack.Emergence, 2, false, PlayerAttackPhase.Active);
            yield return RenderFrame(); AssertFrame(visual, "Emergence", "attack1-02");
            Attack(state, PlayerAttack.Lift, 3, false, PlayerAttackPhase.Active);
            yield return RenderFrame(); AssertFrame(visual, "Lift", "attack1-02");
            state.IsUnderground = true;
            yield return RenderFrame(); Assert.That(visual.GetComponent<SpriteRenderer>().enabled, Is.False);
            Assert.That(animator.GetBool("ActionOverride"), Is.False);
            state.IsUnderground = false; state.AttackPhase = PlayerAttackPhase.Ready;
            yield return RenderFrame(); Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Idle"), Is.True);
            Attack(state, PlayerAttack.Side, 1, false, PlayerAttackPhase.Recovery, .9f);
            yield return RenderFrame();
            Attack(state, PlayerAttack.Side, 1, false, PlayerAttackPhase.Windup);
            yield return RenderFrame(); AssertFrame(visual, "Attack1", "attack1-00");
            visual.gameObject.SetActive(false); state.AttackPhase = PlayerAttackPhase.Ready;
            visual.gameObject.SetActive(true); yield return RenderFrame();
            Assert.That(animator.GetBool("ActionOverride"), Is.False);
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Idle"), Is.True);
            Assert.That(visual.GetComponent<SpriteRenderer>().enabled, Is.True);
        }

        [UnityTest] public IEnumerator CaptureAdventurerAttackPresentation()
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) Assert.Ignore("Graphics device required.");
            var kinds = new[]{PlayerAttack.Side,PlayerAttack.Side,PlayerAttack.Side,PlayerAttack.Side,PlayerAttack.Emergence,PlayerAttack.Slam,PlayerAttack.Slam,PlayerAttack.Slam};
            for (int i=0; i<8; i++)
            {
                var state = new State();
                Attack(state,kinds[i],i<3?i+1:1,i==3||i==5||i==6,
                    i==5?PlayerAttackPhase.Windup:i==6?PlayerAttackPhase.Descending:PlayerAttackPhase.Active);
                Visual(state,new Vector3((i%4)*3.2f-4.8f,102f-(i/4)*3f,0f));
            }
            yield return RenderFrame();
            var go = new GameObject("Adventurer capture camera"); created.Add(go);
            var camera = go.AddComponent<Camera>(); camera.enabled=false; camera.orthographic=true;
            camera.orthographicSize=3.5f; camera.transform.position=new Vector3(.25f,101.5f,-10f);
            camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.08f,.1f,.15f);
            var target = new RenderTexture(1200,600,24); var texture = new Texture2D(1200,600,TextureFormat.RGB24,false);
            created.Add(target); created.Add(texture); var previous = RenderTexture.active;
            try
            {
                camera.targetTexture=target; camera.Render(); RenderTexture.active=target;
                texture.ReadPixels(new Rect(0,0,1200,600),0,0); texture.Apply();
                System.IO.Directory.CreateDirectory("Library/PrototypeValidation");
                System.IO.File.WriteAllBytes("Library/PrototypeValidation/AdventurerAttacks.png",texture.EncodeToPNG());
            }
            finally { RenderTexture.active=previous; camera.targetTexture=null; target.Release(); }
        }
    }
}
#endif
