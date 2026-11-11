# GameManager 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 씬에 있던 `StageFlowController`의 흐름 규칙과 HUD를 씬 전환에도 남는 `GameManager`로 옮기고, 씬에는 Inspector 연결만 담는 `StageLayout`을 남긴다.

**Architecture:** 먼저 웨이브·휴식·구역 이동 규칙을 일반 C# `StageProgress`로 떼어 낸다(동작 변화 없음). 그다음 남은 상태·클리어·재시작·시간·HUD 코드를 DontDestroyOnLoad `GameManager`로 옮기고, `StageFlowController`는 GUID를 유지한 채 `StageLayout`으로 이름을 바꾼다. GameManager는 `Resources/GameManager.prefab`에서 플레이 시작 때 자동 생성되고, `StageLayout.Start`가 자신을 등록한다.

**Tech Stack:** Unity 6000.3.23f1, C#, UI Toolkit(UIDocument), Unity Test Framework 1.6, 열린 Editor를 다루는 Unity CLI.

**Spec:** `Docs/superpowers/specs/2026-11-04-game-manager-design.md`

## Global Constraints

- 커밋·푸시하지 않는다(사용자가 요청할 때만). 로컬 `AGENTS.md`는 절대 `git add` 하지 않는다.
- 네임스페이스는 `ActionPlatformer.Flow`. 기존 코드 스타일(짧은 줄 묶음, 영어 주석)을 따른다.
- 사용자 조정 값, 씬 배치, 흐름 시간 값(0.35초, 0.85초 등)을 바꾸지 않는다.
- 기존 오류 로그 문구를 바꾸지 않는다: `"Stage flow needs a live player, unique initialized enemies, a configured goal/view, and a scene in Build Settings."`, `"Rest transfer destination is blocked or unsupported; player control is preserved."`, `"Rest transfer arrival became invalid; recovery was not consumed."`, `"Final-wave cutscene could not start; the unlocked exit remains available."`.
- 씬·프리팹·에셋은 편집기 API로 고친다(`unity command "script eval_file"`). YAML을 손으로 고치지 않는다. 일회용 편집기 스크립트는 저장소 밖(계획 작업 폴더나 scratchpad)에 둔다.
- 열린 Editor를 Unity CLI로 쓴다. 항상 `--project-path "D:\test_65\test_65\test_65" --caller plugin --skill unity-cli`를 붙인다.
  - 컴파일: `unity command "script recompile"` 후 `unity command "console status"`로 오류 0 확인.
  - 테스트: `unity command "test run" --mode PlayMode --filter ActionPlatformer.Tests.<Class> --async_tests true`, 결과는 `unity command "test status"`. 필터에 `|`는 안 되므로 클래스마다 따로 실행한다. 재컴파일 직후 바로 실행하면 'running'에 멈출 수 있으니 컴파일 완료를 확인한 뒤 실행한다.
  - 테스트 클래스 이름 공간은 `ActionPlatformer.Tests`다.
- 프로젝트는 Domain Reload가 꺼져 있다(`EnterPlayModeOptions = 1`). 정적 필드는 `RuntimeInitializeOnLoadMethod(SubsystemRegistration)`에서 지운다.
- 의도된 오류 로그와 알려진 간헐 실패(`CutsceneTests`의 VirtualKeyboard 검사)는 실패로 세지 않되 결과에 따로 적는다.

## Review Focus

