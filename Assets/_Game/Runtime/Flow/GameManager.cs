using System.Collections.Generic;
using ActionPlatformer.Cinematics;
using ActionPlatformer.Player;
using ActionPlatformer.Units.Features;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace ActionPlatformer.Flow
{
    // Persistent owner of the stage flow and its HUD. Each scene's StageLayout registers its wiring here.
    // The out-game half (title, saves, settings, scene changes) is in GameManager.OutGame.cs.
    [DisallowMultipleComponent]
    public sealed partial class GameManager : MonoBehaviour
    {
        public const string ResourcePath = "GameManager";
        public const string ConfigurationErrorMessage = "Stage flow needs a live player, unique initialized enemies, a configured goal/view, and a scene in Build Settings.";
        public const string DuplicateStageMessage = "Only one StageLayout can be registered at a time; this one is ignored.";
        public const string MissingPrefabMessage = "GameManager prefab is missing at Resources/GameManager.";
        private static GameManager instance;
        private static bool quitting;
        private static bool ensureFailed;

        [SerializeField] private StageFlowView view;
        [SerializeField] private InputActionAsset controls;
        [SerializeField] private PauseMenuView pauseMenu;
        [SerializeField] private StageCatalog catalog;
        [SerializeField] private OutGameView outGame;
        [Tooltip("The player's actions asset. Key settings are stored on it, so every player in every scene uses them.")]
        [SerializeField] private InputActionAsset playerControls;
        [Tooltip("Played when a volume changes so the new level can be heard, even while paused.")]
        [SerializeField] private AudioClip previewClip;
        private StageLayout stage;
        private PlayerUnit player;
        private UnitHealth playerHealth;
        private PlayerInput playerInput;
        private Collider2D playerBody;
        private StageGoal goal;
        private StageBoss bossArena;
        private CutsceneCoordinator cinematics;
        private CutsceneRunner finalWaveCutscene;
        private float initialTimeScale, initialFixedDeltaTime;
        private bool initialized;
        private StageProgress progress;
        private bool finalWaveCutsceneAttempted;
        private double finalWaveRevealReadyAt = double.PositiveInfinity;
        private readonly GamePause pause = new GamePause();
        private readonly List<CutsceneCoordinator> coordinators = new List<CutsceneCoordinator>();
        private InputActionAsset controlsInstance;
        private InputAction pauseAction;
        private bool cutsceneBusyAtLateUpdate;

        // Created on demand while playing; null in Edit Mode or while the application quits.
        public static GameManager Instance
        {
            get
            {
                if (quitting || !Application.isPlaying) return null;
                Ensure();
                return Existing;
            }
        }
        // Never creates a manager, so it is safe from OnDisable while Play Mode shuts down.
        public static GameManager Existing => instance != null ? instance : null;

        public StageFlowView View => view;
        public PauseMenuView PauseMenu => pauseMenu;
        public bool IsPaused => pause.IsPaused;
        public StageLayout Stage => stage;
        public bool HasStage => stage != null;
        // Rises on every successful registration, so a reload can be detected without a destroyed owner.
        public int StageVersion { get; private set; }
        // Meaningful only while HasStage is true; it keeps its last value after the stage unregisters.
        public StageState State { get; private set; } = StageState.Playing;
        public int RemainingEnemies => progress?.RemainingEnemies ?? 0;
        public PlayerUnit Player => player;
        public CutsceneRunner FinalWaveCutscene => finalWaveCutscene;
        public bool UsesEncounters => progress != null && progress.UsesEncounters;
        public EncounterPhase Phase => progress?.Phase ?? EncounterPhase.Combat;
        public int CurrentEncounterIndex => progress?.CurrentEncounterIndex ?? 0;
        public StageRestArea CurrentRestArea => progress?.CurrentRestArea;
        public IReadOnlyList<UnitHealth> ActiveEnemies => progress?.ActiveEnemies ?? System.Array.Empty<UnitHealth>();
        public bool CanRequestTransfer => progress != null && progress.CanRequestTransfer(StagePlaying);
        private bool StagePlaying => stage != null && initialized && State == StageState.Playing;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { instance = null; quitting = false; ensureFailed = false; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap() => Ensure();

        private static void Ensure()
        {
            if (instance != null || ensureFailed || quitting || !Application.isPlaying) return;
            var prefab = Resources.Load<GameObject>(ResourcePath);
            if (prefab == null) { ensureFailed = true; Debug.LogError(MissingPrefabMessage); return; }
            var root = Instantiate(prefab);
            root.name = "GameManager";
            // A prefab without the component would otherwise be instantiated again by every Instance call.
            if (root.GetComponent<GameManager>() == null)
            {
                ensureFailed = true;
                Debug.LogError(MissingPrefabMessage);
                Destroy(root);
                return;
            }
            DontDestroyOnLoad(root);
        }

        private void Awake()
        {
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this;
        }

        private void OnApplicationQuit() => quitting = true;

        private void OnEnable()
        {
            // A duplicate destroyed in Awake still gets OnEnable; it must not listen for the pause key.
            if (instance != this) return;
            if (controlsInstance == null && controls != null)
            {
                controlsInstance = Instantiate(controls);
                pauseAction = controlsInstance.FindAction("Game/Pause");
            }
            pauseAction?.Enable();
            if (pauseMenu != null) pauseMenu.ContinueRequested += Resume;
            EnableOutGame();
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
        }

        // Disabling releases the stage like destruction does; re-enabling does not re-register it (as with StageLayout).
        private void OnDisable()
        {
            if (instance != this) return;
            // Nothing may stay frozen once the manager stops running.
            Resume();
            pauseAction?.Disable();
            if (pauseMenu != null) pauseMenu.ContinueRequested -= Resume;
            DisableOutGame();
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            if (stage != null) Unregister(stage);
        }

        private void OnDestroy()
        {
            if (instance != this) return;
            if (stage != null) Unregister(stage);
            if (controlsInstance != null) Destroy(controlsInstance);
            ReleaseOutGame();
            instance = null;
        }

        // Refused during cutscenes (Esc also skips them, so the frame one ends counts too) and outside stage play.
        public bool TryPause()
        {
            if (IsPaused) return true;
            if (instance != this || !isActiveAndEnabled || cutsceneBusyAtLateUpdate || CutsceneBusy(true) ||
                (stage != null && State != StageState.Playing) || !OutGameAllowsPause) return false;
            pause.Pause();
            if (pauseMenu != null) pauseMenu.Show(true);
            return true;
        }

        public void Resume()
        {
            if (!IsPaused) return;
            // A clear or death resolved under the pause owns time and input now; give back only the audio.
            CloseMenusOverPause();
            pause.Resume(restoreTimeAndInput: stage == null || State == StageState.Playing);
            if (pauseMenu != null) pauseMenu.Show(false);
        }

        // A scene load never keeps the pause; the coordinator list follows the loaded scenes.
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Resume();
            CutsceneBusy(true);
            OnSceneLoadedOutGame(scene, mode);
        }

        private void OnActiveSceneChanged(Scene previous, Scene next) => Resume();

        private bool CutsceneBusy(bool rescan)
        {
            if (rescan)
            {
                coordinators.Clear();
                coordinators.AddRange(FindObjectsByType<CutsceneCoordinator>(FindObjectsInactive.Include, FindObjectsSortMode.None));
            }
            foreach (var coordinator in coordinators)
                if (coordinator != null && coordinator.IsBusy) return true;
            return false;
        }

        internal bool Register(StageLayout layout)
        {
            if (layout == null) return false;
            if (stage != null)
            {
                if (stage == layout) return true;
                Debug.LogError(DuplicateStageMessage, layout);
                return false;
            }
            // Per-run state starts fresh; the time values are restored when this stage leaves.
            initialTimeScale = Time.timeScale;
            initialFixedDeltaTime = Time.fixedDeltaTime;
            State = StageState.Playing;
            finalWaveCutsceneAttempted = false;
            finalWaveRevealReadyAt = double.PositiveInfinity;
            progress = null;
            if (view != null) view.ResetStage();
            player = layout.Player; goal = layout.Goal; cinematics = layout.Cinematics; finalWaveCutscene = layout.FinalWaveCutscene;
            bossArena = layout.BossArena;
            var requiredEnemies = layout.RequiredEnemies;
            var encounters = layout.Encounters;
            bool usesEncounters = encounters != null && encounters.Length > 0;
            if (player == null || goal == null || view == null || !goal.IsConfigured || !view.IsConfigured ||
                (!usesEncounters && (requiredEnemies == null || requiredEnemies.Length == 0)) ||
                !player.TryGetComponent(out playerHealth) || !player.TryGetComponent(out playerInput) ||
                !player.TryGetComponent(out playerBody) || !player.isActiveAndEnabled || player.Motor == null ||
                playerHealth.MaxHealth <= 0 || layout.gameObject.scene.buildIndex < 0 ||
                (bossArena != null && !bossArena.IsConfigured))
                return Reject(layout);
            var entry = TakeEntry(layout.gameObject.scene);
            progress = new StageProgress(player, playerHealth, playerBody, requiredEnemies, encounters, goal,
                cinematics, view, layout.TransferTerrainMask, layout.gameObject.scene, layout, FailConfiguration);
            if (!progress.TryInitialize(entry.Checkpoint)) return Reject(layout);
            stage = layout;
            initialized = true;
            SubscribeStageView(true);
            player.GlitchPressed += RequestTransfer;
            progress.RestReached += OnRestReached;
            // A failure here has already unregistered the stage.
            if (!progress.UpdateObjective() || stage != layout) return false;
            StageVersion++;
            view.ShowStage(true);
            OnStageEntered(entry);
            return true;
        }

        internal void Unregister(StageLayout layout)
        {
            if (layout == null || layout != stage) return;
            // Leaving the stage gives back time, audio and input before the stage's own time is restored.
            Resume();
            Release();
            if (view == null) return;
            view.ResetStage();
            view.ShowStage(false);
        }

        private bool Reject(StageLayout layout)
        {
            Debug.LogError(ConfigurationErrorMessage, layout);
            Release();
            transition?.FailPreparing(SceneTransition.PrepareFailedMessage);
            return false;
        }

        // A setup error found while playing unregisters the stage; the manager itself keeps running.
        private void FailConfiguration()
        {
            Debug.LogError(ConfigurationErrorMessage, stage != null ? (Object)stage : this);
            if (stage != null) Unregister(stage);
        }

        // Undo a registration (or a failed attempt) and forget every reference into the stage scene.
        private void Release()
        {
            // Cancelled cutscenes must already see the stage as gone, as they saw a disabled owner before.
            stage = null;
            progress?.ClearTransferRequest();
            if (player != null) player.GlitchPressed -= RequestTransfer;
            if (progress != null) progress.RestReached -= OnRestReached;
            view?.ShowTransferHint(false);
            progress?.CancelTransfer();
            progress?.Dispose();
            if (cinematics != null) cinematics.CancelActive();
            if (initialized)
            {
                SubscribeStageView(false);
                RestoreTime();
            }
            stageId = null;
            CloseStageMenus();
            initialized = false;
            progress = null;
            player = null; playerHealth = null; playerInput = null; playerBody = null;
            goal = null; cinematics = null; finalWaveCutscene = null; bossArena = null;
        }

        private void RequestTransfer() => progress?.RequestTransfer(StagePlaying);

        private void LateUpdate()
        {
            if (stage != null) ResolveStage();
            // The key is read after every Update and after the stage resolved, so a cutscene or clear that
            // began earlier this frame refuses the pause instead of starting under it.
            if (instance == this && pauseAction != null && pauseAction.WasPressedThisFrame() && !EscapeTakenByOutGame())
            {
                if (IsPaused) Resume();
                else TryPause();
            }
            LateUpdateOutGame();
            // A resolution change re-snaps every menu and HUD text to whole multiples of the 12px pixel font.
            if (view != null && view.Document != null) PixelText.Refresh(view.Document.rootVisualElement);
            // Read by the next frame's key: Esc also skips cutscenes, and the frame one ends still refuses.
            cutsceneBusyAtLateUpdate = CutsceneBusy(false);
        }

        // Resolve after combat Update/FixedUpdate. Death takes precedence over reaching the exit.
        private void ResolveStage()
        {
            // Combat hints (unusable glitch, combo progress) show only while the player has gameplay control.
            bool combatHud = initialized && State == StageState.Playing && player != null && player.isActiveAndEnabled &&
                !player.IsControlLocked && (cinematics == null || !cinematics.IsBusy) && !IsPaused;
            view?.ShowGlitchHint(combatHud ? player.GlitchPreview : default, player != null ? player.AimCamera : null);
            view?.ShowCombo(combatHud ? player.ComboCount : 0, combatHud ? player.ComboLength : 0);
            bool canTransfer = CanRequestTransfer;
            if (!canTransfer) progress?.ClearTransferRequest();
            view?.ShowTransferHint(canTransfer);
            if (!initialized || State == StageState.Restarting || State == StageState.Completed || State == StageState.Failed) return;
            if (playerHealth == null || player == null) { FailConfiguration(); return; }
            if (!playerHealth.IsAlive)
            {
                progress.CancelTransfer();
                progress.ClearTransferRequest();
                cinematics?.CancelActive();
                State = StageState.Dying;
                view.ShowGoalHint(false);
                // After the death presentation the failure screen asks for a retry (planning doc 6.2).
                if (!player.gameObject.activeInHierarchy) Fail();
                return;
            }
            if (State != StageState.Playing || (cinematics != null && cinematics.IsBusy)) return;
            if (UsesEncounters && progress.UpdateEncounter(StagePlaying)) return;
            if (!progress.UpdateObjective()) return;
            // A boss arena replaces the exit: the stage ends a moment after the boss falls.
            if (bossArena != null)
            {
                view.ShowGoalHint(false);
                if (RemainingEnemies == 0 && bossArena.ShouldComplete(Time.timeAsDouble)) Complete();
                return;
            }
            if (UsesEncounters && RemainingEnemies == 0 && finalWaveCutscene != null && !finalWaveCutsceneAttempted)
            {
                view.ShowGoalHint(false);
                // Let the authored death animations (up to 0.8s) finish before pausing the scene.
                if (double.IsPositiveInfinity(finalWaveRevealReadyAt)) finalWaveRevealReadyAt = Time.timeAsDouble + .85;
                // Finish combat-owned time effects before the cutscene acquires the camera and pauses the world.
                if (Time.timeScale <= 0 || Time.timeAsDouble < finalWaveRevealReadyAt || player.IsControlLocked ||
                    (player.Feedback?.IsTimeEffectActive ?? false)) return;
                finalWaveCutsceneAttempted = true;
                if (!finalWaveCutscene.TryPlay())
                    Debug.LogWarning("Final-wave cutscene could not start; the unlocked exit remains available.", stage);
                return;
            }
            bool inside = player.isActiveAndEnabled && !player.IsUnderground && goal.Contains(playerBody);
            view.ShowGoalHint(inside && RemainingEnemies > 0);
            if (inside && RemainingEnemies == 0) Complete();
        }

        private void FixedUpdate()
        {
            if (stage == null || !initialized || State != StageState.Playing) return;
            progress.FixedTick();
        }

        private void Complete()
        {
            progress.CancelTransfer();
            cinematics?.CancelActive();
            State = StageState.Completed;
            playerInput.DeactivateInput();
            // Existing disable cleanup cancels actions and releases feedback-owned time/camera changes.
            player.enabled = false;
            player.Motor.Hover();
            playerHealth.SetDamageEnabled(false);
            Time.fixedDeltaTime = initialFixedDeltaTime;
            Time.timeScale = 0f;
            ShowClearResult();
        }

        private void Fail()
        {
            progress.CancelTransfer();
            cinematics?.CancelActive();
            State = StageState.Failed;
            playerInput.DeactivateInput();
            // The world stops behind the failure screen, as it does behind the clear screen.
            Time.fixedDeltaTime = initialFixedDeltaTime;
            Time.timeScale = 0f;
            EnemyProjectiles.ClearExisting();
            view.ShowFailed(RetryDetail());
        }

        // The current stage from its start (planning doc: 다시 플레이, and 현재 스테이지 재시작 from the pause menu).
        public void Restart()
        {
            if (stage == null || !initialized) return;
            bool pausedPlay = State == StageState.Playing && IsPaused;
            if (State != StageState.Completed && State != StageState.Dying && State != StageState.Failed && !pausedPlay) return;
            EnterStage(stageId, stage.gameObject.scene.name, ProgressData.NoCheckpoint, false, true);
        }

        // From the failure screen: the last rest area of this run, or the stage start.
        public void Retry()
        {
            if (stage == null || !initialized || (State != StageState.Failed && State != StageState.Dying)) return;
            EnterStage(stageId, stage.gameObject.scene.name, saves.RetryCheckpointFor(stageId), false, false);
        }

        private void RestoreTime()
        {
            if (player != null) player.Feedback?.Reset();
            Time.timeScale = initialTimeScale;
            Time.fixedDeltaTime = initialFixedDeltaTime;
        }
    }
}
