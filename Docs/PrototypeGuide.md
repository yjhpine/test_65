# 이동·점프 기초 — Movement Lab

## 전투 → 휴식 → 전투 (2026-10-25)

- `CoreLoopStage`는 웹 프로토타입의 기본 추천 전투 배치(`combat-course.js`, 2026-10-25 확인)에 맞춰 **2 → 3 → 4 → 5마리**, 총 4웨이브로 구성한다. 직선 사격은 포탑, 공중 포격은 드론, 지상 근접은 돼지로 대응하고 마지막 지상 적은 기존 방패병을 사용한다. 각 적의 기존 행동·체력·공격 설정은 유지한다.
- (2026-11-05 사용자 요청으로 적 구성이 3 → 4 → 5 → 6마리 혼합 배치로 바뀌었다. 맨 아래 같은 날 절을 따른다.) 웨이브 1 X=0~60: 포탑 2. 웨이브 2 X=72~110: 돼지·포탑·드론. 웨이브 3 X=122~162: 돼지·포탑 2·드론. 웨이브 4 X=174~217: 돼지·방패병·포탑 2·드론. 휴식 구역은 X=60~72, 110~122, 162~174이며 도착 X=66/116/168, 최종 출구는 X=215다.
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
- 글리치: 마우스로 적의 상·하·좌·우를 조준하고 Shift를 새로 누릅니다. 지형 벽은 통과하지만 닫힌 구역 문 너머, 최대 거리 밖, 화면 밖 대상과 도착 공간이 막힌 요청은 취소하고 이유를 표시합니다(2026-11-02).
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
- 출현 시 **현재 잠복 표시 지점 바로 위의 지상 공간**과 원래 몸 위치에서 그 지점까지의 닫힌 문을 검사합니다. 대상은 살아 있는 글리치 후보인지만 다시 보고, 잠복 뒤 대상이 바닥을 떠나거나 사거리·화면 밖으로 움직여도 출현을 막지 않습니다(2026-11-04 수정, 아래 같은 날 절). 입력이 없으면 X좌표를 유지하며 대상이 이동해도 따라가지 않습니다. 잠복 중 A/D·좌우 방향키는 몸체 대신 표시의 X좌표를 조절합니다. `GlitchTuning.asset`의 **Underground Move Speed=3**(월드 단위/초, 0이면 위치 조절 꺼짐)으로 속도를 설정합니다. Collider 전체가 같은 바닥 안에 출현할 수 있는 범위로 제한합니다. 장애물 아래로 표시를 움직일 수 있지만, 출현 불가한 위치에서는 표시가 빨간색이 됩니다. 주변 출현 후보 없음·대상 소멸·체류 시간 만료·Space 취소는 원래 몸체 위치로 복귀하며, 비활성화도 물리·피격·표시 상태를 복원합니다. 이동해도 2초 체류 시간과 쿨타임은 연장하지 않습니다. Space 취소가 이동·공격보다 우선하며, 이동과 공격을 함께 입력하면 해당 물리 단계의 이동을 반영한 지점에서 출현합니다. 지하 중 재글리치는 금지합니다.
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

잠복 출현 위치가 막혔을 때는 공격 입력 시에만 같은 바닥을 좌우로 탐색합니다. 현재 위치가 비어 있으면 그대로 사용하고, 막혔으면 0.05월드 단위 간격으로 가까운 후보부터 검사한 뒤 처음 발견한 빈 공간의 경계를 6번 세분화합니다(경계 보정 오차 0.001 미만). 양쪽 거리가 같으면 마지막 잠복 이동 방향, 이동이 없으면 진입 전 몸체가 있던 쪽을 우선합니다. 아주 좁은 빈 중심 좌표 구간이 탐색 간격보다 작으면 놓칠 수 있으므로 0.05는 탐색 해상도이며 수학적으로 모든 연속 좌표의 최솟값을 보장하지는 않습니다. 바닥 끝·몸체 전체 공간 검사는 유지합니다. 성공 시 보정 지점에서 출현 공격을 시작하며 쿨타임을 다시 시작하지 않습니다. 같은 바닥에 후보가 없거나, 대상이 죽거나 비활성화됐거나, 원래 몸 위치와 출현 지점 사이에 닫힌 문이 있으면 진입 전 몸체 위치로 복귀합니다. 대상의 현재 위치(바닥·사거리·화면)는 출현 때 다시 보지 않습니다(2026-11-04).

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
6. Entry Area의 BoxCollider2D 위치·크기를 편집한다. World Mode=Pause는 세계 정지, Continue는 조작만 잠그며 적과 중력이 계속 작동한다. Continue에서도 피해를 받으면 즉시 중단한다. Pause 연출을 공중에서 시작하면 조작을 잠근 채 착지를 기다린 뒤 세계를 정지한다(상태 Landing). 착지 대기 중 피격은 기존과 같이 중단한다. Max Landing Wait(기본 1.5초, 0은 대기 없음)가 지나도 착지하지 못하면 그 자리에서 시작한다.
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

## 전역 VFX·SFX 관리 (2026-10-29)

- `EffectsRuntime`이 플레이 시작 시 `Global presentation` 루트와 `VfxManager`/`SfxManager`를 생성한다. 루트는 DontDestroyOnLoad이며 별도 씬 배치가 필요 없다. SubsystemRegistration에서 정적 접근 상태를 초기화하고 종료 중에는 재생성하지 않는다.
- 캐릭터 표시 계층이 공격·글리치·사격 버전, 이동/접지 변화와 공격 교대를 관찰해 요청한다. 피해/공격 판정, 물리, FSM, 카메라/시간 연출, 피격 셰이더와 외형 기울기는 기존 담당에 남는다.
- PlayerVisual의 Vfx Profile은 `Data/Presentation/PlayerVfx.asset`, Sound Effects는 기존 `Audio/Combat/PlayerSfx.asset`이다. EnemyVisual2D의 Vfx/Sfx Profile은 `Data/Presentation/`의 적별 에셋이다. EnemyLaserVisual은 설정을 중복 보관하지 않는 요청 어댑터다.
- 관리자의 RegisterOwner가 발급한 EffectOwnerHandle은 관리자 인스턴스/슬롯/세대 번호를 포함한다. ResetOwner는 같은 세션의 재생을 지우고, ReleaseOwner는 세션을 해제한다. 이전 세대 핸들의 요청은 무시한다.
- 플레이어별 slash 1, 글리치 경로 spark 16, 구역 이동 spark 8, 접촉 슬롯 32, 먼지 8을 유지한다. 적은 레이저 두 방향분과 기존 예고 표시를 가진다. 같은 프로필·재질·정렬 설정의 해제된 VFX 뱅크를 재사용한다.
- SFX는 플레이어별 6, 사격 적별 2 AudioSource를 유지하며 같은 소유자 안에서만 순환 교체한다. 플레이어는 2D, 적은 기존 spatialBlend 0.85/거리 10~38/우선순위 150을 유지한다. 플레이어 우선순위는 100이며 AudioListener.pause/volume을 존중한다.
- 관리자 LateUpdate는 표시 요청자 뒤(실행 순서 10000)에 처리한다. 현재 프레임에 요청되지 않은 지속형 공격/레이저는 숨긴다. 접촉·먼지의 비스케일 소멸과 공격/레이저의 전투 진행률 동기화는 유지한다.
- 모든 효과 객체는 전역 루트의 세션 아래에 있다. 소유자 비활성화/파괴 때 재생을 정리하고, 씬 언로드 때 해당 씬 세션과 사용하지 않는 뱅크를 회수한다. 다른 씬의 활성 세션은 유지한다. 테스트에서 GetSessionRoot(handle)로 해당 소유자의 표시/오디오 객체만 확인할 수 있다.

### 카메라 크기 고정 기준 (2026-10-29)
- CoreLoopStage의 일반 플레이 카메라 Orthographic Size는 사용자 지정값 **8**로 고정한다. 컷신 중 기존 줌은 유지하고 완료·취소·스킵 후에는 8로 복원한다. 카메라 경계 검사의 화면 반폭도 8 × aspect를 사용한다.
- Timeline 미리보기 테스트는 IntroConversation의 실제 SpeechBubbleTrack 클립 내부 시점을 사용한다. 대사 편집으로 클립이 이동해도 오래된 1.9초/2.3초를 대사 시점으로 가정하지 않는다.
- 에디터 미리보기는 카메라 소유권 플래그와 Orthographic Size를 AnimationMode의 복원 항목에 함께 등록한다. Timeline을 닫으면 다음 Editor update를 기다리지 않고 미리보기 전 크기로 복원한다. 런타임 컷신 재생 코드는 변경하지 않는다.

## PixelFX combo slash (2026-11-01)

Normal Side/Air attacks use PlayerVfxProfile.comboSlash1Frames/2Frames/3Frames selected by the actual PlayerAttackPresentation.Strike, independently of the character animation A/B alternation. Each strip is rendered independently, so it cannot contain the next attack or a previous attack tail. Missing optional strips fall back to BasicAttackEffectFrames. Lift/Emergence/Slam retain the original white slash and their existing tint/orientation.

Generated cyan/red source colors are preserved with white per-renderer palette overrides; shared material and teleport sparks remain unchanged. Existing phase progress drives windup/active/recovery frames and Ready, reaction cancellation and underground state hide the renderer. Existing size 2.5 and local offset are retained; generated frames preserve their aspect ratio.

Regenerate with node Tools/PixelFx/pipeline.mjs render custom/glitch-slash-1 (repeat for 2/3), then Game > Effects > Import Generated Glitch Combo. The explicit importer edits only generated textures and the three PlayerVfx profile arrays, preserving existing GUIDs on reimport. No damage, range, combo rule, movement, cooldown, hit-stop, or attack phase duration changes.

## 2026-11-02 — 공격 범위·검격 2배와 플레이어 중심 정렬

- `PlayerCombatTuning.asset`의 Reach/Width를 1.2/1.6에서 2.4/3.2로 2배 키웠다. 옆·공중 공격, 띄우기, 출현, 내려찍기 하강 전방 판정과 띄우기 자동 선택 거리가 모두 이 값을 쓴다. 착지 충격파 반경(2)은 별도 값이라 유지했다. 코드 기본값(새 Tuning 에셋)은 1.2/1.6 그대로다.
- `PlayerVfx.asset`의 Basic Attack Effect Size를 2.5에서 5로 키웠다. 3타 검격(PixelFX)과 띄우기·출현·내려찍기 흰 검격에 모두 적용된다.
- 검격 위치: 기존에는 앞쪽 0.65·위쪽 0.3(띄우기 0.15/0.55, 내려찍기 0.3/-0.35)만큼 띄워 놓았다. 이제는 피벗을 플레이어 Visual 중심에 둔다. PixelFX 원화는 피벗(0.4, 0.5)이 캐릭터 자리이고, 흰 검격은 칸 중앙 피벗 바깥쪽에 호가 그려져 있다. 그래서 일반 공격은 앞, 띄우기는 위, 내려찍기는 아래에 자연스럽게 놓이며 커진 이펙트가 위로 솟지 않는다.
- 공격 타이밍·피해·넉백·콤보 규칙·글리치 이펙트(2.5)는 바꾸지 않았다.

## 2026-11-02 — 순간이동 판정: 벽 관통·닫힌 문·화면 밖·사용 불가 이유

- 기획서 v0.3의 3단계. 대상 판정(`GlitchUtility.TargetBlock`)은 최대 거리 → 화면 밖 → 닫힌 문 순서로 검사한다. 지형 시야 조건은 없앴고, 경로(플레이어 중심 → 대상 중심)에 걸린 충돌체 중 `IGlitchBarrier`가 막는다고 답한 것만 막는다. 현재는 닫힌 `StageGate` 장벽만 해당한다(열린 문은 Collider가 꺼져 있어 통과).
- 화면 밖: 대상 몸 중심이 PlayerUnit의 Aim Camera 화면 밖이면 사용할 수 없다. Aim Camera가 없으면 이 검사는 건너뛴다.
- 커서 근처(Aim Assist Radius)의 사용할 수 없는 대상도 선택해 빨간 도착 표시와 이유를 보여준다. 사용 가능한 대상이 있으면 그쪽이 우선이다. 실행은 사용 가능한 경우에만 된다.
- 이유(`GlitchPreview.Reason`): OutOfRange / OffScreen / GateClosed / NoSpace(도착 공간 막힘) / Cooldown / Busy(공격·피격 중). StageFlowView가 Busy를 뺀 이유를 도착 표시 위에 짧은 영문 문구(OUT OF RANGE, OFF SCREEN, GATE CLOSED, NO SPACE, COOLDOWN)로 띄운다. 조작 잠금·컷신 중에는 숨긴다. StageFlowController가 없는 MovementLab에는 문구가 없다.
- 플레이어 공격·충격파가 벽 너머에 닿지 않는 규칙(`UnitPhysics2D.HasSight`)과 잠복·출현, 거리 8, 쿨타임 0은 바꾸지 않았다. 잠복 중 출현 가능 판정도 같은 대상 판정을 쓰므로 벽 너머 대상에게도 잠복·출현이 된다. (2026-11-04부터 출현은 대상 판정을 다시 하지 않고 대상 생존과 닫힌 문만 본다. 잠복 진입은 그대로 이 판정을 쓴다.)