1. **Play Mode 종료 중 새 매니저 생성**: 종료할 때 씬의 `StageLayout.OnDisable`이 `GameManager.Instance`를 부르면 새 매니저를 만들 수 있다. 등록 해제는 만들지 않는 `GameManager.Existing`을 쓴다. Task 2의 EditMode 테스트가 Edit Mode에서 `Instance`·`Existing`이 null임을 고정한다.
2. **계속 남는 HUD가 이전 판의 화면을 들고 있음**: 클리어 화면에서 RESTART를 누르면, 다시 불러온 뒤에도 클리어 화면이 남아 있으면 안 된다. Task 2에서 `StageFlowTests.AwaitReload`에 클리어 화면 숨김·버튼 활성 검사를 더한다.
3. **스테이지 없는 씬에서 시간 배율을 건드림**: MovementLab·BossTest에서 바로 플레이하면 GameManager는 시간 배율을 건드리지 않고 HUD를 숨겨야 한다. Task 2의 `SceneWithoutStageHidesStageHudAndLeavesTimeAlone`.
4. **HUD 그리기 순서 변화**: 대사·말풍선 UI와 같은 PanelSettings를 쓴다. 프리팹 `UIDocument`의 정렬 순서는 씬의 `Stage UI`와 같은 100이어야 한다. Task 2의 `GameManagerPrefabIsWired`.
5. **진행 중 설정 오류의 정리 누락**: 기존에는 `enabled = false` → `OnDisable`로 정리됐다. 이제는 오류 경로가 등록 해제(구독 해제·시간 복원·HUD 숨김)를 반드시 거쳐야 한다. Task 2의 `StageThatFailsValidationIsNotRegistered`와 검토로 확인한다.

---

### Task 1: 웨이브·휴식·구역 이동 규칙을 `StageProgress`로 떼어 내기

동작은 바꾸지 않는 순수 리팩터링이다. `StageFlowController`는 씬에 그대로 남고 `StageProgress`를 소유한다.

**Files:**
- Create: `Assets/_Game/Runtime/Flow/StageProgress.cs`
- Create: `Assets/_Game/Runtime/Flow/StageState.cs` (`StageState`, `EncounterPhase` 열거형을 `StageFlowController.cs`에서 그대로 옮김)
- Modify: `Assets/_Game/Runtime/Flow/StageFlowController.cs`
- Test: 기존 `StageFlowTests`, `RestCycleTests`, `FinalWaveCutsceneTests`, `CutsceneTests` (수정 없음)

**Interfaces:**
- Produces (Task 2가 그대로 쓴다):

```csharp
public sealed class StageProgress   // plain C#, not a MonoBehaviour
{
    public StageProgress(PlayerUnit player, UnitHealth playerHealth, Collider2D playerBody,
        UnitHealth[] requiredEnemies, StageEncounter[] encounters, StageGoal goal,
        CutsceneCoordinator cinematics, StageFlowView view, LayerMask transferTerrainMask,
        UnityEngine.Object logContext, System.Action failConfiguration);
    public bool UsesEncounters { get; }
    public int RemainingEnemies { get; }
    public EncounterPhase Phase { get; }
    public int CurrentEncounterIndex { get; }
    public StageRestArea CurrentRestArea { get; }
    public IReadOnlyList<UnitHealth> ActiveEnemies { get; }
    public bool TryInitialize();                       // enemy/encounter validation + DamageApplied subscriptions
    public bool CanRequestTransfer(bool ownerPlaying); // ownerPlaying = owner initialized, active, State == Playing
    public void RequestTransfer(bool ownerPlaying);
    public void ClearTransferRequest();
    public bool UpdateObjective();                     // same return meaning as today
    public bool UpdateEncounter(bool ownerPlaying);    // true = this frame belongs to an intermediate room
    public void FixedTick();                           // transfer progress; no-op unless Phase == Transferring
    public void CancelTransfer();
    public void Dispose();                             // unsubscribe DamageApplied
}
```

- [ ] **Step 1: 기준선 확인**

수정 전에 네 클래스를 하나씩 실행해 통과 개수를 기록한다(`StageFlowTests`, `RestCycleTests`, `FinalWaveCutsceneTests`, `CutsceneTests`). VirtualKeyboard 간헐 실패가 나면 그대로 기록한다.

- [ ] **Step 2: 열거형 옮기기**

`StageState`·`EncounterPhase`를 `StageState.cs`로 옮긴다(이름·값·순서 그대로).

- [ ] **Step 3: `StageProgress` 만들기**

`StageFlowController`에서 아래를 본문 그대로 옮긴다. 동작·조건 순서·오류 문구를 유지한다.

