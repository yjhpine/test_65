using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ActionPlatformer.Flow
{
    public enum TransitionPhase { Idle, Covering, Loading, Preparing, Revealing, Failed }

    // The one path for stage entry, retry, restart and returning to the title (planning doc 6.5).
    // Real time drives it, so a paused or frozen world cannot stall it. Requests while busy are ignored.
    public sealed class SceneTransition
    {
        public const float FadeDuration = .18f;
        // A slow frame advances a fade by at most this much, so the fade (and its glitch) is always seen over a few frames.
        public const float MaxFadeStep = 1f / 30f;
        public const float PrepareTimeout = 6f;
        public const string LoadFailedMessage = "화면을 불러오지 못했습니다.";
        public const string PrepareFailedMessage = "스테이지를 준비하지 못했습니다.";

        private readonly Func<string, AsyncOperation> loadScene;
        private AsyncOperation operation;
        private Func<bool> ready;
        private float elapsed;

        public SceneTransition(Func<string, AsyncOperation> loadScene = null)
        {
            this.loadScene = loadScene ?? (scene => SceneManager.LoadSceneAsync(scene, LoadSceneMode.Single));
        }

        public TransitionPhase Phase { get; private set; }
        public bool IsBusy => Phase != TransitionPhase.Idle;
        // The screen cover: 0 clear, 1 opaque.
        public float Cover { get; private set; }
        public string TargetScene { get; private set; }
        public string FailureMessage { get; private set; }
        public int Started { get; private set; }
        public event Action<string> Failed;
        public event Action Finished;

        // ready: true once the loaded scene is playable (its stage or title registered). Null means as soon as it loads.
        public bool Begin(string scene, Func<bool> ready)
        {
            if (IsBusy || string.IsNullOrEmpty(scene)) return false;
            TargetScene = scene;
            this.ready = ready;
            operation = null;
            FailureMessage = null;
            elapsed = 0f;
            Phase = TransitionPhase.Covering;
            Started++;
            return true;
        }

        public void Tick(float unscaledDeltaTime)
        {
            switch (Phase)
            {
                case TransitionPhase.Covering:
                    Cover = Mathf.MoveTowards(Cover, 1f, Mathf.Min(unscaledDeltaTime, MaxFadeStep) / FadeDuration);
                    if (Cover < 1f) return;
                    operation = loadScene(TargetScene);
                    if (operation == null) { Fail(LoadFailedMessage); return; }
                    Phase = TransitionPhase.Loading;
                    return;
                case TransitionPhase.Loading:
                    if (!operation.isDone) return;
                    elapsed = 0f;
                    Phase = TransitionPhase.Preparing;
                    goto case TransitionPhase.Preparing;
                case TransitionPhase.Preparing:
                    if (ready == null || ready()) { Phase = TransitionPhase.Revealing; return; }
                    elapsed += unscaledDeltaTime;
                    if (elapsed >= PrepareTimeout) Fail(PrepareFailedMessage);
                    return;
                case TransitionPhase.Revealing:
                    Cover = Mathf.MoveTowards(Cover, 0f, Mathf.Min(unscaledDeltaTime, MaxFadeStep) / FadeDuration);
                    if (Cover > 0f) return;
                    Phase = TransitionPhase.Idle;
                    ready = null; operation = null;
                    Finished?.Invoke();
                    return;
            }
        }

        // A stage that rejected its registration fails at once instead of waiting for the timeout.
        public void FailPreparing(string message)
        {
            if (Phase == TransitionPhase.Loading || Phase == TransitionPhase.Preparing) Fail(message);
        }

        private void Fail(string message)
        {
            FailureMessage = message;
            Phase = TransitionPhase.Failed;
            ready = null; operation = null;
            Failed?.Invoke(message);
        }

        // Another load took over (a test or tool loading a scene directly): forget this change without a failure.
        public void Cancel()
        {
            if (!IsBusy) return;
            Phase = TransitionPhase.Idle;
            Cover = 0f;
            ready = null; operation = null;
        }

        // Called after the failure was shown: the cover lifts and the next request may start.
        public void Acknowledge()
        {
            if (Phase != TransitionPhase.Failed) return;
            Phase = TransitionPhase.Idle;
            Cover = 0f;
        }
    }
}
