# Movement Lab 검증 기록

## 1·2타 작은 넉백 / 3타 큰 넉백

2026-09-25, Unity 6000.3.23f1 / Windows Editor.

- 옆 공격의 일반 타격에 Light Knockback Speed=3 / Deceleration=30을 추가하고 콤보 막타는 Knockback Speed=12 / Deceleration=18로 조정했다. 동일한 초기 속도·감속 경로를 사용하며 수직 속도는 보존한다. 기본 3타 외 콤보 길이 설정도 IsFinisher로 구분하고 콤보 비활성화 시 작은 넉백, 명중 반응 비활성화 시 넉백 제외를 유지한다.
- 전체 Play Mode **168/168 통과**, 15:55:33~15:56:50 KST, 76.327초 (`Library/PrototypeValidation/ComboKnockbackPlayMode.xml`). 신규 검사는 좌우 방향에서 1·2타의 짧은 이동(0.02~0.35)과 3타의 긴 이동(2.5 이상, 일반 타격의 10배 이상), 작은 넉백 설정의 유효성·속도 0을 확인했다. 실제 저장된 빠른 공격 설정의 3연타 검사에서도 첫 두 타 이후 사거리 유지와 막타 이동 거리(2.5~4.5)를 확인했다. 기존 공중 기본 공격·교체 가능한 연계 규칙 검사도 새 작은 넉백 동작에 맞춰 통과했다.
- 전체 Edit Mode **25/25 통과**, 15:58:00 KST, 0.098초 (`Library/PrototypeValidation/ComboKnockbackEditMode.xml`). C# 컴파일 오류·경고 없음, Unity MCP Console 새 경고/오류 없음(cursor 99), 수정 범위 diff 공백 검사 통과, 원본/검증 복사본 `_Game` 해시 차이 0개.
- 동일 버전의 격리 Unity 프로젝트에서 검증했다. 원본 에디터 수동 조작 및 실행 파일 빌드는 수행하지 않았다. 사용자 변경 공격 시간·잠복 이동 속도·적 체력 및 씬 배치를 보존했다.

## 3타 감속 넉백

2026-09-25, Unity 6000.3.23f1 / Windows Editor.

- GroundHitReaction의 옆 공격 막타를 GroundMovement2D.ApplyKnockback으로 연결했다. 초기 수평 속도만 한 번 설정하고 매 물리 단계 실제 Rigidbody 속도를 Knockback Deceleration(기본 24)만큼 감속한다. Knockback Speed=6을 유지했다. 수직 속도·중력은 보존하며 벽 검사는 수평 속도를 0으로 만들어 장애물이 제거돼도 다시 밀어붙이지 않는다. 기존 ApplyForcedMovement의 띄우기/내려찍기 경로는 유지한다.
- Forced Duration=0.35초는 최소 행동 제한이며 속도가 먼저 멈춰도 남은 제한을 유지한다. 감속 시간이 더 길면 잔여 넉백이 끝날 때까지 IsForcedMoving으로 AI/MoveTo/Stop 덮어쓰기를 방지한다. 새 넉백은 방향·속도·감속을 교체하고 사망/비활성화는 초기화한다.
- 변경 전 전체 Play Mode **126/156 통과, 30개 실패** (`Library/PrototypeValidation/KnockbackBaselinePlayMode.xml`). 기존 검사가 고정 시각/몸체 위치/30 HP를 가정해 현재 조정된 공격 시간, 잠복 이동 속도, 적 체력에서 실패했다. PlayerGlitchCombatTests의 복제 설정에 검사 전용 0.12/0.06/0.18초·내려찍기 준비 0.5초·잠복 이동 속도 3을 명시하고 UnitCombatFsmTests의 복제 적을 30 HP로 고정했다. 실제 에셋의 Windup=0.006, Active=0.1, Recovery=0.08, Slam Hover=0, Underground Move Speed=10, 적 MaxHealth=1000 및 사용자 씬 편집은 보존했다.
- 최종 전체 Play Mode **166/166 통과**, 15:48:05~15:49:17 KST, 72.758초 (`Library/PrototypeValidation/KnockbackPlayMode.xml`). 신규 10개는 좌우 단조 감속/최소 제한/AI 명령 무시, 벽 정지 후 벽 제거에도 재가속 없음, 감속이 제한 시간보다 긴 경우/재피격 교체/비활성화, 수직 속도 유지/내려찍기 교체, 실제 적 FSM 피격·사망 정리, 실제 저장된 빠른 공격 설정의 1·2·3타 명중/넉백, 잘못된 감속값 4종 검사다. 기존 고정형 적 검사에도 넉백 거부를 추가했다.
- 전체 Edit Mode **25/25 통과**, 15:50:06 KST, 0.095초 (`Library/PrototypeValidation/KnockbackEditMode.xml`). C# 컴파일 오류·경고 없음, Unity MCP Console 새 경고/오류 없음(cursor 94), 수정 범위 diff 공백 검사 통과, 원본/검증 복사본 `_Game` 해시 차이 0개.
- 동일 버전의 격리 Unity 프로젝트에서 검증했다. 원본 에디터 수동 조작 및 실행 파일 빌드는 수행하지 않았다. 프리팹·씬 변경 없이 기존 PlayerCombatTuning에 감속 설정만 추가했다.

## 히트스톱·방향성 카메라 흔들림·공격 입력 예약

2026-09-25, Unity 6000.3.23f1 / Windows Editor.

- PlayerCombat에 실제 피해 결과의 방향/강도 전달과 후딜레이 마지막 0.1초의 1회 공격 예약을 추가했다. 같은 검격의 다중/추가 타격은 연출을 반복하지 않는다. 처치 타격도 연출하고 헛공격/무적은 제외한다. 내려찍기 하강과 착지 명중은 별도 단계다. 예약은 실행 시 최신 위치/대상을 사용하며 피격/Reset/성공한 후딜레이 글리치 취소에서 초기화한다.
- PlayerUnit이 일반 C# PlayerImpactFeedback을 생성한다. 싱글 플레이 Time.timeScale 정지(일반 0.035초, 막타·띄우기·출현 0.055초, 내려찍기 0.08초)와 연결된 고정 Aim Camera의 방향성 감쇠 오프셋을 관리한다. 비스케일 시간으로 종료하고 이전 배속을 복원한다. FixedDeltaTime은 건드리지 않는다. 입력 전에 카메라 오프셋을 제거해 조준 좌표를 보존한다. 추가 패키지/씬 컴포넌트/스프라이트/효과음은 없다. 설정은 기존 PlayerCombatTuning에 연결했다.
- 전체 Play Mode **156/156 통과**, 15:30:43~15:31:53 KST, 69.874초. 결과 `Library/PrototypeValidation/ImpactFeedbackPlayMode.xml`. 신규 17개는 예약 시점/단일 소비/대상 변경/취소/0 설정, 실제 명중 중 물리·공격 시간 정지와 재개, 정지 중 입력 수집과 예약 연계, 처치/다중 적/늦은 추가 타격의 중복 연출 방지, 헛공격/무적 제외, 일반/막타/출현/내려찍기 강도·방향, 네 방향 카메라 원위치/조준 복원, 비활성화 정리, 기존 배속/기존 정지/외부 배속 변경 보존, 가장 강한 연출 선택과 무카메라 동작, 설정 검증을 포함한다.
- 전체 Edit Mode **25/25 통과**, 15:32:57 KST, 0.106초. 결과 `Library/PrototypeValidation/ImpactFeedbackEditMode.xml`. C# 컴파일 오류·경고 없음, Unity MCP Console 새 경고/오류 없음(cursor 89), 수정 범위 diff 공백 검사 통과, 원본/검증 복사본 `_Game` 해시 차이 0개. 신규 런타임/검사 스크립트의 Unity 생성 meta도 원본에 반영했다.
- 동일 버전의 격리 Unity 프로젝트에서 검증했다. 원본 에디터 수동 조작 및 실행 파일 빌드는 수행하지 않았다. 저장 씬·프리팹 배치와 기존 이동/공격 수치는 변경하지 않았다. 진폭과 시간은 초기값이며 현재 단일 플레이어/고정 카메라 구성을 대상으로 한다.

## 플레이어 Hit·Die 애니메이션

2026-09-25, Unity 6000.3.23f1 / Windows Editor.

- 기존 Adventurer ZIP의 hurt 3프레임과 die 7프레임을 원본 그대로 추가했다(10개 바이트 비교 일치). Unity에서 Hit/Die Sprite 전용 비반복 클립·상태를 생성했다. 기존 Controller 상태와 GUID를 유지하고 두 상태만 추가했다. Root Motion/Animation Event는 없다.
- PlayerUnit이 피격 표시 시작과 사망 지연을 소유하고 IPlayerActionState.ReactionPresentation으로 전달한다. PlayerVisual은 Hit/Die를 공격·이동보다 우선 재생한다. 피격은 기존 Hit Stun=0.25초에 맞추며 재피격 시 재시작한다. 피격 중 걷기는 유지한다. 사망은 조작/공격/글리치를 정리하고 중력은 유지하며 Death Duration=0.7초 뒤 비활성화한다. Player 프리팹의 UnitHealth.DeactivateOnDeath=false로 연결했다. 0초 사망·잠복 몸체 복원·종료된 사망의 재활성화도 처리한다.
- 전체 Play Mode **139/139 통과**, 13:52:55~13:54:02 KST, 67.169초. 결과 `Library/PrototypeValidation/PlayerReactionsPlayMode.xml`. 새 검사 5개는 재피격/이동 복귀/비활성화 초기화, 사망 7프레임/입력 차단/지연 종료/부활 방지, 잠복 사망 복원/0초 사망, 표시 우선순위/프레임/초기화, 렌더 캡처다. 기존 내려찍기 사망 취소 검사는 즉시 비활성화 대신 다음 상태 처리 단계를 기다리도록 변경했다.
- 전체 Edit Mode **25/25 통과**, 13:54:47 KST, 0.099초. 결과 `Library/PrototypeValidation/PlayerReactionsEditMode.xml`. 기존 에셋 검사를 Hit/Die까지 확장해 Sprite 참조·임포트·Missing Script·Animator 설정을 확인했다.
- `Library/PrototypeValidation/PlayerReactions.png`를 직접 확인했다. 위쪽은 피격 3프레임, 아래쪽은 사망 7프레임이며 크기·발 위치·마지막 자세를 확인했다. C# 컴파일 오류·경고 없음. Unity MCP Console 새 경고/오류 없음(cursor 84), 수정 범위 diff 공백 검사 통과, 원본/검증 복사본 `_Game` 해시 차이 0개.
- 동일 버전의 격리 Unity 프로젝트에서 검증했다. 원본 에디터 수동 조작 및 실행 파일 빌드는 수행하지 않았다. 저장 씬과 기존 이동/공격 수치는 변경하지 않았다.