| 옮길 것 | `StageProgress`에서 |
|---|---|
| `InitializeEncounters`, `EnemiesInitialized`, legacy `requiredEnemies` 검증 루프, `playerCapsule` 캐스트 | `TryInitialize()` 안. 실패는 `false` 반환(로그는 소유자 `FailConfiguration`이 남김) |
| `OnEnemyDamage`, `defeatPositions`, `subscriptions` | 그대로 |
| `UpdateObjective` | 그대로. 안에서 `FailConfiguration()` 부르던 곳은 생성자로 받은 `failConfiguration()` 호출 후 `return false` |
| `UpdateEncounter` | `UpdateEncounter(bool ownerPlaying)`. `CanRequestTransfer` 사용처는 `CanRequestTransfer(ownerPlaying)` |
| `BeginTransfer`, `EnterRest`, `CancelTransfer`, `RequestTransfer`, `transferRequested`, `transferReadyAt`, `route`, `transferElapsed`, `passedWaypoint`, `bodyOffset`, `overlaps` | 그대로. `Debug.LogError(..., this)`는 `logContext`로 |
| `FixedUpdate` 본문 중 `initialized`·`State` 검사 뒤 부분 | `FixedTick()`. 진입 검사 `Phase != Transferring`이면 반환 |
| `CanRequestTransfer` 속성 | `CanRequestTransfer(bool ownerPlaying)`: 기존 식의 `initialized && isActiveAndEnabled && State == StageState.Playing` 부분을 `ownerPlaying`으로 바꾼 것 |

- [ ] **Step 4: `StageFlowController`를 소유자로 줄이기**

남는 것: 직렬화 필드 전부, `Awake`의 시간 기록, `Start`의 플레이어·goal·view·씬 검증(같은 순서) 뒤 `progress = new StageProgress(...)`·`progress.TryInitialize()`(실패 시 `FailConfiguration()`), 구독(`view.RestartRequested`, `player.GlitchPressed += RequestTransfer` → `progress.RequestTransfer(OwnerPlaying)`), `LateUpdate`(HUD 갱신·사망·마지막 웨이브 컷신·출구 판정. `UpdateEncounter`/`UpdateObjective`/`CanRequestTransfer`는 `progress` 호출로), `FixedUpdate`(`initialized`·`State` 검사 뒤 `progress.FixedTick()`), `Complete`(`progress.CancelTransfer()`), `Restart`, `RestoreTime`, `FailConfiguration`, `OnDisable`(`progress.ClearTransferRequest()`·`CancelTransfer()`·`Dispose()`).
- `OwnerPlaying` = `initialized && isActiveAndEnabled && State == StageState.Playing`.
- 공개 속성 `RemainingEnemies`, `UsesEncounters`, `Phase`, `CurrentEncounterIndex`, `CurrentRestArea`, `ActiveEnemies`, `CanRequestTransfer`는 `progress`로 전달한다. `progress`가 아직 없으면 기본값(0 / `Combat` / null / 빈 목록 / false)을 돌려준다. `UsesEncounters`는 지금처럼 직렬화 필드 `encounters`로 계산한다. 테스트는 `UseLegacyObjective`에서 이 필드를 바꾼다.
- 목표: `StageFlowController.cs` 220줄 이하, `StageProgress.cs` 260줄 이하.

- [ ] **Step 5: 컴파일·테스트**

`script recompile` → `console status` 오류 0. Step 1의 네 클래스를 다시 실행해 기준선과 같은 결과가 나와야 한다.

---

### Task 2: 계속 남는 `GameManager`, `StageLayout`, 프리팹 HUD, 연출·테스트 이전

**Files:**
- Create: `Assets/_Game/Runtime/Flow/GameManager.cs`
- Rename: `Assets/_Game/Runtime/Flow/StageFlowController.cs` (+ `.meta`) → `StageLayout.cs` (+ `.meta`). GUID `4dffababc920b284293ddc39e9cea5e5`를 유지해야 한다.
- Modify: `Assets/_Game/Runtime/Flow/StageFlowView.cs`, `Assets/_Game/Runtime/Cinematics/CutsceneRunner.cs`, `Assets/_Game/Editor/Cinematics/CutsceneAuthoring.cs`
- Create (asset): `Assets/_Game/Resources/GameManager.prefab` (`Resources` 폴더도 새로 만듦)
- Modify (scene): `Assets/_Game/Scenes/CoreLoopStage.unity` (`Stage UI` 물체 삭제, 저장)
- Modify tests: `Tests/PlayMode/StageFlowTests.cs`, `RestCycleTests.cs`, `FinalWaveCutsceneTests.cs`, `CutsceneTests.cs`
- Create tests: `Tests/PlayMode/GameManagerTests.cs`, `Tests/EditMode/GameManagerEditModeTests.cs`
- Docs: `Docs/PrototypeGuide.md`, `Docs/PrototypeValidation.md`, 로컬 `AGENTS.md`(Git 제외)

