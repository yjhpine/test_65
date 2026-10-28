# 이동·점프 기초 — Movement Lab

## 전투 → 휴식 → 전투 (2026-10-25)

- `CoreLoopStage`는 웹 프로토타입의 기본 추천 전투 배치(`combat-course.js`, 2026-10-25 확인)에 맞춰 **2 → 3 → 4 → 5마리**, 총 4웨이브로 구성한다. 직선 사격은 포탑, 공중 포격은 드론, 지상 근접은 돼지로 대응하고 마지막 지상 적은 기존 방패병을 사용한다. 각 적의 기존 행동·체력·공격 설정은 유지한다.
- 웨이브 1 X=0~60: 포탑 2. 웨이브 2 X=72~110: 돼지·포탑·드론. 웨이브 3 X=122~162: 돼지·포탑 2·드론. 웨이브 4 X=174~217: 돼지·방패병·포탑 2·드론. 휴식 구역은 X=60~72, 110~122, 162~174이며 도착 X=66/116/168, 최종 출구는 X=215다.
- 웹 추천 배치의 40px 격자를 Unity 1유닛으로 환산해 적의 상대 위치와 발판 높낮이를 연결했다. 첫 구역은 기존 인트로 동선을 위해 X=20 이후에 배치하며, 이후 구역 폭은 38/40/43유닛으로 맞췄다. 발판은 기존 단방향 충돌과 아트를 재사용한다. 카메라 오른쪽 경계는 217이며 배경 반복·원근 계수는 유지한다. 웹의 전투 수치는 가져오지 않는다.
- 현재 구역 전멸 후 게임 시간 0.35초와 진행 중인 컷신/시간 연출이 끝나면 `SHIFT > REST AREA` 안내를 표시한다. 이때 기존 글리치 키(왼쪽/오른쪽 Shift)를 새로 누르면 휴식 구역 이동을 시작한다. 입력 전에는 자유롭게 대기하며 자동 이동·회복하지 않는다. 전투 중 또는 준비 완료 전에 누른 키는 이동 입력으로 예약하지 않는다.
- 이동은 사망 위치를 즉시 기록하고, 플레이어 앞쪽의 몸체 배치 가능한 위치만 경유한다. 60유닛/초 기준 0.4~1.1초 동안 연속 이동하며 마지막 22%만 감속한다. 경유지마다 기존 spark를 재생한다. 이동 도중 키를 다시 눌러도 재시작하지 않는다.
- 도착점과 경로 공간 검사는 캡슐의 설정 오프셋을 사용한다. 물리 bounds와 보간된 표시 Transform의 위치 차이를 오프셋으로 사용하지 않아 점프·낙하 중 Shift를 눌러도 같은 휴식 도착점으로 이동한다.
- `RestArea.prefab`은 범위, 분리된 바닥, 도착 앵커, 다음 전투 입구와 민트색 안내를 묶는다. 세 휴식 구역은 각각 안전하게 최초 도착했을 때 체력을 한 번 완전히 회복한다. 재방문 회복·체크포인트·상점·추가 HUD·효과음은 없다. 휴식 시간 제한 없이 기존 조작을 사용할 수 있다.
- 플레이어 몸체가 다음 입구를 완전히 통과하면 뒤쪽 문을 잠그고 다음 적 그룹을 활성화한다. 비활성 적의 초기 체력 0은 처치로 세지 않는다. 살아 있는 적을 비활성화하는 것만으로 전멸할 수 없다. 경계문은 지형 레이어를 사용하고 구간별 바닥 Collider가 논리 지하 이동을 제한한다.
- `StageFlowController.encounters`가 구역 순서를 소유한다. 각 항목은 적 루트·명시적 목표 적·X 범위·출구문·다음 휴식을 연결하며, 마지막 항목의 다음 휴식은 비운다. 빈 배열이면 기존 `requiredEnemies` 단일 전투 완료 방식을 사용한다.
- `PlayerUnit.BeginScriptedTransfer / SetScriptedTransferPosition / EndScriptedTransfer`가 구역 이동의 조작 잠금과 모터 연결을 맡는다. 전투 글리치 쿨타임은 보존한다. 모터가 이동 동안만 물리 시뮬레이션을 보류하고 표시 좌표를 갱신한다. 취소하면 출발 위치와 물리 상태를 복원하고 회복하지 않는다.
- `UnitHealth.RestoreToFull()`은 살아 있는 활성 유닛의 실제 회복량만 반환한다. 피해 이벤트·DamageVersion·공유 정의를 변경하지 않고 사망자를 부활시키지 않는다.
- 막힌 도착점은 이동을 시작하지 않고 `TransferBlocked`와 설정 오류를 남긴다. 이동 중 목적지가 무효화되면 출발점으로 돌아간다. 자동 재시도는 없으며 설정 수정 후 씬을 재시작한다.
- 4웨이브의 마지막 적을 처치하면 출구가 열린다. 기존 사망 동작을 위한 게임 시간 0.85초와 전투 시간 연출이 끝난 뒤 `FinalWaveExitReveal.playable`을 한 번 재생한다. 문을 0.65초 동안 확대(Orthographic Size 3.2)하고, 0.7초 보여준 후 0.65초 동안 현재 플레이어에게 복귀(Size 4.5)한다. 플레이어 머리 위에 **“문이 열렸다. 어서 탈출하자.”**를 2.75초간 표시하고 평소 카메라·조작으로 돌아온다. 대사는 자동 진행하며 기존 Esc/Start 스킵을 지원한다.
- 마지막 처치 때 출구 안에 있어도 위 컷신이 끝난 뒤 클리어한다. 사망을 우선 처리하며 외부 일시정지 중에는 컷신을 시작하지 않는다. 새 씬 실행마다 재생 여부가 초기화되고 구역 설정이 없는 단일 전투 씬의 클리어 방식은 유지한다. `StageFlowController`의 Final Wave Cutscene에 전용 Runner가 연결돼 있다.

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
- 내려찍기가 선택되면 입력 실행 위치에서 0.5초 동안 수평·수직 속도를 0으로 유지한 뒤 `Descending` 단계로 내려찍습니다. 대기·하강 중 이동·점프·재공격·글리치를 제한하며 피해는 계속 받을 수 있습니다. 모터가 몸체 전체를 매 물리 프레임 이동 거리만큼 검사하여 가장 먼저 닿는 바닥/발판에서 멈춥니다. 일반 공중 검격은 별도 `PlayerAttack.Air` 상태로 기존 Windup/Active Duration/Recovery를 사용하며, 준비·타격·후딜레이 동안 좌우 조작과 중력을 유지합니다. 점프키를 떼면 기존 점프 높이도 줄일 수 있습니다. 새 점프는 공격 중 예약하지 않습니다. 지상 공격·출현·내려찍기는 기존 이동 제한을 유지하고, 공중 검격 도중 착지하면 남은 공격 시간 동안 수평 이동을 제한합니다. 공격 종료 또는 피격 취소 후 누르고 있는 이동 방향으로 다시 움직입니다. `PlayerVisual`이 Air 상태를 기존 Adventurer `AirAttack1/2/3` 클립에 연결하며, 착지 시 검격을 지상 애니메이션으로 재시작하지 않습니다.
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