## 공격 중 이동·점프 차단

2026-09-25, Unity 6000.3.23f1 / Windows Editor.

- PlayerUnit이 기존 PlayerCombat.IsAttacking 상태에 따라 준비·판정·후딜레이 중 수평 속도를 즉시 0으로 만든다. 점프 요청과 버퍼를 차단하며, 공격 종료/피격 취소 후 누르고 있는 방향의 이동을 재개한다. 별도 FSM이나 Unit/FsmRuntime 기능은 추가하지 않았다. CharacterMotor2D가 속도 쓰기를 담당한다.
- 일반 공중 공격의 중력, 출현 상승, 내려찍기 준비/하강/충격파, 후딜레이 글리치 취소는 유지한다. 씬·프리팹·이동/공격 수치는 변경하지 않았다.
- 전체 Play Mode 실행은 **133/134 통과**, 13:13:05~13:14:10 KST, 64.797초. 결과 `Library/PrototypeValidation/AttackMovementLockPlayMode.xml`. 신규 이동/점프 검사가 공격 종료 프레임에도 새 JumpPressed를 만들어 실패했다. 마지막 점프를 후딜레이 안에서 보내도록 테스트만 수정한 후 해당 검사 **1/1 통과**, 13:15:06~13:15:07 KST, 0.811초 (`AttackMovementLockFocused.xml`). 런타임 코드는 최초 전체 실행 이후 변경하지 않았다. 최종 코드 기준 134개 모두 통과 결과를 확보했으며 전체 묶음을 다시 실행하지는 않았다.
- 신규 검사는 달리기 중 즉시 정지, 공격+점프 동시 입력, 모든 일반 공격 단계에서 이동/점프 차단, 후딜레이 입력의 버퍼 제거, 종료 후 이동/새 점프 복구, 이동 입력 중 출현 상승 보존을 확인했다. 기존 공중 공격 검사는 수평 정지·중력 유지로 기대값을 변경했다. 기존 피격 취소 후 이동, 내려찍기, 글리치 연계 검사도 통과했다.
- 전체 Edit Mode **25/25 통과**, 13:16:09 KST, 0.097초 (`AttackMovementLockEditMode.xml`). C# 컴파일 오류·경고 없음, Unity MCP Console 새 경고/오류 없음(cursor 79), 수정 범위 diff 공백 검사 통과, 원본/검증 복사본 `_Game` 해시 차이 0개.
- 동일 버전의 격리 Unity 프로젝트에서 검증했다. 원본 에디터 수동 조작 및 실행 파일 빌드는 수행하지 않았다.

## 막힌 잠복 위치의 가까운 출현 지점 보정

2026-09-25, Unity 6000.3.23f1 / Windows Editor.

- 출현 공격 요청 시 현재 위치가 막혔으면 같은 바닥의 좌우 후보를 가까운 순서로 검사한다. 0.05월드 단위 간격으로 탐색한 뒤 발견한 빈 경계를 6번 세분화한다. 양쪽이 발견되면 보정 후 거리를 비교하며 0.001 이내 동률은 마지막 실제 잠복 이동 방향, 이동 전에는 원래 몸체 쪽을 우선한다. 콜라이더 offset·전체 몸체 공간·바닥 끝 검사와 대상/지면 유효성을 유지한다. 0.05보다 좁은 빈 중심 좌표 구간은 샘플링에서 놓칠 수 있다는 해상도 한계를 가이드에 기록했다.
- 탐색은 출현 요청 시에만 실행하며, 매 물리 프레임의 CanEmerge와 표시 색은 여전히 현재 열 하나의 검사 결과다. 성공하면 표시 좌표를 실제 보정 출현 위치에 맞춘 뒤 기존 출현 공격을 시작한다. 쿨타임을 다시 시작하지 않는다. 같은 바닥 전체가 막혔거나 대상/지면이 무효이면 기존 정리 경로로 복귀한다.
- 전체 Play Mode **132/132 통과**, 12:54:21~12:55:24 KST, 63.610초. 결과 `Library/PrototypeValidation/NearestEmergencePlayMode.xml`, 로그 `NearestEmergencePlayMode.log`. 신규 5개는 좌우 동률 방향 2개, 선호 방향보다 가까운 반대쪽/복수 장애물, 바닥 끝과 offset Capsule, 실제 공격 입력의 보정 출현·상승·쿨타임 유지다. 기존 차단 검사도 근처 출현 또는 전체 바닥 차단 복귀라는 최신 규칙으로 갱신했다.
- 전체 Edit Mode **25/25 통과**. 결과 `Library/PrototypeValidation/NearestEmergenceEditMode.xml`, 로그 `NearestEmergenceEditMode.log`. C# 컴파일 오류·경고 없음, Unity MCP Console 새 경고/오류 없음(cursor 74), 수정 범위 diff 공백 검사 통과, 원본/검증 복사본 `_Game` 해시 차이 0개.
- 동일 버전의 격리 Unity 프로젝트에서 검증했다. 원본 에디터 수동 조작과 실행 파일 빌드는 수행하지 않았다. 씬·프리팹·기존 이동/공격 설정값은 변경하지 않았다.

## 잠복 중 출현 위치 조절

2026-09-25, Unity 6000.3.23f1 / Windows Editor.

- 기존 Move 액션의 A/D·좌우 방향키 입력을 사용한다. PlayerUnit은 FixedUpdate에서 PlayerGlitch.MoveUnderground를 호출하며 몸체 대신 논리 좌표와 지면 표시를 이동한다. GlitchTuning의 Underground Move Speed 기본값은 3월드 단위/초다. 같은 바닥의 양 끝에서 몸체 전체 너비를 확보하도록 제한하고, 출현 가능 여부는 실제 출현 검사와 같은 함수로 계산해 표시 색을 청록/빨강으로 갱신한다.
- 전체 Play Mode **127/127 통과**, 12:40:48~12:41:52 KST, 63.271초. 결과 `Library/PrototypeValidation/BurrowMovePlayMode.xml`, 로그 `BurrowMovePlayMode.log`. 신규 6개는 좌우 입력·정지·조절한 지점의 출현/상승, 실제 몸체 Kinematic/속도 0/피해 차단 유지, 시간 간격별 이동량, 속도 0 및 유효성 검사, 체류/쿨타임 비연장, 바닥 양 끝 제한, 막힌 지점 표시와 이동 후 복구, 출현 직전 차단 재검사 및 비활성화 복원을 확인한다. 기존 Space 취소 검사에 이동+공격 동시 입력 우선순위도 추가했다.
- 전체 Edit Mode **25/25 통과**, 12:42:56 KST, 0.139초. 결과 `Library/PrototypeValidation/BurrowMoveEditMode.xml`, 로그 `BurrowMoveEditMode.log`. C# 컴파일 오류·경고 없음. Unity MCP Console 새 경고/오류 없음(cursor 69), 수정 범위 diff 공백 검사 통과, 원본/검증 복사본 `_Game` 해시 차이 0개.
- 동일 버전의 격리 Unity 프로젝트에서 검증했다. 원본 에디터 수동 키보드 조작이나 실행 파일 빌드는 수행하지 않았다. 입력 액션·씬·프리팹·기존 이동/공격 수치는 변경하지 않았다. 신규 설정은 GlitchTuning.asset에만 추가했다.

## 겹침 공격의 띄우기 오분류와 잠복 지점 출현

2026-09-25, Unity 6000.3.23f1 / Windows Editor.

- 수정 전 재현 검사 7개 중 5개 실패: 높이 2.6/4.2인 몬스터와 겹쳤을 때 Side 대신 Lift가 선택됐고, 잠복 출현은 표시 X=0 대신 대상 이동에 따라 -0.525/0.975/0.525로 이동했다. 결과 `Library/PrototypeValidation/BurrowOverlapBefore.xml`. 중심 간 높이만 비교하는 공격 선택 및 좌우 후보/오른쪽 동률 우선이 원인이었다.
- PositionAttackSelector는 대상 하단과 플레이어 상단을 비교하여 실제 아래쪽 공격일 때만 Lift를 선택한다(접촉 오차 0.02). TryEmergence는 잠복 당시 표시의 X축을 몸체 중심에 맞추며 콜라이더 offset도 보정한다. 대상 이동을 따라가지 않고, 해당 열의 지상 공간이 막히면 원위치/물리/피해 상태를 복원한다. 출현 공격 Box의 가로 편향도 제거했다.
- 전체 Play Mode **121/121 통과**, 02:24:47~02:25:48 KST, 61.352초. 결과 `Library/PrototypeValidation/BurrowOverlapPlayMode.xml`, 로그 `BurrowOverlapPlayMode.log`. 신규 8개는 몸 크기별 겹침 공격, 대상의 좌/우 이동 후 고정 출현, 열린 좌우 공간으로 우회하지 않는 차단 취소, offset Capsule과 출현 지점 좌우 타격을 검사한다. 실제 입력 출현 검사에도 잠복 X 유지와 수직 상승을 추가했다. 기존 띄우기/출현 피해·콤보·공중 연계·취소·복원 검사를 포함한다.
- 전체 Edit Mode **25/25 통과**. 결과 `Library/PrototypeValidation/BurrowOverlapEditMode.xml`, 로그 `BurrowOverlapEditMode.log`. C# 컴파일 오류·경고 없음. Unity MCP Console 새 경고/오류 없음(cursor 64). 수정 범위 diff 공백 검사 통과, 원본과 검증 복사본 `_Game` 해시 차이 0개.
- 동일 버전의 격리 Unity 프로젝트에서 검증했다. 원본 에디터 수동 플레이나 실행 파일 빌드는 수행하지 않았다. 씬·프리팹·공격/이동 설정값은 수정하지 않았다.

## 위쪽 글리치 후에만 내려찍기 선택

2026-09-25, Unity 6000.3.23f1 / Windows Editor.

