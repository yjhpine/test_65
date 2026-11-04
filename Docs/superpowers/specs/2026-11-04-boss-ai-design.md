# 보스 AI 구조 설계 (FSM + 패턴 선택 트리)

작성 2026-11-04. 기준 커밋 `0c51b38a`(YJH). 보스 기획서는 아직 없다.

## 1. 의도

보스 몬스터를 만들기 전에 **패턴을 쉽게 추가·관리하고, 다음 보스에서도 그대로 쓰는 뼈대**를 먼저 만든다.
특정 보스 하나의 완성이 목표가 아니다. 기획서가 나오면 그 보스의 패턴 에셋과 선택 트리만 만들면 되게 한다.

사용자가 정한 방향:

- **FSM이 큰 행동 방식**을 정한다: 대기, 플레이어 쪽으로 이동, 패턴 준비, 패턴 실행, 회복 등.
- **BT(행동 트리)가 어떤 패턴을 수행할지** 정한다. 플레이어의 위치·상태를 보고 고른다.
- **패턴 선택 트리는 Inspector에서 고친다** (코드 수정 없이).
- 아래 추측을 사용자가 확인했다.
  - BT는 매 프레임이 아니라 **고를 때만** 평가한다.
  - 고른 패턴은 FSM이 끝까지 실행한다. 중간에는 경직·사망만 끼어든다.
  - 패턴은 "설정 데이터 + 개체별 실행 상태"를 묶은 독립 단위다.
  - 기존 Unit·FsmDefinition 위에 올리고, 플레이어 상태는 읽기 전용 계약으로만 읽는다.

## 2. 목표 / 비목표 / 반목표

**목표**

- 새 패턴 = 패턴 클래스 1개(필요할 때만) + 패턴 에셋 1개 + 선택 트리에 잎 노드 1개. FSM·공통 코드 수정 없음.
- 새 보스 = 보스 FSM 에셋 + 선택 트리 에셋 + 패턴 에셋들 + 프리팹. 공통 코드 수정 없음.
- 선택 트리를 Inspector에서 추가·삭제·순서 변경·조건 수치 편집. 잘못된 트리는 Inspector와 실행 시 오류로 알린다.
- Play Mode에서 현재 FSM 상태, 실행 중 패턴, **마지막 선택 경로**(어떤 조건을 거쳐 그 패턴이 골라졌는지)를 볼 수 있다.

**비목표 (이번에 하지 않음)**

- 특정 보스의 실제 패턴·수치·아트·연출(기획서 이후).
- 보스 체력 HUD, 보스 등장 컷신, 보스 전용 스테이지·배치. 씬 배치는 사용자 요청 없이 바꾸지 않는다.
- 노드 그래프 편집기(창 형태). Inspector의 중첩 목록으로 편집한다.
- 경로 탐색, 비행 보스 이동(구조상 자리만 둔다), 유닛 풀링·부활.

**반목표 (기술적으로 돌아가도 실패인 것)**

- 패턴을 추가할 때마다 FSM이나 공통 선택 코드에 `switch`/`if`가 늘어나는 구조.
- 공유 에셋(FSM·트리·패턴 정의)에 실행 중 상태(쿨타임, 타이머, 이력)를 기록하는 것. 같은 에셋을 쓰는 보스 두 마리가 서로 영향을 주면 실패.
- 보스가 `PlayerUnit`, `CharacterMotor2D` 같은 구체 클래스를 직접 참조하는 것.
- Animation Event로 피해를 주는 것 (프로젝트 규칙).

## 3. 제약 (기존 프로젝트 규칙)