## 2026-11-02 — 기획서 v0.3 4~7단계: 공격 선택·강공격·적 반응

- 공격 종류: 공격이 시작되는 순간 사거리 안(Reach + 몸 크기)의 적으로 정한다. 전달된 대상(마지막 글리치 대상)이 사거리 안이면 그 적을 쓰고, 아니면 시야가 열린 가장 가까운 적을 쓴다. 적 아래(적의 발이 머리 위)면 띄우기, 공중에서 적 위(플레이어 중심이 적 머리 위)면 내려찍기, 그 밖에는 지상 일반/공중 일반 공격이다. 공중에서도 띄우기가 나간다. '위쪽 글리치 후 첫 공중 공격만 내려찍기' 규칙은 없앴다. 출현은 지상에서만이다.
- 이동: 지상 일반 공격 중에는 수평 이동과 점프를 막는다(10-28의 이동 허용 철회). 공중 일반 공격은 이동·중력을 유지한다.
- 공격 간격: 글리치는 후딜레이를 취소하지 않는다. 공격 중 언제 누르든 다음 공격 1개를 예약해 Ready에서 실행한다(Attack Buffer Time 0이면 예약 끔). 수치(선딜/판정/후딜)는 바꾸지 않았다. 2026-11-05부터 순간이동 입력도 예약된다(아래 같은 날 절).
- 띄우기: 명중(피해 > 0)했을 때만 플레이어도 Launch Speed로 함께 상승한다. 방패 등으로 막히면 상승하지 않는다.
- 내려찍기: 하강 판정이 플레이어 아래 칸(Width × Reach)으로 바뀌었다. 기본 내려찍기는 착지 충격파가 없고, 3타(마무리) 내려찍기만 충격파를 낸다.
- 적 공격 끊기: 띄우기·내려찍기·출현·충격파 명중은 `UnitHealth.InterruptVersion`을 올린다. 순찰 돼지는 공격 중 일반 공격을 맞아도(가벼운 넉백 포함) 공격을 계속하고, 끊는 타격에만 피격 경직으로 넘어간다. 방패병·포탑·드론은 준비/조준 고정/돌진 중 끊는 타격을 맞으면 회복 상태로 넘어간다. 일반 공격은 기존처럼 준비를 초기화하지 않는다.
- 3타 강공격: 세 공격이 하나의 타수를 공유하고, 실행한 공격마다(선딜레이가 끝날 때) 타수를 1 올린다. 빗나감은 타수에 포함한다(기획서의 미정 항목, 사용자 결정으로 실행 기준). 방패 차단은 이때 포함했지만, 2026-11-03 방패병 기획서 v1.8부터 한 공격에서 하나라도 막히면 그 공격 끝에 타수를 1로 초기화한다. 피격·사망·구역 이동은 초기화한다. 글리치·출현은 살아 있는 타수의 유지 시간을 다시 채운다. 3타는 종류와 관계없이 피해 × Finisher Damage Multiplier(기본 2), 강한 띄우기는 상승 속도 × Finisher Launch Multiplier(기본 1.5, 플레이어와 적 모두), 강한 내려찍기는 착지 충격파, 강한 일반 공격은 기존 강한 넉백을 쓴다. 강한 띄우기·내려찍기 검광은 1.3배·밝게, 3타 휘두르기는 무거운 효과음이다.
- 타수 표시: 스테이지 HUD 왼쪽 아래 COMBO 칸(타수만큼 청록), 다음이 강공격이면 마지막 칸과 STRONG NEXT가 주황으로 켜진다. 조작 잠금·컷신 중에는 숨긴다.
- 테스트 도구: Player/Visual의 Show Glitch Range를 켜면 플레이어 중심의 글리치 최대 거리 원을 표시한다. 거리는 Play Mode 중 GlitchTuning.asset Inspector에서 바로 바꿀 수 있다(에셋에 그대로 저장되므로 끝나면 되돌린다). 웹 프로토타입의 슬라이더·브라우저 저장·자동 일시정지는 Inspector와 에디터 일시정지로 대신한다.
- 바꾸지 않은 것: 피해·체력 체계(10/50~90), 공격 타이밍 수치, 거리 8, 잠복·출현, 각 속도 수치(px 환산 미정).

## 2026-11-02 — 강공격 장전 오라

- 다음 공격이 3타 강공격일 때(`PlayerCombat.FinisherReady`: 콤보 길이 2 이상이고 실행한 타수 = 길이 − 1) 플레이어 뒤에 청록·빨강 글리치 오라를 반복 재생한다. 강공격 선딜레이 동안 유지되고, 강공격이 실행되거나 타수가 만료·초기화되면 사라진다. 잠복 중·사망·피격 초기화 때는 숨긴다. HUD의 STRONG NEXT와 같은 조건이다.
- 원화: `Tools/PixelFx/effects/finisher-aura.mjs`(PixelFX 자체 효과, 1.2초 루프, 30fps, 36프레임, 60×64). 플레이어 원화와 같은 18 PPU 격자로 그렸고 피벗(0.5, 0.28125)이 Visual 중심이다. 몸을 감싸는 불꽃 외곽(측면 파형이 위로 흐름), 머리 위 불꽃 끝 3개, 위로 흩어지는 빛줄기, 빨간 채널 어긋남·글리치 조각, 발밑 고리로 구성된다.
- 표시: `PlayerActionEffects`의 `Finisher aura` 렌더러가 `PlayerVfxProfile.finisherAuraFrames / finisherAuraFps`를 비스케일 시간으로 재생한다. 몸과 같은 정렬 순서에 z만 0.01 뒤로 두어 지형(0) 위·플레이어 뒤에 그린다. 장전될 때마다 루프 첫 프레임부터 0.12초 동안 페이드 인하고 바라보는 방향으로 좌우 반전한다. 원화 색은 흰색 팔레트 덮어쓰기로 그대로 쓴다.
- 다시 만들기: `node Tools/PixelFx/pipeline.mjs render custom/finisher-aura` 후 `Game > Effects > Import Generated Finisher Aura`. 가져오기는 `Art/Effects/PixelFxFinisherAura`의 프레임과 PlayerVfx의 오라 배열만 갱신한다. 전투 수치·콤보 규칙은 바꾸지 않았다.

## 2026-11-03 — 글리치 조준 표시를 PixelFX 글리치 마커로 교체

- 조준 중 적의 상하좌우 도착 지점에 표시하던 원(채움 + 테두리, 0.8유닛, 청록/빨강 색만 다름)을 PixelFX로 만든 글리치 마커 애니메이션으로 바꿨다. 사용 가능 판정·위치·지하 표시 높이 보정(+0.04)·숨김 조건은 그대로다.
- 원화: `Tools/PixelFx/effects/glitch-marker.mjs` 하나에서 `glitch-marker-ready`(청록, 맥동하는 마름모 코어, 위→아래 스캔선)와 `glitch-marker-blocked`(빨강, 깜박이는 X 코어, 더 잦은 좌우 흔들림·찢김)를 만든다. 공통으로 숨 쉬듯 1px 줄었다 커지는 모서리 괄호, 다른 색 채널이 1px 어긋나는 RGB 분리, 괄호 하나가 옆으로 찢기는 프레임, 옆의 글리치 조각, 주변 픽셀 노이즈, 옅은 내부 판이 있다. 0.8초 루프 24프레임, 32×32, 플레이어 원화와 같은 18 PPU, 중앙 피벗(괄호 상자 약 0.9유닛). 색뿐 아니라 코어 모양(마름모/X)으로도 구분된다.
- 표시: `PlayerVisual`의 `Glitch preview` 렌더러가 `PlayerVfxProfile.glitchMarkerReadyFrames / glitchMarkerBlockedFrames / glitchMarkerFps(30)`를 비스케일 시간으로 재생하고 원래 크기로 그린다. 이때 `Glitch preview ring`은 끈다. 프로필 배열이 비어 있으면 기존 원 표시로 돌아간다.
- 다시 만들기: `node Tools/PixelFx/pipeline.mjs render custom/glitch-marker-ready`, `... custom/glitch-marker-blocked` 후 `Game > Effects > Import Generated Glitch Marker`. `Art/Effects/PixelFxGlitchMarker/Ready·Blocked`의 프레임과 PlayerVfx의 두 배열만 갱신한다. 글리치 규칙·거리·쿨타임은 바꾸지 않았다.

## 2026-11-03 — 도착 공간이 막힌 글리치의 NO SPACE 문구 제거

- 사용자 요청. 도착 지점이 막혀 글리치를 쓸 수 없을 때(`GlitchBlockReason.NoSpace`) 표시 위에 띄우던 `NO SPACE` 문구를 없앴다. Busy처럼 빨간 마커만 보인다. OUT OF RANGE, OFF SCREEN, GATE CLOSED, COOLDOWN 문구와 NoSpace 판정 자체(실행 불가, GlitchPreview.Reason)는 그대로다.

## 2026-11-03 — 타격감 보강 6가지

사용자 요청(조사 후 추천안 1~6). 기존 사용자 조정 수치(타이밍·넉백·피해·카메라 8)는 바꾸지 않았고, 새 값은 `PlayerCombatTuning`의 코드 기본값으로 들어간다(에셋에 필드가 생기기 전까지 기본값 사용). 각 값은 0으로 끌 수 있다.

1. **처치 강조**: 처치한 휘두름의 히트스톱에 `Kill Hit Stop Bonus`(0.04초)를 더한다(기본 히트스톱이 켜져 있을 때만). 현재 적 그룹(구역/목표)의 마지막 적이 쓰러지면 `StageFlowController.UpdateObjective`가 `PlayerUnit.PresentFinalBlow`를 호출하고, `PlayerImpactFeedback.PlayFinalBlow`가 그 타격의 히트스톱이 끝난 뒤 `Final Blow Slow`(0.3배 0.2초 + 0.15초 복귀)를 건다. 플레이어 피격 슬로가 우선한다. 시간 효과가 끝날 때까지 최종 출구 연출은 기존처럼 기다린다.
2. **적 떨림**: `UnitDamageFlash`가 피해를 받은 유닛의 Visual(유닛 루트의 직계 자식)만 1 원화 픽셀씩 좌우로 떤다. 최소 0.05초, 히트스톱으로 세계가 멈춘 동안은 최대 0.2초까지 이어지고 끝나면 원래 위치로 돌아온다. 몸체·물리는 움직이지 않으며, 포탑 머리·받침대처럼 Visual 아래 부품은 Visual을 따라간다. 허수아비는 이 컴포넌트가 없어 떨지 않는다.
3. **효과음 피치**: `SoundCue`/`ShortSoundPool`에 피치를 추가했다. 일반 휘두름은 타수마다 +5%(1타 1.0, 2타 1.05)에 ±4% 무작위, 강공격·특수 휘두름은 0.97 ±3%(강공격) 또는 타수 상승 ±3%, 명중음 ±6%, 처치·내려찍기 무거운 명중음 0.97 ±3%. 적 효과음은 그대로 1.0이다.
4. **파편·파괴 이펙트(PixelFX)**: `custom/glitch-hit-debris`(0.45초 15프레임, 56×44, 피벗=접촉점, 오른쪽 방향)는 일반 명중마다 맞은 자리에서 앞쪽으로 튀어 떨어지는 청록·빨강 파편·데이터 블록이고, 접촉 이펙트(0.10초)보다 오래 남는다. 공격 방향이 왼쪽이면 좌우 반전한다. `custom/glitch-shatter`(0.6초 19프레임, 80×72, 피벗=몸 중심)는 처치 때 대상 몸 중심에서 흰 선택 상자 → 데이터 블록 폭발 → 찢김 선 → 픽셀 비가 이어진다. 둘 다 18 PPU 원래 크기, 비스케일 시간이며 `PlayerContactEffects`의 `Contact burst` 렌더러가 재생한다(대상 정렬 순서 +1). PlayerVfx `hitDebrisFrames/killShatterFrames/contactBurstFps(30)`, 가져오기는 `Game > Effects > Import Generated Contact Bursts`.
5. **3타 글리치 화면**: 콤보의 강공격이 피해를 주면(`PlayerImpact.Finisher`) `Finisher Glitch Duration`(0.1초, 앞 절반 최대 후 감소) 동안 피격 화면 표면(`PlayerDamageScreen`, 같은 셰이더에 `_Glitch/_GlitchSeed` 추가)이 화면 위에 청록·빨강 찢김 띠, 깜박이는 블록, 양옆 색 분리, 스캔선을 그린다. 1/30초마다 무늬가 바뀐다. 장면을 다시 샘플링하지 않는 오버레이이며 렌더 파이프라인·카메라 후처리 설정은 바꾸지 않았다. 감소된 움직임(reduced motion)과 컷신 카메라 억제 중에는 나오지 않는다.
6. **흔들림 떨림**: 내려찍기·충격파·처치 흔들림은 기존처럼 한 번 밀린 뒤 `Shake Frequency`(22Hz, 이전에는 미사용)로 앞뒤로 떨며 줄어든다. 일반·띄우기 명중과 플레이어 피격은 한 번 밀림 그대로다.