- 위쪽 글리치가 성공한 뒤 첫 공중 공격만 Slam을 선택하도록 변경했다. 일반 공중에서는 대상 상대 위치나 출현 이력과 무관하게 기본 Side를 선택하고 이동·중력을 유지한다. 내려찍기의 0.5초 대기·전방 하향 타격·착지 충격파는 유지했다.
- PlayerUnit이 성공한 위쪽 도착 여부와 접지 사실을 전달하고 PlayerCombat이 일회성 내려찍기 가능 상태를 소유한다. 사용·착지·다른 방향 재배치·피격·사망·비활성화로 해제한다. PlayerGlitch에는 공격 상태를 추가하지 않았다.
- 전체 Play Mode **109/109 통과**, 01:12:41~01:13:41 KST, 60.47초. 신규 8개는 한 번 사용 후 기본 공격/콤보 유지, 접지·다른 도착·피격·초기화 후 상태 해제(4개), 글리치 유무 두 구성에서 일반 공중 입력의 기본 피해와 정상 이동, 좌우/아래 글리치 후 기본 공격, 위쪽 글리치 후 착지하고 다시 점프했을 때 기본 공격을 확인했다.
- 기존 내려찍기 구성 검사에는 위쪽 도착이라는 새 전제 정보를 명시했다. 실제 입력으로 위쪽 글리치와 공격을 동시/순차 실행하는 기존 3개 회귀 검사는 그대로 통과했고, 나머지 기존 이동·피격·지하·허수아비·충격파 검사도 통과했다.
- 전체 Edit Mode **24/24 통과**, 01:14:53 KST, 0.057초. 결과: `Library/PrototypeValidation/GlitchOnlySlamPlayMode.xml`, `GlitchOnlySlamEditMode.xml`; 로그: `GlitchOnlySlamPlay.log`, `GlitchOnlySlamEdit.log`. C# 컴파일 오류·경고 없음. Unity MCP Console의 새 경고/오류 없음(cursor 54), git diff --check 통과, 원본과 검증 복사본 _Game 해시 차이 0개.
- 동일 버전의 격리 Unity 프로젝트에서 실행했다. 원본 에디터 수동 조작·실행 파일 빌드는 수행하지 않았으며 씬·프리팹·이동 및 공격 수치는 변경하지 않았다.

## 공중 내려찍기 전방 타격과 적 하향 반응

2026-09-25, Unity 6000.3.23f1 / Windows Editor.

- 0.5초 공중 대기 뒤 Descending 동안 전방 Reach/Width Box로 타격한다. PlayerCombat의 기본 공중 공격에 포함되어 위치 연계 옵션과 독립적이다. GroundHitReaction이 Slam 명중에 Slam Knockdown Speed(기본 36)의 하향 속도를 주며 기존 GroundMovement2D의 중력/충돌과 강제 이동 우선 규칙을 사용한다. 적의 강제 이동 거부 또는 명중 반응 옵션 해제 시 피해만 적용한다.
- 전방 타격과 착지 충격파는 같은 명중 기록을 공유한다. 충격파 반응 메타데이터는 PlayerAttack.Shockwave로 구분하여 기존 피해 전용 반응을 유지한다. PlayerVisual은 하강 중에도 전방 판정을 표시하며, 준비/착지 후에는 그 Box를 표시하지 않는다.
- 전체 Play Mode 101개 실행, **100개 통과/기존 허수아비 검사 1개 실패**, 00:58:31~00:59:30 KST, 59.16초. 신규 6개(방향별 2, 벽/피해 허용 1, 선택적 반응/고정 적 2, 실제 하강/착지 1)는 모두 통과했다. 좌우 전방과 후방 제외, 준비 중 피해 없음, 중복 Collider와 충격파 사이 중복 방지, 벽 차단, 고정형 적, 명중 반응 옵션 해제, 공중 적 하향 속도와 바닥 충돌을 확인했다.
- 실패 원인은 기존 허수아비 검사가 첫 피해 직후 충격파가 이미 생겼다고 가정한 것이다. 새 전방 판정은 착지보다 먼저 명중하므로 실제 착지까지 기다리도록 수정하고 해당 검사 **1/1 재검증 통과**, 01:00:46 KST, 0.77초. 전체 실행 이후 런타임 변경은 없으며 다른 통과 검사도 변경하지 않았다. 최종 검사 코드로 전체 101개를 한 번에 다시 실행하지는 않았다.
- 전체 Edit Mode **24/24 통과**, 01:01:43 KST, 0.052초. 결과: `Library/PrototypeValidation/SlamFrontPlayMode.xml`, `SlamFrontDummy.xml`, `SlamFrontEditMode.xml`; 로그: `SlamFrontPlay.log`, `SlamFrontDummy.log`, `SlamFrontEdit.log`. 최종 C# 컴파일 오류·경고 없음. Unity MCP Console 새 경고/오류 없음(cursor 54), git diff --check 통과, 원본과 검증 복사본 _Game 해시 차이 0개.
- 동일 버전의 격리 Unity 프로젝트에서 실행했다. 원본 에디터 수동 조작과 실행 파일 빌드는 수행하지 않았다. 씬·프리팹·기본 이동 수치는 수정하지 않았다.

## 공중 내려찍기 전 0.5초 정지

2026-09-25, Unity 6000.3.23f1 / Windows Editor.

- 공중 Slam의 준비 시간을 별도 `Slam Hover Duration`(기본 0.5초)으로 분리했다. PlayerCombat이 시간을 관리하고 PlayerUnit은 준비 중 일반 이동·점프를 보류하며 CharacterMotor2D.Hover가 수평·수직 속도를 0으로 유지한다. 물리 모드나 피해 허용은 변경하지 않는다. 준비 중 피격·사망·비활성화도 취소 경로에 포함했다. 지상 공격 준비 시간 0.12초와 기존 착지 충격파는 유지한다.
- 전체 Play Mode **95/95 통과**, 00:38:57~00:39:55 KST, 58.15초. 신규 3개는 0.499초 준비/0.5초 하강 경계·0초 설정·지상 준비 시간 유지, 초기 상승/수평 속도와 반복 이동·점프·공격 입력에도 공중 위치 유지 후 하강/명중, 피격·비활성화·사망 시 공격/정지 해제와 피해·물리 상태를 확인했다. 기존 92개도 통과했다.
- 전체 Edit Mode **24/24 통과**, 00:40:56 KST, 0.059초. 결과: `Library/PrototypeValidation/SlamHoverPlayMode.xml`, `SlamHoverEditMode.xml`; 로그: `SlamHoverPlay.log`, `SlamHoverEdit.log`. C# 컴파일 오류·경고 없음. Unity MCP Console 새 경고/오류 없음(cursor 49), git diff --check 통과, 원본과 검증 복사본 _Game 해시 차이 0개.
- 동일 버전의 격리 Unity 프로젝트에서 실행했으며 원본 에디터 수동 조작과 실행 파일 빌드는 수행하지 않았다. 씬·프리팹 배치와 기존 이동 수치는 변경하지 않았다.

## 유닛 간 충돌 해제·공중 급강하와 착지 충격파

2026-09-25, Unity 6000.3.23f1 / Windows Editor.

- 유닛 몸체의 Ignore Raycast(2) 자기 레이어 충돌을 해제하고 지형 충돌·타깃 조회·피해 판정은 유지했다. 글리치 도착점 검사는 충돌하지 않는 유닛을 장애물에서 제외한다.
- 공격 직전 접지를 확인해 모든 공중 공격을 Slam으로 선택한다. 선딜레이 뒤 모터가 수직 하강하며 몸체 sweep으로 첫 지면에 정지한다. 착지 시 PlayerCombat이 반경 2의 피해를 한 번 적용하고 PlayerVisual이 퍼지는 반원을 표시한다. 하강 중 접촉 피해와 이전 적 하향 속도 반응은 없다. 기본 하강 속도 28, 표시 0.22초, 바닥 없음 제한 3초.
- 최종 전체 Play Mode **92/92 통과**, 00:29:21~00:30:14 KST, 52.43초. 신규 7개는 유닛 겹침/접촉 없음과 지형 접지, 글리치·연계 없는 공중 공격, 좌우 범위 피해/중복 Collider/범위 외 대상, 100 속도에서도 두께 0.06 발판에서 정지, 벽 뒤 피해 차단, 모든 상대 방향·출현 요청의 공중 Slam 선택, 피격·비활성화 정리, 바닥 없음 종료를 확인했다. 기존 내려찍기·허수아비·배치 차단 검사는 변경된 사양으로 갱신했다. 이동·FSM·지하·입력·미리보기 회귀도 통과했다.
- 첫 실행은 90/92였다. 범위 외 적이 선딜레이 동안 이동한 플레이어의 실제 충격파 범위에 들어온 테스트 배치와, 비활성 유닛도 CanReceiveDamage=true라고 기대한 단언을 수정했다. 최종 검사에서는 실제 충격파 중심과 Collider 사이 거리가 반경 밖임을 검증하고 DamageEnabled와 활성 조건을 구분했다. 런타임 오류를 숨기기 위해 검사 범위를 축소하지 않았다.
- 전체 Edit Mode **24/24 통과**, 00:31:10 KST, 0.053초. 충격파 표시 시점을 고정하도록 캡처 검사를 보완하고 해당 Play Mode **1/1 추가 통과**, 00:32:05~00:32:06 KST, 1.49초. 테스트에서만 표시 시간을 늘리고 Time.timeScale을 일시 정지한 뒤 finally로 복원했다. 실제 카메라 캡처 `Library/PrototypeValidation/GlitchShockwave.png`에서 노란 반원을 확인했다. 전체 검사 이후 런타임 변경은 없다.
- 결과: `Library/PrototypeValidation/AirSlamFinalPlayMode.xml`, `AirSlamEditMode.xml`, `AirSlamVisual.xml`; 로그는 같은 접두사의 Play/Edit/Visual 로그다. C# 컴파일 오류·경고 없음. 원본 Unity MCP Console의 새 경고/오류 없음(cursor 44). 기존 설정 누락 테스트의 의도된 오류 로그 두 건은 유지한다.
- 동일 버전의 격리 Unity 프로젝트에서 검증했다. 원본과 복사본의 _Game 및 Physics2D 설정 해시 일치, git diff --check 통과. 씬·프리팹 배치와 기본 이동 수치는 수정하지 않았다. 원본 에디터의 수동 조작·실행 파일 빌드는 수행하지 않았다.

## 기본 공격과 글리치·연계 규칙 분리

2026-09-25, Unity 6000.3.23f1 / Windows Editor.

