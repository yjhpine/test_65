# 이동·점프 기초 — Movement Lab

현재 범위는 **이동 가감속, 가변 점프, 코요테 타임, 점프 입력 버퍼, 글리치 재배치와 플레이어 공격**입니다.
유닛 공통 기반은 **정의 데이터 연결과 선택적 FSM 실행**까지 구현했습니다. 구체적인 몬스터·NPC 행동과 전환은 C#으로 확장합니다.
순찰 적 프리팹은 대기·왕복 이동과 추적·공격·피격·사망 상태를 사용합니다.

## 실행과 조작
`Assets/_Game/Scenes/MovementLab.unity`를 열고 Play를 누릅니다.

- 좌우 이동: A/D 또는 방향키 / 게임패드 왼쪽 스틱.
- 점프: Space / 게임패드 아래 버튼.
- 글리치: 마우스로 적의 상·하·좌·우를 조준하고 Shift를 새로 누릅니다. 벽 너머 또는 도착 공간이 막힌 요청은 취소합니다.
- 공격: 좌클릭 또는 J. 옆 3타·아래 띄우기·위 내려찍기를 사용하고 대상 변경 후에도 콤보를 이어갑니다.
- 지면 적 아래로 글리치하면 지하 상태가 됩니다. 공격으로 출현하고 Space로 취소하며, 최대 2초 뒤 원래 위치로 돌아옵니다.
- 잠복 중 A/D 또는 좌우 방향키로 출현 표시를 움직입니다. 청록색은 해당 지점에서 출현 가능, 빨간색은 해당 지점이 막혔다는 뜻입니다. 빨간 지점에서 공격하면 같은 바닥의 가까운 출현 가능 지점으로 자동 보정합니다. 이동은 같은 바닥 안에서 가능하며 바닥 끝에서는 멈춥니다.
- 점프를 짧게 누르면 낮게, 길게 누르면 높게 뜁니다.
- 발판을 벗어난 뒤 0.1초 안에는 점프할 수 있습니다.
- 착지 전 0.13초 안에 누른 점프는 착지 시 실행됩니다.

씬에는 픽셀아트 플레이어, 단색 바닥, 경계 벽, 점프 확인용 발판 3개와 고정 카메라가 있습니다. 현재 저장된 MovementLab에는 PatrolEnemy 인스턴스 1개도 배치되어 있습니다.
대시·체크포인트·재시작·일시정지·HUD·효과음·건물 배경과 와이어 이동은 없습니다. 공격은 Adventurer의 검격 애니메이션, 지하는 청록색 지면 표시로 확인합니다.

## 조절 위치
`Assets/_Game/Data/PlayerTuning.asset`에서 이동 속도, 지상 가속·감속, 공중 가속, 점프 높이, 상승·낙하 중력, 점프 해제 배율, 코요테 타임, 입력 버퍼를 조절합니다.
초깃값은 이전 프로토타입의 이동·점프 설정을 유지했습니다.
Player 프리팹의 PlayerUnit에서 Definition, Tuning, Ground Mask, Visual 참조를 연결합니다. Definition은 `Assets/_Game/Data/Units/PlayerDefinition.asset`이며 ID는 `player`, Kind는 `Player`, FSM은 비어 있습니다. 이동 수치는 별도의 PlayerTuning에서 관리합니다.