## 2026-11-03 — 강공격 장전 오라 제거

- 사용자 요청. 11-02에 추가한 3타 장전 오라를 없앴다. `PlayerActionEffects`의 `Finisher aura` 렌더러, `PlayerVfxProfile.finisherAuraFrames/finisherAuraFps`, 오라 전용 상태(`IPlayerActionState.FinisherReady`, `PlayerUnit.FinisherReady`, `PlayerCombat.FinisherReady`), `Game > Effects > Import Generated Finisher Aura` 메뉴, `Art/Effects/PixelFxFinisherAura` 프레임과 `Tools/PixelFx/effects/finisher-aura.mjs`를 지웠다. 위 11-02 오라 절은 과거 기록이다.
- 다음 공격이 강공격이라는 표시는 HUD의 COMBO 칸·STRONG NEXT만 남는다. 강공격 성능, 3타 검격·무거운 효과음, 11-03 타격감 보강(3타 글리치 화면 포함)은 그대로다.

## 2026-11-03 — 공격 반발력(플레이어 밀림)

- 사용자 요청. 일반 공격(지상 Side·공중 Air)이 실제 피해를 주면 그 휘두름에 한 번, 플레이어가 공격 방향(시작 때 고정한 Facing)의 반대로 살짝 밀린다. 빗나감·방패 차단(피해 0)·띄우기·내려찍기·충격파·출현에는 없다.
- 값(`PlayerCombatTuning` "Attack recoil", 코드 기본값, 0이면 끔): 일반 `Recoil Speed` 4유닛/초 → `Recoil Duration` 0.1초 동안 선형 감소(약 0.2~0.24유닛), 3타 강공격 `Finisher Recoil Speed` 7 / `Finisher Recoil Duration` 0.12초(약 0.4유닛), 공중 `Air Recoil Multiplier` 0.6배. 기존 사용자 조정 값은 바꾸지 않았다.
- 흐름: `PlayerCombat`이 피해를 준 순간 반발을 계산해 두고(`ConsumeRecoil`), `PlayerUnit`이 같은 물리 단계에 받아 다음 단계부터 적용한다. 히트스톱 동안은 물리가 멈추므로 "멈춤 → 밀림" 순서가 된다. 반발이 남은 동안은 지상 공격의 이동 잠금이나 입력 이동 대신 `CharacterMotor2D.SimulateRecoil`(수평 속도만 지정, 중력·접지는 기존과 같음)을 쓴다. 벽은 물리로 막히고, 지상에서는 `HasGroundAhead`로 발밑이 끝나면 그 자리에서 멈춰 발판에서 떨어지지 않는다.
- 취소: 글리치·잠복·출현·내려찍기 준비/하강·조작 잠금(구역 이동·컷신)·피격·사망·비활성화.
- 연속 3타면 합쳐서 약 0.8유닛 물러난다. 사거리 2.4라 콤보는 이어지지만 적 넉백과 합쳐 간격이 이전보다 벌어진다.

## 2026-11-03 — CoreLoopStage 발판 배치 변경 반영

- 사용자가 CoreLoopStage의 발판 17개(`Terrain/Ledge 1~3`, `Wave 2 Ledge 1~4`, `Wave 3 Ledge 1/3/4/5`, `Wave 4 Ledge 1~6`)와 Ledge 2 위 감시 포탑 위치를 옮겼다(대부분 더 높게). 앞 절에 적힌 발판 좌표는 과거 기록이다. 현재 Ledge 1/2/3은 (26.3, 5.05), (37.31, 5.62), (48.03, 3.65)이고, Ledge 2의 포탑은 (37.31, 6.44)다.
- 이 배치를 고정값으로 검사하던 테스트는 `StageFlowTests.StartsPlayableWithBalancedMixedEnemiesAndLockedExit`의 Ledge X 한 줄뿐이며, 새 값(26.3/37.31/48.03, 허용 오차 0.001)으로 바꿨다.
- 고정형 적(감시 포탑)도 새 발판에 맞췄다. 포탑은 지지면 위 중심 높이 0.62(바닥 간격 0.02)로 서고, 원래 서 있던 발판이 옮겨진 만큼 같이 옮겼다(사용자가 옮긴 Ledge 2 포탑과 같은 방식). Encounter 2: (88.5, 3.62) → (89.30, 5.77, Wave 2 Ledge 3), Encounter 3: (138.5, 3.62) → (138.80, 4.17, Wave 3 Ledge 3), Encounter 4: (189, 3.62) → (188.61, 4.36, Wave 4 Ledge 3)과 (195.25, 6.62) → (196.96, 8.24, Wave 4 Ledge 4). 바닥 포탑 (29, 0.62)·(144.5, 0.62)은 그대로다.
- 드론은 비행 유닛이라 옮기지 않았다. Encounter 2 드론(83.75, 7.63)은 올라간 Wave 2 Ledge 2 윗면과 0.18 간격, Encounter 4 드론(185.75, 8.63)은 Wave 4 Ledge 2와 1.28 간격이 됐다(이전 1.63).

## 2026-11-03 — 방패병 기획서 v1.8 반영 (전면형·돌진형)

사용자 요청으로 `GLITCH · 방패병 기획서 v1.8`을 추천 결정대로 반영했다. 체력·피해 체계(방패병 75, 플레이어 공격 10, 방패병 공격 14), 플레이어 넉백·피격 무적(기획서 D항목), 활성 영역 즉사 규칙(A07)은 이번 범위에서 뺐다. 거리는 1유닛 ≈ 47.5px(v0.3 순간이동 380px = 8유닛)로 환산했고 시간 값은 그대로다.

- **방패 판정(`EnemyCombat2D.BlocksDamage`)**: 방패가 켜져 있으면 공격하는 쪽 중심이 방패 앞쪽이면 높이와 상관없이 막는다(앞쪽 위·아래 대각선 포함). 뒤쪽과 바로 위·아래(공격자 중심 X가 몸 기둥 `Guard Direct Band` 0.5 안)는 통과한다. 띄우기·내려찍기·출현·충격파도 일반 베기와 같은 위치 판정을 받는다(이전에는 무조건 통과). 공중 피격 중(`Reacting`)과 사망 중에는 방패가 꺼진다.
- **막힘**: 피해 0, 띄우기·내려찍기 효과·넉백·반발력 없음. `UnitHealth.GuardBlockVersion`이 막힘을 알리고, `PlayerCombat`은 한 공격에서 하나라도 막히면 그 공격 끝에 타수를 1로 되돌린다(다른 적 명중으로 복구하지 않음).
- **공통 행동(두 종류 모두)**: 걷기 추격 없음. 뒤로 가도 바로 돌지 않고 예고를 시작할 때 플레이어 쪽으로 방향을 정한다. 인식(거리 13·높이 2.8·시야·플레이어 화면 안) 후 0.2초 기다린 뒤 예고 0.4초, 예고 시작 간격 최소 5초. 예고 중 인식을 잃으면 취소하고 간격은 그 예고부터 센다. 회복 중에도 방패 유지. 일반 타격에는 넉백되지 않는다(`GroundMovement2D.Ignore Knockback`, 띄우기·내려찍기 이동은 유지).
- **피격 반응**: 살아 있는 방패병이 유효한 띄우기·내려찍기(끊는 타격)를 맞으면 하던 공격을 취소하고 `Reacting`(방패·공격 꺼짐, Hit 자세 유지)으로 간다. 강제 이동이 끝나고 착지하면 방어를 다시 켜고 Idle로 돌아가며, 취소된 공격은 이어 가지 않는다.
- **돌진형(`ShieldSoldier`, `Shield Attack = Rush`)**: 예고 때 고정한 방향으로 최대 5.5까지 18.9/초 직선 돌진(약 0.29초), 벽·발판 끝에서 짧아지고 플레이어 위치에서 멈추지 않는다. 접촉 피해는 돌진당 1회이고 맞혀도 계속 간다. 회복 0.65초, 도착 자리에 머문다.
- **전면형(`ShieldFront`, `Shield Attack = Strike`)**: `ShieldSoldier` 프리팹의 변형(같은 몸·원화·방패), `ShieldFrontDefinition`(enemy.shieldfront)·`ShieldFrontFsm`. 플레이어가 수평 2.9 이내면 예고 0.4초 → 몸 앞면부터 2.3 범위를 한 번 판정(플레이어 몸·지형 시야 확인, 피해 14) → `Strike` 0.12초(공격 클립) → 회복 0.5초. 자리를 옮기지 않는다.
- **외형**: 예고 중 공격 범위(전면 타격 거리 또는 돌진 경로)를 표시한다. 2026-11-05부터는 바닥 주황 선 대신 몸 높이 전체를 덮는 공용 빨간 위험구역이다(아래 같은 날 절). 방패 막대는 방패가 켜져 있을 때만 그린다.
- **배치**: CoreLoopStage 4웨이브 방패병은 돌진형 그대로다. MovementLab `Enemy Pattern Samples`에 전면형 1마리를 (-5, 0.82)에 추가했다.
- 바뀐 필드: `EnemyFsmDefinition`에서 걷기·회전·돌진 최소 거리·지나침·옛 방패 높이 값(`approachDistance/Speed`, `turnDuration`, `dashMinDistance`, `dashOvershoot`, `guardFrontThreshold`, `guardHeight`)을 없애고 `shieldAttack`, `recognitionDelay`, `attackInterval`, `strikeTriggerDistance`, `strikeReach`, `strikeDuration`, `guardDirectBand`를 추가했다.

## 2026-11-03 — 플레이어 명중 효과음 교체

- 사용자 요청. 기존 명중음은 Kenney 펀치 소리 한 겹(맨주먹 느낌)이었다. 검으로 기계 적을 베는 느낌이 나도록, 이미 받아 둔 같은 CC0 팩(Impact Sounds·Sci-fi Sounds)에서 세 겹을 섞었다. 새 다운로드는 없다.
- 일반 명중 `HitA/HitB/HitC`(0.2초): 펀치(무게, 3.2kHz 저역 통과) + 짧은 금속 틱(날, 1.4kHz 고역 통과 0.1초) + 비트크러시한 크런치 0.07초(글리치 질감). 3종을 차례로 돌려 쓴다(기존 2종 → 3종, `PlayerSfx.asset`의 Hits에 HitC 추가).
- 무거운 명중 `HitHeavy`(0.32초, 처치·내려찍기·충격파): 무거운 펀치 + 금속판 충격 + 크런치 0.24초. 일반 명중보다 낮고 무겁게 맞췄다(스펙트럼 중심 약 0.7kHz, 일반 약 1kHz, 기존 펀치 약 0.5~0.7kHz).
- 음량: 체감 음량(LUFS)을 기존 클립과 ±0.6 이내로 맞춰 `Hit Volume`(0.42)·피치 변화·0.04초 합치기 규칙은 그대로다. HitA/HitB/HitHeavy는 같은 파일을 덮어써 GUID를 유지했다.
- 재생성: `python -I Library/SfxDownloads/PrepareSfx.py`(프로젝트 루트에서). 히트음 레시피가 이 스크립트에 들어 있고, 다른 클립은 같은 결과(바이트 동일)로 다시 만들어진다. 원본 대응은 `Audio/Combat/Sources.json`.

## 2026-11-04 — 보스 AI 구조 (FSM + 패턴 선택 트리)

보스는 모든 보스가 같은 FSM(큰 행동 방식)을 쓰고, 보스마다 다른 것은 설정 값·선택 트리·패턴 에셋이다. 설계 근거는 `Docs/superpowers/specs/2026-11-04-boss-ai-design.md`다. 코드는 `Assets/_Game/Runtime/Units/Bosses`(네임스페이스 `ActionPlatformer.Units.Bosses`), 편집기 코드는 `Assets/_Game/Editor/Bosses`에 있다. 기존 `Unit`·`UnitHealth`·`FsmRuntime`·적 코드·`StageFlowController`는 바꾸지 않았고, `PlayerUnit`에는 `ICharacterMotionState` 전달만 추가했다. 이번에는 어느 씬에도 보스를 배치하지 않았다.

