# Movement Lab 검증 기록

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