- Unity 6000.3.23f1, 기존 어셈블리 `ActionPlatformer.Runtime / Editor / EditModeTests / PlayModeTests`. 새 패키지 설치 없음.
- `Unit`은 정의 검증 → FSM 생성(Start) → `Time.deltaTime`으로 Tick → 비활성화 시 Exit. `FsmDefinition.CreateInitialState`는 호출마다 새 상태 그래프를 만든다. `FsmRuntime.Pause()`(컷신)도 그대로 따른다.
- `IFsmState.Tick`은 null/자기 자신이면 유지, 다른 상태면 한 Tick에 한 번 전환.
- 피해·끊는 타격: `UnitHealth.ApplyDamage(amount, source, bypassGuard, interruptsAttack)`, `DamageVersion`, `InterruptVersion`, `GuardBlockVersion`, 선택적 `IUnitDamageGuard`. 플레이어의 끊는 타격은 띄우기·내려찍기·출현·충격파.
- 이동: `GroundMovement2D.MoveTo/Stop/IsBlocked/HasArrived/IsGrounded/IsForcedMoving`, 강제 이동 우선.
- 플레이어 읽기 계약: `ICharacterMotionState`(Velocity, IsGrounded), `IPlayerActionState`(IsUnderground, AttackPhase, AttackPresentation, Facing …), `IUnitRepositionState.RepositionVersion`(글리치 성공 횟수), `IUnitViewSource.View`(화면 안 판정).
- 몸체 레이어 Ignore Raycast(2), 지형 Default(1). 대상 조회는 `UnitPhysics2D.Filter / IsAvailable / Body / HasSight`.
- 외형은 상태를 읽기만 하고(`LateUpdate`), 동작이 바뀔 때만 `Animator.Play`로 재시작, 진행은 `ActionTime` Float. Root Motion 끔.
- 커밋은 사용자 요청 시에만.
- 기존 코드 수정은 최소로 한다. 허용: `PlayerUnit`의 `ICharacterMotionState` 전달 구현, `UnitEditor`의 Play Mode 보스 디버그 표시 추가, 편집기 파일 추가. 공통 `UnitHealth`·`FsmRuntime`·기존 적 코드는 바꾸지 않는다.
- 잘못된 FSM·선택 트리 에셋은 기존 FSM 정의처럼 `TryValidate`로 검사하고, 실행 시 `CreateInitialState`에서 예외를 던진다(FSM이 멈추고 오류가 남는다).

## 4. 구조

네임스페이스 `ActionPlatformer.Units.Bosses`, 위치 `Assets/_Game/Runtime/Units/Bosses/`.

```text
Unit (기존) ── UnitDefinition.Fsm ──▶ BossFsmDefinition (에셋, 보스 공통 FSM 설정)
                                        ├─ selector: BossPatternSelector (에셋, Inspector 트리)
                                        │    └─ root: BossSelectorNode (SerializeReference 트리)
                                        │         └─ … PatternLeaf → BossPatternDefinition (에셋)
                                        └─ phases: 체력 구간 목록

실행 시 (보스 개체마다 새로 생성)
BossBrain (일반 C#)  ← BossFsmDefinition.CreateInitialState가 생성
  ├─ FSM 상태들: Dormant, Approach, Telegraph, Execute, Recover, Staggered, PhaseShift, Death
  ├─ BossBlackboard: 매 Tick 갱신하는 판단 재료 + 쿨타임·이력·난수
  ├─ IBossMover: 이동 어댑터 (지상: GroundMovement2D)
  └─ BossPatternRun: 지금 실행 중인 패턴의 개체별 실행 객체

보스 루트 컴포넌트: Unit, UnitHealth, BossCombat2D(신규), GroundMovement2D, Rigidbody2D, Collider2D
Visual 자식: SpriteRenderer, Animator, BossVisual2D(신규), UnitDamageFlash(기존)
```

### 4.1 FSM (큰 행동 방식) — `BossFsmDefinition : FsmDefinition`

모든 보스가 같은 상태 집합을 쓴다. 보스마다 다른 것은 설정 값과 선택 트리·패턴 에셋뿐이다.

| 상태 | 하는 일 | 나가는 조건 |
|---|---|---|
| Dormant | 플레이어를 인식하기 전 대기. 인식 = 거리·높이·시야·플레이어 화면 안 | 인식하면 Approach 또는 바로 선택 |
| Approach | 선택 주기(기본 0.2초)마다 선택 트리를 평가. 고른 패턴이 없으면 플레이어 쪽으로 이동(선호 거리에서 멈춤) | 패턴이 골라지면 Telegraph. 인식을 잃은 채 일정 시간 지나면 Dormant |
| Telegraph | 패턴의 예고 단계. 방향은 여기 들어올 때 정하고 고정 | 패턴이 "예고 끝"을 알리면 Execute |
| Execute | 패턴의 실행 단계(공격 판정·이동) | 패턴이 "실행 끝"을 알리면 Recover |
| Recover | 패턴 정의의 회복 시간만큼 대기 | 시간이 지나면 Approach |
| Staggered | 끊는 타격에 맞아 경직. 실행 중 패턴은 취소(재개 없음) | 경직 시간 + 강제 이동 끝 + 착지 → Approach |
| PhaseShift | 체력 구간이 바뀐 뒤 **현재 패턴(회복 포함)이나 경직이 끝나면** 진입. 설정에 따라 무적. 경직되지 않음 | 전환 시간 후 Approach |
| Death | 사망. 패턴 취소, 판정 끔 | 사망 지연 후 GameObject 비활성화 |

