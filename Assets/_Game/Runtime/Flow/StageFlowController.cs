using ActionPlatformer.Player;
using ActionPlatformer.Units.Features;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using ActionPlatformer.Cinematics;
using System.Collections.Generic;
using ActionPlatformer.Units;

namespace ActionPlatformer.Flow
{
    public enum StageState { Playing, Dying, Restarting, Completed }
    public enum EncounterPhase { Combat, TransferPending, Transferring, Rest, TransferBlocked }

    [DisallowMultipleComponent]
    public sealed class StageFlowController : MonoBehaviour
    {
        [SerializeField] private PlayerUnit player;
        [SerializeField] private UnitHealth[] requiredEnemies;
        [SerializeField] private StageGoal goal;
        [SerializeField] private StageFlowView view;
        [SerializeField] private CutsceneCoordinator cinematics;
        [Tooltip("Optional ordered encounter loop. Empty keeps the original single-stage objective.")]
        [SerializeField] private StageEncounter[] encounters;
        [SerializeField] private LayerMask transferTerrainMask = 1;
        private UnitHealth playerHealth;
        private PlayerInput playerInput;
        private Collider2D playerBody;
        private float initialTimeScale, initialFixedDeltaTime;
        private bool initialized;
        private readonly List<Vector2> defeatPositions = new List<Vector2>();
        private readonly List<Collider2D> overlaps = new List<Collider2D>();
        private readonly List<UnitHealth> subscriptions = new List<UnitHealth>();
        private CapsuleCollider2D playerCapsule;
        private StageTransferRoute route;
        private float transferElapsed;
        private double transferReadyAt;
        private int passedWaypoint;
        private Vector2 bodyOffset;
        private bool transferRequested;

        public StageState State { get; private set; } = StageState.Playing;
        public int RemainingEnemies { get; private set; }
        public PlayerUnit Player => player;
        public bool UsesEncounters => encounters != null && encounters.Length > 0;
        public EncounterPhase Phase { get; private set; }
        public int CurrentEncounterIndex { get; private set; }
        public StageRestArea CurrentRestArea => UsesEncounters ? encounters[CurrentEncounterIndex].FollowingRest : null;
        public IReadOnlyList<UnitHealth> ActiveEnemies => UsesEncounters ? encounters[CurrentEncounterIndex].Enemies : requiredEnemies;
        public bool CanRequestTransfer => initialized && isActiveAndEnabled && UsesEncounters && State == StageState.Playing &&
            Phase == EncounterPhase.TransferPending && Time.timeScale > 0 && Time.timeAsDouble >= transferReadyAt &&
            player != null && player.isActiveAndEnabled && playerHealth != null && playerHealth.IsAlive &&
            !player.IsControlLocked && !(player.Feedback?.IsTimeEffectActive ?? false) && !(cinematics != null && cinematics.IsBusy);

        private void Awake()
        {
            initialTimeScale = Time.timeScale;
            initialFixedDeltaTime = Time.fixedDeltaTime;
        }

        private void Start()
        {
            if (player == null || goal == null || view == null || !goal.IsConfigured || !view.IsConfigured ||
                (!UsesEncounters && (requiredEnemies == null || requiredEnemies.Length == 0)) ||
                !player.TryGetComponent(out playerHealth) || !player.TryGetComponent(out playerInput) ||
                !player.TryGetComponent(out playerBody) || !player.isActiveAndEnabled || player.Motor == null ||
                playerHealth.MaxHealth <= 0 || gameObject.scene.buildIndex < 0)
            { FailConfiguration(); return; }
            if (UsesEncounters)
            {
                playerCapsule = playerBody as CapsuleCollider2D;
                if (playerCapsule == null || !InitializeEncounters()) { FailConfiguration(); return; }
            }
            else
                for (int i = 0; i < requiredEnemies.Length; i++)
                {
                    var enemy = requiredEnemies[i];
                    if (enemy == null || enemy == playerHealth || enemy.MaxHealth <= 0 || enemy.gameObject.scene != gameObject.scene)
                    { FailConfiguration(); return; }
                    for (int j = 0; j < i; j++)
                        if (requiredEnemies[j] == enemy) { FailConfiguration(); return; }
                }
            initialized = true;
            view.RestartRequested += Restart;
            player.GlitchPressed += RequestTransfer;
            UpdateObjective();
        }

