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
    public sealed class PlayerVisualTests
    {
        private readonly List<Object> created = new List<Object>();
        private sealed class State : IPlayerActionState, ICharacterMotionState
        {
            public Vector2 Velocity { get; set; }
            public bool IsGrounded { get; set; } = true;
            public GlitchPreview GlitchPreview => default;
            public bool IsUnderground { get; set; }
            public bool CanEmerge { get; set; } = true;
            public Vector2 UndergroundPosition => Vector2.down;
            public Vector2 GroundMarkerPosition => Vector2.zero;
            public float Facing { get; set; } = 1f;
            public PlayerAttackPhase AttackPhase { get; set; }
            public PlayerAttackPresentation AttackPresentation { get; set; }
            public PlayerReactionPresentation ReactionPresentation { get; set; }
            public PlayerGlitchPresentation GlitchPresentation { get; set; }
            public Bounds AttackBounds { get; set; } = new Bounds(Vector3.right, Vector3.one);
            public PlayerShockwave Shockwave => default;
        }

        private PlayerVisual Visual(State state, Vector3 position = default)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Player.prefab");
            var holder = new GameObject("Player visual test"); holder.SetActive(false); created.Add(holder);
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
                Airborne = airborne, PhaseProgress = progress,
                Version = state.AttackPresentation.Version + (phase == PlayerAttackPhase.Windup ||
                    state.AttackPresentation.Kind != kind || state.AttackPresentation.Version == 0 ? 1u : 0u),
                StartedAt = state.AttackPresentation.StartedAt + (phase == PlayerAttackPhase.Windup ? .6 : 0) };
        }
        private static IEnumerator RenderFrame() { yield return null; yield return null; }
        private static void AssertFrame(PlayerVisual visual, string animation, string sprite)
        {
            Assert.That(visual.GetComponent<Animator>().GetCurrentAnimatorStateInfo(0).IsName(animation), Is.True, animation);
            Assert.That(visual.GetComponent<SpriteRenderer>().sprite.name, Is.EqualTo(sprite));
        }
        [TearDown] public void Cleanup()
        {
            for (int i = created.Count - 1; i >= 0; i--) if (created[i] != null) Object.DestroyImmediate(created[i]);
            created.Clear();
        }

        [UnityTest] public IEnumerator BasicSlashFollowsAttackTimingDirectionAndInterruptions()
        {
            var state = new State(); var visual = Visual(state);
            yield return RenderFrame();
            var slash = visual.EffectsRoot.Find("Basic attack slash").GetComponent<SpriteRenderer>();
            foreach (float facing in new[] { -1f, 1f })
            foreach (var kind in new[] { PlayerAttack.Side, PlayerAttack.Air })
            for (int strike = 1; strike <= 3; strike++)
            {
                state.Facing = facing;
                state.AttackBounds = new Bounds(new Vector3(facing, 2f, 0), Vector3.one);
                Attack(state, kind, strike, kind == PlayerAttack.Air, PlayerAttackPhase.Windup);
                yield return RenderFrame(); Assert.That(slash.enabled, Is.True);
                Attack(state, kind, strike, kind == PlayerAttack.Air, PlayerAttackPhase.Active, .3f);
                yield return RenderFrame();
                Assert.That(slash.enabled, Is.True);
                Assert.That(slash.flipX, Is.EqualTo(facing < 0f));
                Assert.That(slash.transform.position, Is.EqualTo(visual.transform.TransformPoint(new Vector3(.65f * facing, .3f))));
                Assert.That(slash.sprite.name, Does.StartWith("white_slash_"));
                var impactFrame = slash.sprite;
                Attack(state, kind, strike, kind == PlayerAttack.Air, PlayerAttackPhase.Recovery, .5f);
                yield return RenderFrame(); Assert.That(slash.sprite, Is.Not.EqualTo(impactFrame));
                state.AttackPhase = PlayerAttackPhase.Ready;
                yield return RenderFrame(); Assert.That(slash.enabled, Is.False);
            }
            Attack(state, PlayerAttack.Lift, 1, false, PlayerAttackPhase.Active);
            yield return RenderFrame(); Assert.That(slash.enabled, Is.True);
            Attack(state, PlayerAttack.Side, 1, false, PlayerAttackPhase.Active);
            state.ReactionPresentation = new PlayerReactionPresentation { Kind = PlayerReaction.Hit, Version = 1 };
            yield return RenderFrame(); Assert.That(slash.enabled, Is.False);
            state.ReactionPresentation = default; state.IsUnderground = true;
            yield return RenderFrame(); Assert.That(slash.enabled, Is.False);
        }

        [UnityTest] public IEnumerator GlitchSparksBurstAtEndpointsThenAlongOneWorldSpaceRowAndReset()
        {
            var state = new State(); var visual = Visual(state);
            // Separate the bursts enough to observe their order even at a low Editor frame rate.
            var settings = new SerializedObject(visual);
            var profile = Object.Instantiate(settings.FindProperty("vfxProfile").objectReferenceValue);
            created.Add(profile);
            settings.FindProperty("vfxProfile").objectReferenceValue=profile;
            settings.ApplyModifiedPropertiesWithoutUndo();
            var profileData=new SerializedObject(profile);
            profileData.FindProperty("glitchTrailInterval").floatValue=.06f;
            profileData.ApplyModifiedPropertiesWithoutUndo();
            yield return RenderFrame();
            Assert.That(visual.transform.Find("Glitch teleport line"), Is.Null);
            var sparks = new List<SpriteRenderer>();
            foreach (var renderer in visual.EffectsRoot.GetComponentsInChildren<SpriteRenderer>())
                if (renderer.name.StartsWith("Glitch spark ")) sparks.Add(renderer);
            Assert.That(sparks.Count, Is.EqualTo(16), "A bounded pool is reused for every teleport.");
            foreach (var spark in sparks) Assert.That(spark.enabled, Is.False);
            state.IsUnderground = true;
            visual.transform.parent.position = new Vector3(20, 10, 0);
            visual.transform.parent.localScale = Vector3.one * 1.25f;
            visual.transform.parent.rotation = Quaternion.Euler(0, 0, 17);
            var from = new Vector2(-3, 2);
            foreach (var to in new[] { new Vector2(0, 2), new Vector2(-3, 5), new Vector2(-4.8f, -.4f) })
            {
                state.GlitchPresentation = new PlayerGlitchPresentation {
                    Version = state.GlitchPresentation.Version + 1, From = from, To = to };
                visual.SendMessage("LateUpdate"); ActionPlatformer.Presentation.EffectsRuntime.Vfx.SendMessage("LateUpdate");
                int count = Mathf.CeilToInt(Vector2.Distance(from, to) / .6f) + 1;
                Assert.That(sparks[0].enabled && sparks[count - 1].enabled, Is.True, "Both original endpoint bursts start immediately.");
                Assert.That(sparks[1].enabled, Is.False, "Interior bursts are delayed.");
                Assert.That(Vector2.Distance(sparks[0].transform.position, from), Is.LessThan(.001f));
                Assert.That(Vector2.Distance(sparks[count - 1].transform.position, to), Is.LessThan(.001f));
                yield return new WaitForSecondsRealtime(.09f);
                yield return RenderFrame();
                Assert.That(sparks[1].enabled, Is.True);
                Assert.That(sparks[1].sprite.name, Does.StartWith("white_spark_"));
                Assert.That(sparks[0].sprite, Is.Not.EqualTo(sparks[1].sprite), "Each spark plays its own staggered animation.");
                for (int i = 0; i < count; i++)
                    if (sparks[i].enabled)
                        Assert.That(Vector2.Distance(sparks[i].transform.position, Vector2.Lerp(from, to, (float)i / (count - 1))), Is.LessThan(.001f));
            }
            float savedTime = Time.timeScale;
            try
            {
                Time.timeScale = 0;
                yield return new WaitForSecondsRealtime(1f);
                foreach (var spark in sparks) Assert.That(spark.enabled, Is.False, "All scheduled bursts finish even during hitstop or pause.");
            }
            finally { Time.timeScale = savedTime; }
            state.GlitchPresentation = new PlayerGlitchPresentation { Version = 4, From = from, To = from };
            visual.SendMessage("LateUpdate"); ActionPlatformer.Presentation.EffectsRuntime.Vfx.SendMessage("LateUpdate");
            Assert.That(sparks[0].enabled, Is.True, "A zero-length teleport plays one local burst.");
            for (int i = 1; i < sparks.Count; i++) Assert.That(sparks[i].enabled, Is.False);
            state.GlitchPresentation = new PlayerGlitchPresentation { Version = 5, From = from, To = Vector2.right };
            yield return RenderFrame(); Assert.That(sparks[0].enabled, Is.True);
            visual.gameObject.SetActive(false); visual.gameObject.SetActive(true);
            yield return RenderFrame();
            foreach (var spark in sparks) Assert.That(spark.enabled, Is.False);
            state.GlitchPresentation = new PlayerGlitchPresentation { Version = 6, From = Vector2.left, To = Vector2.right };
            yield return RenderFrame(); Assert.That(sparks[0].enabled, Is.True);
            state.ReactionPresentation = new PlayerReactionPresentation { Kind = PlayerReaction.Die, Version = 1 };
            yield return RenderFrame();
            foreach (var spark in sparks) Assert.That(spark.enabled, Is.False);
        }

        [UnityTest] public IEnumerator GroundComboAndOrdinaryAirAttacksSampleImpactAndRecovery()
        {
            var state = new State(); var visual = Visual(state);
            yield return RenderFrame();
            for (int strike = 1; strike <= 3; strike++)
            {
                Attack(state, PlayerAttack.Side, strike, false, PlayerAttackPhase.Windup);
                yield return RenderFrame(); AssertFrame(visual, "Attack1", "attack_v7_punch_02");
                Attack(state, PlayerAttack.Side, strike, false, PlayerAttackPhase.Active);
                yield return RenderFrame(); AssertFrame(visual, "Attack1", "attack_v7_punch_02");
                Attack(state, PlayerAttack.Side, strike, false, PlayerAttackPhase.Recovery, .99f);
                yield return RenderFrame(); AssertFrame(visual, "Attack1", "attack_v7_punch_08");
                state.Velocity = new Vector2(8f, -4f);
                Attack(state, PlayerAttack.Air, strike, true, PlayerAttackPhase.Windup);
                yield return RenderFrame(); AssertFrame(visual, "AirAttack1", "attack_v7_punch_02");
                Attack(state, PlayerAttack.Air, strike, true, PlayerAttackPhase.Active);
                yield return RenderFrame(); AssertFrame(visual, "AirAttack1", "attack_v7_punch_02");
                Attack(state, PlayerAttack.Air, strike, true, PlayerAttackPhase.Recovery, .99f);
                state.IsGrounded = true; // Landing cannot restart the swing as a ground animation.
                yield return RenderFrame(); AssertFrame(visual, "AirAttack1", "attack_v7_punch_08");
                state.AttackPhase = PlayerAttackPhase.Ready;
                yield return RenderFrame();
                Assert.That(visual.GetComponent<Animator>().GetCurrentAnimatorStateInfo(0).IsName("Run"), Is.True);
            }
            Assert.That(visual.transform.Find("Attack area").GetComponent<SpriteRenderer>().enabled, Is.False);
            Assert.That(visual.transform.localPosition, Is.EqualTo(Vector3.zero));
            Assert.That(visual.GetComponent<Animator>().applyRootMotion, Is.False);
        }

        [UnityTest] public IEnumerator SlamUsesHoldLoopLandingAndInterruptionsRestoreLocomotion()
        {
            var state = new State(); var visual = Visual(state); var animator = visual.GetComponent<Animator>();
            Attack(state, PlayerAttack.Slam, 1, true, PlayerAttackPhase.Windup, .8f);
            yield return RenderFrame(); AssertFrame(visual, "SlamHover", "stomp_v2_03");
            Attack(state, PlayerAttack.Slam, 1, true, PlayerAttackPhase.Descending);
            yield return RenderFrame();
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("SlamFall"), Is.True);
            var first = visual.GetComponent<SpriteRenderer>().sprite;
            bool changed = false;
            for (int i=0; i<15; i++) { yield return new WaitForFixedUpdate(); changed |= visual.GetComponent<SpriteRenderer>().sprite != first; }
            Assert.That(changed, Is.True, "The descending pose should loop.");
            Attack(state, PlayerAttack.Slam, 1, false, PlayerAttackPhase.Active);
            yield return RenderFrame(); AssertFrame(visual, "SlamLand", "stomp_v2_05");
            Attack(state, PlayerAttack.Slam, 1, false, PlayerAttackPhase.Recovery, .8f);
            yield return RenderFrame(); AssertFrame(visual, "SlamLand", "stomp_v2_08");
            Attack(state, PlayerAttack.Slam, 1, true, PlayerAttackPhase.Recovery);
            yield return RenderFrame();
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Fall"), Is.True, "No landing pose when descent timed out in midair.");
            Assert.That(animator.GetBool("ActionOverride"), Is.False);
            Attack(state, PlayerAttack.Emergence, 2, false, PlayerAttackPhase.Active);
            yield return RenderFrame(); AssertFrame(visual, "Emergence", "uppercut_v5_04");
            Attack(state, PlayerAttack.Lift, 3, false, PlayerAttackPhase.Active);
            yield return RenderFrame(); AssertFrame(visual, "Lift", "uppercut_v5_04");
            state.IsUnderground = true;
            yield return RenderFrame(); Assert.That(visual.GetComponent<SpriteRenderer>().enabled, Is.False);
            Assert.That(animator.GetBool("ActionOverride"), Is.False);
            state.IsUnderground = false; state.AttackPhase = PlayerAttackPhase.Ready;
            yield return RenderFrame(); Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Idle"), Is.True);
            Attack(state, PlayerAttack.Side, 1, false, PlayerAttackPhase.Recovery, .9f);
            yield return RenderFrame();
            Attack(state, PlayerAttack.Side, 1, false, PlayerAttackPhase.Windup);
            yield return RenderFrame(); AssertFrame(visual, "Attack1", "attack_v7_punch_02");
            visual.gameObject.SetActive(false); state.AttackPhase = PlayerAttackPhase.Ready;
            visual.gameObject.SetActive(true); yield return RenderFrame();
            Assert.That(animator.GetBool("ActionOverride"), Is.False);
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Idle"), Is.True);
            Assert.That(visual.GetComponent<SpriteRenderer>().enabled, Is.True);
        }

        [UnityTest] public IEnumerator HitAndDieFramesOverrideAttacksAndResetCleanly()
        {
            var state = new State(); var visual = Visual(state);
            Attack(state, PlayerAttack.Side, 1, false, PlayerAttackPhase.Active);
            foreach (var kind in new[] { PlayerReaction.Hit, PlayerReaction.Die })
            {
                int count = kind == PlayerReaction.Hit ? 8 : 4;
                string prefix = "hurt_v1_";
                for (int i = 0; i < count; i++)
                {
                    state.ReactionPresentation = new PlayerReactionPresentation { Kind = kind, Version = 1, HasDirection = true, Direction = Vector2.right,
                        Progress = (i + .1f) / count };
                    yield return RenderFrame();
                    AssertFrame(visual, kind.ToString(), prefix + (i + 1).ToString("00"));
                }
            }
            visual.gameObject.SetActive(false);
            state.ReactionPresentation = default; state.AttackPhase = PlayerAttackPhase.Ready;
            visual.gameObject.SetActive(true); yield return RenderFrame();
            Assert.That(visual.GetComponent<Animator>().GetCurrentAnimatorStateInfo(0).IsName("Idle"), Is.True);
            Assert.That(visual.GetComponent<Animator>().GetBool("ActionOverride"), Is.False);
        }

        [UnityTest] public IEnumerator AlternatingFramesSurviveTeleportAndNewAttacksOverrideHitPose()
        {
            var state = new State(); var visual = Visual(state);
            state.AttackPhase = PlayerAttackPhase.Active;
            state.AttackPresentation = new PlayerAttackPresentation { Kind = PlayerAttack.Side, Version = 1, StartedAt = 10 };
            yield return RenderFrame(); AssertFrame(visual, "Attack1", "attack_v7_punch_02");
            state.GlitchPresentation = new PlayerGlitchPresentation { Version = 1, From = Vector2.left, To = Vector2.right };
            state.AttackPresentation = new PlayerAttackPresentation { Kind = PlayerAttack.Side, Version = 2, StartedAt = 10.2 };
            yield return RenderFrame(); AssertFrame(visual, "Attack2", "attack_v7_punch_04");
            Assert.That(visual.EffectsRoot.Find("Basic attack slash").GetComponent<SpriteRenderer>().flipY, Is.True);
            state.ReactionPresentation = new PlayerReactionPresentation { Kind = PlayerReaction.Hit, Version = 1,
                HasDirection = true, Direction = Vector2.right, Progress = .4f };
            yield return RenderFrame(); Assert.That(visual.GetComponent<Animator>().GetCurrentAnimatorStateInfo(0).IsName("Hit"), Is.True);
            Assert.That(Quaternion.Angle(visual.transform.localRotation, Quaternion.identity), Is.GreaterThan(1));
            visual.SetReducedMotion(true); yield return RenderFrame();
            Assert.That(Quaternion.Angle(visual.transform.localRotation, Quaternion.identity), Is.LessThan(.01f));
            state.AttackPresentation = new PlayerAttackPresentation { Kind = PlayerAttack.Side, Version = 3, StartedAt = 10.3 };
            yield return RenderFrame(); AssertFrame(visual, "Attack1", "attack_v7_punch_02");
            var properties = new MaterialPropertyBlock(); visual.GetComponent<SpriteRenderer>().GetPropertyBlock(properties);
            Assert.That(properties.GetFloat("_OutlineWidth"), Is.GreaterThan(0), "Reduced motion preserves the damage outline.");
            state.ReactionPresentation = new PlayerReactionPresentation { Kind = PlayerReaction.Die, Version = 2 };
            yield return RenderFrame(); Assert.That(visual.GetComponent<Animator>().GetCurrentAnimatorStateInfo(0).IsName("Die"), Is.True);
        }

        [UnityTest] public IEnumerator CapturePresentationAtReferenceAndFullHdWithReducedMotion()
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) Assert.Ignore("Graphics device required.");
            var states = new State[6]; var visuals = new PlayerVisual[6];
            for (int i = 0; i < 6; i++)
            {
                states[i] = new State();
                var kind = i == 2 ? PlayerAttack.Lift : i == 3 ? PlayerAttack.Slam : PlayerAttack.Side;
                states[i].AttackPhase = i == 3 ? PlayerAttackPhase.Descending : i == 4 ? PlayerAttackPhase.Ready : PlayerAttackPhase.Active;
                states[i].AttackPresentation = new PlayerAttackPresentation { Kind = kind, Version = 1, StartedAt = 10, PhaseProgress = .2f };
                if (i == 4) states[i].ReactionPresentation = new PlayerReactionPresentation {
                    Kind = PlayerReaction.Hit, Version = 1, Progress = .2f, HasDirection = true, Direction = Vector2.left };
                visuals[i] = Visual(states[i], new Vector3((i % 3) * 4 - 4, 103 - (i / 3) * 3, 0));
            }
            yield return RenderFrame();
            states[1].AttackPresentation = new PlayerAttackPresentation { Kind = PlayerAttack.Side, Version = 2, StartedAt = 10.2, PhaseProgress = .2f };
            var victim = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Enemies/PatrolEnemy.prefab"),
                new Vector3(5.1f, 100, 0), Quaternion.identity); created.Add(victim);
            victim.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
            var health = victim.GetComponent<ActionPlatformer.Units.Features.UnitHealth>();
            var go = new GameObject("Presentation acceptance camera"); created.Add(go);
            var camera = go.AddComponent<Camera>(); camera.enabled = false; camera.orthographic = true;
            camera.orthographicSize = 4.1f; camera.transform.position = new Vector3(.4f, 101.5f, -10);
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.08f, .1f, .15f);
            var tuning = AssetDatabase.LoadAssetAtPath<PlayerCombatTuning>("Assets/_Game/Data/PlayerCombatTuning.asset");
            var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Art/PlayerDamageScreen.mat");
            var feedback = new PlayerImpactFeedback(tuning, camera, material);
            float savedTime = Time.timeScale, savedStep = Time.fixedDeltaTime;
            try
            {
                foreach (bool reduced in new[] { false, true })
                foreach (var size in new[] { new Vector2Int(1120, 660), new Vector2Int(1920, 1080) })
                {
                    foreach (var visual in visuals) visual.SetReducedMotion(reduced);
                    health.ApplyDamage(1);
                    visuals[5].PresentHit(new PlayerHitPresentation(1, PlayerAttack.Side, health,
                        health.GetComponent<Collider2D>().ClosestPoint(visuals[5].transform.position), Vector2.right, 1, 1, false));
                    yield return null;
                    int victimOrder = victim.GetComponentInChildren<SpriteRenderer>().sortingOrder;
                    foreach (var effect in visuals[5].GetComponentsInChildren<SpriteRenderer>())
                        if (effect.name.StartsWith("Contact ") && effect.enabled)
                            Assert.That(effect.sortingOrder, Is.EqualTo(victimOrder + 1), "Contacts must appear above the victim and below telegraphs.");
                    feedback.Reset(); feedback.ReducedMotion = reduced;
                    var basePosition = camera.transform.position;
                    double now = Time.unscaledTimeAsDouble; feedback.PlayDamage(Vector2.left, now); feedback.LateTick(now);
                    float screenPixels = Vector3.Distance(basePosition, camera.transform.position) * size.y / (2 * camera.orthographicSize);
                    Assert.That(screenPixels, Is.EqualTo(reduced ? 0 : 4f * size.y / 660f).Within(.001f));
                    var rt = new RenderTexture(size.x, size.y, 24); var texture = new Texture2D(size.x, size.y, TextureFormat.RGB24, false);
                    var previous = RenderTexture.active;
                    try
                    {
                        camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
                        texture.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0); texture.Apply();
                        System.IO.Directory.CreateDirectory("Library/PrototypeValidation");
                        System.IO.File.WriteAllBytes("Library/PrototypeValidation/GlitchPresentation-" + size.x + "-" + (reduced ? "reduced" : "normal") + ".png", texture.EncodeToPNG());
                        Vector2 cursor = camera.WorldToScreenPoint(victim.transform.position);
                        feedback.Tick(now);
                        var ray = feedback.ScreenPointToRay(cursor); var plane = new Plane(Vector3.forward, Vector3.zero);
                        Assert.That(plane.Raycast(ray, out float distance), Is.True);
                        Assert.That(Vector3.Distance(ray.GetPoint(distance), victim.transform.position), Is.LessThan(.001f));
                    }
                    finally { RenderTexture.active = previous; camera.targetTexture = null; rt.Release(); Object.DestroyImmediate(rt); Object.DestroyImmediate(texture); }
                }
            }
            finally { feedback.Dispose(); Time.timeScale = savedTime; Time.fixedDeltaTime = savedStep; }
        }

        [UnityTest] public IEnumerator CapturePlayerHitAndDieFrames()
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) Assert.Ignore("Graphics device required.");
            for (int i=0; i<12; i++)
            {
                bool hit = i < 8; int frame = hit ? i : i-8;
                var state = new State { ReactionPresentation = new PlayerReactionPresentation {
                    Kind = hit ? PlayerReaction.Hit : PlayerReaction.Die, Version = 1, HasDirection = true, Direction = Vector2.right,
                    Progress = (frame + .1f) / (hit ? 8f : 4f) } };
                Visual(state,new Vector3(frame*1.8f-6.3f,hit?103f:100f,0f));
            }
            yield return RenderFrame();
            var go = new GameObject("Reaction capture camera"); created.Add(go);
            var camera = go.AddComponent<Camera>(); camera.enabled=false; camera.orthographic=true;
            camera.orthographicSize=3.7f; camera.transform.position=new Vector3(0f,102f,-10f);
            camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.08f,.1f,.15f);
            var target=new RenderTexture(1400,640,24); var texture=new Texture2D(1400,640,TextureFormat.RGB24,false);
            created.Add(target); created.Add(texture); var previous=RenderTexture.active;
            try
            {
                camera.targetTexture=target; camera.Render(); RenderTexture.active=target;
                texture.ReadPixels(new Rect(0,0,1400,640),0,0); texture.Apply();
                System.IO.Directory.CreateDirectory("Library/PrototypeValidation");
                System.IO.File.WriteAllBytes("Library/PrototypeValidation/PlayerReactions.png",texture.EncodeToPNG());
            }
            finally { RenderTexture.active=previous; camera.targetTexture=null; target.Release(); }
        }

        [UnityTest] public IEnumerator CapturePlayerAttackPresentation()
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) Assert.Ignore("Graphics device required.");
            var kinds = new[]{PlayerAttack.Side,PlayerAttack.Side,PlayerAttack.Side,PlayerAttack.Air,PlayerAttack.Emergence,PlayerAttack.Slam,PlayerAttack.Slam,PlayerAttack.Slam};
            for (int i=0; i<8; i++)
            {
                var state = new State();
                Attack(state,kinds[i],i<3?i+1:1,i==3||i==5||i==6,
                    i==5?PlayerAttackPhase.Windup:i==6?PlayerAttackPhase.Descending:PlayerAttackPhase.Active);
                Visual(state,new Vector3((i%4)*3.2f-4.8f,102f-(i/4)*3f,0f));
            }
            yield return RenderFrame();
            var go = new GameObject("Player capture camera"); created.Add(go);
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
                System.IO.File.WriteAllBytes("Library/PrototypeValidation/PlayerArtAttacks.png",texture.EncodeToPNG());
            }
            finally { RenderTexture.active=previous; camera.targetTexture=null; target.Release(); }
        }
    }
}
#endif