- PlayerCombat이 콤보 진행·공격 시간·피해를 소유하고, 상태 없는 IPlayerComboRule/IPlayerAttackSelector/IPlayerHitReaction을 생성자로 받도록 분리했다. 글리치와 전투 설정은 독립적으로 선택 가능하다. 세 연계의 Inspector 스위치는 기본 활성이고 초기화 시 적용된다. 공통 물리 조회는 UnitPhysics2D로 이동했다.
- 전체 Play Mode **85/85 통과**, 00:02:07~00:02:57 KST, 49.33초. 기존 76개와 신규 9개가 공격 전용·글리치 전용·이동 전용 구성, 연계별 제거 및 전체 제거 시 기본 피해, 교체한 공격 영역·반응, 2타 마무리 반응과 대상 변경 초기화를 확인했다. 기존 내려찍기 회귀·지하 복원·프리뷰·허수아비·이동·적 FSM 검사도 통과했다.
- 공유 규칙을 사용하는 두 PlayerCombat의 콤보 진행이 섞이지 않는 단언을 강화하고 해당 검사 **1/1 추가 통과**, 00:03:48 KST, 0.18초. 이 추가 단언 외에 전체 검사 이후 런타임 코드 변경은 없다.
- 전체 Edit Mode **24/24 통과**, 00:04:42 KST, 0.057초. 결과 파일: `Library/PrototypeValidation/CombatSeparationPlayMode.xml`, `CombatRuleOwnership.xml`, `CombatSeparationEditMode.xml`. 대응 로그: `CombatSeparationPlay.log`, `CombatRuleOwnership.log`, `CombatSeparationEdit.log`.
- C# 컴파일 오류·경고 없음. Play Mode 로그의 설정 누락 두 건은 기존 UnitLifecycleTests의 의도된 검사다. Unity MCP Console의 새 경고/오류 없음(cursor 39). git diff --check 통과, _Game 파일 meta 누락 0개, 원본과 검증 복사본 해시 차이 0개.
- 이전부터 원본 에디터의 MCP 메인 스레드 요청이 시간 초과되어 동일 버전의 격리 프로젝트 `Temp/GlitchValidationProject`에서 실행했다. 원본 에디터 수동 조작과 실행 파일 빌드는 수행하지 않았다. 씬 배치·프리팹·기존 이동 수치는 이번 구조 변경에서 수정하지 않았다.

## 위쪽 글리치 직후 내려찍기 입력 조사·수정

2026-09-24, Unity 6000.3.23f1 / Windows Editor. 보고 경로: 적 위로 글리치한 직후 공격.

- 기존 공격 대기 상태에서 위쪽 글리치와 공격의 동시 입력·순차 입력은 모두 Slam 선택, 실제 명중, 공중 적의 하향 이동으로 통과했다. 따라서 단순 방향 선택이나 같은 물리 프레임의 재배치 자체는 재현 원인이 아니었다.
- 이전 공격의 Recovery 중 위쪽 글리치와 공격을 함께 입력하면 글리치는 성공하지만 공격이 시작되지 않았다. 수정 전 회귀 검사 `SlamAfterRecoveryCancelTopGlitchPreservesSameFrameAttack` 실패: 글리치 뒤 Phase=Ready, Attack=이전 Side 상태. 결과: `Library/PrototypeValidation/SlamRecoveryBaseline.xml`.
- 원인: PlayerUnit.OnUnitUpdate가 Combat.CanAttack이 false인 후딜레이에서 공격 입력을 버렸다. 이후 FixedUpdate에서 글리치가 성공해 후딜레이를 취소해도 공격 요청이 남아 있지 않았다. 공격 요청을 수집하고, 기존 FixedUpdate의 글리치 처리 뒤 TryAttack에서 실행 가능 여부를 검사하도록 조건 한 곳을 수정했다. 실패한 글리치는 공격 제한을 우회하지 않는다.
- 최종 Play Mode **76/76 통과**, 23:34:58~23:35:46 KST, 47.60초. 새로운 5개는 동시/순차 위쪽 글리치·내려찍기, 후딜레이 취소 시 다음 콤보 타수와 명중, 글리치 실패 시 후딜레이 유지, 지상 허수아비 타격을 확인했다. 기존 71개도 통과했다.
- 별도 관찰: 지상 허수아비에 내려찍기는 정상 명중했다(DamageVersion=1). 다음 물리 프레임의 속도는 (0,0)으로, 바닥이 하향 이동을 막는다. 허수아비의 체력 유지 설정으로 HP는 100이다. 이 경우 색 변화가 명중 표시이며 플레이어 급강하 기능은 기존 사양에 없다.
- `SlamFixPlayMode.xml`, `SlamFixPlay.log`에 최종 결과를 보관했다. C# 컴파일 오류·경고 없음, git diff --check 통과. 기존 격리 Unity 프로젝트에서 재현·검증했고 원본 _Game과 검증 복사본의 해시 차이 0개를 확인했다. 원본 에디터 수동 입력·Edit Mode 재실행·실행 파일 빌드는 수행하지 않았다.

## 훈련용 허수아비

2026-09-24, Unity 6000.3.23f1 / Windows Editor.

- TrainingDummy 프리팹·정의 에셋을 만들고 MovementLab 중앙 바닥 `(0, 0.92, 0)`에 한 개 추가했다. 기존 씬 객체의 배치는 유지했다. Unit/UnitHealth/GroundMovement2D를 사용하며 FSM과 공격 기능은 없다. Block 스프라이트를 조합한 외형과 명중 시 색 변화를 추가했다.
- 허수아비의 UnitHealth에만 Preserve Health On Damage를 켰다. 타격 수신·피해량 반환·DamageVersion 증가는 유지하고 체력을 차감하지 않는다. 기존 플레이어·적의 기본 체력·사망 동작은 유지한다.
- 최종 Play Mode **71/71 통과**, 23:21:49~23:22:36 KST, 46.62초. 신규 3개가 수동 대기, 반복 치명타 20회에도 생존·체력 유지, 피해 차단/비활성화, 글리치 조준·배치, 3타 콤보·명중 표시·밀쳐내기, 지하 출현으로 플레이어와 허수아비 띄우기를 검증했다. 기존 68개도 모두 통과했다.
- 프리팹 Missing Script 0개, _Game 파일의 meta 누락 0개. C# 컴파일 오류·경고 없음, git diff --check 통과. 실제 Play Mode 카메라 캡처 `Library/PrototypeValidation/TrainingDummy.png`로 외형을 확인했다.
- 결과: `Library/PrototypeValidation/TrainingDummyPlayMode.xml`, 로그: `TrainingDummyPlay.log`. 기존 격리 Unity 프로젝트에서 생성·검증했고 원본 _Game 파일과 검증 복사본의 해시 차이 0개를 확인했다. 일회성 에셋 생성 코드는 Temp의 격리 프로젝트에만 있으며 원본 프로젝트에는 추가하지 않았다. 원본 에디터 수동 입력, Edit Mode 재실행, 실행 파일 빌드는 이번 변경에서 수행하지 않았다.

## 글리치 도착 위치 미리보기

2026-09-24, Unity 6000.3.23f1 / Windows Editor.

- 마우스로 적의 상하좌우를 조준하면 선택된 도착 몸체 영역을 청록색 테두리로 표시한다. 배치가 막혔거나 쿨타임·공격/피격 제한 중이면 빨간색이며, 지하 진입은 지면 위 납작한 표시를 사용한다. 조준·대상·입력 유효성 상실, 지하 진입, 플레이어 비활성화 시 정리한다.
- PlayerGlitch의 읽기 전용 배치 검사를 미리보기와 실행이 공유한다. PlayerUnit이 입력과 전투 제한을 반영하고 IPlayerActionState를 통해 PlayerVisual에 전달한다. 추가 컴포넌트·프리팹 연결·텍스처는 필요 없다.
- 최종 Play Mode **68/68 통과**, 23:11:38~23:12:23 KST, 44.29초. 새 6개 검사는 4방향 미리보기와 실행 위치 일치·게임 상태 불변, 방향 변경과 차단/지하 표시, 쿨타임·공격 제한·조준/대상/비활성화 정리를 확인한다. 기존 62개도 함께 통과했다.
- 첫 실행에서 캡처 보조 함수가 제거된 임시 벽을 읽어 1개 실패했다. Unity 객체의 null 검사를 추가한 뒤 전체 재실행이 통과했다. 게임 실행 코드의 오류는 아니었다.
- 실제 Play Mode 카메라의 `Library/PrototypeValidation/GlitchPreviewRight.png`, `GlitchPreviewBlocked.png`, `GlitchPreviewUnderground.png`를 확인했다. 결과 XML: `GlitchPreviewPlayMode.xml`, 로그: `GlitchPreviewPlay.log`. 최종 C# 컴파일 오류·경고 없음, git diff --check 통과.
- Unity MCP recompile 요청의 시간 초과가 지속돼 기존 격리 검증 프로젝트에서 실행했다. 검증 복사본과 원본 _Game 파일의 해시 차이 0개를 확인했다. 원본 에디터 수동 입력·실행 파일 빌드·Edit Mode 재실행은 이번 미리보기 변경에서 수행하지 않았다.

## 글리치·플레이어 공격 구현

2026-09-24, Unity 6000.3.23f1 / Windows Editor.