## 신규 적 3종 (2026-09-30)

MovementLab의 `Enemy Pattern Samples` 아래 ShieldSoldier (1, 0.82), SurveillanceTurret (7, 0.62), FlyingDrone (10, 4)를 추가했다. 기존 Player, PatrolEnemy, 비활성 TrainingDummy, 지형과 카메라는 보존했다. 각 프리팹은 `Assets/_Game/Prefabs/Enemies`에 있다.

| 적 | 대응법 / 기본 패턴 | 체력 / 피해 |
|---|---|---|
| 방패병 | 정면의 비슷한 높이 검격을 방패로 차단. 뒤·상하 공격, 출현과 충격파는 통과. 뒤쪽 감지 시 0.55초 회전, 첫 준비 1.2초/이후 0.85초, 속도 13.6 돌진 후 1.2초 회복 | 75 / 14 |
| 감시 백신 | 1초 감시 중 성공한 글리치는 진행 초기화. 0.4초 빨간 조준 고정 이후에는 고정된 방향으로 발사하므로 사선을 벗어날 것. 1.5초 재검사 대기, 강제 이동 불가 | 90 / 10 |
| 공중 드론 | 수평 거리 2 미만이면 자기 X축의 위아래 전격, 멀면 조준 사격. 첫 준비 1.2초/이후 0.75초 중 마지막 0.4초 방향 고정. 공격 중 넉백·띄우기는 이동만 바꿈 | 60 / 전격 10, 조준 8 |

- 밸런스는 `Data/Fsm/ShieldSoldierFsm.asset`, `SurveillanceTurretFsm.asset`, `FlyingDroneFsm.asset`에서 조절한다. HP는 같은 이름의 `Data/Units/*Definition.asset`에서 변경한다. 새 적의 실제 공격 피해는 FSM 설정의 Damage/Vertical Damage를 사용하며 UnitDefinition.AttackPower와 자동 동기화하지 않는다.
- EnemyFsmDefinition은 종류별 개체 상태 그래프를 생성하며 EnemyCombat2D는 대상/돌진/광선을 처리한다. Unit/FsmRuntime 및 기존 PatrolEnemy FSM은 수정하지 않았다. 세 적 모두 피격으로 준비가 초기화되지 않고 색만 점멸한다. 사망은 0.6초 뒤 비활성화된다.
- UnitHealth의 위치를 받는 ApplyDamage 오버로드가 선택적 IUnitDamageGuard를 확인한다. 기존 정수 오버로드는 환경/테스트 피해처럼 방향 방어를 우회한다. PlayerCombat은 실제 피해 0이면 넉백과 명중 피드백을 실행하지 않는다.
- IForcedMovementReceiver는 GroundMovement2D/FlyingMovement2D의 공통 반응 계약이다. 드론은 강제 반응 동안 중력을 적용하고, 공격 회복이 끝난 뒤 원래 순항 경로로 속도 제한을 지키며 복귀한다.
- PlayerUnit.RepositionVersion은 성공한 일반 글리치·잠복 진입·출현에만 증가한다. 실패·A/D 잠복 이동·Space 취소는 증가시키지 않는다. 백신은 IUnitRepositionState를 읽으며 PlayerGlitch에 적이나 공격 타이머를 연결하지 않는다.
- EnemyVisual2D만 Animator와 방패·감시 진행 막대·광선을 그린다. 황색은 추적 중 예고, 적색은 고정된 예고, 밝은 광선은 발사 표시(0.16초, 추가 피해 없음)다. 예고와 타격은 동일한 EnemyBeam 계산 결과의 폭과 끝점을 사용한다. 발사 이후 표시만 발사 시점 위치에 남는다.
- 벽/바닥은 광선을 차단하고 활성 PlatformEffector2D를 사용하는 콜라이더는 사선 검사에서 제외한다. 유닛 자기 레이어 충돌 비활성화는 유지하며 돌진은 지나온 몸체 영역을 조회한다.
- 시각 에셋과 라이선스/ZIP 해시는 ThirdPartyNotices.md를 참고한다. 새 지형, HUD, 소리, 적 협동 및 구역 진행은 추가하지 않았다.


## 플레이어 피격 화면 연출 (2026-09-30)

실제 플레이어 피해가 기록되면 기존 Hit/Die 애니메이션과 함께 전면의 짧은 밝은 섬광, 붉은 가장자리, 방사형 충격선, 피격 전용 슬로우모션·흔들림·짧은 줌을 재생한다. 적을 때렸을 때의 기존 타격 연출과 설정을 분리했다. 잠복 무적 등으로 무시된 피해는 새 연출을 발생시키지 않는다.

`Assets/_Game/Data/PlayerCombatTuning.asset`의 **Player Damage Feedback**에서 조절한다. 각 강도/유지 시간을 0으로 두면 해당 효과가 꺼진다. 슬로우 배율은 0보다 커야 하며 1이면 슬로우가 꺼진다.

| Inspector 항목 | 기본값 | 역할 |
|---|---:|---|
| Damage Slow Time Scale | 0.2 | 피격 시 기존 게임 속도에 곱하는 배율(1이면 비활성) |
| Damage Slow Duration | 0.15초 | 느린 속도를 유지하는 실제 시간(0이면 비활성) |
| Damage Slow Recovery | 0.1초 | 원래 속도로 부드럽게 복귀하는 실제 시간 |
| Damage Shake Amplitude | 0.22 | 피격 카메라 흔들림 거리 |
| Damage Shake Duration | 0.28초 | 흔들림이 감쇠되는 시간 |
| Damage Zoom | 0.035 | 순간 확대 비율(3.5%) |
| Damage Zoom Duration | 0.18초 | 확대 후 원래 화면으로 복귀하는 시간 |
| Damage Flash Opacity | 0.7 | 전면 섬광의 최대 불투명도 |
| Damage Flash Duration | 0.065초 | 전면 섬광이 사라지는 시간 |
| Damage Vignette Opacity | 0.8 | 붉은 가장자리와 충격선의 최대 강도 |
| Damage Screen Duration | 0.45초 | 가장자리 효과가 사라지는 시간 |

PlayerUnit이 피해 버전 변경을 감지해 기존 PlayerImpactFeedback에 전달한다. PlayerDamageScreen은 명시적으로 연결한 조준 카메라 앞의 임시 화면 표시만 담당하며 Collider/입력 차단/HUD를 만들지 않는다. Player 프리팹의 Damage Screen Material에 전용 URP 머티리얼을 연결했다. 재질 원본을 변경하지 않고 실행 인스턴스를 재사용하며 플레이어 비활성화 때 해제한다.

효과는 unscaled time으로 갱신된다. 피격 시 속도를 20%로 0.15초 유지하고 0.1초에 걸쳐 복귀한다. 물리 갱신 간격도 함께 조절하고 종료·비활성화 때 복원한다. 재피격은 지속 시간을 갱신하며 기존 일시정지와 외부 속도 변경은 존중한다. 적 명중 히트스톱은 유지하되 피격 슬로우 중에는 중첩하지 않는다. 카메라 위치와 줌은 입력 조준 계산 전에 복원한다. 사망 시 마지막 피격도 표시하며 비활성화·카메라 교체에서 잔여 흔들림/줌을 정리한다. 피해 수치·무적 시간·이동 튜닝은 바꾸지 않았다.

