using ActionPlatformer.Player;
using ActionPlatformer.Units.Features;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using ActionPlatformer.Cinematics;

namespace ActionPlatformer.Flow
{
    public enum StageState { Playing, Dying, Restarting, Completed }

    [DisallowMultipleComponent]
    public sealed class StageFlowController : MonoBehaviour
    {
        [SerializeField] private PlayerUnit player;
        [SerializeField] private UnitHealth[] requiredEnemies;
        [SerializeField] private StageGoal goal;
        [SerializeField] private StageFlowView view;
        [SerializeField] private CutsceneCoordinator cinematics;
        private UnitHealth playerHealth;
        private PlayerInput playerInput;
        private Collider2D playerBody;
        private float initialTimeScale, initialFixedDeltaTime;
        private bool initialized;

        public StageState State { get; private set; } = StageState.Playing;
        public int RemainingEnemies { get; private set; }
        public PlayerUnit Player => player;

        private void Awake()
        {
            initialTimeScale = Time.timeScale;
            initialFixedDeltaTime = Time.fixedDeltaTime;
        }

        private void Start()
        {
            if (player == null || goal == null || view == null || !goal.IsConfigured || !view.IsConfigured ||
                requiredEnemies == null || requiredEnemies.Length == 0 ||
                !player.TryGetComponent(out playerHealth) || !player.TryGetComponent(out playerInput) ||
                !player.TryGetComponent(out playerBody) || !player.isActiveAndEnabled || player.Motor == null ||
                playerHealth.MaxHealth <= 0 || gameObject.scene.buildIndex < 0)
            { FailConfiguration(); return; }
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
            UpdateObjective();
        }

        private void FailConfiguration()
        {
            Debug.LogError("Stage flow needs a live player, unique initialized enemies, a configured goal/view, and a scene in Build Settings.", this);
            enabled = false;
        }

        // Resolve after combat Update/FixedUpdate. Death takes precedence over reaching the exit.
        private void LateUpdate()
        {
            if (!initialized || State == StageState.Restarting || State == StageState.Completed) return;
            if (playerHealth == null || player == null) { FailConfiguration(); return; }
            if (!playerHealth.IsAlive)
            {
                cinematics?.CancelActive();
                State = StageState.Dying;
                view.ShowGoalHint(false);
                if (!player.gameObject.activeInHierarchy) Restart();
                return;
            }
            if (State != StageState.Playing || (cinematics != null && cinematics.IsBusy) || !UpdateObjective()) return;
            bool inside = player.isActiveAndEnabled && !player.IsUnderground && goal.Contains(playerBody);
            view.ShowGoalHint(inside && RemainingEnemies > 0);
            if (inside && RemainingEnemies == 0) Complete();
        }

        private bool UpdateObjective()
        {
            int remaining = 0;
            foreach (var enemy in requiredEnemies)
            {
                // Destroying an objective is a setup error, never an implicit kill.
                if (enemy == null) { FailConfiguration(); return false; }
                if (enemy.CurrentHealth > 0) remaining++;
            }
            RemainingEnemies = remaining;
            goal.SetUnlocked(remaining == 0);
            return true;
        }

        private void Complete()
        {
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
            cinematics?.CancelActive();
            if (!initialized) return;
            if (view != null) view.RestartRequested -= Restart;
            RestoreTime();
        }
    }
}
