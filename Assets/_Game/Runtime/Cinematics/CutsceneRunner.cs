using System;
using System.Collections.Generic;
using ActionPlatformer.Flow;
using ActionPlatformer.Player;
using ActionPlatformer.Units;
using ActionPlatformer.Units.Features;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace ActionPlatformer.Cinematics
{
    public enum CutsceneWorldMode { Pause, Continue }
    public enum CutsceneState { Idle, Playing, Waiting, Returning }
    public enum CutsceneEndReason { Completed, Skipped, Damaged, Cancelled }

    [DisallowMultipleComponent, RequireComponent(typeof(PlayableDirector))]
    public sealed class CutsceneRunner : MonoBehaviour
    {
        [SerializeField] private PlayableDirector director;
        [SerializeField] private CutsceneView view;
        [SerializeField] private CutsceneCoordinator coordinator;
        [SerializeField] private PlayerUnit player;
        [SerializeField] private StageFlowController stage;
        [SerializeField] private InputActionAsset cutsceneInput;
        [SerializeField] private CutsceneWorldMode worldMode;
        [SerializeField] private bool allowSkip = true;
        [SerializeField, Min(0f)] private float returnDuration = .35f;
        [SerializeField] private UnityEvent completed = new UnityEvent();
        private readonly List<TimelineClip> waitClips = new List<TimelineClip>();
        private readonly List<IDisposable> pausedFsms = new List<IDisposable>();
        private readonly DialoguePlayback dialogue = new DialoguePlayback();
        private readonly List<DialogueSpeaker> boundSpeakers = new List<DialogueSpeaker>();
        private TimelineClip submittedClip;
        private double submittedTime;
        private int submissionCount;
        private bool collectingDialogue, dialogueBubble;
        private DialogueSpeaker dialogueActor;
        public DialoguePlayback Dialogue => dialogue;
        public DialogueSpeaker CurrentSpeaker => dialogueActor;
        private InputActionAsset inputInstance;
        private InputAction confirm, skip;
        private PlayerInput playerInput;
        private UnitHealth health;
        private IDisposable controlLock;
        private bool inputWasActive, feedbackSuppressed, ownsTime, cleaning;
        private float savedScale, returnElapsed;
        private int nextWait, inputFrame;
        private TimelineClip waitingClip;
        private CutsceneEndReason returnReason;
        private uint initialDamage;
        private double currentTime;
        public CutsceneState State { get; private set; }
        public bool IsRunning => State != CutsceneState.Idle;
        public PlayableDirector Director => director;
        public CutsceneView View => view;
        public CutsceneCoordinator Coordinator => coordinator;
        public PlayerUnit Player => player;
        public CutsceneWorldMode WorldMode { get => worldMode; set { if (!IsRunning) worldMode = value; } }
        public double CurrentTime => currentTime;
        public event Action<CutsceneEndReason> Finished;

        public void BindOutputs()
        {
            if (director == null) director = GetComponent<PlayableDirector>();
            if (!(director.playableAsset is TimelineAsset timeline)) return;
            foreach (var track in timeline.GetOutputTracks())
            {
                if (track is DialogueTrack || track is FadeTrack) director.SetGenericBinding(track, view);
                if (track is CinemachineTrack && coordinator != null && coordinator.CameraRig != null)
                    { director.SetGenericBinding(track, coordinator.CameraRig.Brain); director.SetReferenceValue(new PropertyName("CutsceneEntryCamera"), coordinator.CameraRig.EntryCamera); }
            }
        }
        public bool TryValidate(out string error)
        {
            error = null;
            if (director == null || !(director.playableAsset is TimelineAsset timeline) || timeline.duration <= 0 ||
                double.IsInfinity(timeline.duration)) error = "유효한 Timeline을 지정하세요.";
            else if (view == null || !view.IsConfigured) error = "대화 UI의 Document, UXML, Panel Settings를 연결하세요.";
            else if (coordinator == null || coordinator.CameraRig == null || !coordinator.CameraRig.IsConfigured)
                error = "씬의 CutsceneCoordinator와 카메라 연결을 확인하세요.";
            else if (player == null || stage == null || cutsceneInput == null ||
                cutsceneInput.FindAction("Cutscene/Confirm") == null || cutsceneInput.FindAction("Cutscene/Skip") == null)
                error = "플레이어, 스테이지 흐름, 연출 입력을 연결하세요.";
            else if (player.gameObject.scene != gameObject.scene || coordinator.gameObject.scene != gameObject.scene ||
                stage.gameObject.scene != gameObject.scene) error = "연출 연결은 같은 씬에 있어야 합니다.";
            else if (float.IsNaN(returnDuration) || float.IsInfinity(returnDuration) || returnDuration < 0)
                error = "복귀 시간은 유한한 0 이상의 값이어야 합니다.";
            if (error != null) return false;
            int dialogues = 0, fades = 0, cameras = 0;
            var allDialogueClips = new List<TimelineClip>();
            foreach (var track in ((TimelineAsset)director.playableAsset).GetOutputTracks())
            {
                if (track.mutedInHierarchy) continue;
                if (IsDialogueTrack(track))
                {
                    dialogues++;
                    allDialogueClips.AddRange(track.GetClips());
                }
                if (track is SpeechBubbleTrack)
                {
                    var actor = director.GetGenericBinding(track) as DialogueSpeaker;
                    if (actor == null || !actor.IsConfigured || actor.gameObject.scene != gameObject.scene)
                    { error = "말풍선 트랙에 같은 씬의 화자와 자식 SpeechAnchor를 연결하세요."; return false; }
                }
                if (track is FadeTrack) fades++;
                if (track is CinemachineTrack) cameras++;
                var clips = new List<TimelineClip>(track.GetClips());
                clips.Sort((a, b) => a.start.CompareTo(b.start));
                double end = -1;
                foreach (var clip in clips)
                {
                    if ((IsDialogueTrack(track) || track is FadeTrack) && clip.start < end - .00001)
                    { error = "대화/페이드 클립은 같은 트랙에서 겹칠 수 없습니다."; return false; }
                    end = clip.end;
                    if (clip.asset is DialogueClip dialogue && (float.IsNaN(dialogue.CharactersPerSecond) ||
                        float.IsInfinity(dialogue.CharactersPerSecond) || dialogue.CharactersPerSecond < 0))
                    { error = "글자 출력 속도는 유한한 0 이상의 값이어야 합니다."; return false; }
                    if (clip.asset is CinemachineShot shot && shot.VirtualCamera.Resolve(director) == null)
                    { error = "Cinemachine Shot에 카메라를 연결하세요."; return false; }
                }
            }
            allDialogueClips.Sort((a, b) => a.start.CompareTo(b.start));
            for (int i = 1; i < allDialogueClips.Count; i++)
                if (allDialogueClips[i].start < allDialogueClips[i - 1].end - .00001)
                { error = "대화 클립은 다른 트랙에서도 겹칠 수 없습니다."; return false; }
            if (dialogues < 1 || fades != 1 || cameras < 1)
                error = "대화 1개 이상, 페이드 1개, Cinemachine 1개 이상의 활성 트랙이 필요합니다.";
            return error == null;
        }
        public static bool IsDialogueTrack(TrackAsset track) => track is DialogueTrack || track is SpeechBubbleTrack;
        public bool TryPlay()
        {
            if (!Application.isPlaying || !isActiveAndEnabled || IsRunning) return false;
            if (!TryValidate(out string error)) { Debug.LogError(error, this); return false; }
            boundSpeakers.Clear();
            foreach (var track in ((TimelineAsset)director.playableAsset).GetOutputTracks())
                if (track is SpeechBubbleTrack && !track.mutedInHierarchy)
                {
                    var actor = director.GetGenericBinding(track) as DialogueSpeaker;
                    if (!actor.IsAvailable) return false;
                    boundSpeakers.Add(actor);
                }
            if (!player.isActiveAndEnabled || player.Motor == null || stage.State != StageState.Playing ||
                !player.TryGetComponent(out health) || !health.IsAlive || !coordinator.TryAcquire(this)) return false;
            State = CutsceneState.Playing;
            try
            {
                BindOutputs();
                playerInput = player.GetComponent<PlayerInput>();
                inputWasActive = playerInput.inputIsActive;
                controlLock = player.AcquireControlLock();
                feedbackSuppressed = player.Feedback != null && player.Feedback.CameraEffectsSuppressed;
                if (player.Feedback != null) player.Feedback.CameraEffectsSuppressed = true;
                playerInput.DeactivateInput();
                player.ControlInterrupted += OnPlayerInterrupted;
                initialDamage = health.DamageVersion;
                savedScale = Time.timeScale;
                if (worldMode == CutsceneWorldMode.Pause)
                {
                    // Only this scene's initialized FSMs are suspended; no state is exited or recreated.
                    foreach (var unit in FindObjectsByType<Unit>(FindObjectsSortMode.None))
                        if (unit.gameObject.scene == gameObject.scene && unit.Fsm != null) pausedFsms.Add(unit.Fsm.Pause());
                    Time.timeScale = 0; ownsTime = true;
                }
                if (inputInstance == null)
                {
                    inputInstance = Instantiate(cutsceneInput);
                    confirm = inputInstance.FindAction("Cutscene/Confirm", true);
                    skip = inputInstance.FindAction("Cutscene/Skip", true);
                }
                inputInstance.Enable();
                inputFrame = Time.frameCount;
                nextWait = 0; waitingClip = null; currentTime = 0; returnElapsed = 0;
                dialogue.Clear(); view.Clear(); view.ShowSkipHint = allowSkip; waitClips.Clear();
                foreach (var track in ((TimelineAsset)director.playableAsset).GetOutputTracks())
                    if (IsDialogueTrack(track) && !track.mutedInHierarchy)
                        foreach (var clip in track.GetClips())
                            if (clip.asset is DialogueClip data && data.Progress == DialogueProgress.Confirm) waitClips.Add(clip);
                waitClips.Sort((a, b) => a.start.CompareTo(b.start));
                coordinator.CameraRig.Begin();
                director.timeUpdateMode = DirectorUpdateMode.Manual;
                director.extrapolationMode = DirectorWrapMode.Hold;
                director.Play();
                Advance(0);
                return true;
            }
            catch (Exception exception)
            {
                Finish(CutsceneEndReason.Cancelled);
                Debug.LogException(exception, this);
                return false;
            }
        }
        private void Update()
        {
            if (!IsRunning) return;
            if (player == null || !player.isActiveAndEnabled || health == null || !health.IsAlive ||
                health.DamageVersion != initialDamage) { Finish(CutsceneEndReason.Damaged); return; }
            if (stage == null || !stage.isActiveAndEnabled || stage.State != StageState.Playing)
            { Cancel(); return; }
            foreach (var actor in boundSpeakers)
                if (actor == null || !actor.IsAvailable) { Cancel(); return; }
            if (Time.frameCount > inputFrame)
            {
                if (allowSkip && skip.WasPressedThisFrame()) { Skip(); return; }
                if (confirm.WasPressedThisFrame()) Confirm();
            }
            if (State == CutsceneState.Playing) Advance(Time.unscaledDeltaTime);
            else if (State == CutsceneState.Waiting) Evaluate();
            else if (State == CutsceneState.Returning)
            {
                returnElapsed += Time.unscaledDeltaTime;
                if (returnElapsed >= returnDuration) Finish(returnReason);
            }
        }
        private void LateUpdate()
        {
            if (!IsRunning || coordinator == null || coordinator.CameraRig == null) return;
            if (State == CutsceneState.Returning)
                coordinator.CameraRig.BlendBack(returnDuration > 0 ? returnElapsed / returnDuration : 1, Time.unscaledDeltaTime);
            else coordinator.CameraRig.Tick(Time.unscaledDeltaTime);
            UpdatePresentationPosition();
        }
        private void Advance(double delta)
        {
            double destination = Math.Min(currentTime + delta, director.duration);
            if (nextWait < waitClips.Count && waitClips[nextWait].start <= destination + .000001)
            {
                waitingClip = waitClips[nextWait++];
                currentTime = waitingClip.start + Math.Min(.00001, waitingClip.duration * .01);
                dialogue.BeginWait(waitingClip, Time.unscaledTimeAsDouble);
                State = CutsceneState.Waiting;
            }
            else currentTime = destination;
            Evaluate();
            if (State == CutsceneState.Playing && currentTime >= director.duration) BeginReturn(CutsceneEndReason.Completed);
        }
        private void Evaluate() { director.time = currentTime; EvaluatePresentation(false); }
        public void SubmitDialogue(TimelineClip clip, double localTime, bool asBubble, DialogueSpeaker actor)
        {
            if (!collectingDialogue) return;
            submissionCount++; submittedClip = clip; submittedTime = localTime;
            dialogueBubble = asBubble; dialogueActor = actor;
        }
        // Used by both manual runtime evaluation and the Timeline editor preview.
        public void EvaluatePresentation(bool preview)
        {
            submittedClip = null; submissionCount = 0; dialogueActor = null; dialogueBubble = false;
            collectingDialogue = true;
            try { director.Evaluate(); }
            finally { collectingDialogue = false; }
            if (submissionCount > 1 || (submittedClip != null && dialogueBubble &&
                (dialogueActor == null || !dialogueActor.IsAvailable)))
            {
                dialogue.Hide(); view.HideDialogue();
                if (!preview && IsRunning) Cancel();
                return;
            }
            dialogue.Evaluate(submittedClip, submittedTime, Time.unscaledTimeAsDouble, preview);
            view.RenderDialogue(dialogue, dialogueActor, dialogueBubble, preview);
        }
        public void UpdatePresentationPosition()
        {
            if (view != null && coordinator != null && coordinator.CameraRig != null)
                view.UpdateBubblePosition(coordinator.CameraRig.Output, dialogueActor);
        }
        public void ClearPreview()
        {
            if (IsRunning) return;
            dialogue.Clear(); dialogueActor = null; view?.Clear();
        }
        public void Confirm()
        {
            if (State != CutsceneState.Waiting) return;
            if (dialogueBubble && (dialogueActor == null || !dialogueActor.IsAvailable)) { Cancel(); return; }
            if (!dialogue.IsFullyRevealed)
            {
                dialogue.Reveal(); view.RenderDialogue(dialogue, dialogueActor, dialogueBubble, false); return;
            }
            currentTime = waitingClip.end;
            waitingClip = null; dialogue.EndWait(); view.HideDialogue();
            State = CutsceneState.Playing;
            Advance(0);
        }
        public void Skip() { if (IsRunning && allowSkip && State != CutsceneState.Returning) BeginReturn(CutsceneEndReason.Skipped); }
        public void Cancel()
        {
            if (IsRunning) Finish(health != null && (!health.IsAlive || health.DamageVersion != initialDamage)
                ? CutsceneEndReason.Damaged : CutsceneEndReason.Cancelled);
        }
        private void OnPlayerInterrupted() { if (IsRunning) Finish(CutsceneEndReason.Damaged); }
        private void BeginReturn(CutsceneEndReason reason)
        {
            coordinator.CameraRig.CaptureReturnStart();
            director.Stop();
            dialogue.Clear(); view.Clear();
            State = CutsceneState.Returning; returnElapsed = 0; returnReason = reason;
            if (returnDuration <= 0) Finish(reason);
        }
        private void Finish(CutsceneEndReason reason)
        {
            if (!IsRunning || cleaning) return;
            cleaning = true;
            State = CutsceneState.Idle;
            try
            {
                if (director != null) director.Stop();
                dialogue.Clear(); boundSpeakers.Clear(); dialogueActor = null;
                if (view != null) view.Clear();
                if (inputInstance != null) inputInstance.Disable();
                if (coordinator != null) { coordinator.CameraRig?.End(); coordinator.Release(this); }
                foreach (var pause in pausedFsms) pause.Dispose();
                pausedFsms.Clear();
                if (ownsTime)
                {
                    if (Time.timeScale == 0) Time.timeScale = savedScale;
                    // The cutscene does not modify fixedDeltaTime; never overwrite another owner's value.
                    ownsTime = false;
                }
                if (player != null)
                {
                    player.ControlInterrupted -= OnPlayerInterrupted;
                    if (player.Feedback != null) player.Feedback.CameraEffectsSuppressed = feedbackSuppressed;
                }
                controlLock?.Dispose(); controlLock = null;
                if (inputWasActive && playerInput != null && playerInput.isActiveAndEnabled &&
                    player != null && player.isActiveAndEnabled && health != null && health.IsAlive &&
                    stage != null && stage.isActiveAndEnabled && stage.State == StageState.Playing && !playerInput.inputIsActive)
                    playerInput.ActivateInput();
            }
            finally { cleaning = false; }
            Finished?.Invoke(reason);
            if (reason == CutsceneEndReason.Completed || reason == CutsceneEndReason.Skipped) completed.Invoke();
        }
        private void OnDisable() => Cancel();
        private void OnDestroy()
        {
            Cancel();
            if (inputInstance != null) Destroy(inputInstance);
        }
    }
}
