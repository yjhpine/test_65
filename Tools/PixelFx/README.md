# PixelFX 생성 파이프라인

Node.js 18 이상과 Git이 필요하다. PNG와 HTML 생성에는 npm 설치, Python, FFmpeg가 필요 없다.
프로젝트 루트에서 실행한다. 다른 작업 경로에서도 pipeline.mjs의 절대 경로로 실행 가능하다.

```powershell
node Tools/PixelFx/pipeline.mjs setup
node Tools/PixelFx/pipeline.mjs list
node Tools/PixelFx/pipeline.mjs render custom/glitch-burst
node Tools/PixelFx/pipeline.mjs render melee/sword-slash Tools/PixelFx/presets/mint-slash.json
node Tools/PixelFx/pipeline.mjs new my-effect
node Tools/PixelFx/pipeline.mjs render custom/my-effect
```

## 파일과 재현성

- `pixelfx.lock.json`: upstream 저장소와 고정 커밋. setup은 이 커밋으로 checkout하며 수정된 vendor를 덮어쓰지 않는다.
- `effects/*.mjs`: Git으로 보존하는 프로젝트 자체 효과 소스. `core.mjs`에서 painter용 색상 변환·난수·입자 API를 가져온다.
- `presets/*.json`: FPS, seed, options(팔레트 등) 설정. 새 효과는 기본 샘플을 복사한 뒤 meta와 draw를 수정한다.
- `Library/PixelFxPipeline/vendor/pixelfx`: 재다운로드 가능한 upstream. 이곳에서 직접 수정하지 않는다.
- `Library/PixelFxPipeline/output/<category>/<name>`: 생성물 캐시. Library 삭제 시 사라지므로 배포할 결과는 별도 보관한다. 같은 ID 재생성은 이전 출력을 교체한다.

출력은 투명 PNG 시트, Aseprite 형식 JSON, 개별 프레임 PNG, contact.png, **preview.html**, manifest.json, LICENSE, NOTICE.md이다.
preview.html을 브라우저로 열면 외부 서버 없이 재생/일시정지/프레임 탐색이 가능하다. 단발 효과도 비교를 위해 반복 재생한다.
manifest에는 upstream 커밋, 자체 소스 해시, seed, 옵션, FPS, Unity 피벗, 픽셀 해시가 남는다.
매 생성마다 두 번 렌더링해 동일한지 검증하고, 빈 출력은 실패시킨다. 가장자리 픽셀 수와 마지막 프레임 소멸 여부는 진단값이다.
가장자리 픽셀이 있는 효과는 실제 사용 전에 잘림을 확인한다. 루프 효과의 마지막 프레임은 비어 있지 않아도 정상이다.

## Unity에 가져오기

이 도구는 Assets, 씬, 프리팹, VfxManager와 전투 설정을 자동 변경하지 않는다.
선택된 효과만 `Assets/_Game/Art/Effects/` 아래에 복사하고, Point / 압축 없음 / Mipmap 끔 / Sprite로 설정한다.
시트는 Sprite Multiple로 JSON의 frame 사각형을 기준으로 자른다. JSON은 원점이 좌상단이므로 Unity rect.y = sheetHeight - y - h이다.
피벗은 manifest의 unityPivot을 쓰며 PPU는 기존 효과의 월드 크기에 맞춰 결정한다.
Unity 기본 임포터가 이 JSON을 자동 슬라이스하는 것은 아니다. 개별 frames PNG를 사용하면 시트 슬라이스 없이 프레임 배열로 연결할 수 있다.
선택한 프레임을 기존 PlayerVfxProfile/EnemyVfxProfile에 용도에 맞게 연결하고 기존 전투 단계 진행률·재생 수명 계약에 맞춘다.
upstream act의 히트스톱/피해/카메라 데이터는 게임 규칙에 자동 적용하지 않는다.
onBody/actor 효과는 기본 인체 모양이 효과 안에 포함될 수 있으므로 캐릭터 전용 효과는 별도로 수정해야 한다.

## 라이선스와 구현 참고

원본: https://github.com/patch-gg/pixelfx (MIT). 고정 버전의 LICENSE/NOTICE.md를 ThirdParty와 각 출력 폴더에 보존한다.
gallery/fonts의 Patch Pixel 폰트는 MIT 대상이 아니며 이 파이프라인에서는 가져오거나 사용하지 않는다. contact sheet는 기본 mini 글꼴을 사용한다.
원본의 시트 합성에서 반투명 RGB가 검정 바탕에 혼합되는 것을 피하려고, 시트는 렌더링된 straight RGBA를 그대로 복사한다.
자체 효과 작성 API와 예제는 vendor의 docs/AUTHORING.md, src/fx에서 확인한다.
