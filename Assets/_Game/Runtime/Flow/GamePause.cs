using System.Collections.Generic;
using ActionPlatformer.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ActionPlatformer.Flow
{
    // Freezes time, audio and every active PlayerInput, and gives back exactly what it took on resume.
    public sealed class GamePause
    {
        private readonly List<PlayerInput> deactivated = new List<PlayerInput>();
        private float savedTimeScale, savedFixedDeltaTime;
        private bool pausedAudio;

        public bool IsPaused { get; private set; }

        public void Pause()
        {
            if (IsPaused) return;
            // A combat time effect would read the pause as an override and stop restoring; end it first.
            foreach (var input in PlayerInput.all)
            {
                var feedback = input.TryGetComponent(out PlayerUnit player) ? player.Feedback : null;
                if (feedback != null && feedback.IsTimeEffectActive) feedback.Reset();
            }
            savedTimeScale = Time.timeScale;
            savedFixedDeltaTime = Time.fixedDeltaTime;
            Time.timeScale = 0f;
            pausedAudio = !AudioListener.pause;
            AudioListener.pause = true;
            deactivated.Clear();
            // Actions stay disabled while paused, so nothing pressed now reaches gameplay after the resume.
            foreach (var input in PlayerInput.all)
                if (input.inputIsActive) deactivated.Add(input);
            foreach (var input in deactivated) input.DeactivateInput();
            IsPaused = true;
        }

        // restoreTimeAndInput is false when another owner took time and input during the pause; the audio is still given back.
        public void Resume(bool restoreTimeAndInput = true)
        {
            if (!IsPaused) return;
            IsPaused = false;
            if (restoreTimeAndInput)
            {
                Time.timeScale = savedTimeScale;
                // The pause never changes the step; rewriting an equal value would still drop a tick.
                if (Time.fixedDeltaTime != savedFixedDeltaTime) Time.fixedDeltaTime = savedFixedDeltaTime;
                foreach (var input in deactivated)
                    if (input != null && input.isActiveAndEnabled && !input.inputIsActive) input.ActivateInput();
            }
            if (pausedAudio) AudioListener.pause = false;
            pausedAudio = false;
            deactivated.Clear();
        }
    }
}