- PlayerGlitch·PlayerCombat 일반 C# 실행 객체와 GlitchUtility, 기능별 설정 에셋을 추가했다. PlayerUnit이 입력 요청과 실행 순서를 조정하고 모터가 플레이어 물리를 변경한다. Unit/FsmRuntime은 변경하지 않았다.
- 마우스 조준·Shift 글리치·좌클릭/J 공격을 연결했다. 4방향 배치, 논리 지하·출현, 방향별 타격과 3타 콤보, 피격 취소, 후딜레이 글리치 취소, 적 강제 이동을 구현했다. 기존 PlayerTuning과 씬 위치는 그대로이며 씬 수정은 Aim Camera 참조 1개뿐이다.
- 최종 Play Mode **62/62 통과**, 22:46:22~22:47:05 KST, 43.63초. 기존 37개와 신규 25개를 모두 실행했다. 결과: `Library/PrototypeValidation/GlitchPlayMode.xml`, 로그: `GlitchBatchPlay.log`.
- 최종 Edit Mode **24/24 통과**, 22:48:24 KST, 0.061초. 기존 16개와 새 방향·배치 계산 검사 8개를 실행했다. 결과: `Library/PrototypeValidation/GlitchEditMode.xml`, 로그: `GlitchBatchEdit.log`.
- 신규 검사는 조준 우선·보정 해제, 방향 경계·몸 크기, 벽·천장·다른 유닛에 의한 취소, 대상 소멸, 실패 시 이동·쿨타임 보존, 지하 타깃·피해 제외와 종료 복원, 콤보 대상 변경·헛공격·중복 타격 방지, 고정형 적, 상하 공격·출현, 강제 이동 지속, 입력 우선순위·점프 버퍼 초기화·피격 제한을 확인한다. 실제 FixedUpdate 물리에서 지하 → 출현 타격 → 플레이어·적 상승 → 공중 재글리치 흐름도 확인했다.
- 가상 Shift 눌림·유지·재입력과 입력 비활성화 검사 통과. 배치 에디터에서 장치를 포커스 설정 변경 전에 만들면 비활성 상태로 추가되는 원인을 확인했고, 검사에서 포커스 설정 후 가상 장치를 생성하도록 수정했다. 설정은 finally에서 복원한다.
- 기존 점프 검사에 남아 있던 이전 높이 기준을 현재 에셋 값에 맞춰 읽도록 수정했다. 기존 키보드·게임패드 검사도 실물 장치가 없는 배치 환경에서 PlayerInput이 가상 장치를 정상 연결하도록 보완했다. 게임의 점프 높이·입력 설정은 변경하지 않았다.
- 공격 판정 도형과 지하 지면 표시를 실제 Play Mode 카메라로 렌더링하고 이미지로 확인했다. `Library/PrototypeValidation/GlitchAttack.png`, `GlitchUnderground.png`에 보관했다. 새 스프라이트·효과음·HUD는 추가하지 않았다.
- 최종 배치 실행에서 C# 컴파일 오류·경고는 없었다. 테스트 중 게임 오류 로그는 LogAssert.Expect로 의도한 PlayerTuning·UnitDefinition 누락 각 1건이다. _Game 파일의 meta 누락 0개, git diff --check 통과.
- 검증 환경: 원본 에디터의 Unity MCP 메인 스레드 요청이 60초 후 반복해서 시간 초과되어, Assets/Packages/ProjectSettings를 `Temp/GlitchValidationProject`에 복사하고 동일 Unity 버전으로 별도 배치 에디터를 실행했다. 원본 에디터를 종료하지 않았다. MCP Console에는 도구 시간 초과가 남아 있으며, 배치 로그의 라이선스 연결·entitlement 메시지는 게임 테스트 오류와 구분한다. 최종 결과 XML의 시간·통과 수를 기준으로 검증했다.
- 실물 키보드·마우스의 조작감과 원본 에디터 Game 뷰 수동 조작, 실행 파일 빌드는 이번 검증에 포함하지 않았다. 지하는 Rigidbody가 없는 정적인 수평 BoxCollider2D 지면을 지원하며 원래 위치의 보이지 않는 몸체가 다른 유닛을 막는 제약은 계획대로 유지한다.

## FSM 상태 애니메이션 연결

2026-09-16, Unity 6000.3.23f1 / Windows Editor.

- Pig 원본 ZIP의 Attack 5프레임·Hit 2프레임·Dead 4프레임을 추가. 보관된 ZIP SHA-256이 ThirdPartyNotices의 기존 값과 일치함을 확인. 34×28, 16 PPU, Point, 무압축, 기존 피벗 유지.
- 선택적 IUnitAnimationState 계약으로 FSM의 동작·반복 번호·시간·방향을 UnitVisual2D가 읽게 연결. 실제 매 공격·재피격 때 재시작, 공격 타격 프레임은 선딜레이와 동기화, Hit/Death는 경직/사망 시간에 맞춰 샘플링. 피해·전환 판단은 기존 FSM이 유지.
- PatrolEnemy.controller에 ActionTime Float과 비반복 Attack/Hit/Death를 추가. 기존 Moving, Idle/Run 및 참조 GUID 유지. 프리팹 Visual의 State Source를 루트 Unit에 연결.
- Unity 컴파일 성공(errors=[]). 첫 신규 애니메이션 검사 포함 실행은 11/12 통과. 피격 검사에서 FSM Update·Visual LateUpdate·Animator 평가 전에 결과를 읽는 타이밍을 수정해, 고정 두 프레임 대신 제한 시간 내 실제 표시 상태를 확인하도록 보완.
- 최종 전체 Play Mode **37/37 통과**, 40.61초, 14:59:07 KST 종료. 연속 Attack 재생·대기 중 Idle·정지 공격 방향, Hit 중단/재피격/비활성화 복귀, 사망 4프레임 후 비활성화와 기존 이동·점프·입력·순찰·NPC 외형 검사를 확인.
- 프리팹 Missing Script 0개, State Source 연결 정상, Root Motion 꺼짐. 새 클립 길이 Attack 0.5초 / Hit 0.2초 / Death 0.4초이며 모두 비반복·Sprite 전용 바인딩. 실제 재생 시간은 FSM 수치와 Visual 후속 동작 설정을 따름. 신규 meta 누락 0개.
- 테스트 도구 요청 지연과 재시도 중 테스트 실행 충돌로 중단 로그가 발생했음. 최종 단일 전체 실행에서는 모두 통과했으며 새 오류는 의도된 PlayerTuning·UnitDefinition 누락 검사 각 1건뿐. 과거 중단 로그를 최종 런타임 오류와 구별한다.
- 최종 Play Mode 정지, MovementLab 미저장 변경 없음. 씬·입력·플레이어 외형·프로젝트 설정 변경 없음. 적 프리팹을 선택해 둠.
- 결과: `Library/PrototypeValidation/PlayMode.xml`, 복사본 `AnimationPlayMode.xml`. 이번 애니메이션 변경에서 Edit Mode 재실행·실행 파일 빌드·성능 벤치마크는 수행하지 않음. 사용 가이드·출처 문서·Git 제외 AGENTS.md 갱신.

## 지상 유닛 FSM 전투 상태 추가

2026-09-16, Unity 6000.3.23f1 / Windows Editor.

- GroundUnitFsmDefinition에 순찰·추적·공격·피격·사망과 Inspector 전환 수치 추가. 이전 PatrolFsmDefinition의 스크립트 GUID와 PatrolEnemyFsm 에셋 연결을 유지.
- UnitHealth·UnitCombat2D를 기능 컴포넌트로 추가. 적 프리팹에는 두 기능, 플레이어에는 UnitHealth를 연결. 기존 Unit/FsmRuntime과 입력·모터·이동 수치는 이번 작업에서 변경하지 않음.
- Unity MCP 최종 컴파일 성공(errors=[]). Editor 어셈블리의 Runtime 참조 누락으로 발생했던 초기 컴파일 오류는 asmdef 참조 추가로 해결.
- 신규 검사 첫 실행은 테스트의 Rigidbody 위치 변경 직후 Transform을 읽는 시점 문제로 3/9 통과. 테스트 위치를 Transform으로 지정하고 Physics2D.SyncTransforms를 호출하도록 수정한 뒤 **9/9 통과**. 해당 수정은 테스트에만 적용.
- 전체 Play Mode **34/34 통과**, 14:39:12~14:39:50 KST, 37.41초. 새 검사 9개와 기존 이동·점프·입력·순찰·애니메이션·Unit 초기화 검사 25개 포함.
- Edit Mode **16/16 통과**, 14:40:07 KST, 0.014초. 기존 FSM 실행기와 이동 계산 검사 포함.
- 새 검사는 실제 물리 추적·타깃 유지/해제, 공격 선딜레이/재사용 대기시간, 사거리 이탈·벽 차단, 피격 시 공격 취소·추가 피해의 경직 초기화, 사망 우선순위·지연·0초 비활성화, 개체별 체력과 재활성화 시 부활 방지, 잘못된 전환 수치를 확인.
- 플레이어·적 프리팹 Missing Script 0개. 정의·체력·충돌체·Sprite·Animator 연결 정상, Root Motion 꺼짐. 적은 사망 지연 사용, 플레이어는 체력 0에서 즉시 비활성화 설정 확인. _Game 신규 파일의 meta 누락 0개.
- 최종 Console의 이번 전체 실행 오류는 의도적으로 검사하는 PlayerTuning·UnitDefinition 누락 각 1건뿐. 초기 컴파일 오류와 이전 도구 경고는 기록에 남아 있으나 최종 검사에서 새로운 예외는 없음.
- 최종 Editor ready, Play Mode 정지, 컴파일/도메인 리로드 없음. MovementLab 미저장 변경 없음. FSM 설정 에셋을 Inspector에 선택해 둠.
- MCP 요청 1회가 응답 대기하여 상태 확인 후 재시도. 최종 실행 결과 XML을 `Library/PrototypeValidation/CombatPlayMode.xml`, `CombatEditMode.xml`에 복사 보관. MCP 직접 실행의 `Tests.xml`은 마지막 실행 결과로 덮어써지므로 파일별 결과를 구별한다.
- 새 공격·피격·사망 애니메이션, 플레이어 공격 입력, 회복/부활, 실행 파일 빌드·성능 벤치마크는 포함하지 않음. 사용 가이드와 Git 제외된 AGENTS.md 갱신.

## Unit 초기화 실패 후 재활성화 차단 수정

2026-09-16, Unity 6000.3.23f1 / Windows Editor.

- 기존 코드에서 PlayerTuning 누락으로 파생 초기화가 중단돼도 Unit이 initialized=true로 기록하는 문제를 회귀 테스트로 재현. 컴포넌트를 다시 활성화했을 때 enabled=false 기대와 달리 true가 되어 신규 검사 1개 실패 확인.
- 초기화 훅을 bool TryInitializeUnit으로 변경. 성공했을 때만 초기화 완료로 표시하며 false면 Unit이 비활성화. PlayerUnit은 설정 누락 시 오류를 남기고 false 반환, 모터·입력·외형 연결 완료 시 true 반환.
- 같은 개체에서 초기화 재시도 없이 컴포넌트·GameObject 재활성화를 모두 차단. 신규 테스트에서 Motor·FSM 미생성 유지와 예상 밖 로그 없음 확인.
- Unity MCP 컴파일 성공(errors=[]). Play Mode **25/25 통과**, MCP 보고 실행 시간 37.01초. 기존 입력·이동·점프·순찰·애니메이션·정상 Unit 재활성화 검사 포함.
- 최종 에디터 ready, Play Mode 정지, 컴파일·도메인 리로드 없음. Console에서 재현 실행의 예상 Tuning 누락 1건, 수정 후 전체 실행의 예상 Tuning·Definition 누락 각 1건 확인. 예상 밖 오류 없음.
- 결과는 이번 MCP 직접 실행의 `Library/PrototypeValidation/Tests.xml`에 기록. 과거 PlayMode.xml 결과와 구별한다. Edit Mode 재실행·빌드는 포함하지 않음.
- 사용 가이드와 로컬 AGENTS.md의 초기화 계약 갱신. Inspector 의존성 검증·유닛 전체 정지 규약 등 나머지 구조 보완점은 이번 범위에 포함하지 않음.

