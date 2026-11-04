#if UNITY_EDITOR
using ActionPlatformer.Units;
using ActionPlatformer.Units.Bosses;
using ActionPlatformer.Units.Features;
using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Utils;

namespace ActionPlatformer.Tests
{
    public sealed class BossVisualTests
    {
        sealed class FakeState : IBossActionState
        {
            public BossState State { get; set; } = BossState.Approach;
            public uint StateVersion { get; set; } = 1;
            public BossPatternDefinition Pattern { get; set; }
            public float StateElapsed { get; set; }
            public float Progress { get; set; }
            public float Facing { get; set; } = 1f;
            public int Phase { get; set; }
            public BossTelegraph Telegraph { get; set; }
            public string LastSelectionTrace => "";
            public BossFsmDefinition Settings { get; set; }
        }

        readonly List<Object> created = new List<Object>();
        FakeState fake; BossVisual2D visual; Animator animator; SpriteRenderer sprite, box; Rigidbody2D body; GameObject boxHolder;
        static readonly MethodInfo LateUpdate = typeof(BossVisual2D).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic);

        T Track<T>(T o) where T : Object { created.Add(o); return o; }

        [TearDown] public void Cleanup() { foreach (var o in created) if (o != null) Object.DestroyImmediate(o); created.Clear(); }

        IEnumerator Build()
        {
            var controller = new AnimatorController(); controller.AddLayer("Base"); created.Add(controller);
            controller.AddParameter("ActionTime", AnimatorControllerParameterType.Float);
            var machine = controller.layers[0].stateMachine;
            foreach (var name in new[] { "Idle", "Run", "Hit", "PhaseShift", "Death", "Slash", "SlashHit", "SlashEnd" }) machine.AddState(name);
            var settings = Track(ScriptableObject.CreateInstance<BossFsmDefinition>());
            var pattern = Track(ScriptableObject.CreateInstance<ScriptedBossPattern>());
            BossTestRig.Set(pattern, "telegraphAnimation", "Slash"); BossTestRig.Set(pattern, "executeAnimation", "SlashHit");
            BossTestRig.Set(pattern, "recoverAnimation", "Missing");
            var definition = Track(ScriptableObject.CreateInstance<UnitDefinition>());
            BossTestRig.Set(definition, "unitId", "boss.visual"); BossTestRig.Set(definition, "displayName", "Visual Boss");
            BossTestRig.Set(definition, "kind", (int)UnitKind.Monster);
            var parent = Track(new GameObject("Boss visual setup")); parent.SetActive(false);
            var root = new GameObject("Visual Boss"); root.transform.SetParent(parent.transform, false);
            body = root.AddComponent<Rigidbody2D>(); body.bodyType = RigidbodyType2D.Kinematic;
            root.AddComponent<BoxCollider2D>();
            BossTestRig.Set(root.AddComponent<Unit>(), "definition", definition);
            root.AddComponent<UnitHealth>(); var combat = root.AddComponent<BossCombat2D>();
            fake = new FakeState { Settings = settings, Pattern = pattern };
            combat.Bind(fake, null);
            var child = new GameObject("Visual"); child.transform.SetParent(root.transform, false); child.transform.localScale = new Vector3(2f, 4f, 1f);
            sprite = child.AddComponent<SpriteRenderer>(); animator = child.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller; animator.applyRootMotion = false;
            boxHolder = new GameObject("Telegraph box"); boxHolder.transform.SetParent(child.transform, false);
            box = boxHolder.AddComponent<SpriteRenderer>(); boxHolder.transform.position = new Vector3(0, 0, 3f);
            visual = child.AddComponent<BossVisual2D>();
            BossTestRig.Set(visual, "stateSource", combat); BossTestRig.Set(visual, "telegraphRenderer", box);
            parent.SetActive(true);
            yield return null;
        }

        void Tick(float dt = 0f) { LateUpdate.Invoke(visual, null); animator.Update(dt); }
        float Time01 => animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
        bool InState(string name) => animator.GetCurrentAnimatorStateInfo(0).IsName(name);