**Interfaces:**
- Consumes: Task 1의 `StageProgress`(시그니처 그대로), `StageState.cs`.
- Produces (다음 작업인 보스 체력바가 쓴다):

```csharp
[DisallowMultipleComponent]
public sealed class GameManager : MonoBehaviour
{
    public const string ResourcePath = "GameManager";
    public const string ConfigurationErrorMessage = "Stage flow needs a live player, unique initialized enemies, a configured goal/view, and a scene in Build Settings.";
    public const string DuplicateStageMessage = "Only one StageLayout can be registered at a time; this one is ignored.";
    public const string MissingPrefabMessage = "GameManager prefab is missing at Resources/GameManager.";
    public static GameManager Instance { get; }   // creates on demand while playing; null in Edit Mode or while quitting
    public static GameManager Existing { get; }   // never creates
    [SerializeField] private StageFlowView view;
    public StageFlowView View { get; }
    public StageLayout Stage { get; }             // registered layout or null
    public bool HasStage { get; }
    public int StageVersion { get; }              // +1 on every successful registration
    public StageState State { get; }              // kept after unregistration; reset to Playing on registration
    public int RemainingEnemies { get; }  public PlayerUnit Player { get; }  public CutsceneRunner FinalWaveCutscene { get; }
    public bool UsesEncounters { get; }  public EncounterPhase Phase { get; }  public int CurrentEncounterIndex { get; }
    public StageRestArea CurrentRestArea { get; }  public IReadOnlyList<UnitHealth> ActiveEnemies { get; }
    public bool CanRequestTransfer { get; }
    public void Restart();
    internal bool Register(StageLayout layout);
    internal void Unregister(StageLayout layout);
}

[DisallowMultipleComponent]
public sealed class StageLayout : MonoBehaviour   // serialized field names unchanged: player, requiredEnemies, goal, cinematics, finalWaveCutscene, encounters, transferTerrainMask (view removed)
{
    public PlayerUnit Player { get; }  public UnitHealth[] RequiredEnemies { get; }  public StageGoal Goal { get; }
    public CutsceneCoordinator Cinematics { get; }  public CutsceneRunner FinalWaveCutscene { get; }
    public StageEncounter[] Encounters { get; }  public LayerMask TransferTerrainMask { get; }
    public static StageLayout FindIn(Scene scene); // first StageLayout under the scene roots (inactive included), or null
}

// StageFlowView additions
public bool IsStageVisible { get; }
public void ShowStage(bool visible);
public void ResetStage();   // hide goal/transfer/glitch hints, combo and completion; re-enable RESTART
```

- [ ] **Step 1: 새 테스트 작성 (RED)**

`Tests/EditMode/GameManagerEditModeTests.cs`:

```csharp
[Test] public void InstanceAndExistingAreNullOutsidePlayMode()
{
    Assert.That(GameManager.Instance, Is.Null);
    Assert.That(GameManager.Existing, Is.Null);
}
[Test] public void GameManagerPrefabIsWired()
{
    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Resources/GameManager.prefab");
    Assert.That(prefab, Is.Not.Null);
    var document = prefab.GetComponent<UIDocument>();
    Assert.That(document.panelSettings, Is.SameAs(AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/_Game/UI/StagePanelSettings.asset")));
    Assert.That(document.sortingOrder, Is.EqualTo(100f));
    var view = prefab.GetComponent<StageFlowView>();
    Assert.That(view, Is.Not.Null);
    Assert.That(new SerializedObject(view).FindProperty("document").objectReferenceValue, Is.SameAs(document));
    Assert.That(new SerializedObject(prefab.GetComponent<GameManager>()).FindProperty("view").objectReferenceValue, Is.SameAs(view));
}
[Test] public void CoreLoopStageHasOneLayoutAndNoSceneHud()
{
    const string path = "Assets/_Game/Scenes/CoreLoopStage.unity";
    var existing = SceneManager.GetSceneByPath(path);
    var scene = existing.isLoaded ? existing : EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
    try
    {
        var roots = scene.GetRootGameObjects();
        Assert.That(roots.SelectMany(r => r.GetComponentsInChildren<StageLayout>(true)).Count(), Is.EqualTo(1));
        Assert.That(roots.SelectMany(r => r.GetComponentsInChildren<StageFlowView>(true)), Is.Empty);
        Assert.That(roots.SelectMany(r => r.GetComponentsInChildren<GameManager>(true)), Is.Empty);
    }
    finally { if (!existing.isLoaded) EditorSceneManager.CloseScene(scene, true); }
}
```

