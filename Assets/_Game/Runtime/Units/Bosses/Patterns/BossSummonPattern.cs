using System.Collections.Generic;
using ActionPlatformer.Units.Features;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ActionPlatformer.Units.Bosses
{
    [CreateAssetMenu(menuName = "Action Platformer/Bosses/Patterns/Summon", fileName = "BossSummon")]
    public sealed class BossSummonPattern : BossPatternDefinition
    {
        private const float WallMargin = .6f;
        [Tooltip("Spawned for each offset. Needs a Unit and a UnitHealth on the root.")]
        [SerializeField] private GameObject minionPrefab;
        [Tooltip("One minion per entry, relative to the boss body center. X is not mirrored by the boss facing.")]
        [SerializeField] private Vector2[] spawnOffsets = { new Vector2(-2.5f, 0), new Vector2(2.5f, 0) };
        [Min(1)] [SerializeField] private int maxAlive = 2;
        [Min(0)] [SerializeField] private float telegraphTime = .8f;
        [Min(0)] [SerializeField] private float executeTime = .3f;
        [Tooltip("When the boss dies its minions die with it, and they are removed when the fight stops.")]
        [SerializeField] private bool despawnWithBoss = true;

        public GameObject MinionPrefab => minionPrefab;
        public IReadOnlyList<Vector2> SpawnOffsets => spawnOffsets;
        public int MaxAlive => maxAlive;
        public float TelegraphTime => telegraphTime;
        public float ExecuteTime => executeTime;
        public bool DespawnWithBoss => despawnWithBoss;

        public override bool CanStart(BossSelection s) =>
            minionPrefab != null && (s.Context?.RunOf(this) is not Run run || run.AliveCount < maxAlive);

        public override BossPatternRun CreateRun(BossContext context) => new Run(this, context);

        public override bool TryValidate(out string error)
        {
            if (!base.TryValidate(out error)) return false;
            if (minionPrefab == null) { error = $"{DisplayName}: minionPrefab이 비어 있습니다."; return false; }
            if (minionPrefab.GetComponent<Unit>() == null || minionPrefab.GetComponent<UnitHealth>() == null)
            { error = $"{DisplayName}: minionPrefab의 루트에 Unit과 UnitHealth가 있어야 합니다."; return false; }
            if (spawnOffsets == null || spawnOffsets.Length == 0) { error = $"{DisplayName}: spawnOffsets가 비어 있습니다."; return false; }
            foreach (var offset in spawnOffsets)
                if (float.IsNaN(offset.x) || float.IsInfinity(offset.x) || float.IsNaN(offset.y) || float.IsInfinity(offset.y))
                { error = $"{DisplayName}: spawnOffsets는 유한한 값이어야 합니다."; return false; }
            if (maxAlive < 1) { error = $"{DisplayName}: maxAlive는 1 이상이어야 합니다."; return false; }
            if (!BossPatternChecks.NonNegative(telegraphTime) || !BossPatternChecks.NonNegative(executeTime))
            { error = $"{DisplayName}: telegraphTime과 executeTime은 0 이상이어야 합니다."; return false; }
            error = null;
            return true;
        }

        private sealed class Run : BossPatternRun
        {
            private readonly BossSummonPattern owner;
            private readonly List<GameObject> minions = new List<GameObject>();
            private float elapsed;
            private bool telegraphing;

            public Run(BossSummonPattern owner, BossContext context) : base(context) { this.owner = owner; }

            private static bool IsAlive(GameObject minion) =>
                minion != null && minion.activeInHierarchy && minion.TryGetComponent<UnitHealth>(out var health) && health.IsAlive;

            public int AliveCount
            {
                get { int count = 0; foreach (var minion in minions) if (IsAlive(minion)) count++; return count; }
            }

            public override void BeginTelegraph() { elapsed = 0; telegraphing = true; }
            public override bool TickTelegraph(float dt) { elapsed += dt; return elapsed >= owner.telegraphTime; }

            public override void BeginExecute()
            {
                elapsed = 0; telegraphing = false;
                Prune();
                int missing = owner.maxAlive - AliveCount;
                var center = Context.Combat.Center;
                foreach (var offset in owner.spawnOffsets)
                    if (missing > 0 && TrySpawn(center, offset)) missing--;
            }

            public override bool TickExecute(float dt) { elapsed += dt; return elapsed >= owner.executeTime; }
            public override void End() { telegraphing = false; }
            public override void Cancel() { telegraphing = false; }

            public override void OnBossDied()
            {
                if (!owner.despawnWithBoss) return;
                // Killing (not removing) lets each minion play its own death.
                foreach (var minion in minions)
                    if (IsAlive(minion) && minion.TryGetComponent<UnitHealth>(out var health)) health.ApplyDamage(health.CurrentHealth);
            }

            public override void OnBossStopped()
            {
                if (!owner.despawnWithBoss) return;
                foreach (var minion in minions) Remove(minion);
                minions.Clear();
            }

            public override float Progress => Mathf.Clamp01(elapsed / Mathf.Max(.0001f, telegraphing ? owner.telegraphTime : owner.executeTime));

            private bool TrySpawn(Vector2 center, Vector2 offset)
            {
                var combat = Context.Combat;
                float x = combat.RayClampX(center, center.x + offset.x, WallMargin);
                if (!combat.TryFindGround(x, center.y + offset.y + 1f, 10f, out float groundY)) return false;
                var minion = Object.Instantiate(owner.minionPrefab, new Vector3(x, groundY + 2f, owner.minionPrefab.transform.position.z), Quaternion.identity);
                // A root object in the boss's scene: never a child of a stage encounter root.
                SceneManager.MoveGameObjectToScene(minion, Context.Owner.gameObject.scene);
                Physics2D.SyncTransforms();
                foreach (var collider in minion.GetComponentsInChildren<Collider2D>())
                {
                    if (collider.isTrigger) continue;
                    minion.transform.position += Vector3.up * (groundY + .02f - collider.bounds.min.y);
                    Physics2D.SyncTransforms();
                    break;
                }
                minions.Add(minion);
                return true;
            }

            // Dead minions that already switched themselves off would otherwise pile up for the whole fight.
            private void Prune()
            {
                for (int i = minions.Count - 1; i >= 0; i--)
                {
                    var minion = minions[i];
                    if (minion == null) { minions.RemoveAt(i); continue; }
                    if (minion.activeInHierarchy) continue;
                    Remove(minion); minions.RemoveAt(i);
                }
            }

            private static void Remove(GameObject minion)
            {
                if (minion == null) return;
                if (Application.isPlaying) Object.Destroy(minion); else Object.DestroyImmediate(minion);
            }
        }
    }
}
