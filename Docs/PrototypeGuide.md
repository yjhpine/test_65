# 이동·점프 기초 — Movement Lab

현재 범위는 **이동 가감속, 가변 점프, 코요테 타임, 점프 입력 버퍼**입니다.

## 실행과 조작
`Assets/_Game/Scenes/MovementLab.unity`를 열고 Play를 누릅니다.

- 좌우 이동: A/D 또는 방향키 / 게임패드 왼쪽 스틱.
- 점프: Space / 게임패드 아래 버튼.
- 점프를 짧게 누르면 낮게, 길게 누르면 높게 뜁니다.
- 발판을 벗어난 뒤 0.1초 안에는 점프할 수 있습니다.
- 착지 전 0.13초 안에 누른 점프는 착지 시 실행됩니다.

씬에는 픽셀아트 플레이어, 단색 바닥, 경계 벽, 점프 확인용 발판 3개와 고정 카메라만 있습니다.
대시·공격·체크포인트·재시작·일시정지·HUD·효과음·연출·건물 배경은 제거했습니다. 와이어 이동은 없습니다.

## 조절 위치
`Assets/_Game/Data/PlayerTuning.asset`에서 이동 속도, 지상 가속·감속, 공중 가속, 점프 높이, 상승·낙하 중력, 점프 해제 배율, 코요테 타임, 입력 버퍼를 조절합니다.
초깃값은 이전 프로토타입의 이동·점프 설정을 유지했습니다.

`Assets/_Game/Data/PrototypeControls.inputactions`에는 Player 맵의 Move / Jump만 있습니다.
기본 `Assets/Settings/InputSystem_Actions.inputactions`와 프로젝트 전역 입력 등록도 보존했습니다. 현재 플레이어의 PlayerInputReader는 프리팹에 연결된 PrototypeControls를 사용합니다.
`Assets/_Game/Prefabs/Player.prefab`은 루트에서 물리 몸체와 입력·컨트롤러·모터를 관리하고, 바로 아래 `Visual` 자식에서 `SpriteRenderer`와 `Animator`를 관리합니다.

## 플레이어 외형과 애니메이션
Pixel Frog의 [Pixel Adventure](https://pixelfrog-assets.itch.io/pixel-adventure-1)에 포함된 **Virtual Guy**를 적용했습니다. CC0 에셋이며 출처와 라이선스는 `Docs/ThirdPartyNotices.md`에 기록했습니다.

원본 이미지는 `Assets/_Game/Art/Characters/VirtualGuy`, 애니메이션은 `Assets/_Game/Animations/Player`에 있습니다. 32×32 프레임, 16 PPU, Point 필터, 무압축이며, 대기 11프레임과 달리기 12프레임을 20 FPS로 반복합니다. 점프·낙하는 각각 한 프레임입니다.

Player 프리팹의 `Visual`에는 SpriteRenderer, Animator, PlayerVisual이 있습니다. PlayerVisual은 모터의 속도와 접지를 읽어 `Moving`, `Grounded`, `Rising` 파라미터를 갱신하고, 이동 방향에 따라 SpriteRenderer의 Flip X를 변경합니다. 정지하면 마지막 방향을 유지합니다. Animator의 Apply Root Motion은 꺼져 있습니다.

다른 캐릭터로 교체할 때는 `Visual`의 Sprite와 Animator Controller를 교체하고 위 세 Bool 파라미터를 유지합니다. 현재 `Visual`의 위치는 `(0, -0.8, 0)`, 스케일은 `(1, 1, 1)`, Color는 흰색이며 스프라이트 피벗은 하단 중앙입니다.
루트의 Transform과 Rigidbody2D는 이동용으로 유지하고, 스프라이트 프레임이나 외형 변형 애니메이션은 `Visual`에 바인딩합니다. 충돌 크기는 루트의 CapsuleCollider2D에서 별도로 조절합니다.

외형과 애니메이션은 저장된 에셋에서 직접 수정합니다. 초기 에셋 적용 및 테스트 씬 재생성 도구는 정리했습니다.

## 구조
- `PlayerInputReader`: 입력을 프레임별 명령으로 변환.
- `PlayerController`: 점프 입력 버퍼와 코요테 타임, 행동 선택.
- `CharacterMotor2D`: 접지 검사, Rigidbody2D 위치·속도, 가변 점프.
- `PlayerVisual`: 모터 상태에 따른 스프라이트 애니메이션과 좌우 방향.
- `PlayerTuning`: 수정 가능한 설정 데이터.
- `MovementMath / InputBuffer`: 이동 계산과 한 번만 소비하는 입력 버퍼.

실행 중 상태는 컴포넌트에 저장하고 설정 에셋에는 저장하지 않습니다.
빌드 씬에는 MovementLab만 등록되어 있습니다.

## 검증
`Game > Prototype > Validate EditMode / Validate PlayMode`에서 실행합니다.
가상 키보드·게임패드 테스트는 검사 중에만 에디터 입력 포커스 제한을 해제하고, 종료 시 원래 설정을 복원합니다. 실제 플레이 입력 설정은 변경하지 않습니다.
현재 결과와 검증 범위는 `Docs/PrototypeValidation.md`에 기록합니다.
