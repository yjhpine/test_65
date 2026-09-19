# 이동·점프 기초 — Movement Lab

현재 범위는 **이동 가감속, 가변 점프, 코요테 타임, 점프 입력 버퍼**입니다.
유닛 공통 기반은 **정의 데이터 연결과 선택적 FSM 실행**까지 구현했습니다. 구체적인 몬스터·NPC 행동과 전환은 C#으로 확장합니다.
순찰 적 프리팹은 대기·왕복 이동과 추적·공격·피격·사망 상태를 사용합니다.

## 실행과 조작
`Assets/_Game/Scenes/MovementLab.unity`를 열고 Play를 누릅니다.

- 좌우 이동: A/D 또는 방향키 / 게임패드 왼쪽 스틱.
- 점프: Space / 게임패드 아래 버튼.
- 점프를 짧게 누르면 낮게, 길게 누르면 높게 뜁니다.
- 발판을 벗어난 뒤 0.1초 안에는 점프할 수 있습니다.
- 착지 전 0.13초 안에 누른 점프는 착지 시 실행됩니다.

씬에는 픽셀아트 플레이어, 단색 바닥, 경계 벽, 점프 확인용 발판 3개와 고정 카메라가 있습니다. 현재 저장된 MovementLab에는 PatrolEnemy 인스턴스 1개도 배치되어 있습니다.
플레이어 공격 입력, 대시·체크포인트·재시작·일시정지·HUD·효과음·연출·건물 배경과 와이어 이동은 없습니다. 적의 기본 근접 공격과 유닛 피해 처리는 구현되어 있습니다.

## 조절 위치
`Assets/_Game/Data/PlayerTuning.asset`에서 이동 속도, 지상 가속·감속, 공중 가속, 점프 높이, 상승·낙하 중력, 점프 해제 배율, 코요테 타임, 입력 버퍼를 조절합니다.
초깃값은 이전 프로토타입의 이동·점프 설정을 유지했습니다.
Player 프리팹의 PlayerUnit에서 Definition, Tuning, Ground Mask, Visual 참조를 연결합니다. Definition은 `Assets/_Game/Data/Units/PlayerDefinition.asset`이며 ID는 `player`, Kind는 `Player`, FSM은 비어 있습니다. 이동 수치는 별도의 PlayerTuning에서 관리합니다.

`Assets/_Game/Data/PrototypeControls.inputactions`에는 Player 맵의 Move / Jump만 있습니다.
기본 `Assets/Settings/InputSystem_Actions.inputactions`와 프로젝트 전역 입력 등록도 보존했습니다. 플레이어 루트의 Unity 기본 `PlayerInput`에 PrototypeControls를 연결하고 Default Action Map을 `Player`로 설정했습니다.
`PlayerInput`이 액션 활성화와 기기 연결, 싱글 플레이에서 키보드·게임패드 자동 전환을 관리합니다. Behavior는 `Invoke C Sharp Events`이며 이벤트 연결 없이 `PlayerInputReader`가 해당 컴포넌트의 액션 값을 읽습니다. Reader는 PlayerUnit이 생성하는 일반 C# 객체이며 프리팹에 부착하지 않습니다. 액션을 별도로 복제하거나 활성화하지 않습니다.
입력을 끄고 켤 때는 `PlayerInput.DeactivateInput()` / `ActivateInput()`을 사용합니다. PlayerInput 컴포넌트나 Player 액션 맵이 비활성화된 동안 Reader는 빈 명령을 반환합니다. 물리 정지나 점프 버퍼 초기화는 별도 처리입니다.
`Assets/_Game/Prefabs/Player.prefab`은 루트에서 물리 몸체와 입력·컨트롤러·모터를 관리하고, 바로 아래 `Visual` 자식에서 `SpriteRenderer`와 `Animator`를 관리합니다.