## 코어루프 스테이지 (2026-10-05)

- 실행 씬: `Assets/_Game/Scenes/CoreLoopStage.unity`. 빌드 첫 씬이며 기존 MovementLab은 별도 실험 씬으로 보존한다.
- 씬 진입 즉시 시작한다. 왼쪽 시작 지점에서 이동·점프·공격으로 순찰병·방패병·공중 드론·감시 포탑 각 3마리(총 12마리)를 모두 처치하고 오른쪽 출구에 들어가면 완료된다.
- 출구는 잠금 상태에서 빨강, 전원 처치 후 청록색이다. 잠긴 출구에 들어가면 처치 조건을 화면에 표시한다. 출구 안에서 마지막 적을 처치해도 완료된다.
- 사망 시 기존 Die 연출과 플레이어 비활성화가 끝난 뒤 씬 전체를 자동 재로드한다. 플레이어 체력·입력·공격·글리치와 적의 체력·위치·FSM이 처음 상태로 돌아간다.
- 완료 시 STAGE CLEAR / RESTART를 표시한다. 입력·피해·물리를 멈추며 버튼 또는 포커스된 버튼의 Submit으로 재시작한다. 씬 종료 시 시간 배율과 물리 시간 간격을 복원한다.
- Stage Flow의 StageFlowController에서 Player, Required Enemies, Goal, View를 연결한다. 필수 적 목록은 같은 씬의 유효하고 중복 없는 UnitHealth다. 체력 0만 처치로 인정하며, 살아 있는 적의 비활성화나 제거로 조건을 우회할 수 없다.
- Exit의 StageGoal은 비트리거 유닛 몸체와 출구 트리거의 실제 겹침을 확인한다. 잠복 상태는 도달로 계산하지 않는다. 같은 프레임의 사망이 완료보다 우선한다.
- Camera Rig의 StageCameraFollow가 X축을 추적하고 0~180 경계에서 화면 폭을 고려해 멈춘다. Y축은 3.4로 고정하며, 자식 Camera의 피격 흔들림·줌은 기존 PlayerImpactFeedback이 담당한다.
- Stage UI는 UIDocument + StageFlowView와 UI/StagePanelSettings.asset을 사용한다. 체력 HUD, 체크포인트, 점수, 저장, 다음 스테이지는 없다.
- 지형은 180유닛 연속 바닥과 9개 단방향 발판이다. 기존 Player와 네 종류 적 프리팹·튜닝을 재사용하며 씬별 Aim Camera를 연결한다.

### 잠복 중 카메라 (2026-10-06)
- CoreLoopStage의 추적 카메라는 잠복 중 실제 고정 몸체 대신 지면 표시의 X좌표를 따라간다. 취소·시간 만료·출현 후에는 몸체 추적으로 복귀하며 기존 Y 고정과 화면 경계 제한은 유지한다.

### 확장 배치 (2026-10-06)

- 바닥 길이 60→180, 오른쪽 경계 X=180.5, 출구 X=178, 카메라 추적 범위 0~180으로 확장했다. 시작 위치는 (3, 0.82)다.
- 60유닛씩 세 구간에 네 종류를 1마리씩 배치한다. 총 12마리 전부가 완료 조건이며 재시작 시 모두 복원된다.
- 드론 높이는 Y=4, 방패병 Y=0.82, 감시 포탑 Y=0.62, 순찰병 Y=0.52다. 기존 단방향 발판 구성은 구간별로 이어 총 9개다.

| 구간 | 순찰병 X | 방패병 X | 공중 드론 X | 설치형 감시 포탑 X |
|---|---:|---:|---:|---:|
| 1 (0~60) | 14 | 30 | 42 | 50 |
| 2 (60~120) | 74 | 90 | 102 | 110 |
| 3 (120~180) | 134 | 150 | 162 | 170 |

적 프리팹·정의·공격 패턴·기존 사용자 튜닝은 그대로 사용한다.

## 연출 템플릿 (2026-10-07)

Cinemachine 3.1.7 + Timeline 1.8.13을 사용한다. CoreLoopStage의 IntroCutscene은 X=6 구역 진입 시 재생되며, 첫 발판(X=20)을 보여준다. 목표 적은 네 종류 각각 1마리, 총 4마리다. MovementLab은 연출을 연결하지 않는다.

### 기획자 작업 순서

1. `Game > Cinematics > Create Cutscene`에서 새 프리팹 이름을 정한다. 전용 Timeline과 프리팹이 생성되고 현재 씬에 인스턴스가 배치된다. 각 연출의 Timeline은 독립적이다.
2. 루트 CutsceneRunner의 Coordinator에 씬의 Cinematics, Player에 Player, Stage에 Stage Flow를 연결한다. 기존 CoreLoopStage에는 이 공용 구성이 설치되어 있다. 새 씬은 같은 구조의 CutsceneCoordinator/CutsceneCameraRig와 출력 카메라의 비활성 CinemachineBrain이 필요하다.
3. `연결 검사` 후 `Timeline 열기`를 누른다. 카메라 트랙의 Shot Camera를 선택하고 Shot Rig의 Animation Track에서 위치와 `Lens.OrthographicSize` 키를 편집한다. 직교 크기가 작을수록 확대된다. 카메라 Shot 겹침은 블렌딩 구간이다.
4. DialogueClip에서 Speaker, Portrait(선택), Text, Characters Per Second(기본 30), Progress를 편집한다. Confirm은 읽을 때까지 대기하고 Timed는 클립 길이만큼 표시한다. 확인 대기 클립의 시간은 실제 읽는 시간을 의미하지 않는다.
5. FadeClip에서 색과 From/To 불투명도를 정한다. 페이드는 클립 밖에서는 투명해진다. 지속 암전은 해당 구간을 클립으로 덮는다.
6. Entry Area의 BoxCollider2D 위치·크기를 편집한다. World Mode=Pause는 세계 정지, Continue는 조작만 잠그며 적과 중력이 계속 작동한다. Continue에서도 피해를 받으면 즉시 중단한다.
7. Play Mode에서 구역에 들어가거나 Inspector의 재생 버튼으로 확인한다. 기본 확인은 Enter/Space/좌클릭/게임패드 South, 스킵은 Esc/Start다. 입력 변경은 CutsceneControls.inputactions에서 한다.

### 동작과 제약

