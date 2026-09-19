using UnityEngine;

namespace ActionPlatformer.Units.Features
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Unit), typeof(UnitHealth))]
    public sealed class UnitCombat2D : MonoBehaviour
    {
        [SerializeField] private LayerMask targetMask = 1 << 2;
        [SerializeField] private LayerMask obstacleMask = 1;
        private readonly Collider2D[] candidates = new Collider2D[32];
        private readonly RaycastHit2D[] obstacles = new RaycastHit2D[8];
        private ContactFilter2D targetFilter;
        private ContactFilter2D obstacleFilter;
        private Unit owner;
        private UnitHealth health;

        public UnitHealth Target { get; private set; }

        private void Awake()
        {
            owner = GetComponent<Unit>();
            health = GetComponent<UnitHealth>();
            targetFilter = new ContactFilter2D { useLayerMask = true, layerMask = targetMask, useTriggers = false };
            obstacleFilter = new ContactFilter2D { useLayerMask = true, layerMask = obstacleMask, useTriggers = false };
        }

        public void RefreshTarget(UnitKind targetKind, float acquireRange, float loseRange)
        {
            if (!isActiveAndEnabled || !owner.isActiveAndEnabled || !health.IsAlive)
            {
                ClearTarget();
                return;
            }
            if (IsAvailable(Target) && Target.Owner.Kind == targetKind && InRange(Target, loseRange) && HasLineOfSight(Target))
                return;

            ClearTarget();
            float nearestSquared = acquireRange * acquireRange;
            int count = Physics2D.OverlapCircle(transform.position, acquireRange, targetFilter, candidates);
            for (int i = 0; i < count; i++)
            {
                var unit = candidates[i].GetComponentInParent<Unit>();
                if (unit == null || unit == owner || unit.Kind != targetKind || !unit.TryGetComponent<UnitHealth>(out var candidate))
                    continue;
                float squared = ((Vector2)candidate.transform.position - (Vector2)transform.position).sqrMagnitude;
                if (squared > nearestSquared || !IsAvailable(candidate) || !HasLineOfSight(candidate)) continue;
                nearestSquared = squared;
                Target = candidate;
            }
        }

        public bool IsTargetInRange(float range) => IsAvailable(Target) && InRange(Target, range) && HasLineOfSight(Target);

        public bool TryHit(UnitHealth expectedTarget, float range)
        {
            if (!isActiveAndEnabled || !owner.isActiveAndEnabled || !health.IsAlive || expectedTarget != Target ||
                !IsTargetInRange(range)) return false;
            return Target.ApplyDamage(owner.Definition.AttackPower) > 0;
        }

        public void ClearTarget() => Target = null;
        private void OnDisable() => ClearTarget();

        private bool IsAvailable(UnitHealth candidate) => candidate != null && candidate != health &&
            candidate.isActiveAndEnabled && candidate.IsAlive && candidate.Owner != null && candidate.Owner.isActiveAndEnabled;

        private bool InRange(UnitHealth candidate, float range) =>
            ((Vector2)candidate.transform.position - (Vector2)transform.position).sqrMagnitude <= range * range;

        private bool HasLineOfSight(UnitHealth candidate) =>
            Physics2D.Linecast(transform.position, candidate.transform.position, obstacleFilter, obstacles) == 0;
    }
}