## 적·NPC 공용 이동·외형 컴포넌트로 정리한 후 결과

2026-09-13, Unity 6000.3.23f1 / Windows Editor.

- PatrolMovement2D → GroundMovement2D, PatrolVisual2D → UnitVisual2D로 변경하며 기존 스크립트 GUID 보존. PatrolEnemy 프리팹의 컴포넌트 fileID·Unit 정의·FSM·충돌체·Animator 유지. 외형의 이동 컴포넌트 참조를 루트 Rigidbody2D 참조로 명시적으로 교체.
- GroundMovement2D에서 Collider2D 선택, 발판 끝 정지 여부, 도착 오차·발밑 검사 길이를 설정 가능. 순찰 방향·대기는 FSM이 결정하고 이동은 컴포넌트의 FixedUpdate에서 실행. Unit·FsmRuntime에 물리 갱신을 추가하지 않음.
- UnitVisual2D는 선택적 Rigidbody2D 속도로 Moving Bool·좌우 반전을 처리. Body가 없는 정지 NPC에서도 같은 외형 컴포넌트를 사용. 초기화에서 Animator의 Moving Bool 연결 확인.
- Unity 컴파일 통과. Play Mode **24/24 통과**, 21:26:33~21:27:10 KST, 36.89초. 기존 플레이어·순찰 적·Unit/FSM 검사 20개와 신규 공용 기능 검사 4개 통과.
- 신규 검사는 FSM 없는 Npc 정의·CapsuleCollider2D로 목적지 이동·도착·Idle/Run·양방향 반전, 이동 비활성화 후 명령 정리, 발판 끝에서 스스로 반전하지 않고 정지, 발판 끝 정지 해제 시 통과·낙하, 물리·이동·FSM 없는 정지 NPC 외형 사용을 확인.
- 실제 PatrolEnemy 프리팹의 Missing Script 0개, 이동·외형 참조 정상, 정의의 체력 30·공격력 5 유지. 최종 Console에는 정의 누락 방어 테스트의 예상 오류 1건만 있으며 예상 밖 오류 없음.
- 검사 종료 후 Play Mode·컴파일 종료, MovementLab의 미저장 변경 없음 확인. 결과는 `Library/PrototypeValidation/PlayMode.xml`에 저장. 사용 가이드와 로컬 AGENTS.md 갱신.
- 최종 작업 트리에서 MovementLab에 저장된 PatrolEnemy 인스턴스 추가를 발견. 이번 구현 명령은 씬 배치를 수행하지 않았으며, 에디터의 기존 배치(-3.8173537, 0.52298474)를 되돌리지 않고 보존. 해당 씬 인스턴스도 공통 이동·외형·Body 연결 정상, Missing Script 0개 확인.
- Edit Mode 재실행·실행 파일 빌드·성능 벤치마크는 포함하지 않음. NPC는 테스트에서 생성했으며 새 게임용 NPC 프리팹이나 씬 배치는 추가하지 않음.

## 유닛 정의의 체력·공격력 추가 검증

2026-09-13, Unity 6000.3.23f1 / Windows Editor.

- UnitDefinition에 정수 MaxHealth·AttackPower 추가. Inspector에서 각각 1 이상·0 이상으로 제한하고 TryValidate에서도 검사. 새 정의의 기본값은 100/10.
- PlayerDefinition은 100/10, PatrolEnemyDefinition은 30/5로 저장. 기존 ID·분류·FSM 연결 유지 확인. 현재 체력이나 전투 실행 기능은 추가하지 않음.
- Unity MCP에서 새 타입을 사용한 명령 컴파일·실행 성공. 실제 정의 2개의 유효성, 새 정의 기본값, 경계값 5건(체력 1·0·-1, 공격력 0·-1, int 최대값 조합) 검사 통과.
- Play Mode **20/20 통과**, 19:39:51~19:40:22 KST, 31.60초. 기존 플레이어 입력·이동·점프, 순찰 적·애니메이션, Unit 정의·FSM 생명주기 검사 통과. 결과는 `Library/PrototypeValidation/PlayMode.xml`에 저장.
- 종료 후 Play Mode·컴파일 종료와 두 정의의 100/10·30/5 유지 확인. Console에는 정의 누락 방어 테스트의 예상 오류 1건만 있으며 예상 밖 오류 없음.
- 사용 가이드와 로컬 AGENTS.md에 수치 조절 위치와 공유 설정의 역할 기록. Edit Mode 재실행·실행 파일 빌드는 포함하지 않음.

## Unit 직접 부착으로 단순화한 후 결과

2026-09-13, Unity 6000.3.23f1 / Windows Editor.

- Unit의 abstract 지정 제거. 몬스터·NPC는 Unit을 직접 부착하고, 플레이어는 PlayerUnit : Unit 상속을 유지.
- 빈 FsmUnit 클래스와 메타 파일 제거. PatrolEnemy의 컴포넌트 fileID를 유지하면서 스크립트 참조를 기존 Unit.cs GUID로 교체. 정의 데이터·FSM·이동·외형 연결 보존.
- 최초 임포트의 메모리 상태에서 정의 참조가 비어 있는 것을 확인해 프리팹 재임포트·참조 확인 후 테스트 실행. 최종 직접 Unit의 정의는 enemy.patrol, FSM 연결 정상.
- Unity 에디터 컴파일 통과. Play Mode **20/20 통과**, 19:27:58~19:28:29 KST, 31.52초. 직접 생성한 Unit의 선택적 FSM·독립 상태·재활성화와 적 순찰·애니메이션·기존 PlayerUnit 입력·이동 검사 통과.
- 적 프리팹의 실제 컴포넌트 타입이 Unit인지 검증. Assets의 FsmUnit 클래스명·삭제한 스크립트 GUID 참조 0개. 사용 가이드와 로컬 AGENTS.md 갱신.
- 최종 예상 밖 Console 오류 없음. 정의 누락 방어 테스트의 예상 오류 1건은 유지. 적 Missing Script 0개, Unit 1개, 플레이어는 PlayerUnit으로 유지. Play Mode·컴파일 종료, MovementLab 미수정 확인.
- 결과는 `Library/PrototypeValidation/PlayMode.xml`에 저장. Edit Mode 재실행과 실행 파일 빌드는 포함하지 않음.

## 순찰 적 Pig 스프라이트·Animator 적용 후 결과

2026-09-13, Unity 6000.3.23f1 / Windows Editor.

- 기존 PatrolEnemy의 FsmUnit : Unit 상속 및 정의·FSM 연결 확인. 공통 Unit·FSM 실행기와 순찰 상태·이동 수치는 유지.
- 같은 제작자 Pixel Frog의 Kings and Pigs 공식 무료 다운로드에서 Pig Idle·Run 시트만 가져옴. 원본 ZIP과 추출한 PNG의 해시를 비교해 이미지 내용이 동일함을 확인. CC0 출처와 해시는 ThirdPartyNotices에 기록.
- Visual의 임시 자식 6개를 제거하고 SpriteRenderer·Animator·PatrolVisual2D 구성으로 교체. 34×28, 16 PPU, Point, 무압축, Idle 11프레임·Run 6프레임을 10 FPS로 반복. Moving Bool로 전환하고 실제 속도 방향에 따라 SpriteRenderer.flipX 적용.
- 충돌체를 Pig 크기에 맞춰 1×1로 조절. Visual 위치 (0,-0.5,0), 스케일 1, 피벗 (20/34,0). 애니메이션은 Sprite만 변경하고 Root Motion은 비활성화.
- Unity 에디터 컴파일 통과. 최종 Play Mode **20/20 통과**, 19:17:21~19:17:53 KST, 31.47초. 기존 이동·순찰·벽·발판 끝·Unit 생명주기 검사와 새 Idle/Run 프레임 재생·이동 방향 반전·비활성화 후 Idle 복귀 검사 통과.
- 새 검사에서 GetComponent<Unit>()가 실제 적의 FsmUnit을 반환함을 확인. 프리팹 SpriteRenderer 1개, Animator Controller 연결 정상, Missing Script 0개.
- 실제 1920×1080 Scene View 캡처로 Pig 표시 확인. 최종 Console에는 정의 누락 방어 테스트의 예상 오류 1건만 있고 추가 오류는 없음.
- 검사 종료 후 Play Mode·컴파일 종료, MovementLab 미수정 확인. PatrolEnemy를 Prefab Mode로 열어 두었으며 프리팹도 미수정 상태. 씬 배치·플레이어·프로젝트 설정은 이번 작업에서 변경하지 않음.
- 결과는 `Library/PrototypeValidation/PlayMode.xml`에 저장. Edit Mode 재실행과 실행 파일 빌드는 포함하지 않음.

## 순찰 적 프리팹 추가 후 결과

2026-09-13, Unity 6000.3.23f1 / Windows Editor.

- `PatrolEnemy.prefab`과 전용 UnitDefinition·PatrolFsmDefinition 에셋 생성. FsmUnit + PatrolMovement2D + Rigidbody2D + BoxCollider2D를 루트에 구성하고 Visual 자식에 붉은 임시 외형과 방향 전환 기능 연결.
- 대기 0.6초 → 속도 2로 편도 3유닛 이동 → 대기 → 반대 방향 이동. 벽이나 발판 끝에서 조기 정지·반전. 방향별 WaitState·WalkState를 개체마다 생성.
- Unity 에디터 컴파일 통과. Play Mode **19/19 통과**, 18:04:26~18:04:56 KST, 30.01초. 기존 16개와 실제 프리팹을 사용한 신규 검사 3개 실행.
- 신규 검사는 초기 대기·좌우 왕복, 공유 정의를 사용하는 두 개체의 독립 상태, Unit 비활성화 시 수평 정지와 초기 대기 재시작, Visual 반전과 루트 보존, 벽에서 방향 전환, 좁은 발판에서 낙하 방지를 확인.
- 최종 Console에는 기존 정의 누락 방어 테스트의 예상 오류 1건만 존재. 예상 밖 오류 없음. 프리팹 Missing Script 0개, 정의·FSM·외형 이동 참조 정상, 루트 컴포넌트 5개·SpriteRenderer 6개 확인.
- Prefab Mode에서 실제 Scene View 카메라의 1920×1080 캡처로 붉은 몸체·발·눈·눈썹의 표시 확인. 검사 종료 후 Play Mode는 꺼져 있고, 순찰 적 프리팹을 열어 둠. MovementLab과 프리팹 씬 미수정 상태 확인.
- 공통 Unit·FsmRuntime·기존 플레이어 코드 및 에셋·MovementLab·프로젝트 설정은 이번 작업에서 변경하지 않음. 적은 씬에 자동 배치하지 않음. 결과는 `Library/PrototypeValidation/PlayMode.xml`에 저장.
- Edit Mode 재실행, 실행 파일 빌드, 경사면·이동 발판·추적·공격 검증은 이번 범위에 포함하지 않음.