- 대화 트랙은 여러 개를 사용할 수 있지만 전체 대화 트랙에서 동시에 한 대사만 표시한다. 트랙 간 대사 중첩도 오류다. 페이드 트랙은 한 개이며 클립을 겹칠 수 없다. 대화 선택지·분기·캐릭터 연기·음성·범용 이벤트는 없다.
- Confirm 입력은 타이핑 중 전문 표시, 전문 표시 후 다음 구간으로 진행한다. 대사 입력 대기 중에도 현재 카메라와 타이핑은 유지된다. 한글 폰트는 Noto Sans KR(OFL)이다.
- 정상 완료와 스킵은 기본 0.35초에 걸쳐 플레이어의 현재 위치로 카메라가 돌아온 뒤 조작을 복원한다. 피격·사망·취소는 즉시 복원한다.
- 완료/스킵 때만 Completed Inspector 이벤트가 한 번 호출된다. 필수 게임 상태 변경은 이 이벤트에 연결한다. 스킵은 Timeline 끝으로 이동해 임의 이벤트를 재실행하지 않는다.
- Once Per Scene은 완료/스킵 후 재진입을 막는다. 피격/취소는 구역 밖으로 나갔다가 재진입하면 재생한다. 잠복 상태에서는 시작하지 않는다.
- 플레이어 기존 공격·잠복·입력 버퍼는 시작 시 정리한다. 정지형은 씬 FSM을 현재 상태 그대로 보류한다. 동시에 하나의 연출만 재생한다.
- 미리보기는 Timeline Preview에서 카메라·대사 전문·페이드만 평가한다. 완료 이벤트, 입력 잠금, 게임 시간은 변경하지 않는다. Preview 종료나 씬 저장 시 카메라와 UI를 복원한다.
- 런타임 연결은 같은 씬에 한정한다. 연출 중 생성되는 새 전투 유닛을 동결 목록에 추가하는 스폰 시스템은 현재 범위가 아니다.

### 캐릭터 말풍선 대화 (2026-10-07)

CoreLoopStage의 IntroCutscene 인스턴스는 별도 `IntroConversation.playable`을 사용한다. X=20의 발판을 1.5초 동안 보여준 뒤 0.35초 동안 플레이어와 안내자 구도로 돌아와 안내자 → 플레이어 → 안내자의 세 대사를 표시한다. 종료 후 기존 플레이 카메라로 복귀한다. 안내 NPC는 X=9에 있으며 전투·목표 적에 포함하지 않는다. 기존 IntroCutscene 프리팹/Timeline은 하단 안내 템플릿으로 유지한다.

1. 대화할 캐릭터에 `DialogueSpeaker`를 붙이고 Display Name을 지정한다. 자식 `SpeechAnchor`를 머리 위에 배치하고 Anchor에 연결한다. 기준점은 캐릭터 자신 또는 자식이어야 한다. 캐릭터 스크립트 수정은 필요 없다.
2. 연출 루트 Inspector의 **추가할 화자**에 해당 컴포넌트를 지정하고 **말풍선 트랙 추가**를 누른다. 화자 바인딩과 기본 클립이 생성되며 기존 대사 마지막 시각 뒤에 배치된다.
3. Timeline의 `SpeechBubbleTrack` 하나에 화자 한 명을 연결하고 클립의 Text, Characters Per Second, Progress, 시작·길이를 편집한다. 다른 화자는 별도 트랙을 사용한다. 클립을 번갈아 배치하되 전체 대화 트랙에서 중첩하지 않는다.
4. 하단 안내는 기존 `DialogueTrack`을 사용한다. 하단 대화의 이름·초상화는 DialogueClip의 Speaker/Portrait에서, 말풍선 이름은 연결된 DialogueSpeaker에서 가져온다. 말풍선에는 초상화를 표시하지 않는다.
5. 카메라 프레이밍과 기준점은 Timeline 미리보기에서 확인한다. 미리보기는 전문을 표시하며 입력을 기다리지 않는다. 말풍선은 카메라 줌에도 같은 UI 글자 크기를 유지하고 화면 가장자리에서는 안쪽으로 제한된다.

- 확인 입력은 타이핑 중 전문 표시, 전문 표시 후 다음 대사로 진행한다. 대기 중에도 화자 기준점을 추적한다.
- 화자·기준점이 삭제되거나 비활성화되면 연출을 취소하고 게임 상태를 복원한다. 화면 밖의 화자는 말풍선을 가장자리에 표시한다.
- 긴 본문은 자동 줄바꿈한다. 읽기 좋은 길이로 클립을 나누어 작성한다. 동일한 대사 에셋을 다른 클립에 사용해도 타이핑은 다시 시작한다.
- `DialoguePlayback`은 Runner가 소유하는 일반 C# 객체이며 타이핑·글자 경계를 관리한다. `CutsceneView`와 `SpeechBubbleView`는 표시만 담당한다. 대화 트랙은 평가 결과를 Runner에 제출하며, 빈 트랙이 다른 트랙의 대사를 숨기지 않는다.
- 현재는 컷신 전용, 한 번에 한 대사다. 일반 플레이 주변 대사·동시 대화·선택지·음성·캐릭터 연기 트랙은 포함하지 않는다.

### 현재 코어루프 축소 배치 (2026-10-07)

이 배치가 위의 180유닛 확장 배치를 대체한다. 맵은 0~60유닛, 출구 X=58, 오른쪽 벽 X=60.5다. 플레이어 X=3, 안내 NPC X=9, 컷신 진입 X=6은 유지한다.

| 적 | 위치 X | 수 |
|---|---:|---:|
| 순찰병 | 24 | 1 |
| 방패병 | 34 | 1 |
| 공중 드론 | 42 | 1 |
| 감시 포탑 | 50 | 1 |

네 적을 모두 처치한 뒤 출구에 도착하면 완료한다. 발판은 X=20/36/52의 3개이며, 안내 컷신은 첫 발판 X=20을 보여준다. 카메라 추적 경계는 0~60이다. 시작 대화와 첫 전투 사이에 여유를 두었으며 적 프리팹·전투 수치는 바꾸지 않았다.


## 2026-10-15 — Assets/Art 플레이어 외형 연결

- Player 프리팹의 Visual과 기존 Player.controller의 17개 상태를 `Assets/Art` 캐릭터로 교체했다. 런타임 이동·점프·공격 코드, 콤보 순번, 공격 판정/단계 시간, 루트 물리·콜라이더·씬 배치는 유지한다.
- 통합 클립은 `Assets/_Game/Animations/Player/ArtCharacter`의 11개다. 원본 PNG·임포트 설정·Art 안의 개별 클립/컨트롤러는 보존한다. 기존 Adventurer 클립도 남겨 안내 NPC의 GuideIdle 참조를 유지한다.

| 기존 상태 | 사용 프레임 |
|---|---|
| Idle / Run | Idle / Walk |
| Jump / Fall | JumpUP / JumpDown; 끝 프레임 유지 |
| Attack1~3 / AirAttack1~3 | AttackWaiting 준비 + AttackNomerl 타격·복귀; 한 종류를 3타에 공통 사용 |
| Lift / Emergence | AttackUP의 uppercut |
| SlamHover / SlamFall / SlamLand | AttackDown의 stomp 1~3 준비 / 3~4 하강 반복 / 5~8 착지·복귀 |
| Hit | EnemyHit 8프레임 |
| Die | 별도 사망 원본이 없어 EnemyHit 1~4 사용 후 마지막 피격 자세 유지 |