`Tests/PlayMode/GameManagerTests.cs`(시간 저장·복원과 정리용 빈 씬은 `StageFlowTests`의 SetUp/TearDown 방식을 따른다):
- `InstanceIsCreatedOnceAndLivesInDontDestroyOnLoad`: `GameManager.Instance`가 null이 아니고, `gameObject.scene.name == "DontDestroyOnLoad"`이며, `FindObjectsByType<GameManager>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 1`.
- `ReloadKeepsTheSameManagerAndBindsTheNewStage`: CoreLoopStage를 두 번 불러온다(매번 `StageVersion`이 1 오를 때까지 대기). 같은 인스턴스, 개수 1, 이전 플레이어 == null(파괴됨), `Stage.Player.gameObject.scene == SceneManager.GetActiveScene()`, `State == Playing`, `View.IsStageVisible`.
- `SceneWithoutStageHidesStageHudAndLeavesTimeAlone`: MovementLab을 불러온다. `!HasStage`, `!View.IsStageVisible`. `Time.timeScale = .5f`로 둔 뒤 3프레임 지나도 `.5f` 그대로.
- `UnloadingTheStageRestoresTimeAndHidesHud`: CoreLoopStage 등록 뒤 `Time.timeScale = .3f; Time.fixedDeltaTime = .01f;`. MovementLab을 Single로 불러온 뒤 시간 두 값이 등록 때 값으로 돌아오고 `!HasStage`, `!View.IsStageVisible`.
- `SecondStageLayoutIsRejected`: CoreLoopStage 등록 뒤 `LogAssert.Expect(LogType.Error, GameManager.DuplicateStageMessage)`. 활성 씬에 `new GameObject("Extra stage").AddComponent<StageLayout>()`를 만들고 2프레임 대기. `Stage`·`StageVersion` 그대로.
- `StageThatFailsValidationIsNotRegistered`: MovementLab에서 `LogAssert.Expect(LogType.Error, GameManager.ConfigurationErrorMessage)` 후 빈 `StageLayout`을 만든다. 1프레임 뒤 `!HasStage`, `!View.IsStageVisible`.

Run: `script recompile`. Expected: `GameManager`·`StageLayout`이 없어 컴파일 실패(RED).

- [ ] **Step 2: `StageFlowController` → `StageLayout` 이름 바꾸기**

`git mv`로 `.cs`와 `.cs.meta`를 한 번에 옮기고 클래스 이름을 바꾼다. 새 `.meta`의 GUID가 `4dffababc920b284293ddc39e9cea5e5`인지 확인한다. 흐름 코드(Task 1에서 남은 소유자 코드)는 다음 단계에서 `GameManager`로 옮긴다. `StageLayout`에 남는 것:
- 직렬화 필드(`view` 삭제), 읽기 전용 속성, `FindIn(Scene)`.
- `Start() { var game = GameManager.Instance; if (game == null) Debug.LogError("StageLayout could not find the GameManager.", this); else game.Register(this); }`
- `OnDisable() => GameManager.Existing?.Unregister(this);`

- [ ] **Step 3: `GameManager` 구현**

- **생성**: `EffectsRuntime`과 같은 틀이다. `SubsystemRegistration`에서 정적 필드를 지운다. `BeforeSceneLoad`에서 `Ensure()`.
  - `Ensure()`는 이미 있으면 끝낸다. 없으면 `Resources.Load<GameObject>(ResourcePath)`를 `Instantiate`해 이름을 `"GameManager"`로 하고 `DontDestroyOnLoad`한다. 프리팹이 없으면 `MissingPrefabMessage` 오류를 남긴다.
  - `Awake`에서 다른 인스턴스가 이미 있으면 자신을 파괴한다. `OnApplicationQuit`에서 quitting 표시를 하고, `OnDestroy`에서 자신이면 정적 참조를 지운다.