### 구성

- `BossFsmDefinition`(에셋): 상태 설정. 선택 트리(`selector`), 인식 거리·높이, 선택 주기(0.2초), 접근 속도·선호 거리, 경직 시간·면역, 체력 구간 경계(`phaseThresholds`, 엄격한 내림차순, 0~1 사이), 구간 전환 시간·무적, 사망 시간, 공통 애니메이션 이름(Idle/Run/Hit/PhaseShift/Death), 고정 시드.
- 상태: Dormant → Approach → Telegraph → Execute → Recover → Approach … 경직(Staggered)·구간 전환(PhaseShift)·사망(Death). 우선순위는 사망 → 경직 → 현재 상태 진행이다. 구간 전환은 패턴·경직이 끝난 뒤에 들어가며 끼어들지 않는다.
- 경직은 끊는 타격(띄우기·내려찍기·출현·충격파, `InterruptVersion` 증가)만 일으킨다. 패턴 정의의 `interruptibleDuringTelegraph`(기본 true)·`interruptibleDuringExecute`(기본 false)가 끊을 수 있는지를 정한다. 경직 면역(`staggerImmunity`)은 경직이 끝난 시점부터 센다(첫 경직은 항상 허용).
- `BossBlackboard`(개체별): 대상, 거리, 플레이어 상태(지상·공중·잠복·공격 중·최근 글리치·화면 안), 체력 구간, 패턴 이력·쿨타임, 개체별 난수. 쿨타임은 예고를 시작할 때 센다. 플레이어 정보는 `ICharacterMotionState`·`IPlayerActionState`·`IUnitRepositionState`·`IUnitViewSource`로만 얻고 `PlayerUnit`을 직접 참조하지 않는다.
- `BossPatternSelector`(에셋): 선택 전용 트리. 한 번 평가해 패턴 하나(또는 없음)를 돌려준다. 노드는 `[SerializeReference]`로 에셋 안에 중첩 저장된다.
- 패턴: `BossPatternDefinition`(에셋, 수치) → `CreateRun`이 개체별 `BossPatternRun`을 만든다. 현재 `BossMeleeAreaPattern`·`BossChargePattern`·`BossRepositionPattern`·`BossLeapSlamPattern`·`BossSummonPattern` 5종이 있다.
- `BossCombat2D`(대상 찾기·상자/휩쓸기 판정·방어 위임), `BossVisual2D`(`IBossActionState`를 읽어 애니메이션·방향·예고 상자 표시. 게임플레이를 바꾸지 않는다. Animation Event 없음).

- 애니메이션 진행: `BossVisual2D`는 `IBossActionState.Progress`를 Float `ActionTime`에 쓴다. Telegraph·Execute(패턴 진행), Recover(`recovery`), Staggered(`staggerDuration`), PhaseShift(`phaseShiftDuration`), Death(`deathDuration`)가 모두 0→1로 오르므로 Motion Time으로 재생하는 클립을 쓸 수 있다. Dormant·Approach는 0이다.
- 돌진 예고 상자는 실제 판정(몸체 폭 + 이동 거리)과 같은 범위를 덮는다.

### 공중 도약 내려찍기와 하수인 소환 (2026-11-04, 사용자 요청)

- `BossLeapSlamPattern`(메뉴 `Leap Slam`): 지상에서 `telegraphTime`(0.3) 웅크린 뒤 `BeginAirborne`으로 몸체를 Kinematic으로 바꾸고 `riseTime`(0.4)에 `jumpHeight`(5)만큼 올라간다(천장이 가까우면 그 아래에서 멈춘다). 이어 `aimTime`(0.6) 동안 착지 X가 플레이어 X를 따라가고(그 아래에 지면이 있는 X만 받아들이며 벽은 `ClampHorizontal`로 넘지 못한다) 보스는 `airSpeed`(10)로 그 위로 이동한다. 조준이 끝나면 착지 X가 고정되고(2026-11-05부터 색은 바뀌지 않고 위험구역이 착지 순간까지 점점 진해진다) 고정 후 정확히 `landDelay`(1)초에 착지한다. 마지막 `dropTime`(높이 ÷ `dropSpeed` 30) 동안 수직 하강하며, 착지하면 `EndAirborne` 후 반경 `shockwaveRadius`(3)의 원형 충격파로 `damage`(15)를 한 번 준다. 끝나고 `landingTime`(0.15) 뒤에 실행이 끝난다. 시작 조건은 보스가 지상에 있고 대상이 있을 때다. 취소·사망·비활성화는 `EndAirborne`으로 물리를 복원해 보스가 중력으로 떨어진다.
- `BossSummonPattern`(메뉴 `Summon`): 하수인을 `minionPrefab`(Inspector에서 어떤 프리팹이든 지정, 루트에 `Unit`+`UnitHealth` 필요)로 `spawnOffsets`(보스 몸체 중심 기준, 기본 (-2.5,0)·(2.5,0), 보스 방향과 무관)마다 한 마리씩 소환한다. 살아 있는 하수인이 `maxAlive`(2)에 이르면 시작하지 않고, 일부만 죽었으면 모자란 만큼만 만든다. 벽 앞 0.6에서 멈추고 아래 지면이 없는 칸은 건너뛴다. 소환된 하수인은 보스와 같은 씬의 최상위 오브젝트이며 스테이지 적 루트에 속하지 않는다. `despawnWithBoss`(기본 true)이면 보스 사망 때 하수인이 같이 죽고(각자 사망 연출) 보스 전투가 멈추면(비활성화·FSM 정지) 하수인 오브젝트를 제거한다.
- 공용 변경: `BossTelegraph.Locked`(예고가 고정되었는지. 11-05부터 표시 색에는 쓰지 않고, 투명도는 `BossTelegraph.Urgency`가 정한다), `BossCombat2D.HitCircle`·`TryFindGround`·`ClampHorizontal`·`ClampVertical`·`RayClampX`, `IBossMover.BeginAirborne/SetAirbornePosition/EndAirborne/IsAirborne`, `BossPatternRun.OnBossDied/OnBossStopped`(브레인이 캐시된 모든 Run에 부름), `BossContext.RunOf`(패턴 정의의 `CanStart`가 자기 Run을 읽는 길).
- 하수인 프리팹을 바꾸려면 `Data/Bosses/TestBoss/Patterns/TestBossSummon.asset`(또는 새 Summon 에셋)의 `Minion Prefab`에 프리팹을 끌어 놓는다. 임시로 `Prefabs/Enemies/PatrolEnemy.prefab`이 지정되어 있다.

### 새 패턴을 추가하는 순서

1. `BossPatternDefinition`을 상속한 ScriptableObject를 만들고 `[CreateAssetMenu(menuName = "Action Platformer/Bosses/Patterns/...")]`를 붙인다. 자기 수치는 `[SerializeField] private`로 두고 `TryValidate`를 재정의해 잘못된 값을 거부한다.
2. `BossPatternRun`을 상속한 중첩 클래스를 만들고 `CreateRun`에서 돌려준다. 수명은 `BeginTelegraph` → `TickTelegraph`(끝나면 true) → `BeginExecute` → `TickExecute`(끝나면 true) → `End`/`Cancel`이다. 패턴이 쓸 수 있는 것은 `BossContext`(이동 어댑터, `BossCombat2D`, 블랙보드)뿐이다.
3. 실행 중 상태(타이머·타격 기록)는 Run 객체에 둔다. 공유 에셋에는 쓰지 않는다. Run은 보스 한 개체당 패턴마다 하나만 만들어 매 실행에 재사용하므로, 실행마다 초기화할 값은 생성자가 아니라 `BeginTelegraph`/`BeginExecute`에서 초기화한다.
4. 예고 표시가 필요하면 `Telegraph`를 재정의해 상자 중심·크기와 `Urgency`(예고가 뜰 때 0, 실제 타격 순간 1)를 돌려준다. 공용 빨간 위험구역이 그 상자를 덮는다. 방패 같은 방어는 `BlocksDamage`로 알린다.
5. 시작 조건이 있으면 `CanStart`를 재정의한다.
6. Project 창 `Create > Action Platformer > Bosses > Patterns`에서 에셋을 만들고 선택 트리의 잎(패턴 노드)에 연결한다.

### 새 보스를 추가하는 순서

1. 패턴 에셋을 만들고(위) `Create > Action Platformer > Bosses > Pattern Selector`로 선택 트리를 만든다.
2. `Create > Action Platformer > Bosses > Boss FSM Definition`을 만들고 `selector`와 수치·체력 구간 경계·애니메이션 이름을 정한다. Inspector 아래에 잘못된 값이 오류로 표시된다.
3. `UnitDefinition`을 만든다(Kind Monster, MaxHealth, `fsm` = 위 FSM, `allowForcedMovement` false 권장).
4. 프리팹: 루트 레이어 2, Rigidbody2D(회전 고정)·BoxCollider2D·`Unit`·`UnitHealth`(Deactivate On Death 끔)·`BossCombat2D`·`GroundMovement2D`(Ignore Knockback). 자식 Visual에 SpriteRenderer·Animator(Float `ActionTime`, Root Motion 끔, FSM·패턴이 부르는 상태 이름 모두 필요)·`BossVisual2D`(`stateSource` = 루트 `BossCombat2D`)·`UnitDamageFlash`, `BossVisual2D.attackZone`에 공용 `Data/Presentation/AttackZone.asset`을 연결한다(예고 렌더러는 VfxManager가 만든다). 애니메이터에 없는 상태 이름은 경고를 한 번 남기고 현재 모션을 유지한다.
5. 기존 `Assets/_Game/Prefabs/Bosses/TestBoss.prefab`과 `Data/Bosses/TestBoss`를 복제해 시작하면 편하다.

### 선택 트리 편집

- `BossPatternSelector` 에셋을 선택하면 Inspector에 트리가 나온다. 노드 머리글은 한 줄 요약(예: `거리 0~3.5 / 높이 -1.5~2.5`, `패턴: TestBossMelee`)이고 오른쪽 `추가/교체`로 노드 종류를 고르고 `삭제`로 비운다.
- 묶음: Selector(처음 성공한 자식), Sequence(모두 통과하면 마지막 자식의 패턴), Weighted Random(가능한 자식 중 가중치 추첨). 조건: 거리, 앞뒤, 플레이어 상태, 체력 구간, 확률. 장식: Inverter. 잎: 패턴(`avoidLast`로 최근 n번 사용 제외).
- 새 노드 종류는 `BossSelectorNode`(조건은 `BossCondition`) 파생 클래스와 `[BossNodeMenu("묶음/이름")]` 하나면 추가 메뉴에 자동으로 뜬다. 이름을 바꿀 때는 `[MovedFrom]`을 붙인다.
- 검증 오류(빈 묶음·패턴 없는 잎·뒤집힌 범위·같은 노드 중복 연결)는 Inspector 아래에 HelpBox로 보이고 FSM 생성도 막는다.
- 목록의 `+`가 같은 노드를 가리키는 복사본을 만들면(Insert 경로) 다음 그리기에서 뒤의 복사본을 비워 노드를 새로 고르게 한다. 저장된 데이터에 없어진 노드 클래스가 있으면 빈 칸을 "비어 있음" 대신 `(missing type 클래스명 …)`으로 표시한다. 한 에셋에 비어 있는 칸과 없어진 타입이 같이 있으면 비어 있는 칸도 같은 문구로 보인다(Unity가 칸마다 구분해 주지 않는다).

### 디버그 표시

- Play Mode에서 보스 오브젝트를 선택하면 `UnitEditor`가 `Boss State / Boss Pattern / Boss Phase`를 표시한다(`BossCombat2D.State`가 있을 때만). 접을 수 있는 `Last Selection`에는 마지막 선택 경로(통과·실패한 노드 요약)가 나온다. 텍스트는 `Editor/Bosses/BossDebugText.cs`가 만들고 EditMode 테스트가 확인한다.
- Play Mode에서 TestBoss 프리팹을 런타임에 인스턴스화해 선택한 Inspector 캡처가 `Library/PrototypeValidation/TestBossPlayModeInspector.png`에 있다(씬 파일은 건드리지 않음).
- 머리 위 행동 표시(`BossDebugLabel`, 보스 루트 컴포넌트, TestBoss에 부착): Game 뷰에서 몸체 콜라이더 위 0.35에 두 줄을 그린다. 1줄은 현재 행동(`Telegraph · TestBossMelee 59%`, `Execute …`, `Recover …`, `Staggered 30%`, `PhaseShift → P1 30%`, `Death`, `Approach`, `Dormant`), 2줄은 `P구간 · HP 현재/최대`다. 글자색은 예고 주황, 실행 빨강, 회복 회색, 경직 청록, 구간 전환 보라, 그 밖 흰색. 퍼센트는 `IBossActionState.Progress`(예고·실행은 패턴 진행, 나머지는 각 지속 시간 기준)다.
- `Visible`(체크 해제로 숨김), `Height Offset`, `Font Size`(기본 14)는 Inspector에서 바꾼다. IMGUI(`OnGUI`)로 그리므로 Editor와 Development Build에서만 보이고 일반 빌드에는 그려지지 않는다. 새 보스에도 루트에 컴포넌트를 붙이면 된다(`BossCombat2D` 필요). 문구는 `BossDebugLabel.Describe`가 만들고 EditMode `BossDebugLabelTests`가 확인한다.