- 준비·타격·복귀는 기존 PlayerVisual의 정규화 시간 0~0.35 / 0.35~0.6 / 0.6~1에 맞췄다. Idle·Run·SlamFall만 반복한다. 공격/피격/사망은 ActionTime으로 평가하며 실제 피해·사망 처리는 기존 코드가 담당한다.
- 새 프레임은 64×64, 18 PPU, 중앙 피벗이며 서 있는 발은 아래에서 17픽셀이다. Visual의 Y를 `-0.8 + (32-17)/18 = 0.0333333`으로 조정해 기존 콜라이더 하단에 맞췄다. 크기는 1, 좌우 반전은 SpriteRenderer.flipX, Root Motion은 꺼진 상태다.

## 2026-10-21 — 기본 공격·글리치 효과

- `Combat FX Free`의 `white_slash`를 지상/일반 공중 기본 3타에 연결했다. 준비 중에는 숨기고 Active에서 베기 본체, Recovery에서 잔상을 표시한다. 기존 AttackBounds를 따라가고 Facing에 맞춰 좌우 반전한다. 띄우기·출현 공격·내려찍기에는 기본 베기를 추가하지 않는다.
- `white_spark`는 성공한 글리치의 출발·도착 위치에서 30 FPS로 한 번 재생한다. 잠복 진입은 지면 표시 위치, 성공한 출현은 현재 잠복 표시와 실제 출현 위치를 사용한다. 실패·쿨타임 차단·취소/시간 만료에는 새 효과를 발생시키지 않는다.
- PlayerGlitch가 성공한 위치와 표현 번호를 `PlayerGlitchPresentation`으로 공개하고 PlayerUnit의 `IPlayerActionState`가 전달한다. PlayerVisual이 일반 C# 객체 PlayerActionEffects를 소유해 렌더링을 처리한다. 이동·전투 판정/수치·애니메이션 상태 전환은 기존 시스템이 계속 담당한다.
- 효과는 Player/Visual의 **Action effects**에서 설정한다. Basic Attack Effect Size와 Glitch Effect Size는 전체 프레임의 월드 크기(Player 프리팹은 2.5, 최초 2에서 1.25배 확대), Glitch Effect FPS는 기본 30이다. Sprite 배열은 각각 순서가 고정된 16프레임이다. 배열이 비어 있으면 해당 효과를 생략한다.
- 베기 1개·스파크 4개의 SpriteRenderer를 초기화 때 만들고 재사용한다. 출발·도착 스파크는 플레이어 이동과 무관하게 기록한 월드 위치를 유지한다. 피격·사망·비활성화/ResetActions 시 효과를 정리하며 효과에 Collider나 피해 판정은 없다.
- 사용 조건과 크레딧은 `Docs/ThirdPartyNotices.md`, 원본 라이선스는 `Assets/_Game/Art/Effects/CombatFxFree/LICENSE.txt`를 따른다.

### 청록·붉은색 효과 팔레트

- Player/Visual의 **Action Effect Material**에 `Assets/_Game/Art/Effects/PlayerActionTealRed.mat`를 연결했다. 캐릭터의 지직이는 프레임에서 확인한 청록 `#79D9DF`와 붉은색 `#D84255`를 사용한다. 검격은 청록 본체·붉은 끝부분, 스파크는 청록·붉은 파편으로 표시한다.
- `PlayerActionTwoTone.shader`가 원본 텍스처의 음영·알파·프레임 배치를 유지하면서 색을 적용한다. 머티리얼의 Teal / Red로 색, Red boundary / Boundary diagonal로 두 색의 영역, White core strength로 밝은 중심광을 조절한다. 분할은 스프라이트 로컬 좌표를 사용해 프레임 간 고정되며 좌우 공격과 함께 반전한다.
- 이 머티리얼은 베기 1개·스파크 4개만 공유한다. 미지정 시 기존 캐릭터 머티리얼을 사용한다. 크기 2.5, 프레임·재생 시점·피해 판정은 유지한다.

## 2026-10-21 — 고정형 포탑 대기 분리와 조준 외형

- SurveillanceTurret는 왼쪽에서 `Idle`(일반 01), 오른쪽에서 `IdleOpposite`(일반 05)을 사용한다. 3초 대기 → `IdleTurn`(01→05, 0.5초) → 오른쪽으로 3초 대기 → `IdleTurnBack`(05→01, 0.5초)을 반복한다. FSM의 Idle/Turning 상태가 시간과 방향을 소유하고 외형은 이를 표시한다. 반회전 완료 후 방향을 바꾸고 다음 FSM 갱신부터 새 방향에서 감지한다. 평상시 대기로 돌아오면 마지막 전투 방향에서 3초를 다시 센다.
- 감지·조준·발사 후 회복 중에는 `AngryIdle`의 분노 대기 측면 포즈를 사용한다. 조준 중 360도 대기 회전을 재생하지 않아 총열이 목표를 계속 바라본다. `Attack`은 기존 분노 왼쪽 공격 10프레임, `Death`는 새 분노 기본 사망 10프레임이다. 기존 `Run` 상태는 일반 대기 정지 포즈로 남겨두었다.
- 선택적 `SurveillanceTurretVisual`을 포탑 Visual에만 연결했다. EnemyVisual2D가 상태와 발사 표시 시간을 전달하며, 이 컴포넌트는 Animator/외형만 관리한다. 원본 64×64 PNG를 수정하지 않고 초기화 때 사용 프레임 31장의 상부·받침대 Sprite를 생성해 재사용한다. 상부만 FSM의 Aim에 맞춰 좌우/상하 조준하고 받침대·물리 루트는 회전하지 않는다.
- 기존 1초 스캔 → 0.4초 방향 고정 → 발사 → 1.5초 회복을 유지한다. 포탑만 회복 경과가 발사 표시 시간 0.16초를 지나면 유효한 기존 타깃을 다시 추적한다. 다음 공격 시각·피해·사거리·시야·글리치 감지 초기화는 동일하다. 발사 외형은 LastShot 방향을 사용하므로 그동안 타깃이 움직여도 고정된다.
- 방향별 정지 시간/반회전 길이는 `SurveillanceTurretFsm.asset`의 `Idle Turn Interval`(3초) / `Idle Turn Duration`(0.5초)에서 조절한다. 이전 Visual의 독립 타이머는 제거했다. 사망 외형은 마지막 표시 방향을 유지하고, 비활성화·재활성화는 FSM의 초기 방향과 일반 대기로 복귀한다. 드론·방패병·순찰 돼지의 기존 제어 경로는 유지한다.
- **전방 감지:** 대기 중 플레이어 몸체 중심이 현재 방향의 반평면에 있을 때만 새 대상으로 선택한다. 뒤에 있으면 대기·반회전을 계속하며, 회전 중에는 새로 감지하지 않는다. 기존 거리 12·장애물 시야 검사는 유지한다. 한번 감지한 대상은 앞뒤 이동에도 기존 추적을 유지하고, 조준 고정·발사 중에는 방향이 고정된다.
- **포구 발사:** 포탑 루트의 EnemyCombat2D에 `Aim Pivot Offset=(0,-0.1625)`, `Muzzle Distance=0.75`를 연결했다. 원본의 회전 축 (32,27), 포구 (8,27), 32 PPU와 Visual Y=-0.6에 맞춘 값이다. 조준은 총열 회전 축에서 계산하고, 예고선·실제 발사·피해 영역은 회전한 포구에서 시작한다. 총열 축과 포구 사이가 벽으로 막히면 발사 판정도 차단한다. 빔 길이 22와 폭 0.16은 유지한다.

