using System.Linq;
using ActionPlatformer.Flow;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ActionPlatformer.Tests
{
    // CoreLoopStage now ends in a boss arena. Tests of the original exit-goal rules (single objective, final-wave reveal,
    // pause at the goal) put the exit back on solid ground and take the boss arena out of the stage.
    public static class LegacyExitStage
    {
        public const float ExitX = 150f;

        public static void Restore(Scene scene)
        {
            var stage = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<StageLayout>(true)).Single();
            typeof(StageLayout).GetField("bossArena", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(stage, null);
            var exit = scene.GetRootGameObjects().Single(g => g.name == "Exit");
            exit.SetActive(true);
            exit.transform.position = new Vector3(ExitX, exit.transform.position.y, exit.transform.position.z);
        }
    }
}
