# GameManager 설계 (2026-11-04)

## 왜

보스 체력바를 붙이기 전에, 게임 전체 흐름을 한곳에서 관리하는 매니저를 둔다. 지금 흐름은 CoreLoopStage 씬 안의 `StageFlowController`가 맡고 있어서 씬마다 따로 존재하고, 재시작하면 같이 사라졌다가 다시 생긴다. 사용자는 이것을 **씬 전환에도 사라지지 않는 GameManager**로 바꾸고, `StageFlowController`의 기능을 **전부** GameManager로 옮기기로 했다(2026-11-04, "전부 옮기기").

## 목표

- 플레이를 시작하면 어느 씬에서든 GameManager가 하나 생기고, 씬을 바꾸거나 다시 불러와도 그대로 남는다.
- 지금 `StageFlowController`가 하는 흐름 규칙과 `StageFlowView`가 그리는 화면 표시를 모두 GameManager가 소유한다.
- 각 씬에는 Inspector 연결만 담는 데이터 부품 `StageLayout`이 남는다.
- CoreLoopStage의 플레이 동작은 바뀌지 않는다(아래 '유지할 동작').
- 이후 보스 체력바를 GameManager의 화면 표시에 붙일 수 있다.

## 하지 않는 것

- 보스 체력바(이 작업 다음에 따로 한다).
- 타이틀, 일시정지, 다음 스테이지, 저장, 체크포인트. GameManager에 나중에 붙일 자리만 생긴다.
- BossTest·MovementLab에 스테이지 진행 추가. 이 씬들에는 `StageLayout`이 없으므로 GameManager는 아무것도 하지 않는다.
- 웨이브·휴식·구역 이동의 시간·거리 값 변경, CoreLoopStage 배치 변경, 사용자 조정 값(PlayerTuning 등) 변경.

## 실패로 보는 것

- 재시작 뒤 GameManager가 둘이 되거나, 이전 씬의 플레이어·적을 계속 붙잡고 있는 것.
- 테스트 사이에 GameManager의 상태(시간 배율, 화면 표시, 연결된 스테이지)가 다음 테스트로 새어 나가는 것.
- 스테이지가 없는 씬에서 HUD가 보이거나 시간 배율을 건드리는 것.
- 기존 동작이 미묘하게 달라지는 것(예: 0.35초 지연, 사망이 클리어보다 우선, 휴식 회복 1회).

## 구조

| 부품 | 위치 | 내용 |
|---|---|---|
| `GameManager` (새 스크립트) | DontDestroyOnLoad | 흐름 규칙 전부와 HUD. 아래 '옮길 기능' |
| `StageLayout` | 각 씬 | 지금의 `StageFlowController`를 이름만 바꾼 것(스크립트 GUID 유지). Inspector 연결 필드만 가진다 |
| `StageFlowView` | GameManager 프리팹 | 화면 요소 생성 코드는 그대로, 위치만 씬의 `Stage UI`에서 GameManager 프리팹으로 옮긴다 |

- 네임스페이스는 기존 `ActionPlatformer.Flow`.
- `StageLayout`은 GUID를 유지해 CoreLoopStage의 기존 연결 값(player, requiredEnemies, goal, cinematics, finalWaveCutscene, encounters, transferTerrainMask)을 그대로 살린다. `view` 필드는 GameManager로 넘어가므로 지운다.
- 씬에 남는 물체 부품(`StageGate`, `StageGoal`, `StageRestArea`, `StageEncounter`, `StageTransferRoute`, `StageCameraFollow`)은 지금처럼 씬에 있다. 이들은 문·출구·휴식 구역 자체의 작은 동작이며, 이들을 언제 어떻게 쓸지 정하는 규칙이 GameManager로 간다.

### 옮길 기능 (StageFlowController → GameManager)

- 스테이지 상태 `StageState`(Playing/Dying/Restarting/Completed), 구역 단계 `EncounterPhase`.
- 시작 검증과 설정 오류 처리(같은 오류 문구).
- 웨이브 진행, 적 처치 판정(체력 0만 인정), 마지막 적 처치 연출 요청(`PlayerUnit.PresentFinalBlow`).
- 출구문·휴식 구역 입구 잠금과 해제, 휴식 구역 회복, 구역 이동(Shift 요청, 경로 계산, `BeginScriptedTransfer` 등), 막힘 처리(같은 오류 문구).
- 마지막 웨이브 출구 컷신 재생.
- 출구 도달 클리어, 클리어 화면, 입력·피해·물리 정지, 시간 정지.
- 사망 처리와 씬 다시 불러오기, 시간 배율·고정 시간 간격 복원.
- HUD 갱신(목표 안내, 휴식 이동 안내, 글리치 사용 불가 문구, COMBO).
- 지금 `StageFlowController`의 공개 읽기 값(`State`, `RemainingEnemies`, `Player`, `FinalWaveCutscene`, `UsesEncounters`, `Phase`, `CurrentEncounterIndex`, `CurrentRestArea`, `ActiveEnemies`, `CanRequestTransfer`)과 `Restart()`는 GameManager에서 같은 이름·같은 의미로 제공한다.