### 테스트 보스

- `Assets/_Game/Prefabs/Bosses/TestBoss.prefab`: 체력 300, 근접(쿨타임 0)·돌진(3초)·뒤로 물러나기(4초)·도약 내려찍기(5초)·소환(12초) 패턴, 체력 50%에서 구간 전환. 선택 트리는 위에서부터 ① 체력 구간 1 이상이면 소환(하수인 2마리가 이미 살아 있으면 건너뜀) ② 근처(거리 0~3.5·높이 -1.5~2.5)에서 근접 ×3 / 물러나기 ×1(최근 1회 제외) 가중 추첨 ③ 먼 거리(5~12·높이 -1.5~1.5)에서 플레이어가 지상이면 돌진 ④ 거리 3.5~12·높이 -2~6이면 도약 내려찍기다. 외형은 중심 피벗 도형 스프라이트(1.6×2.4)이고 Visual은 루트와 같은 위치 (0,0)에 두어 몸체 콜라이더와 겹친다. 애니메이터는 빈 모션 상태만 있다.
- 에셋은 `Data/Bosses/TestBoss`(정의·FSM·선택 트리·`Patterns/`), `Animations/Bosses/TestBoss/TestBoss.controller`에 있다. 보스 체력 HUD는 없다.

### 보스 테스트 씬

- `Assets/_Game/Scenes/BossTest.unity`: MovementLab을 복사해 적 샘플과 허수아비를 뺀 씬이다. 바닥(X -14~14, 윗면 Y 0)·양쪽 벽·고정 카메라(크기 8)·플레이어(-10, 1)는 MovementLab과 같고, `Boss Test` 아래 TestBoss가 (5, 1.22)에서 왼쪽을 보고 서 있다. 빌드 설정에는 넣지 않았으므로 Editor에서 열어 Play로 확인한다.
- 플레이어가 보스와 수평 14 안으로 들어오면 인식한다. 가까이(3.5 이내) 가면 근접/물러나기, 5~12 거리의 지상에서는 돌진을, 그 밖의 3.5~12 거리(플레이어가 공중이거나 4.5 같은 중간 거리)에서는 도약 내려찍기를 고른다. 체력 50% 아래(구간 1)에서는 소환이 먼저 나와 패트롤 에너미 2마리가 생긴다. 보스를 선택하면 Inspector에서 상태·패턴·선택 경로를 볼 수 있다.

## 2026-11-04 — GameManager (씬 전환에도 남는 흐름 관리자)

사용자 결정: 스테이지 흐름을 **전부** 씬 밖의 GameManager로 옮긴다. 설계는 `Docs/superpowers/specs/2026-11-04-game-manager-design.md`다. CoreLoopStage의 플레이 동작(0.35초·0.85초 대기, 사망 우선, 휴식 회복 1회, 클리어 시 시간 정지, 오류 문구)은 바뀌지 않았다.

### 생성과 프리팹

- 플레이를 시작하면 어느 씬에서든 `RuntimeInitializeOnLoadMethod(BeforeSceneLoad)`가 `Assets/_Game/Resources/GameManager.prefab`을 복제해 DontDestroyOnLoad에 `GameManager` 하나를 만든다. 이미 있으면 만들지 않는다. 씬을 바꾸거나 다시 불러와도 같은 물체가 남는다. 프로젝트는 Domain Reload를 끄고 있으므로 `SubsystemRegistration`에서 정적 참조를 지운다(`EffectsRuntime`과 같은 방식).
- 프리팹은 루트 하나에 `UIDocument`(Panel Settings `UI/StagePanelSettings.asset`, Sorting Order 100), `StageFlowView`(Document 연결), `GameManager`(View 연결)를 둔다. Sorting Order는 예전 씬 `Stage UI`와 같아 대사·말풍선 UI와의 그리기 순서가 그대로다. HUD 설정은 이 프리팹에서 고친다. 프리팹이 없으면 `GameManager prefab is missing at Resources/GameManager.` 오류를 남긴다.
- 코드에서는 `GameManager.Instance`(플레이 중이면 필요할 때 만듦, Edit Mode·종료 중에는 null)와 `GameManager.Existing`(만들지 않음)을 쓴다. 종료 중에도 불리는 `OnDisable`에서는 `Existing`만 쓴다.

### StageLayout (씬 쪽 연결)

- CoreLoopStage의 `Stage Flow` 물체에 있던 `StageFlowController`를 `StageLayout`으로 이름만 바꿨다(스크립트 GUID 유지, 기존 연결 값 그대로). Player, Required Enemies, Goal, Cinematics, Final Wave Cutscene, Encounters, Transfer Terrain Mask만 가진다. `View` 칸은 없어졌고 씬의 `Stage UI` 물체도 지웠다.
- `StageLayout.Start`에서 GameManager에 등록하고 `OnDisable`에서 해제한다. 등록을 `Start`에 두는 이유는 테스트가 `sceneLoaded`(Start 이전)에서 `encounters`·`requiredEnemies`를 바꿔 끼우기 때문이다.
- 등록할 때 시간 배율·고정 시간 간격을 기록하고, 상태를 Playing으로 두고, 이전 판의 HUD(클리어 화면, 꺼진 RESTART 버튼, 안내 문구, COMBO)를 지운 뒤 기존 시작 검증을 한다. 실패하면 예전과 같은 `Stage flow needs a live player, …` 오류를 남기고 등록하지 않는다(GameManager는 계속 동작). 한 번에 한 스테이지만 등록하며 두 번째 `StageLayout`은 `Only one StageLayout can be registered at a time; this one is ignored.` 오류 후 무시한다. 등록에 성공할 때마다 `StageVersion`이 1 오른다(재시작 감지용).
- 등록이 풀리면(씬 언로드, StageLayout 비활성화) 예전 `OnDisable` 정리(구독 해제, 구역 이동 취소, 컷신 취소, 시간 복원)를 하고 스테이지 HUD를 숨긴다. 진행 중 설정 오류도 같은 오류를 남기고 등록을 푼다.
- 공개 값 `State`, `RemainingEnemies`, `Player`, `FinalWaveCutscene`, `UsesEncounters`, `Phase`, `CurrentEncounterIndex`, `CurrentRestArea`, `ActiveEnemies`, `CanRequestTransfer`, `Restart()`는 GameManager에서 같은 뜻으로 읽는다. 추가로 `Stage`(등록된 StageLayout), `HasStage`, `StageVersion`, `View`가 있다. 재시작은 등록된 씬의 build index로 `LoadSceneAsync(Single)`이며 GameManager는 남고 새 씬의 StageLayout이 다시 등록된다.
- 웨이브·휴식·구역 이동 규칙은 GameManager가 소유한 일반 C# `StageProgress`에 있다. 적 같은 씬 검사는 StageLayout의 씬을 기준으로 한다.

### HUD 구역

- `StageFlowView`는 UIDocument 루트 아래 `stage-hud` 구역에 목표·휴식 이동 안내, 글리치 사용 불가 문구, COMBO, 클리어 화면을 만든다. 이 구역은 스테이지 등록을 따라 보이고 숨는다(`ShowStage`, `IsStageVisible`). `ResetStage`가 이전 판의 표시를 지운다.
- 다음 작업의 보스 체력바처럼 스테이지와 무관한 표시는 이 구역 밖 루트에 다른 구역으로 붙인다. 이번에는 스테이지 구역만 있다.

### 스테이지가 없는 씬

- MovementLab·BossTest에는 StageLayout이 없다. GameManager는 흐름 규칙을 돌리지 않고, 시간 배율을 건드리지 않으며, 스테이지 HUD를 숨긴다(글리치 사용 불가 문구·COMBO도 보이지 않는다).

### 컷신 연결

- `CutsceneRunner`의 `Stage` 칸을 지웠다. 시작 가능 여부, 실행 중 취소, 종료 후 입력 복원은 "GameManager에 이 러너와 같은 씬의 StageLayout이 등록돼 있고 상태가 Playing"으로 판단한다. `연결 검사`의 '스테이지 흐름' 항목은 같은 씬에 StageLayout이 있는지를 본다. `Game > Cinematics` 설치(`CutsceneAuthoring.InstallStageExample`)도 StageLayout을 찾는다.
- 이 문서의 이전 절에 나오는 `StageFlowController`(`encounters`, Final Wave Cutscene, `UpdateObjective`, Stage Flow의 View 연결, `Stage UI`)는 이제 연결은 `StageLayout`, 규칙과 HUD는 GameManager(`StageProgress`, 프리팹의 `StageFlowView`)를 뜻한다.

## 2026-11-04 — 일시정지(ESC)와 일시정지 메뉴

사용자 요청: ESC로 게임을 멈추고 `계속하기`·`옵션`·`저장 후 종료` 버튼만 둔다. `계속하기`만 게임을 다시 진행하고, `옵션`·`저장 후 종료`는 눌러도 아무 일도 하지 않는다(기능 미구현).

### 조작

- `Esc` 또는 게임패드 `Start`를 누르면 멈추고, 멈춘 상태에서 한 번 더 누르면 다시 진행한다. 메뉴의 `계속하기`도 다시 진행한다. GameManager가 씬 전환에도 남으므로 CoreLoopStage·MovementLab·BossTest 어디서나 같다.
- 멈춘 동안: `Time.timeScale = 0`, `AudioListener.pause = true`, 입력이 켜져 있던 모든 `PlayerInput`을 `DeactivateInput`한다. 메뉴가 스테이지 HUD 위에 보이고 글리치 사용 불가 문구·COMBO는 숨는다.
- 키는 GameManager의 `LateUpdate` 끝에서 읽는다(모든 `Update`와 그 프레임의 스테이지 판정 뒤). 그래서 같은 프레임에 먼저 시작된 컷신이나 클리어·사망이 있으면 멈추지 않는다.
- 다시 진행할 때: 멈출 때 기록한 `timeScale`으로 되돌리고(`fixedDeltaTime`은 일시정지가 바꾸지 않으므로 값이 달라졌을 때만 되돌린다), 오디오는 일시정지가 직접 멈춘 경우에만 다시 켜며, 일시정지가 끈 `PlayerInput`만 다시 켠다(파괴·비활성 입력은 건너뜀). 멈춘 동안 누른 공격·점프는 다시 진행한 뒤 실행되지 않는다(입력 동작이 꺼져 있어 기록되지 않음). 멈춘 동안 스테이지가 Playing을 벗어났다면(클리어·사망) 시간과 입력은 그쪽 소유이므로 되돌리지 않고, 메뉴를 숨기고 일시정지가 멈춘 오디오만 다시 켠다.

### 멈추지 않는 경우

- 컷신 진행 중(로드된 씬의 어느 `CutsceneCoordinator`라도 `IsBusy`)과 컷신이 끝난 바로 그 프레임. Esc는 컷신 건너뛰기 키이기도 해서, 건너뛰는 Esc가 메뉴까지 열지 않도록 GameManager가 마지막 `LateUpdate`에서 컷신이 진행 중이었는지를 기억한다(스크립트 실행 순서와 무관).
- 스테이지가 등록돼 있고 `State`가 Playing이 아닐 때(사망 연출 중, 재시작 중, 클리어 화면).
- 히트스톱·슬로 같은 전투 시간 효과(`PlayerImpactFeedback.IsTimeEffectActive`)가 있으면 멈추기 전에 그 효과를 `Reset()`으로 끝내고(효과가 가진 시간을 원래대로 되돌림) 그 시간을 기록한다. 그래서 다시 진행하면 평상시 시간으로 돌아온다.
- 안전장치: 멈춘 채로 등록된 스테이지가 해제되거나(씬 언로드·설정 오류), 씬이 불러와지거나 활성 씬이 바뀌거나, GameManager가 비활성화·파괴되면 먼저 다시 진행한다. 씬을 바꿔도 `timeScale 0`이나 오디오 일시정지가 남지 않는다.

### 구성 위치

