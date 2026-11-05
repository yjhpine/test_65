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
        [Header("Vertical follow")]
        [Tooltip("Highest the target may climb above its start height while the camera still follows.")]
        [SerializeField, Min(0f)] private float maxRise = 20f;
        [Tooltip("Climb needed above the start height before the camera starts rising, so normal jumps do not bob the view.")]
        [SerializeField, Min(0f)] private float verticalDeadZone = 4.5f;
        [SerializeField, Min(0f)] private float verticalSmoothTime = 0.2f;
        private float baseOrthographicSize;
        private IPlayerActionState targetActions;
        private float baseRigY;
        private float referenceTargetY;
        private float velocityY;
        private bool started;

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
            baseRigY = transform.position.y;
            referenceTargetY = target.position.y;
            started = true;
            SnapToTarget();
        }

        private void LateUpdate()
        {
            if (Time.timeScale > 0f) Follow();
        }

        private void Follow()
        {
            if (target == null) return;
            Vector3 position = CalculateRigPosition();
            position.y = Mathf.SmoothDamp(transform.position.y, position.y, ref velocityY, verticalSmoothTime);
            transform.position = position;
        }

        public void SnapToTarget()
        {
            if (target == null) return;
            velocityY = 0f;
            transform.position = CalculateRigPosition();
        }

        public Vector3 CalculateRigPosition()
        {
            if (target == null || viewCamera == null || !started) return transform.position;
            // Authored lens prevents damage zoom from shifting the clamp. Child feedback remains additive.
            float halfWidth = baseOrthographicSize * viewCamera.aspect;
            float minimum = leftBoundary + halfWidth;
            float maximum = rightBoundary - halfWidth;
            // Burrow parks the physics root; follow the visible logical position until it ends.
            float targetX = targetActions != null && targetActions.IsUnderground
                ? targetActions.GroundMarkerPosition.x : target.position.x;
            float targetY = targetActions != null && targetActions.IsUnderground
                ? targetActions.GroundMarkerPosition.y : target.position.y;
            // The target's climb is capped at maxRise; the dead zone only delays the camera, never lets it sink below its authored height.
            float climb = Mathf.Clamp(targetY - referenceTargetY, 0f, maxRise);
            Vector3 position = transform.position;
            position.y = baseRigY + Mathf.Max(0f, climb - verticalDeadZone);
            position.x = minimum < maximum ? Mathf.Clamp(targetX, minimum, maximum) : (leftBoundary + rightBoundary) * 0.5f;
            return position;
        }
    }
}