        private void RequestTransfer()
        {
            // Do not bank a combat-time press and unexpectedly depart when the final enemy dies.
            if (CanRequestTransfer) transferRequested = true;
        }

        private bool InitializeEncounters()
        {
            var unique = new HashSet<UnitHealth>();
            for (int i = 0; i < encounters.Length; i++)
            {
                var room = encounters[i];
                if (room == null || room.EnemyRoot == null || room.Enemies == null || room.Enemies.Length == 0 ||
                    room.EndX <= room.StartX || (i > 0 && room.StartX < encounters[i - 1].EndX)) return false;
                if (i < encounters.Length - 1 && (room.ExitGate == null || !room.ExitGate.IsConfigured ||
                    room.FollowingRest == null || !room.FollowingRest.IsConfigured)) return false;
                foreach (var enemy in room.Enemies)
                {
                    // Future inactive objects have not run Awake. Validate authored definitions, not zero runtime HP.
                    if (enemy == null || enemy == playerHealth || !unique.Add(enemy) ||
                        enemy.gameObject.scene != gameObject.scene || !enemy.transform.IsChildOf(room.EnemyRoot.transform) ||
                        !enemy.TryGetComponent<Unit>(out var unit) || unit.Definition == null || !unit.Definition.TryValidate(out _)) return false;
                }
            }
            foreach (var enemy in unique) { enemy.DamageApplied += OnEnemyDamage; subscriptions.Add(enemy); }
            for (int i = 0; i < encounters.Length; i++)
            {
                encounters[i].EnemyRoot.SetActive(i == 0);
                encounters[i].ExitGate?.SetLocked(true);
                encounters[i].FollowingRest?.NextEntry.SetLocked(true);
            }
            return EnemiesInitialized(encounters[0]);
        }

        private static bool EnemiesInitialized(StageEncounter room)
        {
            foreach (var enemy in room.Enemies)
                if (enemy == null || enemy.MaxHealth <= 0 || enemy.Owner == null || !enemy.Owner.isActiveAndEnabled) return false;
            return true;
        }

        private void OnEnemyDamage(UnitDamageResult result)
        {
            if (!UsesEncounters || !result.Killed || result.Target == null) return;
            foreach (var enemy in encounters[CurrentEncounterIndex].Enemies)
                if (enemy == result.Target)
                {
                    Vector2 point = enemy.transform.position;
                    var enemyBody = UnitPhysics2D.Body(enemy);
                    if (enemyBody != null)
                        point.y = Mathf.Max(point.y, enemyBody.bounds.min.y + playerBody.bounds.extents.y + .02f -
                            (playerBody.bounds.center.y - player.Motor.Position.y));
                    defeatPositions.Add(point);
                    return;
                }
        }

        private void FailConfiguration()
        {
            Debug.LogError("Stage flow needs a live player, unique initialized enemies, a configured goal/view, and a scene in Build Settings.", this);
            enabled = false;
        }