- 입력: `Assets/_Game/Data/GameControls.inputactions`(맵 `Game`, Button 동작 `Pause`, `<Keyboard>/escape`·`<Gamepad>/start`). GameManager가 복제본을 만들어 `OnEnable`에서 켜고 `OnDisable`에서 끈다. `PrototypeControls`·`CutsceneControls`·`InputSystem_Actions`와 프로젝트 전역 입력 등록은 바꾸지 않았다.
- 코드: 상태와 시간·오디오·입력의 기록/복원은 일반 C# `Runtime/Flow/GamePause.cs`, 거부 규칙과 키 처리는 `GameManager`(`IsPaused`, `TryPause()`는 거부되면 false, `Resume()`, `PauseMenu`), 화면은 `Runtime/Flow/PauseMenuView.cs`.
- 화면: `GameManager.prefab`의 `PauseMenuView`(Document = 같은 UIDocument, Font = `UI/Cinematics/Fonts/NotoSansKR-Regular.otf`)가 공유 루트 아래 `pause-menu` 구역을 코드로 만든다(반투명 어두운 배경, 가운데 버튼 3개 `pause-continue`/`pause-options`/`pause-save-quit`). 루트를 비우지 않고 `stage-hud`도 건드리지 않는다. 보일 때 `BringToFront()`로 스테이지 HUD 위에 그리고 `계속하기`에 포커스를 준다. HUD 기본 글꼴에는 한글이 없어 이 구역에 Noto Sans KR을 지정한다.
- 아직 없는 것: 옵션 화면, 저장, 종료, 음량 설정, 타이틀 화면. 다른 키로 멈추기도 없다.

## 2026-11-04 — 플레이어 피격 반응 (슬로·흔들림·넉백·피격 무적·피격음)

사용자 요청: 피격 슬로 0.2배·0.35초 유지(같은 날 0.1배·0.75초 → 0.2배·0.5초를 거쳐 조정), 카메라 흔들림 8px, 캐릭터 기울기 제거, 전용 피격음, 넉백과 1.5초 피격 무적(스프라이트 깜빡임).

### 동작

- 시간: 체력이 줄면 0.2배 슬로를 실제 시간 0.35초 유지한 뒤 0.1초 동안 1배로 돌아온다(실제 0.45초 동안 게임 시간은 약 0.13초 흐른다). 나가는 히트스톱·처치 슬로보다 우선한다. (2026-11-05부터 완전 정지 0.35초 + 0.1초 복귀, 아래 같은 날 절)
- 화면: 카메라가 맞은 방향으로 8px(1120×660 기준) 밀렸다가 0.12초에 잦아든다. 흔들림 상한(`PlayerImpactFeedback.MaxShakePixels`)을 4에서 8로 올렸고, 다른 흔들림(1.5~3px)은 그대로다. 분홍 비네트·피격 모션·흰색 번쩍임·외곽선은 그대로이고 캐릭터 기울기는 없앴다.
- 넉백: 적의 공격(방향이 있는 피해, `ApplyDirectedDamage`)은 공격 근원 반대쪽으로 10유닛/초에서 0.25초(게임 시간) 동안 선형으로 줄어드는 속도로 밀고, 시작할 때 위로 7유닛/초 살짝 뜬다. 그동안 걷기·점프 입력은 무시한다. 바로 위/아래에서 맞으면 바라보는 반대쪽으로 민다. 땅에 닿은 뒤 발판 끝에서는 멈춘다(공격 반발력과 같은 단계 사용). 근원이 없는 피해(`ApplyDamage(int)`)는 밀지 않는다. 사망하는 피해는 밀지 않고 그 자리에서 사망 연출을 한다.
- 피격 무적: 체력이 줄면 1.5초 동안 적의 공격(위치·방향이 있는 피해)을 무시한다. 실제 시간 기준이라 피격 슬로 0.45초도 포함되고(정상 속도로 보호받는 시간은 약 1.05초), 일시정지·컷신처럼 timeScale이 0인 동안은 줄지 않는다. (2026-11-05부터는 피격 정지 0.35초 동안도 줄지 않으므로 실제로는 약 1.85초 뒤에 풀린다.) 적은 계속 플레이어를 대상으로 삼는다(`CanReceiveDamage` 그대로). 근원이 없는 `ApplyDamage(int)`는 방패처럼 무적도 우회한다(테스트·환경용 계약 유지). 사망·비활성화 때 풀린다.
- 깜빡임: 무적 동안 스프라이트가 실제 시간 0.08초마다 알파 1 ↔ 0.2로 깜빡인다(슬로 중에도 같은 박자). reduced motion에서는 깜빡이지 않고 알파 0.6으로 유지한다. 사망 모션은 깜빡이지 않는다.
- 피격음: 체력이 줄 때 `Hurt`(0.32초)를 음량 0.5, 피치 ±3%로 한 번 재생한다(0.05초 안에 겹친 피해는 한 번). 사망하는 피해도 같은 소리다.

### 구성 위치

- 값: `Data/PlayerCombatTuning.asset` — Player Damage Feedback(`Damage Slow Time Scale` 0.2, `Damage Slow Duration` 0.35, `Damage Shake Amplitude` 8; 11-05부터 0 / 0.35)과 새 Player Damage Response(`Damage Invulnerability` 1.5, `Damage Knockback Speed` 10, `Damage Knockback Duration` 0.25, `Damage Knockback Lift` 7). 0이면 각각 끈다. 깜빡임 박자·알파는 Player 프리팹 Visual의 `PlayerVisual`(`Damage Blink Interval` 0.08, `Damage Blink Alpha` 0.2).
- 코드: 무적 시간은 `PlayerUnit`이 갖고(`OnDamageApplied`에서 바로 켜서 같은 단계의 두 번째 공격도 막음) `UnitHealth.SetInvulnerable`로 알린다. `UnitHealth.Invulnerable`은 위치·방향 피해만 막는다. 넉백은 `PlayerUnit`의 공격 반발력 단계를 재사용한다(점프 키 중력 완화 없음). `PlayerReactionPresentation.Invulnerable`을 `PlayerVisual`이 읽어 깜빡인다. 피격음은 `PlayerVisual.NotifyDamage` → `PlayerSoundEffects.Hurt`.
- 소리: `Audio/Combat/Hurt.wav`(Kenney Impact `impactPunch_heavy_002` + Sci-fi `laserRetro_004` 25% 낮춰 비트크러시 + `lowFrequency_explosion_000` 0.3초), `PlayerSfx.asset`의 `Hurt`·`Hurt Volume`(0.5). 레시피는 `Library/SfxDownloads/PrepareSfx.py`(`python -I`로 루트에서 실행, 다른 클립은 바이트 동일).
- 테스트: PlayMode `PlayerDamageResponseTests` 5개, `PlayerVisualTests.InvulnerabilityBlinksTheSpriteButNeverWhileDying`, EditMode `PlayerVisualAssetTests.PlayerDamageFeedbackUsesTheRequestedSlowShakeAndHurtSound`.

## 2026-11-04 — 잠복 출현 버그 수정 2건

### 후딜레이 중 잠복한 뒤 공격이 출현으로 이어지지 않던 문제 (`d7da531b`)

- 공격 후딜레이 중에 잠복해도 지하에서 전투 시간이 계속 흐른다. 후딜레이를 줄여 주지는 않지만, 끝나는 즉시(공격 가능 상태가 되면) 출현할 수 있다.
- 후딜레이 중 지하에서 누른 공격은 예약 입력 1개로 남았다가 공격 가능 상태가 되면 출현한다.
- 잠복을 여는 글리치와 같은 물리 단계에 누른 공격은 다음 단계의 출현 요청이 된다.
- Space 취소·체류 시간 만료·출현 실패로 지하를 벗어나면 예약 입력을 버린다(`PlayerCombat.ClearBufferedAttack`). 지상으로 돌아온 뒤 남은 입력이 일반 공격으로 나가지 않는다.
- 코드: `PlayerUnit`(지하에서 `Combat.Tick`·예약 입력 소비), `PlayerCombat.ClearBufferedAttack`.

### 보스 아래 잠복 후 출현이 거부돼 원위치로 돌아가던 문제 (`3c6826e5`)

- 원인: 출현할 때마다 대상이 잠복한 바닥 위에 있는지, 원래 몸 위치에서 사거리·화면 안인지 다시 검사했다. 보스는 잠복 중인 플레이어도 계속 쫓으며 도약하거나 물러나므로, 출현 지점이 비어 있어도 이 검사에 걸려 원위치로 돌아갔다.
- 지금 규칙: 출현은 잠복 바닥의 표시 지점에서 한다. 대상은 살아 있는 글리치 후보(활성·생존·`AllowGlitchTarget`·대상 레이어)인지만 다시 보고, 대상의 현재 위치(바닥·사거리·화면)는 보지 않는다. 사거리·화면은 잠복에 들어갈 때 이미 판정했다.
- 그대로인 것: 출현 지점의 몸체 공간 검사와 주변 후보 보정, 원래 몸 위치와 출현 지점 사이의 닫힌 문 차단, 대상 사망·비활성화 때 원위치 복귀, 2초 체류·Space 취소.
- 코드: `PlayerGlitch.TryGetEmergencePoint`가 `GlitchUtility.IsCandidate`와 `GlitchUtility.BarrierBetween`(공개로 바꿈)만 쓴다. 쓰지 않게 된 `GlitchUtility.ValidTarget`은 지웠다.

### 테스트

- `PlayerGlitchCombatTests`에 회귀 테스트 8개를 추가했다.
  - 후딜레이 잠복: `BurrowDuringRecoveryStillEmergesOnALaterPress`, `UndergroundPressDuringRecoveryWaitsAsTheFollowUpThenEmerges`, `CancellingTheBurrowDropsTheEmergenceWaitingForRecovery`, `AttackPressedWithTheBurrowingGlitchEmergesOnTheNextStep`
  - 보스 아래 출현: `EmergenceAtMarkerSurvivesTargetLeavingTheFloor`, `EmergenceAtMarkerSurvivesTargetMovingOutOfGlitchRange`, `AttackPressEmergesAtMarkerWhileTheTargetIsAirborne`, `ClosedGateBetweenParkedBodyAndEmergenceStillReturnsToOrigin`

## 2026-11-05 — 공격 위험구역 통일 (모든 몬스터 공용 빨간 글리치 사각형)

사용자 요청: 몬스터들의 공격 예고 표시를 PixelFX로 만든 빨간 반투명 사각형 하나로 통일하고, 몬스터마다 늘려서 재사용한다. 기준은 보스가 쓰던 공격 범위 상자이며, 공격 임박은 투명도로 알린다. 같은 날 후속 요청으로 단색 사각형을 "컴퓨터 세상 속 버그" 느낌의 글리치 무늬로 다시 만들었다(빨강 계열만, 청록 없음).

### 동작

- 예고가 뜨면 공격 범위 위에 빨간 사각형이 옅게(알파 0.2) 나타나고, 실제 공격 순간까지 점점 진해진다(알파 0.7). 색은 바뀌지 않는다. 조준 고정(Locked)도 색을 바꾸지 않고 투명도만 계속 올라간다.
- 모양: 깨진 데이터처럼 보이는 빨간 타일이 반복된다. 한 줄씩 내려가는 스캔라인, 빠진 픽셀(구멍)과 번쩍이는 죽은 픽셀, 옆으로 밀린 데이터 블록과 그 옆의 진홍색 잔상, 가끔 옆으로 찢기는 가로줄, 군데군데 끊기는 1픽셀 테두리. 8프레임을 게임 시간 12fps로 반복하므로 일시정지 중에는 멈추고 슬로 중에는 느려진다.
- 늘리는 방식: Tiled(테두리 1px). 안쪽 무늬를 잡아 늘리지 않고 원화 픽셀 크기 그대로 반복해 깔기 때문에, 구역 크기와 몬스터 Transform 스케일에 상관없이 글리치 픽셀 크기가 같다(보스처럼 Visual이 늘어난 몬스터도 구역의 실제 스케일은 1). 몬스터 스프라이트보다 Sorting Order +2 위에 그려지므로, 구역 안의 캐릭터도 덮인다.
- 몬스터별 구역:

