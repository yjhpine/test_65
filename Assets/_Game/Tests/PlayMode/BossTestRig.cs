#if UNITY_EDITOR
using ActionPlatformer.Player;
using ActionPlatformer.Units;
using ActionPlatformer.Units.Bosses;
using ActionPlatformer.Units.Features;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ActionPlatformer.Tests
{
    // Stand-in for the player's read-only state interfaces. Tests change the public fields directly.
    public sealed class BossPlayerProbe : MonoBehaviour, IPlayerActionState, ICharacterMotionState, IUnitRepositionState, IUnitViewSource
    {
        public Vector2 velocity;
        public bool grounded = true;
        public bool underground;
        public PlayerAttackPhase attackPhase;
        public uint repositionVersion;
        public Camera view;
        public float facing = 1;
        public Vector2? marker;

        public Vector2 Velocity => velocity;
        public bool IsGrounded => grounded;
        public uint RepositionVersion => repositionVersion;
        public Camera View => view;
        public GlitchPreview GlitchPreview => default;
        public float GlitchMaxDistance => 0;
        public bool IsUnderground => underground;
        public bool CanEmerge => false;
        public Vector2 UndergroundPosition => transform.position;
        public Vector2 GroundMarkerPosition => marker ?? (Vector2)transform.position;
        public float Facing => facing;
        public PlayerAttackPhase AttackPhase => attackPhase;
        public PlayerAttackPresentation AttackPresentation => default;
        public PlayerReactionPresentation ReactionPresentation => default;
        public PlayerGlitchPresentation GlitchPresentation => default;
        public Bounds AttackBounds => default;
        public PlayerShockwave Shockwave => default;
    }

    // Builds a boss, a floor and a player stand-in in code; no boss assets are needed. Call Cleanup from [TearDown].
    public sealed class BossTestRig
    {
        public const float FloorTop = 50.5f;
        public static readonly WaitForFixedUpdate Step = new WaitForFixedUpdate();
        public readonly List<Object> Created = new List<Object>();
        public readonly List<Unit> Bosses = new List<Unit>();
        public BossFsmDefinition Fsm;
        public BossPlayerProbe Probe;
        public UnitHealth Player;

        public Unit Boss => Bosses[0];
        public UnitHealth Health => Boss.GetComponent<UnitHealth>();
        public BossCombat2D Combat => Boss.GetComponent<BossCombat2D>();
        public BossBrain Brain => (BossBrain)Combat.State;
        public static BossBrain BrainOf(Unit boss) => (BossBrain)boss.GetComponent<BossCombat2D>().State;

        public static void Set(Object obj, string name, object value)
        {
            var so = new SerializedObject(obj); var p = so.FindProperty(name);
            if (p == null) throw new ArgumentException("No serialized field " + name);
            if (value == null) p.objectReferenceValue = null; else if (value is Object o) p.objectReferenceValue = o; else if (value is float f) p.floatValue = f;
            else if (value is int i) p.intValue = i; else if (value is bool b) p.boolValue = b; else if (value is string s) p.stringValue = s;
            else throw new ArgumentException("Unsupported value for " + name);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetThresholds(Object obj, params float[] values)
        {
            var so = new SerializedObject(obj); var p = so.FindProperty("phaseThresholds");
            p.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).floatValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public T Track<T>(T obj) where T : Object { Created.Add(obj); return obj; }

        public GameObject Floor()
        {
            var go = new GameObject("Boss test floor"); Created.Add(go);
            go.transform.position = new Vector2(0, 50);
            go.AddComponent<BoxCollider2D>().size = new Vector2(60, 1);
            Physics2D.SyncTransforms();
            return go;
        }

        public BossPatternSelector Selector(params BossSelectorNode[] nodes)
        {
            var selector = Track(ScriptableObject.CreateInstance<BossPatternSelector>());
            selector.SetRoot(new BossSelectorBranch(nodes));
            return selector;
        }

        public ScriptedBossPattern Pattern(float telegraph = .3f, float execute = .3f, float recovery = .2f, float cooldown = 0, bool blockAll = false)
        {
            var p = Track(ScriptableObject.CreateInstance<ScriptedBossPattern>());
            p.telegraphTime = telegraph; p.executeTime = execute; p.blockAll = blockAll; p.name = "Scripted";
            Set(p, "recovery", recovery); Set(p, "cooldown", cooldown);
            return p;
        }

        public BossFsmDefinition CreateFsm(BossPatternSelector selector, Action<BossFsmDefinition> configure = null)
        {
            var fsm = Track(ScriptableObject.CreateInstance<BossFsmDefinition>());
            Set(fsm, "selector", selector); Set(fsm, "useFixedSeed", true); Set(fsm, "seed", 1);
            configure?.Invoke(fsm);
            return fsm;
        }

        // Spawns a boss standing on the floor. Pass sharedFsm to share one set of assets between several bosses.
        public IEnumerator SpawnBoss(BossPatternSelector selector, Action<BossFsmDefinition> configure = null, float x = 0, BossFsmDefinition sharedFsm = null)
        {
            Fsm = sharedFsm != null ? sharedFsm : CreateFsm(selector, configure);
            var definition = Track(ScriptableObject.CreateInstance<UnitDefinition>());
            Set(definition, "unitId", "boss.rig"); Set(definition, "displayName", "Rig Boss");
            Set(definition, "kind", (int)UnitKind.Monster); Set(definition, "maxHealth", 300);
            Set(definition, "fsm", Fsm); Set(definition, "allowForcedMovement", false);
            var parent = new GameObject("Boss test setup"); parent.SetActive(false); Created.Add(parent);
            var root = new GameObject("Rig Boss"); root.transform.SetParent(parent.transform, false);
            root.transform.position = new Vector3(x, FloorTop + 1.2f + .02f); root.layer = 2;
            var body = root.AddComponent<Rigidbody2D>(); body.freezeRotation = true;
            root.AddComponent<BoxCollider2D>().size = new Vector2(1.6f, 2.4f);
            var unit = root.AddComponent<Unit>(); Set(unit, "definition", definition);
            var health = root.AddComponent<UnitHealth>(); Set(health, "deactivateOnDeath", false);
            root.AddComponent<BossCombat2D>(); root.AddComponent<GroundMovement2D>();
            parent.SetActive(true);
            Bosses.Add(unit);
            yield return null; yield return Step;
            Physics2D.SyncTransforms();
        }

        // The player stand-in is a Player-kind unit offset from the first boss. It is created without waiting a frame:
        // a frame would let the boss tick on its own and start a pattern before the test body begins.
        public void SpawnPlayer(Vector2 offset)
        {
            var def = Track(ScriptableObject.CreateInstance<UnitDefinition>());
            Set(def, "unitId", "player.rig"); Set(def, "displayName", "Rig Player");
            Set(def, "kind", (int)UnitKind.Player); Set(def, "maxHealth", 1000);
            var player = new GameObject("Boss pattern target"); player.SetActive(false); Created.Add(player); player.layer = 2;
            player.transform.position = (Vector2)Boss.transform.position + offset;
            Set(player.AddComponent<Unit>(), "definition", def);
            player.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
            player.AddComponent<BoxCollider2D>().size = new Vector2(.6f, 1f);
            Player = player.AddComponent<UnitHealth>(); Set(Player, "deactivateOnDeath", false);
            Probe = player.AddComponent<BossPlayerProbe>();
            player.SetActive(true);
            Physics2D.SyncTransforms();
            foreach (var boss in Bosses) { boss.Fsm.Stop(); boss.Fsm.Start(); }
        }

        public void MovePlayer(Vector2 position)
        {
            Player.GetComponent<Rigidbody2D>().position = position; Player.transform.position = position;
            Physics2D.SyncTransforms();
        }

        public void Tick(float dt, Unit boss = null) => (boss ?? Boss).Fsm.Tick(dt);

        public void TickFor(float seconds, float dt = .02f, Unit boss = null)
        {
            for (float t = 0; t < seconds - 1e-4f; t += dt) Tick(dt, boss);
        }

        public void Cleanup()
        {
            for (int i = Created.Count - 1; i >= 0; i--) if (Created[i] != null) Object.DestroyImmediate(Created[i]);
            Created.Clear(); Bosses.Clear();
        }
    }
}
#endif