## 유닛 정의 데이터와 FSM 실행 기반 추가 후 결과

2026-09-13 17:42~17:47 KST, Unity 6000.3.23f1 / Windows Editor.

- UnitDefinition에 Unit Id·Display Name·Kind(Player/Monster/Npc)·선택적 FsmDefinition 추가. 누락·잘못된 정의는 Unit 시작 시 오류를 기록하고 컴포넌트를 비활성화.
- Unit이 개체별 FsmRuntime을 생성하고 Start·Update·OnDisable에서 실행·종료. 재활성화하면 초기 상태 재진입. FsmDefinition은 C# 상태 객체 생성 팩토리이며 실제 몬스터·NPC 상태·전환과 그래프 편집기는 구현하지 않음.
- PlayerUnit은 Unit의 OnUnitAwake·OnUnitUpdate·OnUnitDisabled 훅으로 기존 입력·모터 처리를 유지. FsmUnit은 물리·입력 컴포넌트 없는 몬스터·NPC 공통 호스트.
- Player 프리팹에 `Assets/_Game/Data/Units/PlayerDefinition.asset` 연결. ID player, Kind Player, FSM 미지정.
- Unity 에디터 컴파일 통과. Edit Mode **16/16 통과**, 17:42:47 KST, 0.32초. 기존 7개와 FSM 시작·종료, 상태 전환 순서, 재시작·타이머 초기화, 전환 중 정지·중복 종료 방지, 예외 시 정지 등 신규 9개 검사.
- Play Mode **16/16 통과**, 17:44:57~17:45:10 KST, 13.42초. 기존 플레이어 9개와 공유 정의를 사용하는 두 유닛의 독립 상태·타이머·소유 유닛 참조, 활성화·비활성화·파괴 시 종료, FSM 없는 NPC, 정의 누락·유효성 검사 7개.
- 최종 Console에는 정의 누락 방어 테스트가 LogAssert.Expect로 의도한 `Unit definition is missing.` 오류 로그 1건만 존재. 예상 밖 오류는 없음.
- 플레이어 프리팹·씬 Missing Script 0개, 정의 데이터 일치, 루트 컴포넌트 5개와 Tuning·Ground Mask 1·Visual 참조 정상. Play Mode 종료, 컴파일 종료, MovementLab 씬 미수정 확인.
- 결과는 `Library/PrototypeValidation/EditMode.xml`, `PlayMode.xml`에 저장. 씬·입력 액션·이동 수치·외형·프로젝트 설정은 유지. 실행 파일 빌드와 실물 게임패드 검사는 포함하지 않음.

## CharacterMotor2D를 일반 C# 객체로 분리한 후 결과

2026-09-13 17:09 KST, Unity 6000.3.23f1 / Windows Editor.

- CharacterMotor2D의 MonoBehaviour 상속, RequireComponent, GetComponent, Awake 제거. Rigidbody2D·Collider2D·PlayerTuning·Ground Mask를 생성자로 전달받는 일반 C# 객체로 변경.
- PlayerUnit이 모터를 생성하고 실행. 모터 컴포넌트의 기존 Tuning 에셋과 Ground Mask 1을 PlayerUnit으로 이관하고 Visual 참조 연결.
- PlayerVisual은 PlayerUnit이 전달한 ICharacterMotionState의 속도·접지만 읽음. 구체적인 모터 클래스의 직렬화 참조 제거, Awake 순서에 의존하지 않도록 초기화와 검증 분리.
- Unity 에디터 컴파일 통과. Edit Mode **7/7 통과**, 0.24초. 두 모터가 각각 전달받은 Rigidbody2D만 변경하고 CapsuleCollider2D·BoxCollider2D를 사용할 수 있는지 확인.
- Play Mode **9/9 통과**, 13.17초. 기존 이동·점프·입력 전환·재활성화·애니메이션 검사 통과.
- 프리팹·씬의 루트 컴포넌트는 Transform, Rigidbody2D, CapsuleCollider2D, PlayerInput, PlayerUnit 다섯 개. 직접 만든 루트 컴포넌트는 PlayerUnit 하나.
- 최종 Console 오류 0건, Missing Script 0개, Tuning·Ground Mask·Visual 참조 정상. Play Mode 종료, 씬 미수정 상태 확인.
- 씬·입력 액션·이동 수치·외형 에셋은 변경하지 않음. 결과는 `Library/PrototypeValidation/EditMode.xml`, `PlayMode.xml`에 저장. 실행 파일 빌드와 실물 게임패드 검사는 포함하지 않음.

## 이동·점프 기능을 PlayerUnit으로 이관한 후 결과

2026-09-13 16:51 KST, Unity 6000.3.23f1 / Windows Editor.

- Unit의 모터 참조, 이동·점프 판단과 상태, 코요테 타임, 점프 입력 버퍼, 물리 갱신·비활성화 초기화를 모두 PlayerUnit으로 이동. Unit은 상속 기반과 중복 부착 방지만 유지.
- PlayerUnit이 Awake·Update·FixedUpdate·OnDisable을 직접 처리. 기존 PlayerInput / Reader / Motor / Visual 연결과 이동 수치는 유지.
- Unit 공통 이동을 전제로 한 테스트와 UnitTestDriver를 제거. 기존 플레이어 Play Mode 검사 **9/9 통과**, 13.13초. 입력·이동·점프·애니메이션 동작 확인.
- Unity 에디터 컴파일 통과, 최종 Console 오류 0건, 씬 플레이어 Missing Script 0개. Play Mode 종료, 씬 미수정 상태 확인.
- 결과는 `Library/PrototypeValidation/PlayMode.xml`에 저장. Edit Mode 재실행, 실행 파일 빌드, 실물 게임패드 검사는 이번 검증에 포함하지 않음.

## Unit / PlayerUnit 상속 구조 적용 후 결과

2026-09-13 16:43 KST, Unity 6000.3.23f1 / Windows Editor.

- Unit 추상 기반 클래스 추가. 공통 모터 참조, 이동 명령, 점프 버퍼·코요테 타임·점프 해제 처리와 FixedUpdate, 비활성화 초기화를 기존 Controller에서 이동.
- PlayerUnit이 Unit을 상속하고 PlayerInputReader를 통해 Update에서 입력을 전달. 기존 PlayerController 스크립트 GUID를 PlayerUnit으로 옮겨 프리팹 참조 유지.
- 프리팹과 씬의 루트 스크립트는 PlayerInput, PlayerUnit, CharacterMotor2D 세 개. Unit은 별도로 부착하지 않으며 GetComponent<Unit>()로 PlayerUnit 조회 가능.
- Unity 에디터 컴파일 통과. Play Mode **10/10 통과**, 13.93초. 기존 입력·이동·점프·애니메이션 검사와 함께, PlayerInput 없는 테스트용 파생 Unit의 이동·점프 및 비활성화 시 예약 점프 초기화 확인.
- 최종 Console 오류 0건, 프리팹·씬 플레이어 Missing Script 0개, 유닛 컴포넌트 1개. Play Mode 종료, 씬 미수정 상태 확인.
- 이동 수치·입력 액션·씬·외형은 유지. 이번 검증에서 Edit Mode 재실행, 실행 파일 빌드, 실물 게임패드 검사는 수행하지 않음. 결과는 `Library/PrototypeValidation/PlayMode.xml`에 저장.

## 입력 Reader 컴포넌트 정리 후 결과

2026-09-13 16:29 KST, Unity 6000.3.23f1 / Windows Editor.

- PlayerInputReader를 일반 C# 객체로 전환하고 PlayerController가 생성·보관하도록 변경. 프리팹의 Reader 컴포넌트와 관련 참조 제거.
- 프리팹과 씬 플레이어의 루트 스크립트는 Unity PlayerInput, PlayerController, CharacterMotor2D 세 개. 직접 만든 루트 컴포넌트는 두 개이며 Visual 구조는 유지.
- Unity 에디터 컴파일 통과. Play Mode **9/9 통과**, 13.29초. 이동·점프·입력 기기 전환·비활성화와 재활성화·외형 동작 확인.
- 프리팹과 씬 플레이어 Missing Script 0개, 최종 Console 오류 0건. Play Mode 종료, 씬 미수정 상태 확인.
- 이번 검증에서 Edit Mode 재실행, 실행 파일 빌드, 실물 게임패드 검사는 수행하지 않음. 결과는 `Library/PrototypeValidation/PlayMode.xml`에 저장.

## Unity PlayerInput 적용 후 결과

2026-09-13 16:21 KST, Unity 6000.3.23f1 / Windows Editor.

- Player 프리팹 루트에 Unity 기본 PlayerInput 추가. Actions는 PrototypeControls, Default Action Map은 Player, Behavior는 Invoke C Sharp Events, 기기 자동 전환 활성화.
- PlayerInputReader는 PlayerInput이 관리하는 액션을 읽는 역할로 변경. 직접 복제·활성화·파괴하던 코드는 제거했으며, PlayerInput 또는 Player 맵 비활성화 시 빈 명령을 반환.
- Unity 에디터 컴파일 통과. Play Mode **9/9 통과**, 13.04초.
- 기존 이동·가변 점프·코요테 타임·입력 버퍼·공중 추가 점프 차단·외형 검사 유지.
- 가상 키보드·게임패드 자동 전환과 점프 누름·유지·뗌 확인. 실제 PlayerInput → Reader → Controller → Motor 경로로 이동·점프 실행 확인.
- DeactivateInput / ActivateInput, 액션 맵 비활성화, PlayerInput 컴포넌트 반복 비활성화·재활성화 검사 통과.
- 최종 Console 오류 0건. 씬 플레이어의 PlayerInput 1개, 액션 참조 정상, Missing Script 0개.
- MovementLab 씬과 두 입력 액션 에셋, 이동·점프 코드 및 수치는 변경하지 않음. 검사 종료 후 Play Mode 종료, 씬 미수정 상태 확인.

