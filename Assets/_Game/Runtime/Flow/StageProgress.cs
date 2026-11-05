using System;
using System.Collections.Generic;
using ActionPlatformer.Cinematics;
using ActionPlatformer.Player;
using ActionPlatformer.Units;
using ActionPlatformer.Units.Features;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ActionPlatformer.Flow
{
    // Wave, rest and transfer rules of a stage. The owner decides when it is allowed to run.
    public sealed class StageProgress
    {
        private readonly PlayerUnit player;
        private readonly UnitHealth playerHealth;
        private readonly Collider2D playerBody;
        private readonly UnitHealth[] requiredEnemies;
        private readonly StageEncounter[] encounters;
        private readonly StageGoal goal;
        private readonly CutsceneCoordinator cinematics;
        private readonly StageFlowView view;
        private readonly LayerMask transferTerrainMask;
        private readonly Scene stageScene;
        private readonly UnityEngine.Object logContext;
        private readonly Action failConfiguration;
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

        public StageProgress(PlayerUnit player, UnitHealth playerHealth, Collider2D playerBody,
            UnitHealth[] requiredEnemies, StageEncounter[] encounters, StageGoal goal,
            CutsceneCoordinator cinematics, StageFlowView view, LayerMask transferTerrainMask, Scene stageScene,
            UnityEngine.Object logContext, Action failConfiguration)
        {
            this.player = player; this.playerHealth = playerHealth; this.playerBody = playerBody;
            this.requiredEnemies = requiredEnemies; this.encounters = encounters; this.goal = goal;
            this.cinematics = cinematics; this.view = view; this.transferTerrainMask = transferTerrainMask;
            this.stageScene = stageScene; this.logContext = logContext; this.failConfiguration = failConfiguration;
        }

        public int RemainingEnemies { get; private set; }
        public bool UsesEncounters => encounters != null && encounters.Length > 0;
        public EncounterPhase Phase { get; private set; }
        public int CurrentEncounterIndex { get; private set; }
        public StageRestArea CurrentRestArea => UsesEncounters ? encounters[CurrentEncounterIndex].FollowingRest : null;
        public IReadOnlyList<UnitHealth> ActiveEnemies => UsesEncounters ? encounters[CurrentEncounterIndex].Enemies : requiredEnemies;

        // ownerPlaying: the owner is initialized, active and in its Playing state.
        public bool CanRequestTransfer(bool ownerPlaying) => ownerPlaying && UsesEncounters &&
            (Phase == EncounterPhase.TransferPending || Phase == EncounterPhase.TransferBlocked) &&
            Time.timeScale > 0 && Time.timeAsDouble >= transferReadyAt &&
            player != null && player.isActiveAndEnabled && playerHealth != null && playerHealth.IsAlive &&
            !player.IsControlLocked && !(player.Feedback?.IsTimeEffectActive ?? false) && !(cinematics != null && cinematics.IsBusy);

        public bool TryInitialize()
        {
            if (UsesEncounters)
            {
                playerCapsule = playerBody as CapsuleCollider2D;
                if (playerCapsule == null || !InitializeEncounters()) return false;
            }
            else
                for (int i = 0; i < requiredEnemies.Length; i++)
                {
                    var enemy = requiredEnemies[i];
                    if (enemy == null || enemy == playerHealth || enemy.MaxHealth <= 0 || enemy.gameObject.scene != stageScene)
                        return false;
                    for (int j = 0; j < i; j++)
                        if (requiredEnemies[j] == enemy) return false;
                }
            return true;
        }

        public void RequestTransfer(bool ownerPlaying)
        {
            // Do not bank a combat-time press and unexpectedly depart when the final enemy dies.
            if (CanRequestTransfer(ownerPlaying)) transferRequested = true;
        }

        public void ClearTransferRequest() => transferRequested = false;

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
                        enemy.gameObject.scene != stageScene || !enemy.transform.IsChildOf(room.EnemyRoot.transform) ||
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

        public bool UpdateObjective()
        {
            int remaining = 0;
            foreach (var enemy in ActiveEnemies)
            {
                // Destroying an objective is a setup error, never an implicit kill.
                if (enemy == null) { failConfiguration(); return false; }
                if (enemy.CurrentHealth > 0) remaining++;
            }
            // The kill that clears the current group gets a short slow motion after its hit stop.
            if (RemainingEnemies > 0 && remaining == 0 && player != null) player.PresentFinalBlow();
            // A cleared group takes the shots its shooters left behind with it.
            if (RemainingEnemies > 0 && remaining == 0) EnemyProjectiles.ClearExisting();
            RemainingEnemies = remaining;
            goal.SetUnlocked(remaining == 0 && (!UsesEncounters || CurrentEncounterIndex == encounters.Length - 1));
            return true;
        }

        // True means this frame belongs to an intermediate room, never to final-stage completion.
        public bool UpdateEncounter(bool ownerPlaying)
        {
            if (Phase == EncounterPhase.Transferring)
            {
                if (!player.IsScriptedTransferring || !player.isActiveAndEnabled) CancelTransfer();
                return true;
            }
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
                    if (!EnemiesInitialized(next)) { failConfiguration(); return true; }
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
            // Clearing combat opens the physical route independently of the optional Shift transfer.
            // Keep this available after a failed/cancelled transfer as well.
            encounters[CurrentEncounterIndex].ExitGate.SetLocked(false);
            if (Time.timeScale > 0 && player.isActiveAndEnabled && !player.IsUnderground &&
                !player.IsControlLocked && CurrentRestArea.TryVisit(playerHealth, playerBody))
            {
                EnterRest();
                return true;
            }
            bool requested = transferRequested;
            transferRequested = false;
            if (requested && CanRequestTransfer(ownerPlaying))
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
                Debug.LogError("Rest transfer destination is blocked or unsupported; player control is preserved.", logContext);
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

        // The owner calls this only while it is initialized and playing.
        public void FixedTick()
        {
            if (Phase != EncounterPhase.Transferring) return;
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
                Debug.LogError("Rest transfer arrival became invalid; recovery was not consumed.", logContext);
                route = null;
                return;
            }
            route = null;
            EnterRest();
        }

        private void EnterRest()
        {
            encounters[CurrentEncounterIndex].ExitGate.SetLocked(false);
            CurrentRestArea.NextEntry.SetLocked(false);
            transferRequested = false;
            view.ShowTransferHint(false);
            Phase = EncounterPhase.Rest;
        }

        public void CancelTransfer()
        {
            if (Phase != EncounterPhase.Transferring) return;
            player?.EndScriptedTransfer();
            route = null;
            Phase = EncounterPhase.TransferBlocked;
        }

        public void Dispose()
        {
            foreach (var enemy in subscriptions) if (enemy != null) enemy.DamageApplied -= OnEnemyDamage;
            subscriptions.Clear();
        }
    }
}
