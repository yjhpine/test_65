using ActionPlatformer.Player;
using UnityEngine;

namespace ActionPlatformer.Flow
{
    [DisallowMultipleComponent]
    public sealed class StageCameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Camera viewCamera;
        [SerializeField] private float leftBoundary;
        [SerializeField] private float rightBoundary = 60f;
        private float baseOrthographicSize;
        private IPlayerActionState targetActions;

        private void Start()
        {
            if (target == null || viewCamera == null || !viewCamera.orthographic ||
                viewCamera.transform.parent != transform || rightBoundary <= leftBoundary)
            {
                Debug.LogError("Stage camera needs a target, a direct child orthographic camera, and ordered boundaries.", this);
                enabled = false;
                return;
            }
            targetActions = target.GetComponent<IPlayerActionState>();
            baseOrthographicSize = viewCamera.orthographicSize;
            Follow();
        }

        private void LateUpdate()
        {
            if (Time.timeScale > 0f) Follow();
        }

        private void Follow()
        {
            if (target == null) return;
            transform.position = CalculateRigPosition();
        }

        public void SnapToTarget() => Follow();

        public Vector3 CalculateRigPosition()
        {
            if (target == null || viewCamera == null) return transform.position;
            // Authored lens prevents damage zoom from shifting the clamp. Child feedback remains additive.
            float halfWidth = baseOrthographicSize * viewCamera.aspect;
            float minimum = leftBoundary + halfWidth;
            float maximum = rightBoundary - halfWidth;
            // Burrow parks the physics root; follow the visible logical position until it ends.
            float targetX = targetActions != null && targetActions.IsUnderground
                ? targetActions.GroundMarkerPosition.x : target.position.x;
            Vector3 position = transform.position;
            position.x = minimum < maximum ? Mathf.Clamp(targetX, minimum, maximum) : (leftBoundary + rightBoundary) * 0.5f;
            return position;
        }
    }
}