| 몬스터 | 구역 | 진해지는 기준 |
|---|---|---|
| 보스 | 패턴의 `BossTelegraph` 상자(근접 상자·돌진 경로·도약 착지 띠) | `BossTelegraph.Urgency`. 근접·돌진은 예고 진행, 도약 내려찍기는 착지 순간(상승+조준+착지 지연)까지 |
| 전면 방패병 | 앞쪽 타격 범위(`Strike Reach` 2.3 × 몸 높이) | 준비 0.4초 |
| 돌진 방패병 | 돌진 경로(`Dash Max Distance` 5.5 × 몸 높이). 벽·발판 끝으로 짧아지는 실제 돌진보다 길 수 있다 | 준비 0.4초 |
| 포탑·드론 | 실제 판정과 같은 `EnemyBeam`을 따라 늘린 띠(벽에서 잘림, 폭은 광선 폭). 레이저 충전 연출은 그대로 | 준비 + 조준 고정 0.4초 전체를 한 번에 |
| 순찰 돼지 | 몸 중심에서 바라보는 쪽으로 `Attack Range`(1.2) × 몸 높이. 실제 판정은 중심 거리라 근사 상자 | 선딜레이 0.2초 |

- 공격이 시작되거나(돌진·타격·발사) 취소·피격·사망하면 바로 사라진다. 쏘는 중의 광선은 기존 레이저가 그린다.
- 바뀌지 않은 것: 모든 공격 판정·타이밍·수치, 포탑 스캔 막대, 방패 막대, 레이저. 원형 범위 표시는 없다(도약 내려찍기도 지금처럼 착지 띠).
- 없어진 것: 방패병 바닥 주황 선, 포탑·드론의 황색/적색 예고선, 보스 `Telegraph` 자식 렌더러와 주황/빨강 잠금 색.

### 구성 위치

- 에셋: PixelFX 소스 `Tools/PixelFx/effects/attack-zone.mjs`(32×32, 8프레임 12fps 루프, 팔레트 `#2a050c`·`#5c0f1a`·`#9c1b2e`·`#c42c42`·`#ff6b7d`·`#ffd7dd`, seed 11). 안쪽 30px는 타일로 반복되므로 무늬는 30px 주기이거나 타일 안에 머문다. 결과물 `Art/Effects/PixelFxAttackZone/attack-zone-000~007.png`(18 PPU, 중앙 피벗, Border 1px, Full Rect, Point, 무압축).
- 설정: `Data/Presentation/AttackZone.asset`(`AttackZoneProfile`: Frames 8, Fps 12, Start Alpha 0.2, Impact Alpha 0.7, Sorting Offset 2). 투명도 범위와 깜빡이는 속도는 여기서 조절한다.
- 다시 만들기: `node Tools/PixelFx/pipeline.mjs render custom/attack-zone` 후 `Game > Effects > Import Generated Attack Zone`(프레임 목록과 fps만 교체하고 투명도·정렬 값은 유지. 예전 프레임 PNG는 먼저 지운다).
- 코드: 렌더러는 `VfxManager`가 만든다(`RegisterOwner(Transform, AttackZoneProfile, SpriteRenderer)`, `ShowAttackZone(handle, center, size, angle, urgency)`, `HideAttackZone`). 요청한 프레임에만 보인다. 각 외형은 `AttackZoneOwner`를 하나 들고 위치·크기·각도·긴급도만 넘긴다: `EnemyVisual2D`(방패병·포탑·드론), `BossVisual2D`, `UnitVisual2D`(돼지, 지상 FSM이 있을 때만). 프리팹은 각 외형의 `Attack Zone` 칸에 같은 에셋을 연결했다(ShieldFront는 ShieldSoldier에서 물려받음, Guide NPC는 비어 있음).
- 새 몬스터: 외형의 `Attack Zone`에 `AttackZone.asset`을 연결하고, 예고 중 매 프레임 `AttackZoneOwner.Show`를 부른다. 보스 패턴은 `Telegraph`에 상자와 `Urgency`(0→1)만 돌려주면 된다.
- 테스트: EditMode `AttackZoneAssetTests` 3개(에셋 형식·빨강 계열만·프레임마다 다름, 프레임 순환, 모든 몬스터 연결), PlayMode `AttackZoneTests` 3개(방패병 범위·투명도, 포탑·드론 띠·조준 고정까지 한 번에 진해짐, 비활성화 시 숨김), `UnitCombatFsmTests.AttackWindupShowsTheDangerZoneInFrontUntilImpact`, `BossVisualTests`의 위험구역 검사 2개(기존 상자·잠금 색 검사 대체, 늘어난 Visual에서도 구역 스케일 1).

## 2026-11-05 — 기획서 v0.3 대조 후 조정: 내려찍기 속도 통일·순간이동 입력 예약

사용자 요청: 캐릭터 기획서 v0.3과 대조한 결과 중 두 가지를 기획서대로 맞춘다.

- 내려찍기 하강 속도: 플레이어 하강(`Slam Speed`)을 28 → 36으로 올려 맞은 적의 하강(`Slam Knockdown Speed` 36)과 같게 했다(기획서: 캐릭터·적 같은 속도). `PlayerCombatTuning.asset`과 코드 기본값 모두 36이다.
- 순간이동 입력 예약: 공격의 선딜레이·판정·내려찍기 하강 중에는 순간이동을 쓸 수 없는데, 그때 누른 Shift를 버리지 않고 하나 남겨 둔다. 여러 번 누르면 마지막 입력(누른 순간의 대상·방향)이 남는다. 후딜레이가 시작되어 순간이동할 수 있게 되면 바로 실행한다(후딜레이는 줄이지 않는다). 실행할 때 거리·화면·문·도착 공간은 새로 누른 것처럼 다시 검사하고, 안 되면 버린다. 피격·사망·조작 잠금·비활성화와 공격이 아닌 이유(피격 경직 등)로 막혀 있을 때는 예약하지 않거나 버린다. 공격 예약과 같은 `Attack Buffer Time`(0이면 둘 다 끔)을 쓴다. 공격 입력까지 함께 예약했다면 순간이동이 먼저(후딜레이 시작), 공격은 Ready에서 나간다.
- 안내 문구: 순간이동 불가 이유 문구(OUT OF RANGE 등)는 스테이지 HUD가 있는 씬, 즉 `StageLayout`이 GameManager에 등록된 CoreLoopStage에서만 보인다. MovementLab·BossTest에는 빨간 표시만 있다.
- 코드: `PlayerUnit`(`bufferedGlitch`·`bufferedGlitchRequest`, 입력 수집 때 저장, FixedUpdate에서 `CanReposition`이 되면 실행). `PlayerCombatTuning.slamSpeed` 기본값 36.
- 테스트: PlayMode `PlayerGlitchCombatTests.GlitchPressedMidSwingIsKeptAndRunsAtRecovery`·`HitDropsAGlitchKeptDuringTheSwing`·`ZeroAttackBufferTimeKeepsNoGlitchEither`, EditMode `PlayerVisualAssetTests.SlamDescentAndKnockdownShareOneSpeed`.

## 2026-11-05 — 글리치 일직선 줄기 (PixelFX)

사용자 요청: 순간이동 효과를 PixelFX로 새로 만든다. 출발·도착 지점에는 기존 spark를 그대로 두고, 두 지점을 일직선으로 잇는, 글리치 느낌과 속도감이 나는 효과를 추가한다. 중간에 한 줄로 터지던 작은 spark는 새 줄기와 겹치므로 없앴다(사용자 확인). 10-25에 지운 "연속 선"과는 다른, 픽셀 아트 글리치 효과다.

### 동작 (실제 시간 약 0.27초, 히트스톱·일시정지에도 끝까지 재생)

- 0~0.05초: 출발 spark가 터지고, 밝은 청록 줄기가 출발점에서 도착점까지 빠르게(처음이 가장 빠르게) 뻗는다. 줄기가 닿는 순간 도착 spark가 터진다.
- 프레임 0~1: 밝은 2픽셀 선과 앞으로 흐르는 속도선, 빨간 잔상 점.
- 프레임 2~4: 글리치 흔들림. 선의 블록이 위아래로 1픽셀씩 밀리고, 빨강·청록 채널이 위아래로 갈라지며, 픽셀이 빠지고 밝은 조각이 튄다.
- 프레임 5~7: 조각나며 흩어짐. 프레임의 60%(약 0.16초)가 지나면 출발점 쪽 끝이 도착점 쪽으로 당겨져 들어가 0.27초에 사라진다.
- 길이와 각도는 매번의 경로에 맞추고, 원화 픽셀 크기 그대로 반복해 깐다(Tiled). 그래서 짧은 이동과 긴 이동 모두 같은 픽셀 크기이고, 플레이어 Transform 스케일·회전과 상관없이 실제 경로 길이를 덮는다.
- 거리 0인 순간이동(같은 자리)에는 줄기가 없고 spark 하나만 터진다. 피격·사망·비활성화는 줄기도 지운다.

### 구성 위치

- PixelFX 소스: `Tools/PixelFx/effects/glitch-streak.mjs`(32×16, 8프레임 30fps, 왼쪽 가운데 피벗 = 출발 쪽, +x = 진행 방향, 청록 `#79d9df` 계열·빨강 `#d84255` 계열). 모든 무늬는 32px 주기라 타일 이음새가 없다.
- 결과물: `Art/Effects/PixelFxGlitchStreak/glitch-streak-000~007.png`(18 PPU, Full Rect, Point, 무압축).
- 다시 만들기: `node Tools/PixelFx/pipeline.mjs render custom/glitch-streak` 후 `Game > Effects > Import Generated Glitch Streak`(예전 PNG를 지우고 PlayerVfx의 `Glitch Streak Frames`만 교체).
- 설정: `Data/Presentation/PlayerVfx.asset`의 `Glitch Streak Frames`(8), `Glitch Streak Fps`(30), `Glitch Streak Draw Time`(0.05, 뻗는 시간 = 도착 spark 지연), `Glitch Streak Retract Start`(0.6). 프레임 배열을 비우면 예전처럼 0.6 간격의 작은 spark 열로 돌아간다.
- 코드: `PlayerActionEffects`의 `Glitch streak` 렌더러(VfxManager의 플레이어 세션 소유, Sorting Order +2로 spark +3 아래, 공용 효과 재질의 색조는 이 렌더러에서만 흰색으로 꺼서 원화 색 그대로). `PlayGlitch`는 줄기가 있으면 spark를 양 끝 2개만 쓴다.
- 테스트: PlayMode `PlayerVisualTests.GlitchStreakJoinsTheEndpointBurstsThenPullsInFromTheDeparture`(출발 즉시·도착은 줄기 도달 뒤, 중간 spark 없음, 경로 각도·끝점, 스케일·회전된 소유자 아래 실제 크기 1, 꼬리는 도착 쪽으로만 이동, 끝나면 숨김, 거리 0·비활성화), `GlitchSparksFillThePathWhenThereIsNoStreak`(기존 spark 열 테스트를 프레임 없음 대체 동작으로 유지), EditMode `PlayerVisualAssetTests.GlitchStreakIsAnEightFrameTealAndRedTileOnThePlayerGrid`.

## 2026-11-05 — 플레이어 피격: 슬로모션 대신 0.35초 완전 정지 + 0.1초 복귀

사용자 요청: 피격 슬로모션을 "0.2초 동안 게임을 아예 멈췄다가 0.1초 만에 원래 속도로 돌아오는" 느낌으로 바꾼다. 같은 날 후속 요청으로 정지 시간을 0.35초로 늘렸다(아래 실제 측정값은 0.2초일 때 기록).

- 시간: 체력이 줄면 게임 시간(timeScale)을 0으로 실제 시간 0.35초 멈추고, 이어서 0.1초 동안 부드럽게 1배로 돌린다(0.2초일 때 실제 측정: 0.19초까지 0, 0.21초 0.04 → 0.25초 0.55 → 0.29초 0.99 → 0.31초 1. 0.35초면 같은 곡선이 0.15초 늦게 온다). 멈춘 동안 적·플레이어 이동·물리·넉백이 모두 멈추고, 화면 흔들림·비네트·깜빡임·피격음은 실제 시간이라 그대로 나온다. 정지 중에 다시 맞으면 정지가 처음부터 다시 시작된다(겹쳐 길어지지 않음). 나가는 히트스톱·처치 슬로보다 우선하는 규칙은 그대로다.
- 넉백은 정지가 풀린 뒤 게임 시간으로 진행한다.
- 피격 무적 1.5초는 "timeScale이 0이면 세지 않는다"는 기존 규칙 그대로라, 정지 0.35초 뒤부터 세어 실제로는 약 1.85초 뒤에 풀린다(세계가 움직이는 시간 기준 1.5초).
- 값: `Data/PlayerCombatTuning.asset`의 `Damage Slow Time Scale` 0(이전 0.2), `Damage Slow Duration` 0.35(처음 0.2로 바꿨다가 같은 날 0.35로 조정, 슬로였던 값과 같음), `Damage Slow Recovery` 0.1(그대로). `Damage Slow Time Scale`은 이제 0(완전 정지)을 허용한다(이전 최소 0.01).
- 코드: `PlayerImpactFeedback.ApplySlowScale`이 배율 0이면 물리 간격(`fixedDeltaTime`)을 건드리지 않고(히트스톱과 같음), 복귀 중에는 물리 간격을 평소의 5% 아래로 줄이지 않는다. 끝나면 시간 배율과 물리 간격을 원래 값으로 되돌린다. `PlayerCombatTuning` 검증이 0을 허용한다.
- 테스트: `PlayerImpactFeedbackTests.DamageFreezeStopsTimeThenEasesBackAndRestoresThePhysicsStep`(새), `DamageSlowScaleValidationAndInstantRecovery`(0은 이제 유효), EditMode `PlayerVisualAssetTests`의 에셋 값(0/0.35/0.1), `PlayerDamageResponseTests`의 무적 종료 시점(정지 시간만큼 늦춤). `PlayerGlitchCombatTests.ActualDamageFreezesMovingPlayer…`(이전 `ActualDamageSlows…`: 정지 중 위치 그대로·물리 간격 유지, 풀린 뒤 이동)과 `AirSwingJumpRelease…`(피격 반응은 정지 중 바로 확인), `PauseTests.PausingDuringHitStop…`(물리 간격을 줄이는 슬로 경로는 처치 슬로로 확인하고, 피격 정지 중 일시정지도 확인)을 바꿨다.
- 피격 자세: 피격 반응 표시는 실제 0.12초라 정지 0.35초 안에 Hit 모션이 재생되고, 정지가 끝날 때까지 마지막 프레임에 멈춰 있다가 이어진다.