우선순위(매 Tick): 사망 → 경직 → 현재 상태 진행. 구간 전환은 끼어들지 않는다(사용자가 확인한 "패턴은 끝까지, 경직·사망만 끼어듦"을 지킨다).

- 체력 구간은 매 Tick 체력 비율로 계산해 "대기 중인 구간"으로 기억한다. Dormant/Approach에 있거나 Recover·Staggered를 마치는 순간, 대기 중인 구간이 현재보다 높으면 PhaseShift로 간다.
- 한 번의 큰 피해로 경계를 여러 개 넘으면 가장 깊은 구간으로 한 번만 전환한다. 경계에서 피해를 자르지 않는다(공통 `UnitHealth`를 바꾸지 않는다). 치명타면 사망이 우선이다.

- **경직은 끊는 타격(`InterruptVersion` 증가)만** 일으킨다. 일반 베기는 어떤 상태도 끊지 않는다.
- 끊을 수 있는지는 상태·패턴이 정한다. Dormant/Approach/Recover는 항상 끊을 수 있다. Telegraph/Execute는 패턴 정의의 `interruptibleDuringTelegraph / interruptibleDuringExecute`(기본 true / false)를 따른다.
- 경직 사이에 최소 간격(`staggerImmunity`)을 둬서 연속 경직으로 묶이지 않게 한다.
- 띄우기·내려찍기에 실제로 날아갈지는 기존 `UnitDefinition.AllowForcedMovement`가 정한다(보스 기본 false 권장).
- 패턴 실행은 FSM 상태가 패턴 실행 객체의 메서드를 부르는 방식이다. 패턴이 FSM 상태를 직접 바꾸지 않는다.

### 4.2 판단 재료 — `BossBlackboard`

보스마다 하나 있는 일반 C# 객체. FSM이 매 Tick 처음에 갱신하고, 선택 트리와 패턴이 읽는다.

- 대상: 플레이어 `UnitHealth`(Kind Player, 살아 있고 활성). `Target Mask`로 찾는다.
  - **기존 적과 다른 점**: 잠복 중인 플레이어는 `CanReceiveDamage`가 false지만 보스는 대상을 놓지 않는다(추적은 `IsAlive`·활성만 확인). 그래야 "잠복 중" 조건과 출현을 노리는 패턴을 만들 수 있다. 실제 피해는 지금처럼 `CanReceiveDamage`일 때만 들어간다.
  - 잠복 중 플레이어 위치는 몸체가 아니라 `IPlayerActionState.GroundMarkerPosition`(논리 위치)을 쓴다. 잠복만으로는 인식을 잃지 않는다.
- 위치: 수평 거리(부호 포함, 보스 기준 앞/뒤), 수직 차, 직선 거리, 플레이어가 보스 앞쪽인지.
- 플레이어 상태: 공중/지상, 잠복 중, 공격 중(단계), 최근 글리치 후 경과 시간(`RepositionVersion` 변화 감지), 보스를 화면에 보고 있는지.
- 보스 상태: 체력 비율, 현재 구간 번호, 마지막 패턴, 최근 패턴 이력(기본 4개), 패턴별 쿨타임 종료 시각, 같은 패턴 연속 횟수.
- 난수: 개체별 `System.Random`(시드 지정 가능 — 테스트용).

플레이어 정보는 `ICharacterMotionState`/`IPlayerActionState`/`IUnitRepositionState`/`IUnitViewSource` 인터페이스로만 얻는다. 없으면 해당 값은 "알 수 없음"으로 두고, 그 조건 노드는 실패로 처리한다.

- 지금 `PlayerUnit`은 `ICharacterMotionState`를 구현하지 않는다(구체 `Motor`로만 공개). `PlayerUnit`에 `Motor`로 넘겨주는 구현을 추가한다. 동작 변화는 없다.
- 시간: 쿨타임·이력·경과 시간은 FSM Tick의 `deltaTime`을 쌓은 **보스 자체 시계**로 잰다. 컷신 등으로 FSM이 멈추면 시계도 멈춘다.
- 쿨타임은 **예고 시작 때** 시작한다. 예고 중 취소돼도 쿨타임은 그대로 센다(방패병 예고 간격 규칙과 같음).