        // Resolve after combat Update/FixedUpdate. Death takes precedence over reaching the exit.
        private void LateUpdate()
        {
            bool canTransfer = CanRequestTransfer;
            if (!canTransfer) transferRequested = false;
            view?.ShowTransferHint(canTransfer);
            if (!initialized || State == StageState.Restarting || State == StageState.Completed) return;
            if (playerHealth == null || player == null) { FailConfiguration(); return; }
            if (!playerHealth.IsAlive)
            {
                CancelTransfer();
                transferRequested = false;
                cinematics?.CancelActive();
                State = StageState.Dying;
                view.ShowGoalHint(false);
                if (!player.gameObject.activeInHierarchy) Restart();
                return;
            }
            if (State != StageState.Playing || (cinematics != null && cinematics.IsBusy)) return;
            if (UsesEncounters && UpdateEncounter()) return;
            if (!UpdateObjective()) return;
            bool inside = player.isActiveAndEnabled && !player.IsUnderground && goal.Contains(playerBody);
            view.ShowGoalHint(inside && RemainingEnemies > 0);
            if (inside && RemainingEnemies == 0) Complete();
        }

        private bool UpdateObjective()
        {
            int remaining = 0;
            foreach (var enemy in ActiveEnemies)
            {
                // Destroying an objective is a setup error, never an implicit kill.
                if (enemy == null) { FailConfiguration(); return false; }
                if (enemy.CurrentHealth > 0) remaining++;
            }
            RemainingEnemies = remaining;
            goal.SetUnlocked(remaining == 0 && (!UsesEncounters || CurrentEncounterIndex == encounters.Length - 1));
            return true;
        }

        // True means this frame belongs to an intermediate room, never to final-stage completion.
        private bool UpdateEncounter()
        {
            if (Phase == EncounterPhase.Transferring)
            {
                if (!player.IsScriptedTransferring || !player.isActiveAndEnabled) CancelTransfer();
                return true;
            }
            if (Phase == EncounterPhase.TransferBlocked) return true;
            if (Phase == EncounterPhase.Rest)
            {
                var rest = CurrentRestArea;
                if (player.isActiveAndEnabled && !player.IsUnderground &&
                    playerBody.bounds.min.x >= rest.NextEntry.EntryX + .02f &&
                    playerBody.bounds.max.x <= encounters[CurrentEncounterIndex + 1].EndX)
                {
                    rest.NextEntry.SetLocked(true);
                    CurrentEncounterIndex++;
                    defeatPositions.Clear();
                    var next = encounters[CurrentEncounterIndex];
                    next.EnemyRoot.SetActive(true);
                    if (!EnemiesInitialized(next)) { FailConfiguration(); return true; }
                    Phase = EncounterPhase.Combat;
                    UpdateObjective();
                }
                return true;
            }
            if (!UpdateObjective()) return true;
            if (CurrentEncounterIndex == encounters.Length - 1) return false;
            if (RemainingEnemies > 0) return true;
            if (Phase == EncounterPhase.Combat)
            {
                Phase = EncounterPhase.TransferPending;
                transferReadyAt = Time.timeAsDouble + .35;
            }
            bool requested = transferRequested;
            transferRequested = false;
            if (requested && CanRequestTransfer)
            {
                view.ShowTransferHint(false);
                BeginTransfer();
            }
            return true;
        }

        private void BeginTransfer()
        {
            if (!CurrentRestArea.TryArrival(playerCapsule, transferTerrainMask, overlaps, out var destination))
            {
                Phase = EncounterPhase.TransferBlocked;
                Debug.LogError("Rest transfer destination is blocked or unsupported; player control is preserved.", this);
                return;
            }
            var room = encounters[CurrentEncounterIndex];
            var points = new List<Vector2> { player.Motor.Position };
            var candidates = new List<Vector2>();
            foreach (var point in defeatPositions)
                if (point.x >= room.StartX && point.x < room.EndX &&
                    StageTransferRoute.SpaceFree(playerCapsule, point, transferTerrainMask, overlaps)) candidates.Add(point);
            Vector2 from = points[0];
            while (true)
            {
                int closest = -1; float distance = float.PositiveInfinity;
                for (int i = 0; i < candidates.Count; i++)
                {
                    var point = candidates[i];
                    float d = (point - from).sqrMagnitude;
                    if (point.x > from.x + .02f && point.x < destination.x && d < distance)
                    { closest = i; distance = d; }
                }
                if (closest < 0) break;
                from = candidates[closest]; points.Add(from); candidates.RemoveAt(closest);
            }
            points.Add(destination);
            bodyOffset = (Vector2)playerBody.bounds.center - player.Motor.Position;
            if (!player.BeginScriptedTransfer()) return;
            // Cancelling underground restores the real body before the transfer owns its position.
            points[0] = player.Motor.Position;
            route = new StageTransferRoute(points);
            transferElapsed = 0; passedWaypoint = 0;
            Phase = EncounterPhase.Transferring;
            player.PresentTransferSpark(route[0] + bodyOffset);
        }