`Assets/_Game/Data/PrototypeControls.inputactions`에는 Player 맵의 Move / Jump / Aim / Glitch / Attack이 있습니다. 새 기능은 키보드·마우스용이며 기존 게임패드 이동·점프는 유지합니다.
기본 `Assets/Settings/InputSystem_Actions.inputactions`와 프로젝트 전역 입력 등록도 보존했습니다. 플레이어 루트의 Unity 기본 `PlayerInput`에 PrototypeControls를 연결하고 Default Action Map을 `Player`로 설정했습니다.
`PlayerInput`이 액션 활성화와 기기 연결, 싱글 플레이에서 키보드·게임패드 자동 전환을 관리합니다. Behavior는 `Invoke C Sharp Events`이며 이벤트 연결 없이 `PlayerInputReader`가 해당 컴포넌트의 액션 값을 읽습니다. Reader는 PlayerUnit이 생성하는 일반 C# 객체이며 프리팹에 부착하지 않습니다. 액션을 별도로 복제하거나 활성화하지 않습니다.
입력을 끄고 켤 때는 `PlayerInput.DeactivateInput()` / `ActivateInput()`을 사용합니다. PlayerInput 컴포넌트나 Player 액션 맵이 비활성화된 동안 Reader는 빈 명령을 반환합니다. 물리 정지나 점프 버퍼 초기화는 별도 처리입니다.
`Assets/_Game/Prefabs/Player.prefab`은 루트에서 물리 몸체와 입력·컨트롤러·모터를 관리하고, 바로 아래 `Visual` 자식에서 `SpriteRenderer`와 `Animator`를 관리합니다.