### 4.3 패턴 선택 트리 — `BossPatternSelector` (Inspector 편집)

- ScriptableObject 하나에 트리 전체를 `[SerializeReference]` 중첩 노드로 저장한다. 노드마다 별도 에셋을 만들지 않는다.
- **선택 전용 BT**: 한 번 평가해 패턴 하나(또는 없음)를 돌려준다. 평가는 즉시 끝나며 `Running` 상태가 없다. 실행은 FSM 몫이다.
- 노드 종류(모두 공통, 보스와 무관):
  - 묶음: `Selector`(위에서부터 처음 성공한 것), `Sequence`(조건들을 모두 통과하면 마지막 자식), `WeightedRandom`(가능한 자식 중 가중치 추첨).
  - 조건: 거리(수평·수직 범위), 플레이어 위치(앞/뒤/위/아래), 플레이어 상태(공중·지상·잠복·공격 중·최근 글리치 n초 이내), 체력 구간, 확률, 최근 n번 안에 사용한 패턴 제외.
  - 장식: `Inverter`(조건 반전).
  - 잎: `Pattern` — 패턴 에셋 참조. 패턴의 쿨타임과 패턴 자체의 시작 가능 조건(`CanStart`)을 확인한다.
- 새 조건이 필요하면 `BossSelectorNode` 파생 클래스 하나를 추가한다. Inspector의 추가 메뉴에 자동으로 뜬다.
- 편집기(`ActionPlatformer.Editor`):
  - 노드 필드 옆 "노드 추가/교체" 메뉴: `TypeCache`로 노드 파생 타입을 찾아 고른다(Unity 6에는 SerializeReference 타입 선택 UI가 기본으로 없다).
  - 각 노드 머리글에 한 줄 요약(예: `거리 0~3.5`, `패턴: 근접 베기`).
  - 검증 결과(빈 묶음, 패턴 없는 잎, 범위 역전, 순환 참조 불가 확인)를 HelpBox로 표시.
- 클래스 이름을 바꿀 때는 `[MovedFrom]`로 직렬화 연결을 지킨다.
- 평가할 때 **선택 경로**(통과/실패한 노드 요약)를 기록해 Play Mode 디버그에 보여 준다.

### 4.4 패턴 — `BossPatternDefinition` (에셋) + `BossPatternRun` (개체별)

- `BossPatternDefinition`은 추상 ScriptableObject. 공통 필드: 표시 이름, 쿨타임, 회복 시간, 끊김 허용(예고/실행), 애니메이터 상태 이름(예고/실행/회복), 예고 표시 모양.
- 파생 패턴 타입은 자기 수치(거리, 속도, 피해, 판정 크기 등)를 갖고 `CreateRun(BossContext)`로 **개체별 실행 객체**를 새로 만든다.
- `BossPatternRun`의 수명: `CanStart`(선택 시 잎이 확인) → `BeginTelegraph` → `TickTelegraph`(끝나면 true) → `BeginExecute` → `TickExecute`(끝나면 true) → `End` / `Cancel`.
- 패턴이 쓸 수 있는 것은 `BossContext`(보스 루트, 이동 어댑터, `BossCombat2D`, 블랙보드, 정의)뿐이다.
- 처음 만들 공통 패턴 타입(보스와 무관하게 재사용, 수치는 에셋에서):
  - `MeleeAreaPattern`: 예고 → 보스 앞 상자 영역 한 번 판정 → 끝. 여러 번 휘두르기는 판정 횟수·간격 필드로.
  - `ChargePattern`: 예고 때 방향 고정 → 정해진 거리·속도로 돌진, 돌진당 접촉 피해 1회, 벽·발판 끝에서 멈춤.
  - `RepositionPattern`: 플레이어 기준 목표 거리로 물러서거나 다가감(공격 없음). "이동도 패턴으로 고를 수 있다"는 예시.

### 4.5 전투·이동 보조

- `BossCombat2D` (보스 루트 MonoBehaviour):
  - 대상 찾기, 상자·원 영역 판정(시야 확인, 한 번의 판정에서 대상별 1회), 접촉 피해(돌진당 1회).
  - `IUnitDamageGuard` 구현 — 실행 중 패턴이 막기 규칙을 가지면 그것에 맡긴다(기본은 막지 않음). 방패형 패턴을 나중에 붙일 자리.
  - 외형용 버전 번호(공격 발생, 막기).