        private void FixedUpdate()
        {
            if (!initialized || Phase != EncounterPhase.Transferring || State != StageState.Playing) return;
            if (playerHealth == null || !playerHealth.IsAlive || player == null || !player.isActiveAndEnabled || !player.IsScriptedTransferring)
            { CancelTransfer(); return; }
            transferElapsed = Mathf.Min(route.Duration, transferElapsed + Time.fixedDeltaTime);
            player.SetScriptedTransferPosition(route.Evaluate(transferElapsed, out int passed));
            while (passedWaypoint < passed) player.PresentTransferSpark(route[++passedWaypoint] + bodyOffset);
            if (transferElapsed < route.Duration) return;
            player.EndScriptedTransfer(true);
            // Verify again after restoring the collider, before consuming the one-time heal.
            bool validArrival = CurrentRestArea.TryArrival(playerCapsule, transferTerrainMask, overlaps, out var destination);
            if (!validArrival ||
                Vector2.Distance(player.Motor.Position, destination) > .05f || !CurrentRestArea.TryVisit(playerHealth, playerBody))
            {
                player.Motor.Teleport(route[0]);
                Phase = EncounterPhase.TransferBlocked;
                Debug.LogError("Rest transfer arrival became invalid; recovery was not consumed.", this);
                route = null;
                return;
            }
            encounters[CurrentEncounterIndex].ExitGate.SetLocked(false);
            CurrentRestArea.NextEntry.SetLocked(false);
            route = null;
            Phase = EncounterPhase.Rest;
        }

        private void CancelTransfer()
        {
            if (Phase != EncounterPhase.Transferring) return;
            player?.EndScriptedTransfer();
            route = null;
            Phase = EncounterPhase.TransferBlocked;
        }

        private void Complete()
        {
            CancelTransfer();
            cinematics?.CancelActive();
            State = StageState.Completed;
            playerInput.DeactivateInput();
            // Existing disable cleanup cancels actions and releases feedback-owned time/camera changes.
            player.enabled = false;
            player.Motor.Hover();
            playerHealth.SetDamageEnabled(false);
            Time.fixedDeltaTime = initialFixedDeltaTime;
            Time.timeScale = 0f;
            view.ShowCompleted();
        }

        public void Restart()
        {
            if (!initialized || (State != StageState.Completed && State != StageState.Dying)) return;
            State = StageState.Restarting;
            view.SetRestartEnabled(false);
            RestoreTime();
            SceneManager.LoadSceneAsync(gameObject.scene.buildIndex, LoadSceneMode.Single);
        }

        private void RestoreTime()
        {
            if (player != null) player.Feedback?.Reset();
            Time.timeScale = initialTimeScale;
            Time.fixedDeltaTime = initialFixedDeltaTime;
        }

        private void OnDisable()
        {
            transferRequested = false;
            if (player != null) player.GlitchPressed -= RequestTransfer;
            view?.ShowTransferHint(false);
            CancelTransfer();
            foreach (var enemy in subscriptions) if (enemy != null) enemy.DamageApplied -= OnEnemyDamage;
            subscriptions.Clear();
            cinematics?.CancelActive();
            if (!initialized) return;
            if (view != null) view.RestartRequested -= Restart;
            RestoreTime();
        }
    }
}