- **`Register`**:
  1. 이미 다른 스테이지가 있으면 `DuplicateStageMessage` 오류를 남기고 `false`를 돌려준다(같은 layout이면 `true`).
  2. 시간 두 값을 기록하고, 판 단위 상태(State=Playing, 컷신 시도·대기 시각, progress)를 새로 둔다. `view.ResetStage()`를 부른다.
  3. Task 1 소유자의 `Start` 검증을 layout 값으로 수행한다. 씬 조건은 `layout.gameObject.scene.buildIndex >= 0`이다. 실패하면 `ConfigurationErrorMessage`(context = layout)를 남기고 정리한 뒤 `false`.
  4. 성공하면 `stage`를 정하고, 구독한 뒤 `StageVersion++`, `progress.UpdateObjective()`, `view.ShowStage(true)` 순서로 진행한다.
- **`Unregister(layout)`**:
  - layout이 등록된 것과 다르면 아무것도 하지 않는다.
  - 같으면 Task 1 소유자의 `OnDisable` 정리를 수행한다.
  - 이어서 `view.ResetStage()`와 `view.ShowStage(false)`를 부르고 `stage = null`로 둔다. `State`는 유지한다.
- **진행 중 `FailConfiguration`**: 같은 문구를 남기고 `Unregister(stage)`를 부른다. 이것이 기존 `enabled = false`를 대신한다. 이후 그 프레임의 `LateUpdate`·`FixedUpdate`는 즉시 끝낸다.
- **`LateUpdate`/`FixedUpdate`**: `stage == null`이면 반환한다. 아니면 Task 1 소유자 본문을 그대로 쓴다(`isActiveAndEnabled` 대신 `stage != null && initialized`). 실행 순서 특성(`DefaultExecutionOrder`)은 붙이지 않는다.
- **`Restart`**: 기존과 같은 조건이다. 씬은 `stage.gameObject.scene.buildIndex`로 다시 불러온다.
- `GameManager.cs`는 400줄 이하로 유지한다.

- [ ] **Step 4: `StageFlowView` 구역화**

`OnEnable`에서 만드는 요소를 모두 새 `stageSection`(`name = "stage-hud"`, absolute, 상하좌우 0, `PickingMode.Ignore`)에 넣고, `stageSection`을 root에 붙인다. `glitchHint.parent`가 `stageSection`이 되므로 위치 계산은 그대로 맞는다. 만든 직후에는 `ShowStage(false)`이다. `ResetStage()`는 `ShowGoalHint(false)`(전송 플래그 해제), 글리치 문구 숨김, `ShowCombo(0, 0)`, 클리어 화면 숨김, `SetRestartEnabled(true)`를 한다. 그 밖의 공개 API는 바꾸지 않는다.

- [ ] **Step 5: 컷신 쪽 전환**

- `CutsceneRunner`: `stage` 필드를 삭제한다.
  - 설정 검사의 `stage == null`은 `StageLayout.FindIn(gameObject.scene) == null`로 바꾼다(같은 문구). 같은 씬 검사에서는 `stage` 항목을 뺀다.
  - 실행 중 세 곳(시작 가능 여부, `Update`의 취소, 정리 중 입력 복원)은 비공개 속성 `StagePlaying`으로 바꾼다. `StagePlaying` = `GameManager.Existing`이 있고, `Stage != null && Stage.gameObject.scene == gameObject.scene && State == StageState.Playing`.
- `CutsceneAuthoring.InstallStageExample`: `InScene<StageLayout>`로 바꾸고 `Set(runner, "stage", flow)`를 삭제한다.

- [ ] **Step 6: 프리팹·씬 만들기 (일회용 `eval_file`)**