## 2026-10-21 — CoreLoopStage 벽·바닥과 배경

- 바탕화면 `벽,바닥`의 `terrain_tiles.zip`(32장)과 `Background_runtime.zip`(3장)을 `Assets/_Game/Art/Environment/Industrial`에 가져왔다. 원본 PNG를 보존하고 Point 필터·무압축·Mip Map 없음·Full Rect로 설정했다. 지형은 128 PPU, 배경은 64 PPU이며 최대 8192 크기의 원본 배경 해상도를 유지한다.
- `CoreLoopStage/Terrain`의 Floor, Left/Right Boundary, Ledge 1/2/3에 `Industrial Visual` 자식을 추가했다. 기존 단색 SpriteRenderer만 비활성화하고 Transform, BoxCollider2D, PlatformEffector2D 및 게임플레이 연결은 유지한다. 바닥·벽은 1유닛 패널을 반복하며 일부 변형 타일을 섞었다.
- 발판은 원본 128×128 중 상단 40픽셀만 불투명하다. 이 부분이 기존 5×0.4 충돌체와 일치하도록 4개의 타일을 배치했다. 투명 영역을 포함한 전체 스프라이트 사각형과 실제 보이는 발판 영역은 다르다.
- `Environment Background`에 Base/Far/Mid를 정적으로 겹쳤다. Sorting Order는 -30/-20/-10, 중심은 (30,3.4), 높이는 16이다. Base는 가로 80으로 반복하고 Far/Mid는 원본 비율의 폭 68/128을 사용한다. 카메라·시차 이동 로직을 추가하지 않았다.
- 변경 범위는 CoreLoopStage의 외형이다. MovementLab, 캐릭터 프리팹, 이동·전투 수치 및 프로젝트 설정은 변경하지 않았다.

### 배경 패럴랙스