## 플레이어 외형과 애니메이션
Pixel Frog의 [Pixel Adventure](https://pixelfrog-assets.itch.io/pixel-adventure-1)에 포함된 **Virtual Guy**를 적용했습니다. CC0 에셋이며 출처와 라이선스는 `Docs/ThirdPartyNotices.md`에 기록했습니다.

원본 이미지는 `Assets/_Game/Art/Characters/VirtualGuy`, 애니메이션은 `Assets/_Game/Animations/Player`에 있습니다. 32×32 프레임, 16 PPU, Point 필터, 무압축이며, 대기 11프레임과 달리기 12프레임을 20 FPS로 반복합니다. 점프·낙하는 각각 한 프레임입니다.

Player 프리팹의 `Visual`에는 SpriteRenderer, Animator, PlayerVisual이 있습니다. PlayerUnit이 Awake에서 PlayerVisual에 `ICharacterMotionState`를 전달합니다. PlayerVisual은 이 읽기 전용 인터페이스의 속도와 접지만 읽어 `Moving`, `Grounded`, `Rising` 파라미터를 갱신하고 이동 방향에 따라 SpriteRenderer의 Flip X를 변경합니다. 구체적인 모터나 PlayerUnit 참조는 보관하지 않습니다. 정지하면 마지막 방향을 유지합니다. Animator의 Apply Root Motion은 꺼져 있습니다.

다른 캐릭터로 교체할 때는 `Visual`의 Sprite와 Animator Controller를 교체하고 위 세 Bool 파라미터를 유지합니다. 현재 `Visual`의 위치는 `(0, -0.8, 0)`, 스케일은 `(1, 1, 1)`, Color는 흰색이며 스프라이트 피벗은 하단 중앙입니다.
루트의 Transform과 Rigidbody2D는 이동용으로 유지하고, 스프라이트 프레임이나 외형 변형 애니메이션은 `Visual`에 바인딩합니다. 충돌 크기는 루트의 CapsuleCollider2D에서 별도로 조절합니다.

외형과 애니메이션은 저장된 에셋에서 직접 수정합니다. 초기 에셋 적용 및 테스트 씬 재생성 도구는 정리했습니다.

## 구조
- `PlayerInput` (Unity 기본 컴포넌트): 입력 액션과 기기 연결·전환 관리.
- `Unit` (직접 부착·상속 가능한 컴포넌트): 정의 데이터 검증·공개, 개체별 FSM 생성·시작·갱신·종료. 이동·점프·입력·모터 기능은 없음.
- `UnitDefinition` (ScriptableObject): ID·표시 이름·분류(Player/Monster/Npc)·기본 최대 체력(MaxHealth)·기본 공격력(AttackPower)·선택적 FsmDefinition을 보관하는 공유 설정.
- `FsmDefinition` (추상 ScriptableObject): C# 상태 객체를 생성하는 설정·팩토리. 호출마다 개체 전용 상태 그래프를 생성.
- `FsmRuntime / IFsmState` (일반 C# 객체): 현재 상태의 Enter·Tick·Exit와 상태 전환 실행. 타이머·타깃은 개체별 상태 객체가 소유.
- `GroundMovement2D` (공용 컴포넌트): 적·NPC의 목적지 이동·정지와 벽·발판 끝 감지. 물리는 자체 FixedUpdate에서 실행하며 순찰 판단이나 FSM을 요구하지 않음.
- `GroundUnitFsmDefinition`: 순찰·추적·공격·피격·사망 상태와 전환 수치 설정. 기존 PatrolFsmDefinition의 스크립트 GUID를 유지해 연결을 보존.
- `UnitHealth`: 정의의 최대 체력을 읽어 개체별 현재 체력·피해 횟수 관리. ApplyDamage로 피해 적용.
- `UnitCombat2D`: 종류·레이어·거리·시야에 따른 타깃 탐색과 공격 적중 시 피해 적용. 공격 시점과 주기는 FSM이 결정.
- `UnitVisual2D` (공용 컴포넌트): Rigidbody2D 속도로 Idle/Run·좌우 반전 처리. 선택적 State Source의 현재 상태가 IUnitAnimationState를 구현하면 Attack/Hit/Death도 재생. 구체적인 FSM 클래스·이동 컴포넌트는 참조하지 않음.
- `IUnitAnimationState`: 외형이 읽는 애니메이션 종류·반복 번호·경과 시간·기준 시간·바라볼 방향 계약. FSM은 Animator나 클립을 참조하지 않음.
- `PlayerUnit : Unit` (컴포넌트): PlayerInputReader 생성·보관, 플레이어 입력과 이동·점프 판단, 코요테 타임·점프 입력 버퍼, 모터 실행, 비활성화 시 명령 초기화 담당. 기존 PlayerController를 대체.
- `PlayerInputReader` (일반 C# 객체): PlayerInput의 이동·점프 액션을 프레임별 명령으로 변환.
- `CharacterMotor2D` (일반 C# 객체): 생성자로 전달받은 Rigidbody2D, Collider2D, PlayerTuning, Ground Mask로 접지·속도·중력·점프 처리. GetComponent나 Unity 생명주기 함수가 없으며, 물리 객체를 생성·파괴하지 않음.
- `PlayerVisual`: ICharacterMotionState의 읽기 전용 상태에 따른 스프라이트 애니메이션과 좌우 방향.
- `PlayerTuning`: 수정 가능한 설정 데이터.
- `MovementMath / InputBuffer`: 이동 계산과 한 번만 소비하는 입력 버퍼.

실행 중 상태는 개체별 PlayerUnit·모터·FSM 상태 객체에 저장하고 설정 에셋에는 저장하지 않습니다.
플레이어 루트의 컴포넌트는 Transform, Rigidbody2D, CapsuleCollider2D, PlayerInput, PlayerUnit, UnitHealth입니다. Unit과 모터는 별도로 부착하지 않으며, 외형 컴포넌트는 Visual 자식에 유지합니다.
Unit은 `Assets/_Game/Runtime/Units/Unit.cs`, PlayerUnit은 `Assets/_Game/Runtime/Player/PlayerUnit.cs`에 있습니다. PlayerUnit은 기존 Controller의 스크립트 GUID를 이어받아 프리팹 연결을 유지합니다.
Unit의 Awake가 Definition을 검사한 뒤 PlayerUnit의 `TryInitializeUnit`을 호출해 모터와 입력 Reader를 생성하고 외형을 연결합니다. `OnUnitUpdate`에서 입력을 수집하고 PlayerUnit의 FixedUpdate에서 모터를 실행합니다. Unit 자체는 이동 관련 객체를 요구하지 않으며, PlayerUnit은 PlayerInput·Rigidbody2D·CapsuleCollider2D를 필수로 요구합니다.
빌드 씬에는 MovementLab만 등록되어 있습니다.

## 유닛 데이터와 FSM 확장

UnitDefinition의 `Max Health`와 `Attack Power`에서 정수 기본 수치를 조절합니다. 최대 체력은 1 이상, 공격력은 0 이상이며 잘못된 값은 TryValidate에서 거부합니다. 새 정의의 기본값은 100/10, 현재 PlayerDefinition은 100/10, PatrolEnemyDefinition은 30/5입니다. UnitHealth는 Awake에서 최대 체력을 복사하고 현재 체력을 개체별로 관리합니다. UnitCombat2D는 적중 시 정의의 공격력을 읽어 피해를 적용합니다. 회복·부활 API는 없으며 재활성화만으로 체력이 회복되지 않습니다.

1. Project 창의 `Create > Action Platformer > Units > Unit Definition`에서 정의를 만듭니다. Unit Id·Display Name·Kind를 설정합니다. ID는 종류별로 고유하게 지정하며, 같은 종류의 개체들은 같은 에셋을 공유합니다. 전역 ID 중복 검사는 아직 없습니다.
2. 몬스터·NPC는 Unit을 직접 부착하고 Definition을 연결합니다. 별도 기능이 필요한 플레이어는 기존 PlayerUnit : Unit 상속 구조를 사용합니다. PlayerUnit과 Unit을 한 루트에 함께 부착하지 않습니다.
3. FSM이 필요하면 `FsmDefinition`을 상속한 C# 클래스에 `CreateAssetMenu`를 지정하고 `CreateInitialState(Unit owner)`를 구현합니다. 필요한 기능 컴포넌트는 owner에서 조회하여 상태 생성자에 전달합니다. 상태와 다음 상태 객체는 호출마다 새로 만들고 설정 에셋에 저장하지 않습니다.
4. 상태는 `IFsmState`를 구현합니다. `Enter()`는 진입과 타이머 초기화, `Tick(float deltaTime)`은 갱신과 다음 상태 결정, `Exit()`는 이벤트 구독 해제 등 종료 처리를 담당합니다. Tick이 `null` 또는 현재 객체를 반환하면 유지하고, 다른 상태 객체를 반환하면 현재 Exit → 다음 Enter 순서로 전환합니다. 한 번의 Tick에서 최대 한 번 전환하며 다음 상태의 Tick은 다음 프레임에 실행합니다.
5. 구체적인 FSM 정의 에셋을 만들어 UnitDefinition의 Fsm에 연결합니다. 연결하지 않으면 FSM 없이 동작합니다. PlayerDefinition에는 FSM이 없고, PatrolEnemyDefinition에는 PatrolEnemyFsm이 연결되어 있습니다.

FSM은 모든 Awake 초기화가 끝난 뒤 Unit의 Start에서 생성·시작하고 Update에서 `Time.deltaTime`으로 실행합니다. Unit/GameObject 비활성화나 활성 개체 파괴 시 현재 상태를 종료합니다. 재활성화하면 같은 런타임의 초기 상태부터 다시 진입하므로, 상태의 Enter에서 타이머 등 실행 상태를 초기화해야 합니다. 일시정지 후 이어서 실행하는 기능은 없습니다. FSM 콜백 예외는 호출자에게 전달되고 런타임은 정지합니다.

파생 Unit은 `Awake / Start / OnEnable / Update / OnDisable`을 새로 선언하지 않고 `TryInitializeUnit / OnUnitUpdate / OnUnitDisabled` 훅을 사용합니다. 공통 생명주기 호출을 빠뜨리지 않도록 Unit이 훅 호출을 관리합니다. 물리는 필요한 기능에서 FixedUpdate를 처리합니다. Definition은 실행 전에 연결해야 하며, 실행 도중 교체하는 API는 없습니다. 누락되거나 잘못된 데이터는 오류를 기록하고 해당 Unit을 비활성화합니다.

`TryInitializeUnit()`은 초기화 성공 시 true, 실패 시 false를 반환합니다. 실패하면 Unit이 비활성화되고 컴포넌트나 GameObject를 다시 켜도 실행을 차단합니다. 초기화는 같은 개체에서 재시도하지 않으므로 설정을 고친 뒤 새 개체를 생성하거나 Play Mode를 다시 시작해야 합니다. `OnUnitDisabled`는 초기화 실패 때도 실행될 수 있어 일부 참조가 아직 없는 상태에서도 안전해야 합니다.

## 적·NPC 공용 이동과 외형

이동하는 적·NPC의 루트에 `Unit + Rigidbody2D + Collider2D + GroundMovement2D`를 구성합니다. Collider2D는 Box 또는 Capsule처럼 해당 Rigidbody2D에 연결된 비트리거 몸체를 사용합니다. GroundMovement2D의 Body Collider에 지정하고, 비어 있으면 같은 오브젝트의 Collider2D를 찾습니다. UnitDefinition에서 Monster/Npc를 구분하며 이동 기능은 분류를 검사하지 않습니다.

- `MoveTo(destinationX, speed)`: 지정한 X 위치로 수평 이동합니다. 목적지는 유한한 값, 속도는 0보다 큰 유한한 값이어야 합니다.
- `Stop()`: 이동 명령과 수평 속도를 지웁니다. 이동 컴포넌트 비활성화도 같은 처리를 하며 비활성 상태에서 받은 명령은 무시합니다. 재활성화 후 새 명령을 내려야 합니다.
- `Position / Velocity / HasArrived / IsBlocked`: 현재 위치·속도와 도착·막힘을 읽습니다. 막혔을 때 반전하거나 다른 목적지를 고르는 것은 FSM 또는 명령을 내린 코드의 책임입니다.

Environment Mask로 바닥·벽 레이어를 지정합니다. Stop At Ledges는 기본 true이며, false면 발판 끝에서도 계속 이동합니다. 벽 검사는 항상 수행합니다. Arrival Tolerance(기본 0.02)는 도착 오차, Ground Probe Distance(기본 0.3)는 발밑 검사 길이입니다. 이동은 수평 전용이며 중력은 Rigidbody2D가 처리합니다. 대상 추적·경로 탐색·점프·이동 발판 지원은 포함하지 않습니다.

Visual 자식에는 `SpriteRenderer + Animator + UnitVisual2D`를 둡니다. UnitVisual2D의 Body에 루트 Rigidbody2D를 연결하고 Moving Bool이 있는 Animator Controller를 지정합니다. Sprite Faces Right는 원본 스프라이트 방향에 맞춥니다. 정지 NPC는 Body를 비워 Idle로 표시할 수 있으며 Rigidbody2D·이동 컴포넌트·FSM을 추가할 필요가 없습니다.

FSM 없는 NPC도 별도 상호작용 코드 등에서 이동 컴포넌트의 초기화 이후 `MoveTo/Stop`을 호출할 수 있습니다. Unit을 비활성화하는 것만으로 임의의 외부 이동 명령이 자동 중단되지는 않으므로, 명령을 내린 코드가 종료 시 Stop을 호출하거나 이동 컴포넌트/GameObject를 비활성화해야 합니다. 순찰 FSM은 Exit에서 Stop을 호출합니다. 기존 PlayerUnit·플레이어 모터·PlayerVisual에는 이 공용 컴포넌트를 추가하지 않습니다.

## 순찰 적 프리팹

`Assets/_Game/Prefabs/Enemies/PatrolEnemy.prefab`을 씬의 바닥 위에 배치하면 대기 → 오른쪽 이동 → 대기 → 왼쪽 이동을 반복합니다. 바닥은 Default 레이어를 사용하며, 충돌체 높이는 1이므로 루트 중심을 바닥보다 약 0.6유닛 위에 놓습니다. 현재 MovementLab의 기존 인스턴스도 이 프리팹을 사용합니다.

| 구성 | 연결 및 역할 |
|---|---|
| `Unit` | `Data/Units/PatrolEnemyDefinition.asset`: ID `enemy.patrol`, 이름 `순찰병`, Kind `Monster` |
| `GroundUnitFsmDefinition` | `Data/Fsm/PatrolEnemyFsm.asset`: 대기 0.6초, 이동 속도 2유닛/초, 편도 거리 3유닛 |
| `GroundMovement2D` | 목적지 명령을 받고 FixedUpdate에서 Rigidbody2D 수평 속도 적용. 중력은 Rigidbody2D가 처리 |
| `Rigidbody2D / BoxCollider2D` | 동적 몸체, 회전 고정, 중력 배율 4, 충돌 크기 1×1 |
| `Visual` | SpriteRenderer·Animator·UnitVisual2D. Pixel Frog의 Kings and Pigs에 포함된 Pig(CC0) |

순찰 상태는 GroundUnitFsmDefinition 내부의 일반 C# `WaitState / WalkState`입니다. 오른쪽·왼쪽용 상태 객체와 타이머는 개체마다 새로 만들어 공유 에셋을 변경하지 않습니다. 각 이동 상태에 진입한 위치에서 해당 방향으로 3유닛을 이동하고, 도착하거나 벽·발판 끝을 감지하면 멈춰 기다린 뒤 반대로 걷습니다. 장애물로 일찍 돌아서면 왕복 구간도 달라질 수 있습니다. 타깃을 발견하면 추적·공격으로 전환합니다.

Unit 비활성화 시 이동 명령과 수평 속도를 정지하고, 재활성화 시 현재 위치에서 오른쪽 이동 전 대기부터 다시 시작합니다. 이동 컴포넌트는 벽을 Collider.Cast로, 앞쪽 발밑 지면을 Raycast로 검사합니다. Environment Mask는 Default(1), 적은 Ignore Raycast(2) 레이어로 자신과 플레이어를 지면으로 인식하지 않습니다. 평평한 정적 발판용 예제이며 점프·경로 탐색·이동 발판 대응은 없습니다.

속도·편도 거리·대기 시간은 PatrolEnemyFsm 에셋에서, ID·이름·분류·최대 체력·공격력은 PatrolEnemyDefinition에서 조절합니다. 적은 Unit을 직접 사용하며 `GetComponent<Unit>()`로 공통 정의·FSM에 접근합니다. 별도 적 전용 Unit 클래스를 만들지 않고 데이터와 기능 컴포넌트를 조합합니다.

```text
PatrolEnemy
├─ Unit (PatrolEnemyDefinition → PatrolEnemyFsm)
├─ Rigidbody2D + BoxCollider2D
├─ GroundMovement2D
├─ UnitHealth (Deactivate On Death 꺼짐)
├─ UnitCombat2D
└─ Visual
   ├─ SpriteRenderer
   ├─ Animator (PatrolEnemy.controller)
   └─ UnitVisual2D
```

Pig 이미지 원본은 `Assets/_Game/Art/Characters/Pig`, 애니메이션 에셋은 `Assets/_Game/Animations/Enemies/PatrolEnemy`에 있습니다. 34×28 프레임, 16 PPU, Point 필터, 무압축이며 Idle 11프레임·Run 6프레임을 원작 기준 10 FPS로 반복합니다. 출처와 라이선스는 `Docs/ThirdPartyNotices.md`에 기록했습니다.

UnitVisual2D는 실제 수평 속도를 읽어 Animator의 `Moving` Bool로 Idle/Run을 전환합니다. Root Motion은 꺼져 있고 클립은 SpriteRenderer의 Sprite만 바꿉니다. 원본 Pig는 왼쪽을 바라보므로 오른쪽으로 걸을 때 Flip X를 켭니다. 원본 방향이 다른 외형으로 교체하면 Sprite Faces Right를 조절합니다. Visual과 물리 루트의 스케일은 유지하며, 정지할 때는 마지막 방향을 유지합니다. GameObject 재활성화 시에는 원본 방향과 Idle로 초기화합니다.

Visual의 로컬 위치는 `(0, -0.5, 0)`, 스케일은 1입니다. 스프라이트 피벗은 프레임의 `(20/34, 0)`으로 실제 몸체 중심과 발바닥을 충돌체에 맞췄습니다. 외형 교체는 Visual의 Sprite·Animator Controller에서 수행합니다. 기존 플레이어의 모터·입력·애니메이션에는 의존하지 않습니다.

## 추적·공격·피격·사망 설정

Project에서 `Assets/_Game/Data/Fsm/PatrolEnemyFsm.asset`을 선택합니다. 새 설정은 `Create > Action Platformer > Units > FSM > Ground Unit`에서 만듭니다.

| Inspector 필드 | 현재 값 | 동작 |
|---|---|---|
| Target Kind | Player | 탐색 대상 종류. 진영 시스템은 아님 |
| Detection Range / Lose Target Range | 6 / 8 | 신규 탐색 거리 / 이미 찾은 타깃을 유지하는 거리 |
| Chase Speed / Target Refresh Interval | 3 / 0.15초 | 추적 속도 / 타깃 재검사 주기 |
| Attack Range | 1.2 | 유닛 루트 중심 간 거리로 공격 진입·적중 판정 |
| Attack Windup / Attack Interval | 0.2초 / 1초 | 선딜레이 / 공격 시작 사이의 최소 간격 |
| Hit Stun Duration | 0.25초 | 피해 후 행동 중단 시간. 추가 피해로 다시 시작 |
| Death Delay | 0.8초 | 사망 진입 후 GameObject 비활성화까지의 시간. 0이면 즉시 |

`Attack Range ≤ Detection Range ≤ Lose Target Range`, `Attack Windup ≤ Attack Interval`이어야 합니다. Inspector에서 잘못된 설정을 오류로 표시하고 FSM 생성 시에도 검증합니다.

전환 우선순위는 **사망 → 피격 → 공격/추적 → 순찰**입니다. 공격 중 피격되면 준비 중인 공격이 취소되고, 이미 시작한 공격의 재사용 대기시간은 유지합니다. 선딜레이 종료 시 원래 타깃의 활성 상태·체력·사거리·시야를 다시 검사해 한 번만 피해를 줍니다. 벽 너머 대상은 탐색하거나 공격하지 않습니다. 추적 이동에도 기존 벽·발판 끝 정지가 적용됩니다.

UnitCombat2D의 `Target Mask`는 Ignore Raycast(4), `Obstacle Mask`는 Default(1)입니다. 타깃에 Unit·UnitHealth와 비트리거 Collider2D가 필요합니다. 탐색은 재사용 버퍼의 최대 32개 Collider를 검사하며 대규모 군중용 공간 분할은 아직 없습니다. 이동만 필요한 NPC는 UnitCombat2D·UnitHealth 없이 같은 FSM의 순찰 부분만 사용할 수 있습니다.

적의 UnitHealth는 `Deactivate On Death`를 꺼서 FSM이 사망 지연을 처리합니다. 플레이어는 FSM이 없으므로 이 옵션을 켜서 체력 0에서 즉시 비활성화합니다. 사망한 개체를 다시 활성화해도 체력은 0이며 적은 다시 사망 상태로 들어갑니다. 풀링용 부활·체력 초기화 API는 별도 구현 대상입니다.

Play 중 Unit/PlayerUnit Inspector에서 `FSM State`, `Target`, `Health`를 확인할 수 있습니다. 적을 선택하고 `Test Damage`와 `Apply Test Damage`로 피격·사망을 시험합니다. 플레이어 공격 입력은 없습니다.

### FSM과 애니메이션 연결

PatrolEnemy의 Visual > UnitVisual2D > `State Source`에 루트 Unit을 연결했습니다. 다른 NPC에서 비워 두면 기존 속도 기반 Idle/Run만 사용합니다. Animator의 기존 Moving Bool은 유지하고 ActionTime Float과 Attack/Hit/Death 상태를 추가했습니다. 세 상태는 ActionTime을 Motion Time으로 사용하며 반복하지 않습니다.

- 순찰·추적: 실제 이동 시 Run, 정지 시 Idle.
- 공격: 매 공격 시작마다 Attack을 재시작하고 타깃 방향으로 반전. 선딜레이 종료 시 타격 프레임에 도달한 뒤 후속 동작을 재생하고, 다음 공격까지 Idle. 공격 상태가 계속 유지되어도 매 공격을 별도로 표시합니다.
- 피격: Attack을 중단하고 Hit. 추가 피해 시 Hit를 처음부터 재생하며 경직 시간이 끝나면 다음 상태를 따릅니다.
- 사망: Death를 사망 지연 시간에 맞춰 재생한 후 FSM이 비활성화합니다. Death Delay=0이면 표시할 대기 시간 없이 즉시 비활성화됩니다.

애니메이션은 IUnitAnimationState의 읽기 전용 정보를 LateUpdate에서 반영합니다. 피해·상태 전환·사망 시점은 FSM이 결정하며 Animation Event로 게임 규칙을 실행하지 않습니다. Unit 비활성화나 살아 있는 개체 재활성화 시 이전 공격·피격 표시를 지우고 Idle/Run으로 복귀합니다.

Pig의 Attack 5프레임, Hit 2프레임, Dead 4프레임을 기존 원본 압축파일에서 추가했습니다. 34×28, 16 PPU, Point, 무압축, 하단 피벗 (20/34, 0)을 유지합니다. UnitVisual2D의 `Attack Impact Normalized Time`은 0.4(세 번째 프레임), `Attack Recovery Duration`은 0.3초입니다. 다른 공격 그림으로 교체할 때 이 두 외형 설정을 조정합니다. 경직·사망 시간과 공격 선딜레이는 기존 FSM 에셋에서 변경합니다. Root Motion은 꺼져 있고 클립은 Sprite만 변경합니다.

## 검증
`Game > Prototype > Validate EditMode / Validate PlayMode`에서 실행합니다.
가상 키보드·게임패드 테스트는 검사 중에만 에디터 입력 포커스 제한을 해제하고, 종료 시 원래 설정을 복원합니다. 실제 플레이 입력 설정은 변경하지 않습니다.
현재 결과와 검증 범위는 `Docs/PrototypeValidation.md`에 기록합니다.