Play Mode가 아닌지, 지금 열린 씬과 dirty 상태를 먼저 기록한다.
1. `Assets/_Game/Resources` 폴더를 만든다.
2. 루트 `GameManager`를 만들고 `UIDocument`(panelSettings = `Assets/_Game/UI/StagePanelSettings.asset`, sortingOrder = CoreLoopStage `Stage UI`의 값 100), `StageFlowView`(`document` 연결), `GameManager`(`view` 연결)를 붙여 `Assets/_Game/Resources/GameManager.prefab`으로 저장한다.
3. CoreLoopStage를 열어 `Stage UI` 물체에 `UIDocument`·`StageFlowView` 외 컴포넌트·자식이 없는지 확인한 뒤 지우고 씬을 저장한다. `Stage Flow` 물체의 컴포넌트가 `StageLayout`으로 보이고 기존 연결 값이 남아 있는지 확인한다.
4. 처음 열려 있던 씬으로 돌아간다.
`git status`로 CoreLoopStage·새 프리팹 외의 씬·에셋 변경이 없는지 확인한다.

- [ ] **Step 7: 기존 테스트 이전**

- **공통**
  - `FindFirstObjectByType<StageFlowController>()`를 `GameManager.Instance`로 바꾼다(`Assert.That(..HasStage, Is.True)` 포함).
  - 반사(reflection)로 필드를 바꾸던 대상 타입은 `StageLayout`이다. 필드 이름은 같다.
  - `flow.gameObject.scene`은 `flow.Stage.gameObject.scene`으로 바꾼다. 씬을 내리는 테스트는 내리기 전에 씬을 기억해 둔다.
- **다시 불러오기 대기**: "이전 컨트롤러가 null이 될 때까지"를 "`StageVersion`이 이전 값보다 커질 때까지"로 바꾼다.
  - 대상: `StageFlowTests.AwaitReload`(인자를 `int previousVersion`으로), `RestCycleTests.DeathWinsOverFinalKillAndResetsTheWholeLoop`, `CutsceneTests`의 재시작 대기.
- **`StageFlowTests.AwaitReload`**: `Assert.That(view.IsCompletedVisible, Is.False)`와 RESTART 버튼 `enabledSelf == true` 검사를 더한다.
- **`StageFlowTests` 재시작 반복 테스트**: `FindObjectsByType<StageFlowController>` 개수 검사를 `StageLayout` 1개와 `GameManager` 1개 검사로 바꾼다.

- [ ] **Step 8: 테스트 (GREEN)**

`script recompile` → 오류 0. 클래스별 실행:
- EditMode: `GameManagerEditModeTests`, `CutsceneAuthoringTests`
- PlayMode: `GameManagerTests`, `StageFlowTests`, `RestCycleTests`, `FinalWaveCutsceneTests`, `CutsceneTests`

모두 통과해야 한다. VirtualKeyboard 간헐 실패는 따로 적는다.

- [ ] **Step 9: 실제 플레이 확인**

`eval_file`로 CoreLoopStage를 Play Mode에서 연다. 스크립트에서 `Application.runInBackground = true`로 두고, 끝나면 `false`로 되돌린다. 아래를 확인한다.
- `GameManager`가 DontDestroyOnLoad에 하나 있다.
- 목표 안내·COMBO가 보인다.
- 플레이어를 죽이면 씬이 다시 불러와지고, 같은 매니저가 새 스테이지에 연결된다.

BossTest에서는 `HasStage == false`이고 HUD가 숨겨져 있는지 확인한다. 콘솔 오류는 0이어야 한다.

- [ ] **Step 10: 전체 실행과 문서**

- **전체 실행**: EditMode 전체와 PlayMode 전체를 실행한다. 기준은 EditMode 104 + 새 테스트, PlayMode 369 + 새 테스트이며 모두 통과해야 한다.
- **`Docs/PrototypeGuide.md`**: 2026-11-04 GameManager 절을 쓴다. 생성 방식, `Resources/GameManager.prefab`, `StageLayout`, HUD 구역, 스테이지 없는 씬의 동작을 적는다.
- **`Docs/PrototypeValidation.md`**: 결과를 적는다.
- **로컬 `AGENTS.md`**: 코드 구조 표의 `StageFlowController` 행, 코어루프 절의 `StageFlowController.encounters`·`StageFlowController.UpdateObjective` 언급, 씬·프리팹 목록을 고친다.
- `git status`로 의도한 파일만 바뀌었는지 확인한다.
