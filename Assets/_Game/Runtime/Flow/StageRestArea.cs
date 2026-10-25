using System.Collections.Generic;
using ActionPlatformer.Player;
using ActionPlatformer.Units.Features;
using UnityEngine;

namespace ActionPlatformer.Flow
{
    [DisallowMultipleComponent]
    public sealed class StageRestArea : MonoBehaviour
    {
        [SerializeField] private BoxCollider2D area;
        [SerializeField] private BoxCollider2D floor;
        [SerializeField] private Transform arrival;
        [SerializeField] private StageGate nextEntry;
        public bool Visited { get; private set; }
        public StageGate NextEntry => nextEntry;
        public float EndX => area.bounds.max.x;
        public bool IsConfigured => area != null && area.isTrigger && floor != null && !floor.isTrigger &&
            arrival != null && nextEntry != null && nextEntry.IsConfigured;

        public bool TryArrival(CapsuleCollider2D body, int terrainMask, List<Collider2D> overlaps, out Vector2 position)
        {
            Vector2 offset = (Vector2)body.bounds.center - (Vector2)body.transform.position;
            position = new Vector2(arrival.position.x, floor.bounds.max.y + body.bounds.extents.y + .02f - offset.y);
            return floor.enabled && floor.gameObject.activeInHierarchy &&
                position.x - body.bounds.extents.x >= area.bounds.min.x &&
                position.x + body.bounds.extents.x <= area.bounds.max.x &&
                position.x - body.bounds.extents.x >= floor.bounds.min.x &&
                position.x + body.bounds.extents.x <= floor.bounds.max.x &&
                StageTransferRoute.SpaceFree(body, position, terrainMask, overlaps);
        }

        public bool TryVisit(UnitHealth health, Collider2D body)
        {
            if (Visited || health == null || !health.IsAlive || body == null || !body.isActiveAndEnabled ||
                !area.Distance(body).isOverlapped) return false;
            health.RestoreToFull();
            Visited = true;
            return true;
        }
    }
}