### 생성과 유지

- 효과 관리자(`EffectsRuntime`)와 같은 방식으로 `RuntimeInitializeOnLoadMethod(BeforeSceneLoad)`에서 자동 생성한다. 이미 있으면 새로 만들지 않는다.
- 생성은 `Assets/_Game/Resources/GameManager.prefab`을 불러와 복제한다(지금 `Resources` 폴더는 없으므로 새로 만든다). 프리팹은 GameManager, `UIDocument`(`UI/StagePanelSettings.asset`), `StageFlowView`를 담는다. 화면 설정은 이 프리팹에서 고친다.
- 프로젝트는 플레이 시작 때 코드를 다시 불러오지 않는다(Enter Play Mode Options의 Domain Reload 끔). 그래서 `SubsystemRegistration`에서 정적 참조를 지운다(`EffectsRuntime`과 같은 방식).
- `GameManager.Instance`로 접근한다. 플레이 중이 아니거나 종료 중이면 null이다.

### 씬 연결 시점

- `StageLayout`의 `Start`에서 `GameManager.Instance`에 자신을 등록하고, `OnDisable`에서 등록을 푼다. 이것이 `StageLayout`이 가진 유일한 코드다.
- 연결 시점을 지금 `StageFlowController.Start`와 같게 두는 이유: 기존 테스트가 `sceneLoaded` 콜백(Start 이전)에서 `encounters`·`requiredEnemies`를 바꿔 끼운다. GameManager가 자기 `sceneLoaded`에서 먼저 읽으면 이 순서가 깨진다.
- 등록되면 GameManager가 지금의 `Start` 검증을 수행한다. 실패하면 같은 오류를 남기고 그 씬의 진행을 하지 않는다(GameManager 자체는 꺼지지 않는다).
- 동시에 등록되는 스테이지는 하나다. 이미 등록된 상태에서 또 등록되면 설정 오류로 남기고 무시한다.
- 원래 시간 배율과 고정 시간 간격은 등록 시점에 기록한다(지금 `Awake`에서 기록하던 값에 해당).
- 등록이 풀리면(씬 언로드·비활성화) 지금의 `OnDisable` 정리를 수행한다: 구독 해제, 구역 이동 취소, 컷신 취소, 시간 복원, 스테이지 HUD 숨김.
- 등록된 뒤 진행 중에 설정 오류가 나면(지금 코드의 `FailConfiguration` → `enabled = false`) 같은 오류를 남기고 위 정리를 한 뒤 등록이 풀린 것으로 본다.
- 등록된 스테이지가 없으면 GameManager는 흐름 규칙을 돌리지 않고 스테이지 HUD를 숨긴다.

### HUD 구역

- HUD는 구역으로 나눈다. 이번 작업에서는 **스테이지 구역** 하나만 만든다: 목표·휴식 이동 안내, 글리치 사용 불가 문구, COMBO, 클리어 화면. 스테이지 구역은 스테이지 등록을 따른다.
- 다음 작업의 보스 체력바는 스테이지 등록과 상관없는 별도 구역이 된다. 보스가 GameManager에 표시를 요청하는 방식이라 스테이지가 없는 BossTest에서도 보인다. 이번에는 그 구역을 만들지 않고, 스테이지 구역 밖에 다른 구역을 붙일 수 있는 구조만 둔다.
- 새 스테이지가 등록되면 이전 스테이지가 남긴 HUD 상태를 지운다: 클리어 화면 숨김, RESTART 버튼 다시 활성화, 안내 문구 숨김.

### 재시작

- 지금처럼 등록된 스테이지 씬의 build index로 `LoadSceneAsync(..., Single)`을 호출한다. GameManager는 남고, 새 씬의 `StageLayout`이 다시 등록되면 상태를 새로 시작한다(Playing, 단계·타이머·이동 상태 초기화).
- build index가 없는 씬(빌드 설정에 없는 씬)의 스테이지는 지금처럼 설정 오류다.

### 컷신 연결

- `CutsceneRunner`의 `stage` Inspector 필드를 없애고 `GameManager.Instance`로 상태를 읽는다. 지금 `stage`를 확인하던 세 곳(시작 가능 여부, 실행 중 취소, 종료 후 입력 복원)은 "GameManager에 이 러너와 같은 씬의 스테이지가 등록돼 있고 상태가 Playing"으로 바꾼다.
- 러너 설정 검사(`연결 검사`)의 "스테이지 흐름" 항목은 같은 씬에 `StageLayout`이 있는지로 바꾼다.
- `Game > Cinematics` 설치 메뉴(`CutsceneAuthoring.InstallStageExample`)는 `StageFlowController` 대신 `StageLayout`을 찾고, 러너에 `stage`를 넣던 줄을 지운다.

### 파일 나누기