## 2026-11-05 — 원거리·영역 적 7종 (탄막병·레이저병·영역 전개형 기획서 v1.0)

설계·결정: `Docs/superpowers/specs/2026-11-05-ranged-enemy-family-design.md`. 사용자 결정은 체력 30(일반 공격 10×3)·피해 10, 직선·샷건만 '약한 피격'(무적 1.5초 유지), 임시 도형 몸체, 새 테스트 씬이다. 길이는 1유닛=47.5px로 환산했다.

- 구성: `RangedFsmDefinition`(종류 `RangedAttack`) + `RangedCombat2D` + `RangedVisual2D`. 기존 방패병·포탑·드론(`EnemyFsmDefinition`)은 그대로다. 프리팹 `Prefabs/Enemies/Ranged/{RangedStraight, RangedShotgun, RangedHeavy, RangedMortar, SweepLaser, DomainSelf, DomainFixed}`, 정의 `Data/Units/*Definition`, FSM `Data/Fsm/*Fsm`(값은 여기서 조절).
- 공통: 인식(생존·거리 13.05·지형 시야·플레이어 화면 안, 잠복 중도 대상 유지) → 최초 0.2초(한 번만) → 예고 0.4초(조준·낙하지점·영역 고정, 인식을 잃으면 취소) → 행동. 재공격은 이전 예고 시작 후 5초 + 자기 탄·영역이 모두 사라진 뒤. 일반 타격은 체력만 깎고, 띄우기·내려찍기·출현·충격파는 예고·훑기를 취소하고 착지·복귀할 때까지 새 예고가 없다. 사망 0.6초 뒤 비활성화.
- 탄: 직선 4.42/반지름 .295(약한 피격), 샷건 ±12° 3발 .21(약한 피격), 큰 탄 2.21/.505, 투하 7.58/.379(예고 때 정한 아래 지면에서 반지름 1.26·0.12초 폭발, 몸에 직접 맞으면 폭발 없음). 수명 6초, 벽·지형에서 사라지고 일방 발판은 통과. 무적 중에 닿아도 탄은 사라진다. 발사자가 죽어도 남고, 구역 마지막 적이 죽으면(`StageProgress`) 모두 지운다. 씬마다 생기는 `EnemyProjectiles`가 고정 단계로 처리하고 `EnemyProjectileView`가 그린다.
- 공중 투하병: 놓인 높이에서 플레이어 X를 3.79/초로 따라가고, 가로 차이 1.26 이내에서 멈춰 예고한다(`FlyingMovement2D.Follow/Hover`, 내려찍히면 떨어졌다가 원래 높이로 돌아오는 동안 사격 없음).
- 훑기 레이저: 조준 ±30°를 화면 기준 시계 방향으로 0.8초에 한 번 훑고(지형에서 길이가 잘림, 최대 13.05·두께 .21), 0.15초 잔상은 피해 없음. 실제로 체력이 줄었을 때만 그 훑기의 1회를 쓴다(무적 중 접촉은 안 셈).
- 영역: 5.05×4.21 사각형, 예고 0.4초 → 활성 2초. 자기 중심형은 주인을 따라가고 특정 구역형은 인스턴스의 `domainCenter`(월드 좌표)에 고정. 활성 중 순간이동·출현의 출발점(몸 중심)이 안이면 이동 없이, 도착점이 안이면 예약 공격 전에 즉사한다(무적 무시). 경로·걷기·점프는 검사하지 않는다. 조준 중 해당 순간이동은 빨간 마커와 `LETHAL ZONE` 문구로 알리지만 막지는 않는다(`GlitchPreview.Lethal`, 문구는 스테이지 씬에서만). 띄우기 등으로 활성 영역은 사라지지 않고, 주인이 죽으면 즉시 사라진다.
- 약한 피격(직선·샷건): 넉백·피격 정지·피격 자세·공격/콤보 취소가 없고 다음 순간이동만 0.2초 막는다(`PlayerCombatTuning.Light Hit Glitch Delay`, 코드 기본값). 피격 무적·깜빡임·피격음·작은 흔들림은 그대로다.
- 외형: 모든 예고는 공용 빨간 공격 위험구역(탄 경로 띠, 샷건 3줄, 투하 기둥+폭발 반원, 레이저 7줄 부채꼴, 영역 사각형). 활성 영역은 보라 빗금 타일 `Data/Presentation/DomainField.asset`, 레이저는 드론의 빨간 ARCBIT 레이저, 탄·폭발·소멸은 `Data/Presentation/EnemyProjectiles.asset`. PixelFX 원본은 `Tools/PixelFx/effects/{enemy-shot, enemy-bolt, enemy-pellet, enemy-heavy, enemy-shell, enemy-blast, enemy-shot-pop, domain-field}.mjs`, 다시 만들 때는 render 후 `Game > Effects > Import Generated Ranged Enemy Effects`. 몸체는 Block.png 색 도형(원화 받으면 Visual만 교체).
- 테스트 씬: `Scenes/EnemyTest.unity`(빌드 미포함, 처음 이름 RangedEnemyTest. BossTest 복사본에서 보스를 빼고 바닥 X -14~220, 카메라 X 추적). 신규 7종은 X=10/28/46/64(높이 5)/82/100/118(`Ranged Enemy Samples`), 특정 구역형 영역 중심은 (111, 2.1). 같은 날 사용자 요청으로 기존 적 5종도 한 씬에 모았다(`Existing Enemy Samples`: 순찰 돼지 138, 돌진 방패병 156, 전면 방패병 174, 감시 포탑 192, 공중 드론 210·높이 3.5). 18유닛 간격이라 한 번에 한 종류만 인식 범위에 든다. MovementLab 배치는 그대로다. CoreLoopStage는 같은 날 사용자 요청으로 다시 배치했다(아래 절).

## 2026-11-05 — CoreLoopStage 적 재배치 (기존 적 + 원거리 5종 혼합)

사용자 요청: CoreLoopStage의 기존 몬스터(저장된 14마리와 저장 안 된 채 맨 위에 놓인 원거리·영역 7개)를 모두 지우고, 새 적을 섞어 다시 배치한다. 영역 전개형(DomainSelf·DomainFixed)은 넣지 않는다.

- 구조는 그대로다: 4웨이브, 구역 X 범위(0~60 / 72~110 / 122~162 / 174~217), 휴식 구역·문·출구·발판·카메라·인트로. 적 수만 **3 → 4 → 5 → 6마리(총 18)**로 바꿨다. 각 웨이브 루트(`Required Enemies`, `Encounter 2/3/4 Enemies`) 아래 프리팹 인스턴스이고, `StageLayout`의 `requiredEnemies`·`encounters[].enemies`를 새 목록으로 채웠다.
- 새 종류는 앞 웨이브에서 하나씩 먼저 나오고 마지막 웨이브에서 섞인다. 적의 행동·체력·공격 수치는 바꾸지 않았다.

| 웨이브 | 적 (X, Y) |
|---|---|
| 1 (X 0~60) | 순찰 돼지 (24, 0.52) · 직선 사격 (40, 0.57) · 감시 포탑 Ledge 3 위 (48.03, 4.47) |
| 2 (X 72~110) | 전면 방패병 (79, 0.82) · 샷건 Wave 2 Ledge 3 위 (89.3, 5.67) · 공중 드론 (96, 7.6) · 공중 투하 (103, 9.5) |
| 3 (X 122~162) | 순찰 돼지 (130, 0.52) · 큰 탄 (139, 0.67) · 훑기 레이저 Wave 3 Ledge 4 위 (145.75, 6.02) · 돌진 방패병 (150, 0.82) · 감시 포탑 Wave 3 Ledge 5 위 (154.68, 4.63) |
| 4 (X 174~217) | 샷건 Wave 4 Ledge 1 위 (178.79, 5.01) · 돌진 방패병 (183, 0.82) · 공중 드론 (190, 8.6) · 공중 투하 (197, 9.8) · 훑기 레이저 Wave 4 Ledge 5 위 (202.28, 5.09) · 큰 탄 (209, 0.67) |

- 높이: 지상 적은 지지면 윗면 + 몸 절반 + 0.02(돼지 0.52, 방패병 0.82, 포탑 0.62, 직선 0.57, 샷건 0.52, 큰 탄 0.67, 레이저 0.47). 공중 투하병은 그 웨이브에서 가장 높은 발판(7.45/7.62)보다 위에 두어 플레이어를 따라가도 발판에 걸리지 않게 했다. 화면 세로 범위는 카메라 Y 3.4 ± 8이다.
- 첫 웨이브 적은 모두 X 24 이후에 둔다. 플레이어 시작 X 3·인트로 X 6·안내자 X 9에서는 원거리 적의 인식 거리 13.05와 순찰 돼지 탐색 12에 들지 않는다.
- 종류별 수: 순찰 돼지 2, 돌진 방패병 2, 전면 방패병 1, 감시 포탑 2, 공중 드론 2, 직선 1, 샷건 2, 큰 탄 2, 공중 투하 2, 훑기 레이저 2.

## 2026-11-05 — 플레이어를 항상 맨 위에 그리기

사용자 요청: 플레이어와 적이 겹치면 가끔 플레이어가 가려진다. 항상 플레이어가 제일 위에 오게 한다.

- 원인: 모든 스프라이트가 Default 정렬 레이어이고, 플레이어 Visual의 Sorting Order는 1, 적·보스는 10이었다. 겹치는 순간에는 늘 적이 위에 그려졌다(겹칠 때만 보이므로 '가끔'으로 보였다).
- 변경: `Prefabs/Player.prefab`의 Visual SpriteRenderer Sorting Order를 **50**으로 올렸다. 씬의 Player 인스턴스에는 이 값을 덮어쓴 곳이 없다. 플레이어 효과는 몸체 순서를 기준으로 계산되므로(먼지 −1, 조준 표시·글리치 줄기 +2, 검격·접촉 +3) 같이 올라간다.
- 결과: 적 몸체(10~14)와 그 위의 효과(포탑 머리, 레이저, 방패병 방어막, 빨간 위험구역, 보라 영역, 피격 섬광)와 적 탄·폭발(12~14), 안내 NPC, 휴식 구역 안내판이 모두 플레이어 아래에 그려진다. 그래서 빨간 위험구역이나 레이저가 플레이어 몸 위를 지날 때도 플레이어는 덮이지 않는다. 피격 화면 효과(32760)만 플레이어 위다.
- 정렬 레이어를 따로 만들지 않고 숫자만 올린 이유: 2D 조명은 정렬 레이어 단위로 대상을 정하고, 피격 화면은 Default 레이어 32760이라 레이어를 나누면 플레이어가 피격 화면보다 위로 갈 수 있다.
- 지키는 테스트: EditMode `RenderOrderTests` — 적·보스·NPC 프리팹의 모든 렌더러가 같은 레이어이고 (순서 + 런타임 여유 2)가 플레이어보다 낮은지, 위험구역·영역 오프셋과 적 탄(+2)이 플레이어보다 낮은지 본다. 새 적·효과의 순서를 올리면 이 테스트가 실패한다.
- 저장하면서 Player 프리팹에 PlayerVisual의 기본값 필드 3개(`showGlitchRange` false, `damageBlinkInterval` 0.08, `damageBlinkAlpha` 0.2)가 함께 기록됐다. 코드 기본값과 같아 동작은 그대로다.

