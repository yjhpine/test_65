using UnityEngine;

namespace ActionPlatformer.Flow
{
    // Scene-side marker of the title scene. While it is registered the GameManager shows the main menu.
    [DisallowMultipleComponent]
    public sealed class TitleScreen : MonoBehaviour
    {
        private void Start()
        {
            var game = GameManager.Instance;
            if (game == null) Debug.LogError("TitleScreen could not find the GameManager.", this);
            else game.RegisterTitle(this);
        }

        // Never create a manager here: this also runs while Play Mode is shutting down.
        private void OnDisable() => GameManager.Existing?.UnregisterTitle(this);
    }
}