        [UnityTest]
        public IEnumerator PlaysPatternStatesOnlyWhenStateOrVersionChanges()
        {
            yield return Build();
            Tick(.1f); Assert.That(InState("Idle"));
            body.linearVelocity = new Vector2(3f, 0); Tick(.1f); Assert.That(InState("Run"), "moving approach");
            body.linearVelocity = Vector2.zero; Tick(.1f); Assert.That(InState("Idle"));
            fake.State = BossState.Telegraph; fake.StateVersion = 2; Tick(.1f); Assert.That(InState("Slash"));
            Tick(.5f); float t = Time01; Assert.That(t, Is.GreaterThan(.4f));
            Tick(.1f); Tick(.1f); Assert.That(Time01, Is.GreaterThan(t), "same state and version must not restart");
            fake.StateVersion = 3; Tick(0f); Assert.That(Time01, Is.LessThan(.05f), "new version restarts from 0");
            fake.State = BossState.Execute; fake.StateVersion = 4; Tick(); Assert.That(InState("SlashHit"));
            fake.State = BossState.Staggered; fake.Pattern = null; fake.StateVersion = 5; Tick(); Assert.That(InState("Hit"));
            fake.State = BossState.PhaseShift; fake.StateVersion = 6; Tick(); Assert.That(InState("PhaseShift"));
            fake.State = BossState.Death; fake.StateVersion = 7; Tick(); Assert.That(InState("Death"));
        }

        [UnityTest]
        public IEnumerator WritesProgressIntoActionTimeForReactionStates()
        {
            yield return Build();
            foreach (var state in new[] { BossState.Staggered, BossState.PhaseShift, BossState.Death, BossState.Recover })
            {
                fake.State = state; fake.StateVersion++; fake.Progress = .4f; Tick();
                Assert.That(animator.GetFloat("ActionTime"), Is.EqualTo(.4f).Within(1e-4f), state.ToString());
            }
        }

        [UnityTest]
        public IEnumerator FlipsWithFacingAndShowsTelegraphBoxOnlyWhenVisible()
        {
            yield return Build();
            fake.Facing = 1f; Tick(); Assert.That(sprite.flipX, Is.False);
            fake.Facing = -1f; Tick(); Assert.That(sprite.flipX, Is.True);
            Assert.That(box.enabled, Is.False);
            fake.Telegraph = new BossTelegraph { Visible = true, Center = new Vector2(5f, 2f), Size = new Vector2(3f, 1.5f) };
            Tick();
            Assert.That(box.enabled, Is.True);
            Assert.That((Vector2)boxHolder.transform.position, Is.EqualTo(new Vector2(5f, 2f)).Using(Vector2EqualityComparer.Instance));
            Assert.That(boxHolder.transform.position.z, Is.EqualTo(3f).Within(1e-4f));
            Assert.That(boxHolder.transform.lossyScale.x, Is.EqualTo(3f).Within(1e-3f));
            Assert.That(boxHolder.transform.lossyScale.y, Is.EqualTo(1.5f).Within(1e-3f));
            Assert.That(box.color.r, Is.EqualTo(1f).Within(1e-3f)); Assert.That(box.color.a, Is.EqualTo(.35f).Within(1e-3f));
            fake.Telegraph = default; Tick(); Assert.That(box.enabled, Is.False);
        }

        [UnityTest]
        public IEnumerator LockedTelegraphUsesTheLockedColor()
        {
            yield return Build();
            fake.Telegraph = new BossTelegraph { Visible = true, Center = Vector2.zero, Size = Vector2.one, Locked = false };
            Tick(); var tracking = box.color;
            fake.Telegraph = new BossTelegraph { Visible = true, Center = Vector2.zero, Size = Vector2.one, Locked = true };
            Tick();
            Assert.That(box.color, Is.Not.EqualTo(tracking));
            Assert.That(box.color.r, Is.EqualTo(1f).Within(1e-3f)); Assert.That(box.color.g, Is.EqualTo(.25f).Within(1e-3f));
            Assert.That(box.color.b, Is.EqualTo(.2f).Within(1e-3f)); Assert.That(box.color.a, Is.EqualTo(.35f).Within(1e-3f));
            fake.Telegraph = new BossTelegraph { Visible = true, Center = Vector2.zero, Size = Vector2.one };
            Tick(); Assert.That(box.color, Is.EqualTo(tracking), "unlocked goes back to the tracking color");
        }

        [UnityTest]
        public IEnumerator MissingAnimatorStateWarnsOnceAndKeepsCurrent()
        {
            yield return Build();
            Tick(.1f); Assert.That(InState("Idle"));
            fake.State = BossState.Recover; fake.StateVersion = 2;
            LogAssert.Expect(LogType.Warning, new Regex("Missing"));
            Tick(.1f); Assert.That(InState("Idle"), "keeps the current state");
            fake.StateVersion = 3; Tick(.1f); Tick(.1f); // would fail the test on any further warning
        }
    }
}
#endif
