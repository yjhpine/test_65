using System.Collections.Generic;
using UnityEngine;

namespace ActionPlatformer.Units.Features
{
    // Presentation only. EnemyCombat2D/FSM still own the target, aim lock, shot and damage.
    [DisallowMultipleComponent, RequireComponent(typeof(SpriteRenderer), typeof(Animator))]
    public sealed class SurveillanceTurretVisual : MonoBehaviour
    {
        [Tooltip("All sprites used by this controller; split once during initialization without editing source textures.")]
        [SerializeField] private Sprite[] sourceFrames;
        [Tooltip("Pixel row measured from the bottom, above the stationary pedestal.")]
        [SerializeField] private int pedestalHeight = 19;
        [SerializeField] private Vector2 headPivot = new Vector2(32f, 27f);
        private readonly Dictionary<Sprite, Parts> parts = new Dictionary<Sprite, Parts>();
        private SpriteRenderer source, head, pedestal;
        private Animator animator;
        private string animationName;
        private float shownFacing;
        private bool hasShownFacing;
        private static readonly int ActionTime = Animator.StringToHash("ActionTime");
        private readonly struct Parts
        {
            public readonly Sprite Head, Pedestal;
            public readonly Vector3 HeadPosition;
            public Parts(Sprite head, Sprite pedestal, Vector3 position)
            { Head = head; Pedestal = pedestal; HeadPosition = position; }
        }

        public void Initialize(SpriteRenderer renderer, Animator controller)
        {
            if (source != null) return;
            source = renderer; animator = controller;
            pedestal = CreatePart("Turret pedestal", 0);
            head = CreatePart("Turret head", 1);
            if (sourceFrames != null) foreach (var frame in sourceFrames) Cache(frame);
        }

        public void Render(IEnemyActionState state, bool firing, bool dead, float actionTime, Vector2 shotDirection = default)
        {
            if (source == null || !isActiveAndEnabled) return;
            Vector2 aim = firing && shotDirection.sqrMagnitude > .00001f ? shotDirection.normalized : state.Aim;
            bool idle = !dead && !firing && (state.Phase == EnemyPhase.Idle || state.Phase == EnemyPhase.Turning);
            float facing = !dead && !idle && Mathf.Abs(aim.x) > .00001f ? Mathf.Sign(aim.x) : state.Facing;
            if (dead && hasShownFacing) facing = shownFacing;
            string desired;
            if (idle)
            {
                // The FSM owns turning and acquisition. These native clips already contain both facings.
                bool turning = state.Phase == EnemyPhase.Turning;
                desired = turning ? (state.Facing > 0f ? "IdleTurnBack" : "IdleTurn") : (state.Facing > 0f ? "IdleOpposite" : "Idle");
                actionTime = turning ? state.Progress : 0f;
                shownFacing = turning && actionTime >= .5f ? -state.Facing : state.Facing;
                facing = -1f;
                head.transform.localRotation = Quaternion.identity;
            }
            else
            {
                shownFacing = facing;
                desired = dead ? "Death" : firing ? "Attack" : "AngryIdle";
                if (!dead && aim.sqrMagnitude > .00001f)
                {
                    // Authored art faces left. Flip horizontally, then elevate the head toward the FSM aim.
                    float elevation = Mathf.Atan2(aim.y, Mathf.Abs(aim.x)) * Mathf.Rad2Deg;
                    head.transform.localRotation = Quaternion.Euler(0, 0, facing > 0f ? elevation : -elevation);
                }
                if (!dead && !firing) actionTime = 0f;
            }
            hasShownFacing = true;
            animator.SetFloat(ActionTime, Mathf.Clamp(actionTime, 0f, .9999f));
            if (desired != animationName)
            {
                animationName = desired;
                animator.Play(desired, 0, 0f);
            }
            // Sample before splitting so a newly detected target changes pose in this same render frame.
            animator.Update(0f);
            if (!parts.TryGetValue(source.sprite, out var frame))
            {
                source.forceRenderingOff = false;
                head.enabled = pedestal.enabled = false;
                return;
            }
            source.forceRenderingOff = true;
            head.sprite = frame.Head; pedestal.sprite = frame.Pedestal;
            head.transform.localPosition = frame.HeadPosition;
            head.flipX = pedestal.flipX = facing > 0f;
            head.color = pedestal.color = source.color;
            head.enabled = pedestal.enabled = true;
        }

        private SpriteRenderer CreatePart(string label, int order)
        {
            var go = new GameObject(label); go.transform.SetParent(transform, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sharedMaterial = source.sharedMaterial;
            renderer.sortingLayerID = source.sortingLayerID;
            renderer.sortingOrder = source.sortingOrder + order;
            // These parts replace the hidden source renderer, so each needs its own hit flash.
            if (source.GetComponent<UnitDamageFlash>() != null) go.AddComponent<UnitDamageFlash>();
            renderer.enabled = false;
            return renderer;
        }

        private void Cache(Sprite frame)
        {
            if (frame == null || parts.ContainsKey(frame)) return;
            var rect = frame.rect;
            float split = Mathf.Clamp(pedestalHeight, 1, (int)rect.height - 1);
            var upper = Sprite.Create(frame.texture, new Rect(rect.x, rect.y + split, rect.width, rect.height - split),
                new Vector2(headPivot.x / rect.width, (headPivot.y - split) / (rect.height - split)),
                frame.pixelsPerUnit, 0, SpriteMeshType.FullRect);
            var lower = Sprite.Create(frame.texture, new Rect(rect.x, rect.y, rect.width, split),
                new Vector2(frame.pivot.x / rect.width, frame.pivot.y / split), frame.pixelsPerUnit, 0, SpriteMeshType.FullRect);
            upper.name = frame.name + " head"; lower.name = frame.name + " pedestal";
            parts.Add(frame, new Parts(upper, lower, (headPivot - frame.pivot) / frame.pixelsPerUnit));
        }

        private void OnDisable()
        {
            hasShownFacing = false; animationName = null;
            if (source != null) source.forceRenderingOff = false;
            if (head != null) { head.enabled = false; head.transform.localRotation = Quaternion.identity; }
            if (pedestal != null) pedestal.enabled = false;
        }
        private void OnDestroy()
        {
            foreach (var frame in parts.Values) { Destroy(frame.Head); Destroy(frame.Pedestal); }
            parts.Clear();
        }
    }
}
