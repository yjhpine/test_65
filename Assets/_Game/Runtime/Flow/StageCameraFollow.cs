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
        [Tooltip("Climb needed above the ground the target stands on before the camera starts rising, so normal jumps do not bob the view. " +
                 "Ground higher than this above the start is framed like the start floor.")]
        [SerializeField, Min(0f)] private float verticalDeadZone = 4.5f;
        [Tooltip("Climb past the dead zone over which the camera gains on the target until the headroom matches the start floor. 0 keeps the dead-zone lag.")]
        [SerializeField, Min(0f)] private float verticalCatchUp = 4.5f;
        [SerializeField, Min(0f)] private float verticalSmoothTime = 0.2f;
        [Header("Descending (arenas below the start height)")]
        [Tooltip("Deepest the target may drop below its start height while the camera still follows. 0 keeps the camera at its authored height.")]
        [SerializeField, Min(0f)] private float maxDrop;
        [Tooltip("Drop needed below the start height before the camera starts sinking.")]
        [SerializeField, Min(0f)] private float lowerDeadZone = 1f;
        [Tooltip("Falls are fast, so the camera follows them with a shorter smoothing time than climbs.")]
        [SerializeField, Min(0f)] private float descentSmoothTime = 0.08f;
        private float baseOrthographicSize;
        private IPlayerActionState targetActions;
        private ICharacterMotionState targetMotion;
        private float baseRigY;
        private float referenceTargetY;
        // Climb of the high ground the target last stood on (0 = the start floor); the view frames it like the start floor.
        private float groundAnchor;
        // Grounded state only refreshes in physics steps, so a teleport can still read as standing for a frame;
        // ground counts once the target has stood still on it this long.
        private const float GroundSettleTime = 0.06f;
        private const float StandingDrift = 0.02f;
        private float standingTime;
        private float lastTargetY;
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
            targetMotion = target.GetComponent<ICharacterMotionState>();
            baseOrthographicSize = viewCamera.orthographicSize;
            baseRigY = transform.position.y;
            referenceTargetY = target.position.y;
            lastTargetY = target.position.y;
            groundAnchor = 0f;
            standingTime = 0f;
            started = true;
            SnapToTarget();
        }

        private void LateUpdate()
        {
            if (Time.timeScale <= 0f) return;
            UpdateGroundAnchor(Time.deltaTime);
            Follow();
        }

        // Only standing ground sets the framing: jumps, glitches, teleports and burrows leave it as it was.
        private void UpdateGroundAnchor(float deltaTime)
        {
            if (!started || target == null) return;
            float y = target.position.y;
            bool standing = targetMotion != null && targetMotion.IsGrounded &&
                            (targetActions == null || !targetActions.IsUnderground) &&
                            Mathf.Abs(y - lastTargetY) < StandingDrift;
            lastTargetY = y;
            standingTime = standing ? standingTime + deltaTime : 0f;
            if (standingTime < GroundSettleTime) return;
            float climb = y - referenceTargetY;
            groundAnchor = climb > verticalDeadZone ? Mathf.Min(climb, maxRise) : 0f;
        }

        private void Follow()
        {
            if (target == null) return;
            Vector3 position = CalculateRigPosition();
            float smooth = position.y < transform.position.y ? descentSmoothTime : verticalSmoothTime;
            position.y = Mathf.SmoothDamp(transform.position.y, position.y, ref velocityY, smooth);
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
            // The target's climb is capped at maxRise (and its drop at maxDrop, 0 by default: the camera never sinks).
            // Above the ground it stands on, the dead zone delays the camera and the catch-up then restores the floor headroom;
            // below it, the lower dead zone delays the descent.
            float climb = Mathf.Clamp(targetY - referenceTargetY, -maxDrop, maxRise);
            float offset = climb >= groundAnchor
                ? groundAnchor + RiseAbove(climb - groundAnchor)
                : Mathf.Min(groundAnchor, climb + lowerDeadZone);
            Vector3 position = transform.position;
            position.y = baseRigY + Mathf.Clamp(offset, -maxDrop, maxRise);
            position.x = minimum < maximum ? Mathf.Clamp(targetX, minimum, maximum) : (leftBoundary + rightBoundary) * 0.5f;
            return position;
        }

        // Camera rise for a climb above the ground: nothing inside the dead zone, then faster than the target
        // until it has caught up (the rise equals the climb), so the view keeps the start floor's headroom.
        private float RiseAbove(float climb)
        {
            float past = climb - verticalDeadZone;
            if (past <= 0f) return 0f;
            if (verticalCatchUp <= 0f) return past;
            return Mathf.Min(climb, past * (verticalDeadZone + verticalCatchUp) / verticalCatchUp);
        }
    }
}