- GameManager가 한 파일에 400줄을 넘기지 않도록, 웨이브·휴식·구역 이동 규칙은 GameManager가 소유하는 일반 C# 객체로 떼어 낸다(이름은 builder가 정한다. 예: `EncounterFlow`). 플레이어가 `PlayerGlitch`·`PlayerCombat`을 일반 C# 객체로 갖는 지금 방식과 같다. MonoBehaviour로 만들지 않는다.
- 실행 중 상태는 GameManager와 그 객체에만 두고, `StageLayout`·프리팹·에셋에 쓰지 않는다.

## 유지할 동작

`StageFlowController`의 현재 코드가 기준이다. 특히:
- LateUpdate에서 판정, FixedUpdate에서 구역 이동 진행(지금과 같은 단계). GameManager가 DontDestroyOnLoad로 옮겨 가도 실행 순서 설정은 지금과 같은 기본값으로 두고, 컷신·플레이어와의 갱신 순서가 그대로인지 테스트로 확인한다.
- 사망이 클리어보다 우선. 사망 중 컷신 취소, 플레이어가 비활성화되면 재시작.
- 구역 전멸 후 0.35초 뒤 이동 요청 가능, 마지막 적 처치 후 0.85초 뒤 출구 컷신, 컷신은 한 번.
- 휴식 구역 최초 도착 회복 1회, 다음 입구 통과 시 뒤쪽 문 잠금과 다음 그룹 활성화.
- 클리어 시 `timeScale 0`, 재시작 시 원래 시간 배율·고정 시간 간격 복원.
- 기존 오류 로그 문구(테스트가 `LogAssert.Expect`로 검사).

## 장면·에셋 변경

- CoreLoopStage: `Stage Flow` 물체의 컴포넌트가 `StageLayout`이 된다(GUID 유지라 자동). `Stage UI` 물체(`UIDocument` + `StageFlowView`)는 지운다. 씬의 `CutsceneRunner` 인스턴스에 남은 `stage` 값은 필드 삭제로 사라진다.
- 새 `Assets/_Game/Resources/GameManager.prefab`.
- 씬·프리팹 수정은 편집기 API로 한다(YAML 직접 편집 금지). 일회용 편집기 스크립트는 저장소에 남기지 않는다.

## 테스트

- 고칠 테스트: `StageFlowTests`, `RestCycleTests`, `FinalWaveCutsceneTests`, `CutsceneTests`(PlayMode), `CutsceneAuthoringTests`(EditMode). `FindFirstObjectByType<StageFlowController>` → `GameManager.Instance`, 필드 바꿔 끼우기는 `StageLayout` 대상으로.
- 새 테스트(최소):
  - 플레이 시작 시 GameManager가 하나 생기고 DontDestroyOnLoad에 있다.
  - 재시작 뒤에도 같은 GameManager 하나이고, 새 씬의 플레이어·적에 연결된다.
  - 스테이지가 없는 씬에서는 HUD가 숨겨지고 시간 배율을 바꾸지 않는다.
  - 스테이지 씬을 내린 뒤 시간 배율이 복원되고 HUD가 숨겨진다.
- 테스트 사이에 GameManager가 다음 테스트로 상태를 넘기지 않도록 한다(스테이지 연결 해제·HUD 숨김·시간 복원 확인).
- 지금 재시작 테스트는 이전 `StageFlowController`가 파괴되기를 기다려 다시 불러오기를 알아챈다. GameManager는 파괴되지 않으므로, 새 스테이지가 등록된 것을 알 수 있는 값(예: 등록할 때마다 오르는 번호)을 공개하고 테스트는 그것을 기다린다.
- 마지막에 EditMode·PlayMode 전체 실행. 기준: 마지막 기록 EditMode 104/104, PlayMode 369/369. 알려진 간헐 실패(`CutsceneTests`의 VirtualKeyboard 검사)는 따로 표시한다.

## 문서

- `Docs/PrototypeGuide.md`, `Docs/PrototypeValidation.md`에 2026-11-04 GameManager 절.
- 로컬 `AGENTS.md`의 코드 구조 표(StageFlowController 행)와 코어루프 절 갱신(Git에 추가하지 않음).

## 설계자가 정한 것 (검토 후 반영)

사용자가 정한 것은 '전부 옮기기'와 GameManager가 씬 전환에도 남는다는 것이다. 아래는 사용자가 아니라 설계자(Claude)가 정했다.
- 씬에 Inspector 연결만 담는 `StageLayout`을 남김(남는 물체는 씬 물체 연결을 들고 갈 수 없음).
- `Resources` 프리팹 자동 생성, 등록 시점 = `StageLayout.Start`.
- HUD 구역 나눔과 보스 체력바를 스테이지와 무관한 구역으로 두는 것.
- 진행 중 설정 오류 = 등록 해제, 등록 시점에 시간 값 기록, 중복 등록 무시, 새 등록 시 HUD 초기화.
- 재시작 감지용 등록 번호.

## builder에게 맡기는 것

- 일반 C# 객체의 이름과 나누는 경계.
- GameManager 프리팹의 물체 구성, 테스트 정리 방식.
- `CutsceneRunner`가 GameManager를 찾는 세부 방식.
