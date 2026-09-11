# Movement Lab 검증 기록

## 프로젝트 정리 후 현재 결과

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