- Unity의 Perspective 카메라와 Z축 거리로도 시차를 만들 수 있다([공식 설명](https://learn.unity.com/article/2d-in-unity-tech-art-essentials)). 이 프로젝트는 기존 Orthographic 카메라·조준·대화 연출을 유지하고 배경별 `ParallaxLayer2D`로 시차를 적용한다. 위의 정적 배경 배치는 이 동작으로 확장됐다.
- Hierarchy의 `Environment Background/01 Base`, `02 Far`, `03 Mid`에서 **Parallax Layer 2D > Scroll Speed**를 조절한다. 현재 설정은 각각 **0.05 / 0.1 / 0.25**다. 고정된 지형의 화면상 스크롤을 1로 볼 때 먼 배경일수록 작은 값을 사용한다. 0은 카메라를 완전히 따라가 화면에 고정되고, 1은 지형처럼 월드에 고정된다. X/Y에 같은 비율을 적용하며 Z 위치·크기·Sorting Order는 유지한다.
- 각 레이어의 **View Camera**는 최종 출력 Camera다. StageCameraFollow, Cinemachine, PlayerImpactFeedback의 처리가 끝난 뒤 URP `beginCameraRendering`에서 실제 카메라 위치를 읽는다. 다른 카메라의 렌더 콜백은 무시하고, 대화·히트스톱의 Time.timeScale=0에서도 카메라를 따라간다.
- 첫 활성화의 카메라 위치와 배경 로컬 위치를 기준으로 절대 변위를 계산한다. 카메라가 되돌아오거나 글리치·화면 흔들림이 반복돼도 이동량이 누적되지 않는다. 비활성화하면 원래 배치와 구독을 정리하고, 재활성화하면 같은 기준 위치에서 이어간다. 새로 로드한 씬은 새 기준을 사용한다.
- 각 레이어에 `Repeat` 자식 SpriteRenderer를 하나씩 추가해 **두 장으로 가로 무한 반복**한다. `ParallaxLayer2D > Repeat Image`가 두 번째 장을 참조한다. 원본과 같은 Sprite/머티리얼/크기/정렬을 공유하며, 원경·중경의 좌우 끝 무늬가 달라 두 번째 장의 Flip X를 반대로 설정했다. 좌우 끝을 교대로 맞붙여 원본 PNG를 수정하지 않고 연결한다.
- 카메라 화면의 왼쪽 끝을 덮는 타일 번호를 직접 계산하고, 원본은 짝수 칸·Repeat는 홀수 칸을 담당한다. 화면 밖의 장만 반대편으로 옮기며 큰 순간 이동에도 여러 번 이동하는 반복문 없이 필요한 위치에 바로 배치한다. 플레이 중 배경 수는 총 6개로 고정되고 추가 생성·파괴는 없다.
- 가로폭은 한 장당 Base 80 / Far 68 / Mid 128유닛이다. 두 장만으로 화면을 덮는 방식이므로 한 장의 폭이 카메라 화면 폭 이상이어야 한다(현재 16:9, Orthographic Size 6.5의 화면 폭은 약 23.11). 세로는 기존 패럴랙스 이동이며 무한 반복하지 않는다. 새 패키지, 카메라 이동 제한·투영 방식, 지형/캐릭터 동작은 변경하지 않았다.

## 2026-10-22 — 방패병 전용 공격·방어·피격·사망 애니메이션

- 바탕화면 `벽,바닥`의 새 Monster3 공격·사망·방어·피격 PNG 66장을 추가했다. 대기·이동은 기존 모션을 유지하며, 돌진 중 `Attack`은 전용 검격 14프레임, `Death`는 분해되는 분노 사망 10프레임을 사용한다. 기존 Attack/Death 클립 GUID와 방패병 프리팹 연결을 보존했다.
- Visual의 선택적 `ShieldSoldierVisual`이 기존 EnemyVisual2D에서 상태를 전달받아 표시한다. 준비 시간의 처음 20%는 `GuardEnter`, 가운데 60%는 `GuardHold`, 마지막 20%는 `GuardExit`다. 돌진은 기존 FSM Progress로 공격을 재생하고 회복 시 대기로 돌아간다. 준비·돌진·회복·사망 시간과 이동·피해·방어 판정은 변경하지 않았다.
- BlockVersion 증가 시 `Block` 0.24초, 실제 DamageVersion 증가 시 `Hit` 0.32초를 표시한다. 우선순위는 사망 → 피격 → 막기 → 기존 행동이며, 연속 피격/막기는 해당 모션을 재시작한다. 외형 반응은 FSM을 중단하거나 타이머를 초기화하지 않는다. 비활성화하면 반응 시간을 지워 재활성화에 남지 않는다.
- 모든 새 클립은 Sprite만 변경하고 Root Motion·Animation Event를 사용하지 않는다. 왼쪽 원본을 기존 Flip X 규칙으로 반전하며 몸체 크기·발 위치·기존 방패 예고선과 피격 색 표시는 유지한다. 새 `Monster3_Hit_PNG.zip`을 사용하고 드론 그림이 들어 있는 이전 `Monster3Hit_PNG.zip`은 제외한다.

## 전투 연출 보강 (2026-10-25)

- 기존 체력·피해·공격 단계·입력 버퍼·3타 넉백·피격 경직·적 FSM·글리치는 유지한다. 히트스톱 0.035/0.055/0.08초, 피격 슬로모션 0.2배·유지 0.15초·복귀 0.1초도 유지한다. 효과음·HUD·새 원화·피격 후 무적은 추가하지 않는다.
- UnitHealth.DamageApplied는 피해가 수용된 직후, 사망 비활성화 전에 UnitDamageResult를 전달한다. AcceptedDamage와 HealthLost를 구분해 훈련용 허수아비의 체력 보존을 유지한다. ApplyDirectedDamage는 방향 정보만 추가하며 기존 호출자의 방어 정책을 변경하지 않는다. 방향 없는 ApplyDamage는 방향을 추측하지 않는다.
- PlayerCombat은 실제 공격 시작 시각과 대상별 PlayerHitPresentation을 제공한다. PlayerUnit이 외형과 화면 연출에 전달하며 애니메이션이 피해 판정을 실행하지 않는다. 명중은 대상마다 한 번, 히트스톱은 기존 공격 단계별 한 번이다.
- PlayerAttackAlternation은 기존 Strike/IsFinisher와 독립적이다. 같은 Side/Air 공격의 게임 시간상 시작 간격이 0.40초 이내면 A/B를 교대한다. 공격 종류 변경·시간 초과·피격·비활성화 시 초기화하며 순간이동 자체는 유지한다. SideAttackA/B는 기존 원화의 타격 구간 순서를 정방향/역방향으로 구성하고 복귀 구간을 공유한다.
- 일반 공격은 민트, 띄우기/출현은 보라, 내려찍기는 호박색으로 표시한다. 기존 청록/붉은 글리치와 효과 크기는 유지한다. 타격 준비 단계부터 공격 자세를 표시하되 실제 판정 시간은 그대로다.
- 접촉 효과는 기존 spark 프레임을 사용한다. 섬광 0.05초, 파편 0.10초, 처치 마무리 최대 0.15초이며 비스케일 시간으로 소멸한다. 활성 대상의 로컬 접촉점을 따라가고 대상이 비활성화되면 마지막 위치에서 끝난다. 기존 지면 충격파 범위 피해는 유지하고 화면에는 짧은 착지 먼지를 표시한다.
- UnitDamageFlash는 적에게 0.06초 밝기 변화를 표시한다. 기존 EnemyVisual2D의 붉은 피격 틴트는 중복하지 않는다. 적 사망 애니메이션/비활성화 시간은 그대로다.
- 플레이어 실제 체력 감소에 붉은 윤곽·밝기 변화를 표시한다. 방향을 알 때만 Hit 모션/Visual 기울기를 최대 0.12초 사용하며 물리 루트에는 적용하지 않는다. 실제 새 공격은 피격 자세보다 우선하고 사망은 최우선이다. 짧아진 연출이 기존 피격 경직을 줄이지 않는다.
- 카메라 설정의 Shake Amplitude 값은 이제 1120×660 기준 화면 픽셀이다. 일반 1.5px/0.06초, 띄우기·출현 2px/0.08초, 내려찍기·착지 명중 3px/0.10초, 처치 3px/0.10초, 플레이어 피해 4px/0.12초. 최대 4 기준 픽셀을 출력 뷰포트 높이에 비례해 환산한다.
- 화면 반응은 플레이어 피해 > 처치 > 명중 순서로 하나를 선택한다. 방향 없는 피해에는 카메라 방향 이동을 생략한다. 전체 화면 섬광/확대는 없으며 중앙이 투명한 분홍 테두리를 최대 12%·0.16초 표시한다. 조준 변환은 마지막 표시 카메라 오프셋을 반영한다.
- PlayerUnit의 Reduced Motion 및 SetReducedMotion(bool)은 흔들림·피격 기울기만 끈다. 접촉 효과·윤곽·테두리·기존 히트스톱/슬로모션은 유지한다. 별도 설정 UI는 없다.
- 에셋 연결 도구: Game > Prototype > Configure Combat Presentation. A/B 클립·반응 재질·적 피격 컴포넌트와 화면 연출 값만 연결하며 씬을 저장하거나 전투/시간 연출 수치를 변경하지 않는다.
- 접촉 섬광/파편은 대상 몸체의 Sorting Order +1로 표시한다. 적 사격 예고(+2)보다 아래에 두어 위험 정보를 가리지 않는다. 착지 먼지는 플레이어 효과 순서를 사용한다.

## 2026-10-25 — 글리치 spark 한 열 연쇄 재생

- 이동 경로에 연속 선을 그리는 방식은 사용자의 수정 요청으로 제거했다. 기존 청록·붉은 spark 프레임을 출발/도착 지점에서 즉시 재생하고, 두 지점 사이에 작은 spark를 한 열로 배치해 출발 방향부터 차례로 터뜨린다. 잠복 진입/출현도 실제 `GlitchPresentation.From`과 `To`를 사용한다.
- PlayerVisual의 Glitch Effect Size는 기존 2.5, Glitch Effect Fps는 30이다. 경로 중간의 spark만 크기 50%로 표시한다. Glitch Trail Spacing은 0.6 월드 단위, Glitch Trail Interval은 비스케일 시간 0.025초다. 각 spark는 원본 16프레임을 한 번 재생한다.
- 최대 16개 렌더러를 재사용하며 긴 경로는 간격을 넓혀 양 끝을 포함한다. 연속 사용은 이전 예약을 지우고 최신 경로를 재생한다. 이동 후에도 모든 spark는 기록된 월드 좌표에 고정된다. 길이가 0이면 해당 지점에 spark 하나만 표시한다.
- 비활성화·재시작·피격 취소·사망 시 표시와 예약을 정리하며 과거 경로를 재생하지 않는다. 명중 효과, 글리치 이동 판정·쿨타임·전투 수치는 변경하지 않았다.

## 2026-10-25 — 포탑·드론 ARCBIT 레이저

- SurveillanceTurret의 Visual에는 파란색, FlyingDrone의 Visual에는 빨간색 `EnemyLaserVisual`을 연결했다. 원본 출처와 라이선스는 ThirdPartyNotices의 ARCBIT Lite 절을 따른다. 플레이어 공격·글리치 효과에는 적용하지 않는다.
- EnemyVisual2D가 기존 준비/조준 고정 상태를 읽어 발사구 charge 프레임을 표시한다. 예고선은 준비 28%, 고정 75% 불투명도를 유지하며 적별 레이저 색상을 사용한다. 실제 ShotVersion이 증가하면 LastShot의 고정된 양 끝점으로 시작/몸통/끝을 렌더링한다.
- 발사는 기존 BeamDisplayDuration(두 적 모두 0.16초) 안에서 on → loop → off 프레임을 재생한다. 렌더링으로 피해를 발생시키지 않으며 조준·사거리·벽 차단·공격 시각·피해량은 EnemyCombat2D와 FSM이 계속 담당한다. 포탑의 기존 포신 좌표를 그대로 사용한다.
- 몸통은 Full Rect/Tiled로 반복하고 마지막 타일을 자른다. 매우 짧은 빔은 양 끝 조각을 함께 축소한다. 드론 수직 공격은 기존 전체 판정선의 중심 발사점에서 위/아래 끝점으로 두 조각을 표시한다. 사망·비활성화와 재시작 시 남은 표시를 제거한다.
- 초기화 때 최대 두 세트의 렌더러를 만들고 재사용한다. 프레임마다 게임 오브젝트·Sprite·Material을 생성하지 않는다. 레이저 컴포넌트/리소스가 없는 적은 기존 도형 표시를 유지한다.

## 2026-10-28 — 빠른 이동 반응 1차 조정

- `PlayerTuning.asset`: 최고 속도 10.5→12, 지상 가속 125→240, 제동 110→300, 공중 가속 45→180. 공중 무입력 제동은 하드코딩 3 대신 `Air Braking` 설정으로 분리하고 현재 플레이어에 150을 적용했다. 다른 신규/미설정 Tuning의 기본 제동은 기존 3을 유지한다.
- 점프 높이 4, 점프 컷 0.45, 코요테 0.10초, 입력 버퍼 0.13초는 유지한다. 상승 중력 48→64, 하강 중력 55→80, 최대 낙하 속도 28→32로 체공 시간을 줄였다. 같은 높이의 지형에 도달하도록 점프 초기 속도는 기존 공식으로 재계산한다.
- Side/Air 일반 검격은 준비·활성·복귀 전 구간에 이동·점프와 점프 해제를 허용한다. 지상 검격 중 이륙하거나 공중 검격 중 착지해도 이동 잠금이 생기지 않는다. 공격 방향과 타격 종류는 공격 시작 시 확정된 값을 유지하며, 다음 공격은 최신 이동 방향을 사용한다. 점프 1회 소비와 착지 버퍼 규칙을 그대로 사용해 공중 추가 점프를 허용하지 않는다.
- Lift/Emergence/Slam의 전용 이동, 글리치·잠복·구역 전환, 체력·피해·공격 단계 시간·콤보·넉백·히트스톱·피격 슬로모션은 변경하지 않았다. 강화된 일반 중력은 출현 후 상승 궤적에도 적용되므로 출현 초기 속도 자체는 같아도 체공 시간은 짧아진다.
- 할로우 나이트는 빠른 입력 반응의 방향성 참고이며 해당 게임의 내부 수치를 복제한 것이 아니다. 기본 이동과 전투 연결을 먼저 조정한 버전이다.- 검증: Unity 재컴파일 완료(컴파일 오류 없음). MovementRulesTests 9/9 통과. 전체 PlayMode 270개 중 269개 통과 후, 오래된 테스트용 선딜레이 때문에 빗나간 공중 검격 검사 1개의 타이밍을 실제 에셋 값에 맞췄고 해당 검사 재실행 1/1 통과. 전체 검사를 수정 후 다시 실행한 것은 아니다. 콘솔의 차단된 휴식 도착점·초기화 누락 오류 4건은 실패 경로 테스트가 의도적으로 기대하는 로그다. 수동 플레이 감각 평가는 별도이며 자동 검사로만 할로우 나이트와 같은 느낌이라고 단정하지 않는다.
## 2026-10-28 — 마우스 좌우 조준과 공격 이동 잠금

- 앞선 조작감 조정 중 ‘일반 공격 중 이동·점프 허용’은 최신 요청으로 대체했다. 평상시 이동·점프 수치는 그대로 유지한다.
- 유효한 마우스 조준과 Aim Camera가 있으면 커서 월드 위치와 플레이어 중심의 X 차이로 좌우를 결정한다. 위아래 각도는 사용하지 않고 중심 ±0.1유닛에서는 마지막 방향을 유지한다. 마우스 조준이 없으면 이동 방향을 사용한다. 글리치가 없는 구성도 조준을 지원한다.
- 준비·활성·복귀 전 구간에서 모든 공격의 수평 속도를 0으로 고정하고 새 점프와 점프 예약을 차단한다. 공중 일반 검격은 기존 수직 속도와 중력을 적용하며, 이미 시작한 점프의 키 해제는 높이를 줄인다. 출현·내려찍기의 전용 수직 이동은 유지한다.
- 현재 공격의 방향은 실행 시작 시 고정한다. 커서의 최신 방향은 별도로 계속 읽어 공격 종료 후 외형과 다음 공격에 반영한다. 예약 공격은 실행 순간의 조준 좌표를 다시 계산하고 이전 공격과 다음 공격 사이에 이동 잠금을 풀지 않는다.
- 글리치 후에는 도착 위치를 기준으로 커서 방향을 다시 계산한다. 유효 조준이 없는 경우 기존 도착 대상 바라보기를 유지한다. 피격·사망·비활성화와 기존 글리치 후딜레이 취소는 공격 상태를 종료하므로 별도 이동 잠금이 남지 않는다.- 검증: Unity 컴파일 오류 없음. 전체 PlayMode 272개 중 271개 통과. 남은 공중 잠금 검사는 공격 종료 후 정상 이동 재개 프레임과 가속 반전 시간을 고려하도록 검사 시점을 고쳤고, 최종 재실행 1/1 통과했다. 수정 후 전체 검사를 다시 실행한 것은 아니다. 휴식 이동 차단·초기화 누락 관련 콘솔 4건은 기존 실패 경로 테스트에서 기대하는 오류다. 씬·프리팹·프로젝트 설정은 변경하지 않았다.
## 2026-10-28 — 공격 실행 순간에만 마우스 방향 적용

- 상시 마우스 좌우 바라보기를 제거했다. 평상시에는 이동 방향을 바라보고, 정지 중에는 마지막 방향을 유지한다. 공격 종료만으로 커서 방향으로 돌아서지 않는다.
- 일반·공중·출현 및 예약 공격이 실제로 시작될 때만 마우스 좌우 방향을 판정한다. 공격 불가 시 눌린 입력은 방향을 바꾸지 않는다. 글리치 단독 이동은 기존 도착 대상 바라보기를 유지한다.
- 공격 중 수평 이동·새 점프 잠금, 방향 고정, 공중 중력과 점프 해제 처리, 중심 ±0.1유닛 여유는 유지한다.
- 검증: Unity 컴파일 오류 없음. PlayerGlitchCombatTests 122/122 통과(공격 시 방향 결정·정지 중 커서 무시·연속 공격·글리치·피격 취소 포함).

## 2026-10-28 — 일반 공격 이동 잠금 철회

- 최신 요청으로 Side/Air 일반 공격 중 수평 이동·점프를 다시 허용한다. 띄우기·출현·내려찍기의 전용 이동은 유지한다.
- 공격 실행 순간에만 마우스 좌우를 판정하고 현재 검격 방향은 종료까지 고정한다. 이동으로 공격 방향이 바뀌거나 상시 커서를 따라 돌아보지 않는다. 일반 공격 도중 점프 해제도 정상 처리한다.
- 검증: Unity 컴파일 오류 없음, PlayerGlitchCombatTests 122/122 통과.