- `IBossMover` (일반 C# 어댑터): `MoveTowards(x, speed)`, `Stop()`, `IsBlocked`, `HasArrived`, `IsGrounded`, `IsForcedMoving`, `Position`. 이번에는 `GroundMovement2D` 래퍼만 만든다. 비행 보스는 같은 인터페이스로 나중에 추가.

### 4.6 외형 — `IBossActionState` + `BossVisual2D`

- `IBossActionState`: FSM 상태, 현재 패턴 정의, 단계 진행률(0~1), 바라보는 방향, 상태 변경 버전, 예고 표시 모양.
- `BossVisual2D`: `LateUpdate`에서 읽고, 상태·패턴이 바뀔 때만 패턴 정의의 애니메이터 상태 이름으로 `Animator.Play`, 진행은 `ActionTime`. 이동 중 Run / 대기 Idle / 경직 Hit / 사망 Death 같은 공통 상태 이름은 FSM 에셋에서 지정.
- 예고 표시: 패턴이 공개한 모양(상자·선)을 바닥 선 또는 반투명 상자로 그린다. 방패병 예고선과 같은 방식.
- 피격 밝기·떨림은 기존 `UnitDamageFlash`를 그대로 붙인다.

### 4.7 스테이지 연결

- 보스는 일반 적처럼 encounter의 적 루트 아래 `UnitHealth`로 집계된다. `StageFlowController`는 수정하지 않는다.
- 이번에는 어느 씬에도 배치하지 않는다. 테스트는 테스트 코드가 직접 만든 장면에서 한다.

## 5. 검증용 테스트 보스

- `Data/Bosses/TestBoss/`에 정의·FSM·선택 트리·패턴 3종 에셋, `Prefabs/Bosses/TestBoss.prefab`(임시 외형: 기존 스프라이트를 크게 쓴 상자 수준).
- 테스트:
  - **EditMode**: 선택 트리 평가(조건별 통과/실패, Selector 우선순위, WeightedRandom 시드 고정 결과, 쿨타임·최근 사용 제외, 대상 없음), 검증 오류, `SerializeReference` 트리 직렬화 왕복.
  - **PlayMode**: Dormant → Approach → Telegraph → Execute → Recover 흐름, 거리에 따라 다른 패턴 선택, 끊는 타격 경직(허용/불허 단계), 일반 타격 무시, 구간 전환, 사망 우선, 같은 에셋을 쓰는 보스 2마리 독립, 비활성화 후 재활성화 시 초기 상태, FSM 일시정지 존중.
- 기존 전체 테스트(EditMode 48, PlayMode 318)가 그대로 통과해야 한다(알려진 간헐 실패 `CutsceneTests.VirtualKeyboard…` 제외).

## 6. 빌더에게 맡기는 것

- 파일 분할, 내부 이름, 편집기 그리기 세부(접기, 색).
- 공통 패턴 3종의 기본 수치(테스트가 통과하는 범위에서).
- 인식·선호 거리 등 FSM 기본값(문서에 적고 에셋에서 조정 가능하게).

## 7. Claude가 정한 것 (사용자 확인 필요)

- 선택 트리는 `Running` 없는 선택 전용 BT로 단순화.
- 노드는 한 에셋 안의 `SerializeReference` 중첩. 그래프 창은 만들지 않음.
- 패턴 정의는 패턴마다 에셋 하나(여러 보스가 같은 패턴 에셋을 공유 가능).
- 경직은 끊는 타격만, 예고 중 기본 허용·실행 중 기본 불허, 경직 면역 간격 있음.
- 체력 구간(페이즈)과 구간 전환 상태를 공통 FSM에 포함. 구간 전환은 실행 중 패턴을 끊지 않고 끝난 뒤에 한다. 경계에서 피해를 자르지 않는다.
- 잠복 중인 플레이어도 보스는 대상으로 유지하고, 위치는 지면 표시 위치로 잰다.
- `PlayerUnit`에 `ICharacterMotionState` 전달 구현 추가.
- 쿨타임은 예고 시작부터, 보스 자체 시계(FSM 정지 시 멈춤)로 잰다.
- 이번에는 지상 이동만, 씬 배치·보스 체력 HUD 없음, 테스트 보스는 임시 외형.