결과는 `Library/PrototypeValidation/PlayMode.xml`에 있습니다. 이번 검증에서 Edit Mode 재실행, 실행 파일 빌드, 실물 게임패드 검사는 수행하지 않았습니다.

## 프로젝트 정리 후 결과

2026-09-11 18:04 KST, Unity 6000.3.23f1 / Windows Editor.

- SampleScene, Lit2DSceneTemplate, URP2DSceneTemplate, PlayerArtSetup, PrototypeBuilder와 해당 메타 파일 및 빈 씬 폴더 2개 제거.
- 미사용 패키지 6개 제거: Visual Scripting, Timeline, Multiplayer Center, Aseprite Importer, PSD Importer, SpriteShape. Unity가 packages-lock.json을 갱신했으며 나머지 패키지 버전은 그대로임.
- EditorBuildSettings에는 MovementLab만 유지. ProjectSettings의 templateDefaultScene 경로도 MovementLab으로 변경.
- 기본 InputSystem_Actions와 플레이어용 PrototypeControls, 각 메타 파일, Player 프리팹, MovementLab 씬은 정리 전후 SHA-256이 동일. 기본 전역 입력 등록과 플레이어의 PrototypeControls 연결 유지.
- 삭제된 에셋·스크립트 GUID의 남은 참조 0개, 플레이어 Missing Script 0개, _Game 메타 파일 누락 0개.
- 에디터 컴파일 및 기존 Block 스프라이트 재임포트 통과.
- 최종 Play Mode **8/8 통과**, 13.37초. Project 창에서 시작해 이동·점프·애니메이션·가상 키보드·게임패드 입력 확인.
- 최종 Console 오류 0건. Play Mode 종료, 씬 미수정, 테스트 전 입력 포커스·백그라운드 설정 복원 확인.

검증 중 발생한 문제도 기록합니다. 패키지 재로딩 중 기존 임포트 작업자 2개가 제거된 Timeline의 asmdef를 찾다가 종료됐습니다. 이후 재컴파일·에셋 재임포트가 완료됐고 최종 테스트와 콘솔 확인에서는 재발하지 않았습니다. 첫 Play Mode 실행은 기존 가상 키보드 테스트의 에디터 포커스 의존성으로 7/8 통과했습니다. 해당 테스트에서만 포커스 제한을 해제하고 finally에서 복원하도록 보완한 뒤 8/8 통과했습니다. 실제 입력 에셋이나 런타임 입력 코드는 변경하지 않았습니다.

최신 결과는 `Library/PrototypeValidation/PlayMode.xml`, 정리 전 복구용 파일과 첫 실패 결과는 `Library/PrototypeValidation/CleanupBackup`에 있습니다. Edit Mode 재실행과 실행 파일 빌드는 이번 검증에 포함하지 않았습니다. 아래는 이전 작업의 검증 이력입니다.

## Virtual Guy 적용 당시 결과

2026-09-11 17:34 KST, Unity 6000.3.23f1 / Windows Editor.

- 에디터 C# 컴파일 및 프리팹 적용 성공.
- Play Mode **8/8 통과**, 13.37초. 기존 이동·점프·입력 검사 7개와 새 외형 검사 1개.
- 외형 검사는 대기 프레임 재생, 달리기·점프·낙하·착지 후 대기 전환, 좌우 반전, 정지 시 방향 유지, 애니메이션에 의한 물리 루트 이동 방지를 확인.
- 프리팹의 모터 참조, Animator Controller, Root Motion 비활성화 확인. Idle/Run/Jump/Fall 스프라이트 수는 11/12/1/1, 모두 16 PPU와 Point 필터.
- 신규 파일을 포함한 `Assets/_Game`의 파일별 `.meta` 누락 0개.
- 테스트 완료 후 Unity Console 오류 0건.
- 실제 Game 뷰 1920×1080 캡처에서 캐릭터 표시와 바닥 배치를 확인. 접지 상태에서 Idle 프레임이 재생되며 플레이어의 Missing Script는 0개.

현재 화면은 `Library/PrototypeValidation/VirtualGuyMovementLab.png`입니다. 확인 후 Play Mode를 종료하고 Player 프리팹을 선택했습니다. 실행 파일 빌드와 실물 게임패드 검증은 이번 범위에 포함하지 않습니다.

이하 기록은 초기 프로토타입 작업 당시의 검증 이력입니다.

## 이동·점프 기초 정리 당시 결과

2026-09-11, Unity 6000.3.23f1 / Windows Editor. 이동 가감속·가변 점프·코요테 타임·점프 입력 버퍼만 남긴 버전의 결과입니다.

| 검사 | 결과 |
|---|---|
| 에디터 C# 컴파일 및 MCP 실행 | 통과 |
| Edit Mode | 6/6 통과, 16:59 KST |
| Play Mode | 7/7 통과, 17:00 KST, 10.73초 |
| 씬 스크립트 참조 | Missing Script 0개 |
| 입력 설정 | Player 맵 1개, Move / Jump 액션 2개 |
| 새 에셋 메타데이터 | 파일 21개 모두 `.meta` 존재 |
| 최종 런타임 Console Error 조회 | 0건 |
| 실제 Game 뷰 | 1920×1080 캡처 확인, 단색 플레이어·테스트 발판만 표시 |

Play Mode에서는 좌우 가속·반전·정지, 누르는 시간에 따른 점프 높이, 착지 직전 점프 버퍼, 발판 이탈 직후 코요테 점프, 코요테 시간 만료 후 점프 거부, 추가 공중 점프 방지, 가상 키보드·게임패드의 이동·점프 바인딩을 확인했습니다.

최종 씬은 오브젝트 10개와 스프라이트 7개이며, 실행 스크립트는 PlayerController / CharacterMotor2D / PlayerInputReader 세 개입니다. 단색 플레이어, 바닥·벽·발판과 고정 카메라만 남겼습니다. 제거한 기능의 코드·설정·입력·사운드·이펙트와 관련 테스트도 정리했습니다.

테스트 XML은 `Library/PrototypeValidation/EditMode.xml`, `PlayMode.xml`에 있습니다. 실행 파일 빌드, 실물 게임패드 및 프레임률별 손맛 검증은 포함하지 않습니다.

현재 화면 캡처는 `Library/PrototypeValidation/MovementLab.png`입니다. 점검 후 Play Mode를 종료하고 MovementLab을 편집 모드로 열어 두었습니다.

## 플레이어 Visual 분리 후 검사

2026-09-11 17:17 KST. 기존 Player 프리팹을 유지하고 직접 자식 `Body`를 `Visual`로 변경해 SpriteRenderer와 Animator를 배치했습니다. 루트의 물리·입력·이동 컴포넌트와 기존 스프라이트의 위치·크기·색은 유지했습니다. Animator Controller는 미지정이며 Apply Root Motion은 꺼져 있습니다.

프리팹과 MovementLab 인스턴스의 구조 및 에디터 컴파일을 확인했습니다. 최초 Play Mode 실행에서는 이동 동작 6개가 통과했고 가상 키보드 입력 검사에서 Move 값이 0으로 관측돼 1개가 실패했습니다. Game 뷰에 포커스를 두고 재실행한 결과 7/7 통과(10.61초)했습니다. 입력 검증 시 에디터 포커스의 영향을 유의해야 합니다. 실제 애니메이션 재생·전환 검증은 리소스를 연결한 뒤 수행해야 합니다.
# Adventurer 외형과 공격 애니메이션 연결

2026-09-25, Unity 6000.3.23f1 / Windows Editor.

- rvros Adventurer 1.5의 원본 PNG 46개와 이동/공격 클립 15개를 연결했다. 원본과 PNG 해시 불일치 0개, 필요한 `.meta` 누락 0개. Player 프리팹의 Missing Script 0개와 모든 클립의 Sprite 참조를 검사했다. Root Motion, Transform 곡선, Animation Event는 없다.
- 최종 Play Mode **113/113 통과**, 01:43:13~01:44:14 KST, 60.925초. 결과 `Library/PrototypeValidation/AdventurerPlayModeVerified.xml`, 로그 `AdventurerPlayModeVerified.log`. 지상 3타 준비/명중/회복, 일반 공중 검격, 띄우기·출현, 내려찍기 준비/하강 반복/착지, 지하 숨김/복원, 공격 반복/비활성화 복원, 바닥 없는 하강 종료 후 낙하 표시와 기존 이동·전투를 검증했다. 읽기 전용 표시 조회가 공격을 진행하거나 타수를 소비하지 않는 것도 검사했다.
- 전체 Edit Mode **25/25 통과**, 01:41:59 KST, 0.097초. 결과 `Library/PrototypeValidation/AdventurerEditMode.xml`, 로그 `AdventurerEditMode.log`. 새 에셋 참조·임포트·Animator 전환 차단 검사 1개 포함. 이후 바뀐 것은 공중에서 하강이 시간 초과될 때 착지 대신 낙하를 표시하는 런타임/PlayMode 검사이며, 최종 PlayMode에서 재컴파일·검증했다.
- 첫 전체 실행은 107/112 통과했다. 새 반복 표시 검사의 프레임 수 대기를 물리 시간 대기로 수정했고, 비활성 Animator에 Play를 호출하던 경고를 없앴다. 기존 검사 4개는 편집된 씬에서 제거된 발판 및 기존 띄우기 값 10을 가정했다. 테스트 전용 발판 생성/정리와 현재 Tuning 값 조회로 수정했다. 저장 씬이나 사용자의 Launch Speed=20 / Emergence Speed=25는 변경하지 않았다.
- 최종 C# 컴파일 오류·경고 및 비활성 Animator 경고 없음. 원본/검증 복사본의 `_Game` 파일 해시를 비교했다. 이번 수정 범위의 `git diff --check` 통과. 전체 diff에는 기존 편집된 MovementLab의 공백 경고가 남아 있으며 관련 없는 씬을 재직렬화하지 않았다.
- 렌더링 미리보기 `Library/PrototypeValidation/AdventurerAttacks.png`를 직접 확인했다. 위쪽은 지상 1·2·3타/일반 공중 공격, 아래쪽은 출현/내려찍기 준비/하강/착지다. 실제 전투 테스트의 `GlitchAttack.png`에서도 발 위치와 적 상대 검격을 확인했다.
- 검사는 동일 버전의 격리 Unity 프로젝트에서 실행했다. 원본 에디터 수동 조작 및 실행 파일 빌드는 수행하지 않았다. 제작자 라이선스와 출처는 `Docs/ThirdPartyNotices.md`에 기록했다.