## 플레이어 외형과 애니메이션
rvros의 [Animated Pixel Adventurer](https://rvros.itch.io/animated-pixel-hero)를 적용했습니다. 개인·상업적 사용 및 수정이 허용되는 제작자 라이선스이며 에셋 단독 재배포는 금지됩니다. 출처·원본 해시·사용 조건은 `Docs/ThirdPartyNotices.md`에 기록했습니다.

원본 이미지는 `Assets/_Game/Art/Characters/Adventurer`, 애니메이션은 `Assets/_Game/Animations/Player`에 있습니다. 50×37 프레임, 18 PPU, Point 필터, 무압축이며 발 피벗 `(25/50, 1/37)`을 사용합니다. 대기 4프레임(10 FPS), 달리기 6프레임(12.5 FPS), 점프 4프레임, 낙하 2프레임입니다. 기존 몸체·Visual 위치와 이동 수치는 유지합니다. VirtualGuy 원본은 미사용 에셋으로 보존합니다.

Player 프리팹의 `Visual`에는 SpriteRenderer, Animator, PlayerVisual이 있습니다. PlayerUnit이 초기화 시 `ICharacterMotionState`와 선택적 `IPlayerActionState`를 전달합니다. PlayerVisual은 읽기 전용 상태로 이동·공격 애니메이션과 Flip X를 갱신합니다. 구체적인 모터나 PlayerUnit 참조는 보관하지 않습니다. 정지하면 마지막 방향을 유지합니다. Animator의 Apply Root Motion은 꺼져 있습니다.

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

적의 UnitHealth는 `Deactivate On Death`를 꺼서 FSM이 사망 지연을 처리합니다. 플레이어도 이 옵션을 끄고 PlayerUnit이 Die 표시 시간(Death Duration, 기본 0.7초) 뒤 비활성화합니다. 사망한 개체를 다시 활성화해도 체력은 0이며 적은 다시 사망 상태로 들어갑니다. 풀링용 부활·체력 초기화 API는 별도 구현 대상입니다.

Play 중 Unit/PlayerUnit Inspector에서 `FSM State`, `Target`, `Health`를 확인할 수 있습니다. 적을 선택하고 `Test Damage`와 `Apply Test Damage`로 피격·사망을 시험합니다. 플레이어의 좌클릭/J 공격으로도 피해를 줄 수 있습니다.

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

## 글리치·플레이어 공격 (2026-09-24)

- `PlayerUnit`이 일반 C# 객체 `PlayerGlitch`와 `PlayerCombat`을 생성하고 FixedUpdate 실행 순서를 조정합니다. `GlitchUtility`는 대상·방향·배치·공간 검사를 담당하며 Unit/FsmRuntime에 플레이어 기능을 추가하지 않습니다.
- `GlitchTuning.asset`: 최대 거리 6, 보정 반경 0.75(0이면 직접 조준만), 중심 반경 0.1, 여유 간격 0.15, 쿨타임 0, 지하 깊이 0.6, 체류 2초. 쿨타임은 성공 시에만 시작하며 0 설정은 기존 타이머도 해제합니다.
- `PlayerCombatTuning.asset`: 선딜레이/판정/후딜레이 0.12/0.06/0.18초, 콤보 유지 1초, 공격 길이/폭 1.2/1.6, 밀쳐내기/띄우기 속도 6/10, 출현 상승 10, 강제 이동 0.35초, 플레이어 피격 제한 0.25초입니다. 피해는 UnitDefinition.AttackPower를 사용합니다. 공중 내려찍기 설정은 아래 절을 참고합니다.
- Player 프리팹에 두 설정 에셋을 연결했습니다. 카메라는 씬 객체이므로 MovementLab의 Player 인스턴스 `Aim Camera`에 고정 카메라를 연결합니다. 다른 씬에 배치할 때도 해당 카메라를 명시적으로 연결해야 합니다. 두 행동 설정이 모두 없으면 기존 이동 전용으로 동작합니다.
- 직접 조준 우선, 동률은 개체 Instance ID 순서, 대각선은 좌우 우선입니다. 중앙을 가리키면 플레이어가 있던 좌우 측을 사용합니다. 좌우 도착은 몸 크기가 다른 적과 발 높이를 맞춥니다. 선택 방향이 막히면 다른 방향으로 바꾸지 않습니다.
- Shift 입력 시 대상·방향을 고정하고 실제 실행 직전에 생존·활성·거리·시야·도착 공간을 다시 검사합니다. 성공한 재배치에서만 이전 점프 버퍼와 코요테 상태를 지웁니다.
- 적의 상하좌우를 마우스로 조준하면 해당 방향의 도착 몸체 영역이 청록색 테두리로 표시됩니다. 공간 차단·쿨타임·공격/피격 제한 중에는 빨간색입니다. 아래 조준이 지하 진입으로 이어지면 지면에 납작한 표시가 나타납니다. 미리보기는 실제 이동과 같은 배치 검사를 사용하며 이동이나 쿨타임을 발생시키지 않습니다. 조준 해제·대상 소멸·입력 비활성화·지하 체류·플레이어 비활성화 시 사라집니다.
- 지하는 정적인 수평 BoxCollider2D 바닥만 지원합니다. 이동 발판·경사·인접한 중첩 층은 제외합니다. 실제 몸체는 원래 위치에서 Kinematic으로 고정되고 Collider를 유지하지만 유닛 간 충돌은 하지 않습니다. 화면에는 적 아래의 지면 표시만 보입니다. 지하 중에는 피해와 적의 타깃 선정을 명시적으로 차단합니다.
- 출현 시 대상과 **현재 잠복 표시 지점 바로 위의 지상 공간**을 재검사합니다. 입력이 없으면 X좌표를 유지하며 대상이 이동해도 따라가지 않습니다. 잠복 중 A/D·좌우 방향키는 몸체 대신 표시의 X좌표를 조절합니다. `GlitchTuning.asset`의 **Underground Move Speed=3**(월드 단위/초, 0이면 위치 조절 꺼짐)으로 속도를 설정합니다. Collider 전체가 같은 바닥 안에 출현할 수 있는 범위로 제한합니다. 장애물 아래로 표시를 움직일 수 있지만, 출현 불가한 위치에서는 표시가 빨간색이 됩니다. 주변 출현 후보 없음·대상 소멸·체류 시간 만료·Space 취소는 원래 몸체 위치로 복귀하며, 비활성화도 물리·피격·표시 상태를 복원합니다. 이동해도 2초 체류 시간과 쿨타임은 연장하지 않습니다. Space 취소가 이동·공격보다 우선하며, 이동과 공격을 함께 입력하면 해당 물리 단계의 이동을 반영한 지점에서 출현합니다. 지하 중 재글리치는 금지합니다.
- 공격과 글리치가 동시에 요청되면 재배치 후 공격합니다. 지하에서는 Space 취소가 공격보다 우선합니다. 공격 판정이 끝난 후에는 글리치로 후딜레이를 취소할 수 있습니다.
- 후딜레이 중 함께 누른 공격 입력도 FixedUpdate까지 보관하고, 글리치가 성공해 후딜레이를 취소한 다음 공격 가능 여부를 판단합니다. 글리치가 실패하면 기존 후딜레이 제한을 유지합니다. 단, 마지막 공격 예약 구간에서 누른 입력은 후딜레이 종료 뒤 한 번 실행됩니다.
- 콤보는 공격 판정 시작에 소비하며 공중 내려찍기는 하강 시작에 소비합니다. 헛공격·출현 공격도 한 타이며 대상 변경은 타수를 유지합니다. 피격·사망·비활성화·유효 시간 만료 시 초기화합니다.
- `UnitDefinition.AllowGlitchTarget`과 `AllowForcedMovement`는 독립 설정입니다. PatrolEnemy는 둘 다 허용합니다. 글리치 불가 적도 타격할 수 있고, 고정형 적은 피해를 받으면서 강제 이동만 거부합니다.
- 적의 강제 이동은 `GroundMovement2D`가 소유합니다. 강제 이동 중 MoveTo/Stop이 속도를 덮어쓰지 않으며 FSM은 새 추적·공격을 보류하고 피격·사망 처리는 유지합니다.
- 옆 공격은 `ApplyKnockback`으로 초기 수평 속도를 한 번 주고 실제 Rigidbody 속도를 0까지 감속합니다. 1·2타는 **Light Knockback Speed=3 / Light Knockback Deceleration=30**으로 짧게 밀고, 3타(콤보 막타)는 **Knockback Speed=12 / Knockback Deceleration=18**로 멀리 밀어냅니다. `PlayerCombatTuning`에서 각각 조절하며 Speed는 시작 속도, Deceleration은 초당 줄어드는 속도입니다. 감속값이 클수록 짧고 빠르게 멈춥니다. 콤보 기능을 꺼도 기본 공격의 작은 넉백은 유지하며, 명중 반응 기능을 끄면 넉백을 적용하지 않습니다. 벽을 만나면 수평 이동을 멈추며 벽이 사라져도 원래 속도를 다시 적용하지 않습니다. 수직 속도와 중력은 유지합니다.
- 넉백의 **Forced Duration**(현재 0.35초)은 최소 행동 제한 시간입니다. 속도가 먼저 0이 되어도 남은 제한 시간은 유지하고, 감속이 더 오래 걸리면 넉백이 끝날 때까지 일반 이동/AI가 속도를 덮어쓰지 않습니다. 재피격은 새 초기 속도·감속값으로 교체하며 사망·비활성화는 정리합니다. 띄우기·출현·내려찍기의 기존 수직 강제 이동 경로는 유지합니다.
- `PlayerVisual`은 `ICharacterMotionState`와 선택적 `IPlayerActionState`만 읽습니다. 방향·공격 애니메이션·지하 표시를 갱신하며 물리 루트는 변경하지 않습니다. 판정 도형은 Visual의 **Show Attack Area** 디버그 옵션으로 켤 수 있으며 기본 프리팹에서는 꺼져 있습니다.

### 히트스톱·방향성 흔들림·공격 입력 예약

- `PlayerCombatTuning.asset`의 **Attack Buffer Time=0.1초**: 후딜레이 마지막 구간에서 새로 누른 공격 하나를 예약하고 Ready가 되는 물리 단계에서 한 번 실행합니다. 선딜레이/판정/하강/후딜레이 초반 입력은 예약하지 않습니다. 누르고 있기만 해서는 반복 공격하지 않습니다. 0이면 예약을 끕니다. 피격·사망·비활성화·성공한 글리치의 후딜레이 취소는 예약을 지웁니다. 새 공격은 실행 시점의 위치·대상을 다시 판정합니다.
- **Hit Stop Duration=0.035초 / Heavy Hit Stop Duration=0.055초 / Slam Hit Stop Duration=0.08초**: 실제 피해가 적용됐을 때만 게임 시간을 잠시 정지합니다. 일반 옆 공격은 기본, 콤보 막타·띄우기·출현은 Heavy, 내려찍기는 Slam입니다. 처치 타격도 포함하며 헛공격·벽 차단·무적 대상은 제외합니다. 한 검격에서 늦게 들어온 적을 추가로 맞혀도 히트스톱을 다시 발생시키지 않습니다. 내려찍기 하강과 착지는 서로 다른 명중 단계입니다.
- **Shake Amplitude=0.025 / Heavy Shake Amplitude=0.075 / Slam Shake Amplitude=0.14**(월드 단위), **Shake Duration=0.14초**, **Shake Frequency=22**: 옆 공격은 좌우, 띄우기·출현은 위, 내려찍기는 아래 방향으로 시작해 감쇠하는 흔들림입니다. 연결된 Aim Camera를 사용합니다. 같은 시점의 여러 타격은 가장 강한 값으로 합치며 진폭·시간을 합산하지 않습니다. 각 시간/진폭을 0으로 설정해 효과를 끌 수 있습니다.
- PlayerCombat은 명중 사실만 내보내며 PlayerUnit이 생성한 일반 C# 객체 `PlayerImpactFeedback`이 정지와 카메라 연출을 담당합니다. 별도 카메라 패키지나 씬 컴포넌트를 추가하지 않았습니다. 고정 카메라의 위치 오프셋은 입력 수집 전에 제거하고 LateUpdate에서 적용하므로 조준 좌표를 흔들지 않습니다.
- 히트스톱은 현재 싱글 플레이의 Time.timeScale을 0으로 설정하고 비스케일 시간으로 종료해 이전 배속을 복원합니다. FixedDeltaTime은 변경하지 않습니다. 입력 수집은 계속하며 게임 시간 기준의 공격·글리치·콤보·피격/사망 타이머도 함께 멈춥니다. 이미 정지 중인 게임을 임의로 재개하지 않습니다. 피격·사망·비활성화 시 본인이 만든 정지와 카메라 오프셋을 복원합니다.

### Adventurer 공격 표시 (2026-09-25)

- 지상 1·2·3타는 Attack1/2/3, 일반 공중 공격은 AirAttack1/2/3 상태를 사용합니다. 공중 원본 횡검격은 두 종류이므로 3타는 첫 검격을 재사용합니다. 타수와 피해량은 그대로 유지합니다.
- Lift와 Emergence는 위로 베는 Attack1 프레임을 재사용합니다. 위쪽 글리치 이후 내려찍기는 원본 air-attack3의 준비 자세(SlamHover) → 하강 반복(SlamFall) → 착지(SlamLand)를 사용합니다. 0.5초 대기와 착지 충격파는 기존 전투 타이밍을 따릅니다.
- `PlayerCombat.GetPresentation`이 공격 종류·타수·반복 번호·시작 시 공중 여부·단계 진행률을 읽기 전용으로 제공합니다. PlayerUnit이 IPlayerActionState로 전달하고 PlayerVisual만 Animator를 조작합니다. 글리치에는 애니메이션·콤보 책임을 추가하지 않았습니다.
- 기존 Moving/Grounded/Rising 파라미터를 유지하고 ActionOverride Bool과 ActionTime Float을 추가했습니다. 준비는 클립의 0~0.35, 타격은 0.35~0.6, 회복은 0.6~1 구간에 맞춰 재생합니다. 하강은 별도 반복 클립이며 착지 첫 프레임은 충격파 시점에 시작합니다. Animation Event와 Root Motion은 사용하지 않습니다.
- 피격 시 공격을 취소하고 Hit(원본 hurt 3프레임)를 재생합니다. `PlayerCombatTuning > Hit Stun`(기본 0.25초)에 맞추며 다시 맞으면 첫 프레임부터 재시작합니다. 기존 피격 중 걷기는 유지합니다. 글리치 취소·지하·비활성화에서도 공격 표시를 정리합니다.
- 체력이 0이면 Die(원본 die 7프레임)가 모든 동작보다 우선합니다. 공격·글리치·입력을 정리하고 `PlayerUnit > Death Duration`(기본 0.7초) 뒤 GameObject를 비활성화합니다. 공중 사망은 중력을 유지하며 지상 사망은 제자리에서 재생합니다. 0초이면 재생 대기 없이 비활성화합니다. Player 프리팹의 `UnitHealth > Deactivate On Death`는 꺼 두어야 하며 연결을 완료했습니다. 재활성화는 부활하지 않습니다.
- PlayerUnit은 피격/사망 종류·진행률·피격 번호를 `IPlayerActionState.ReactionPresentation`으로 제공하고 PlayerVisual만 Animator를 변경합니다. 표시가 피해·콤보·사망 완료를 결정하지 않습니다.

### 공격과 연계 규칙의 분리

잠복 출현 위치가 막혔을 때는 공격 입력 시에만 같은 바닥을 좌우로 탐색합니다. 현재 위치가 비어 있으면 그대로 사용하고, 막혔으면 0.05월드 단위 간격으로 가까운 후보부터 검사한 뒤 처음 발견한 빈 공간의 경계를 6번 세분화합니다(경계 보정 오차 0.001 미만). 양쪽 거리가 같으면 마지막 잠복 이동 방향, 이동이 없으면 진입 전 몸체가 있던 쪽을 우선합니다. 아주 좁은 빈 중심 좌표 구간이 탐색 간격보다 작으면 놓칠 수 있으므로 0.05는 탐색 해상도이며 수학적으로 모든 연속 좌표의 최솟값을 보장하지는 않습니다. 바닥 끝·몸체 전체 공간·대상과 지면 유효성 검사는 유지합니다. 성공 시 보정 지점에서 출현 공격을 시작하며 쿨타임을 다시 시작하지 않습니다. 같은 바닥에 후보가 없거나 대상/지면이 무효하면 진입 전 몸체 위치로 복귀합니다.

몬스터와 몸체가 겹친 상태의 일반 공격은 옆 공격으로 처리합니다. 위치 기반 띄우기는 몬스터 몸체 하단이 플레이어 몸체 상단 이상에 있을 때만 선택하며 접촉 오차 0.02를 허용합니다. 중심 높이만으로 큰 몬스터를 위쪽 대상으로 분류하지 않습니다. 잠복 출현은 명시적인 출현 공격이므로 이 규칙과 별도로 띄우기를 유지하고, 타격 영역도 잠복 위치의 X축을 중심으로 대칭입니다.

`PlayerUnit`의 Glitch Tuning과 Combat Tuning은 각각 선택 사항입니다. Glitch Tuning만 비우면 이동·점프·기본 공격을 사용할 수 있고, Combat Tuning만 비우면 글리치·지하 진입/출현을 사용할 수 있습니다. 둘 다 없으면 이동 전용입니다.

`PlayerCombat`이 콤보 순번·대상 이력·공격 단계·유효 시간·피격 제한과 피해 판정을 소유합니다. `PlayerGlitch`는 이를 변경하지 않으며 PlayerUnit이 대상과 출현 여부를 전달하고 실행 순서를 조정합니다. 공통 몸체·시야 조회는 `UnitPhysics2D`, 글리치 대상·도착점 계산은 `GlitchUtility`에 둡니다.

| 교체 계약 | 기본 구현 | 연결하지 않았을 때 |
|---|---|---|
| `IPlayerComboRule` | `SequentialComboRule`: 지정 타수 순환과 대상 변경 시 유지 여부 계산 | 매번 기본 1타, 마무리 반응 없음 |
| `IPlayerAttackSelector` | `PositionAttackSelector`: 상대 위치/출현에 따른 공격 종류·영역·상승 요청 | 바라보는 방향의 기본 옆 공격 |
| `IPlayerHitReaction` | `GroundHitReaction`: 명중 후 밀쳐내기·띄우기·하향 이동 | 피해만 적용 |

규칙은 실행 상태를 저장하지 않고 생성자로 주입합니다. 새 규칙을 구현해 PlayerUnit의 초기화 연결만 교체하면 됩니다. 공격 가능 시간과 타수 소비 시점은 계속 PlayerCombat이 관리합니다. 명중 반응은 숫자 3 대신 콤보 규칙이 반환한 마무리 여부를 사용합니다.

`PlayerCombatTuning.asset` Inspector의 **Enable Combo / Enable Position Attacks / Enable Hit Reactions**로 각 연계를 독립적으로 끌 수 있습니다. 기본은 모두 켜짐, **Combo Length=3**, **Retain Combo On Target Change=켜짐**입니다. 이 옵션과 타수 규칙 변경은 플레이어가 초기화될 때 반영되므로 Play Mode를 다시 시작해야 합니다. 기본 프리팹의 기존 조작·타이밍·연계는 유지합니다.

### 유닛 충돌과 공중 내려찍기 (2026-09-25)

- 현재 유닛 몸체가 사용하는 Ignore Raycast(2) 레이어의 자기 레이어 충돌을 Physics 2D 설정에서 껐습니다. 플레이어·적·허수아비는 서로 통과하며 Default 지형과는 계속 충돌합니다. 새 유닛 몸체도 같은 레이어를 사용합니다. 타격·조준 조회는 유지하며 글리치 도착점도 충돌하지 않는 유닛 때문에 막히지 않습니다.
- 실제 공격 실행 직전 모터의 접지를 확인합니다. **적 위쪽으로 글리치가 성공한 뒤 공중에서 하는 첫 공격만 내려찍기**입니다. 일반 점프·낙하·좌우/아래 글리치 뒤 공중 공격은 바라보는 방향의 기본 옆 공격입니다. 지상의 공격 선택·콤보와 지하 출현 공격은 유지하며, 출현 후 공중에서 다시 공격할 때도 기본 옆 공격을 사용합니다.
- 내려찍기 가능 상태는 PlayerCombat이 소유합니다. PlayerUnit은 성공한 재배치의 위쪽 도착 여부와 접지 여부만 전달하며 PlayerGlitch는 공격·콤보를 변경하지 않습니다. 내려찍기 시작 시 한 번 소비하고 착지·다른 방향 재배치·피격·사망·비활성화 시 해제합니다. 실패한 글리치는 이 상태를 새로 만들지 않습니다.
- 내려찍기가 선택되면 입력 실행 위치에서 0.5초 동안 수평·수직 속도를 0으로 유지한 뒤 `Descending` 단계로 내려찍습니다. 대기·하강 중 이동·점프·재공격·글리치를 제한하며 피해는 계속 받을 수 있습니다. 모터가 몸체 전체를 매 물리 프레임 이동 거리만큼 검사하여 가장 먼저 닿는 바닥/발판에서 멈춥니다. 일반 공중 옆 공격은 기존 준비 시간 0.12초를 사용하며 중력은 계속 적용됩니다. 모든 공격은 준비부터 후딜레이 종료까지 수평 이동과 새 점프를 차단합니다. 공격 중 점프 입력은 예약되지 않으며, 출현 상승과 내려찍기는 유지됩니다. 공격 종료 또는 피격 취소 후 누르고 있는 이동 방향으로 다시 움직입니다.
- 대기 중에는 피해가 없고, 하강 시작부터 공격을 시작할 때 바라보던 전방에 Box 판정이 생깁니다. 영역은 지상 옆 공격과 같은 Reach/Width를 사용합니다. 명중한 적에게 AttackPower 피해와 강한 하향 속도를 적용합니다. 벽에 가려진 적은 제외하며 Enable Hit Reactions가 꺼져 있거나 Allow Forced Movement가 꺼진 적은 피해만 받습니다.
- 착지 지점에서 원형 범위의 충격파 피해를 적용합니다. 전방 타격과 충격파는 명중 기록을 공유하므로 같은 공격에서 이미 맞은 적은 다시 피해를 받지 않습니다. 피해량은 AttackPower이며 지형에 가려진 대상은 제외합니다. 충격파 자체는 강제 하향 반응 없이 피해만 주며 PlayerVisual이 지면 위로 퍼졌다 사라지는 노란 반원으로 표시합니다.
- Inspector의 **Slam Hover Duration=0.5초**, **Slam Speed=28**(플레이어 하강), **Slam Knockdown Speed=36**(전방에 맞은 적 하강), **Shockwave Radius=2**, **Shockwave Duration=0.22초**, **Slam Timeout=3초**가 시작값입니다. 적의 행동을 제한하는 시간은 기존 Forced Duration(0.35초)을 사용하며 수직 이동은 중력·지형 충돌을 따릅니다. 공중 대기 시간은 지상 공격의 Windup과 독립적이며 0이면 즉시 하강합니다. 바닥이 없으면 하강 제한 시간 후 충격파 없이 일반 이동으로 돌아옵니다. 피격·사망·비활성화는 공중 대기·하강·충격파 표시를 취소합니다.
- 공격 상태·타수·충격파 피해는 PlayerCombat, 물리 이동은 CharacterMotor2D, 표현은 PlayerVisual이 담당합니다. 선택적 연계 세 가지를 꺼도 위쪽 글리치의 내려찍기 선택은 유지하며, 글리치 없는 구성에서는 기본 공중 공격을 사용합니다.

## 훈련용 허수아비

`Assets/_Game/Prefabs/Enemies/TrainingDummy.prefab`을 MovementLab 중앙 바닥 `(0, 0.92, 0)`에 한 개 배치했습니다. 기존 플레이어·순찰 적·발판 배치는 유지합니다. 다른 곳에서는 같은 프리팹을 바닥 위에 놓으면 됩니다.

- 루트: Unit, UnitHealth, GroundMovement2D, Rigidbody2D, BoxCollider2D. FSM과 UnitCombat2D가 없어 스스로 움직이거나 공격하지 않습니다.
- `TrainingDummyDefinition.asset`: ID `enemy.training-dummy`, 이름 훈련용 허수아비, Kind Monster, 최대 체력 100, 공격력 0, 글리치 대상·강제 이동 허용. Monster 분류는 기존 플레이어 공격의 타격 대상으로 사용하기 위한 설정입니다.
- UnitHealth의 `Preserve Health On Damage`를 허수아비에서만 켰습니다. 타격 피해량 반환과 DamageVersion 증가는 유지하며 체력을 차감하지 않아 반복 연습할 수 있습니다. DamageEnabled/비활성화의 피해 차단은 그대로 적용됩니다. 일반 플레이어·적은 기본값 false로 기존 체력 감소·사망을 유지합니다.
- 좌우/상하 글리치, 지하 출현, 3타 밀쳐내기·띄우기·내려찍기를 받습니다. 밀려난 위치로부터 자동 복귀하는 기능은 없습니다.
- 공중에서 공격하면 하강 전방 타격 또는 착지 충격파로 허수아비에 피해를 줍니다. 한 공격에서는 한 번만 맞으며 색 변화로 명중을 확인합니다. 체력은 유지됩니다.
- Visual은 기존 Block 스프라이트로 만든 나무 기둥·짚 몸체·표적입니다. TrainingDummyVisual이 명중 시 0.15초 동안 붉게 표시하며, 비활성화 시 원래 색으로 복원합니다. 외형 코드는 피해·이동·체력을 변경하지 않습니다.
