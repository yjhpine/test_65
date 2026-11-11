using ActionPlatformer.Cinematics;
using ActionPlatformer.Player;
using ActionPlatformer.Units.Features;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ActionPlatformer.Flow
{
    // Scene-side stage wiring. The persistent GameManager owns every flow rule and the HUD.
    [DisallowMultipleComponent]
    public sealed class StageLayout : MonoBehaviour
    {
        [SerializeField] private PlayerUnit player;
        [SerializeField] private UnitHealth[] requiredEnemies;
        [SerializeField] private StageGoal goal;
        [SerializeField] private CutsceneCoordinator cinematics;
        [Tooltip("Optional one-shot reveal after the final encounter, before the player can finish at the goal.")]
        [SerializeField] private CutsceneRunner finalWaveCutscene;
        [Tooltip("Optional ordered encounter loop. Empty keeps the original single-stage objective.")]
        [SerializeField] private StageEncounter[] encounters;
        [Tooltip("Optional boss arena after the last encounter. Defeating the boss clears the stage in place of the exit goal.")]
        [SerializeField] private StageBoss bossArena;
        [SerializeField] private LayerMask transferTerrainMask = 1;

        public PlayerUnit Player => player;
        public UnitHealth[] RequiredEnemies => requiredEnemies;
        public StageGoal Goal => goal;
        public CutsceneCoordinator Cinematics => cinematics;
        public CutsceneRunner FinalWaveCutscene => finalWaveCutscene;
        public StageEncounter[] Encounters => encounters;
        public StageBoss BossArena => bossArena;
        public LayerMask TransferTerrainMask => transferTerrainMask;

        // First layout under the scene roots, inactive objects included.
        public static StageLayout FindIn(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded) return null;
            foreach (var root in scene.GetRootGameObjects())
            {
                var layout = root.GetComponentInChildren<StageLayout>(true);
                if (layout != null) return layout;
            }
            return null;
        }

        // Registering in Start lets sceneLoaded callbacks adjust the wiring first.
        private void Start()
        {
            var game = GameManager.Instance;
            if (game == null) Debug.LogError("StageLayout could not find the GameManager.", this);
            else game.Register(this);
        }

        // Never create a manager here: this also runs while Play Mode is shutting down.
        private void OnDisable() => GameManager.Existing?.Unregister(this);
    }
}
