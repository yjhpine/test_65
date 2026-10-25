# Third-party assets

## Pixel Adventure — Virtual Guy

- Creator: **Pixel Frog**.
- Original asset pack: [Pixel Adventure on itch.io](https://pixelfrog-assets.itch.io/pixel-adventure-1).
- License: [Creative Commons Zero v1.0 Universal (CC0)](https://creativecommons.org/publicdomain/zero/1.0/).
- Retrieved from the creator's free download on **2026-09-11**.
- The creator's page explicitly permits commercial use, modification and redistribution; attribution is optional. Credit is retained here for provenance.
- Original archive: `Pixel Adventure 1.zip`, 209,186 bytes.
- Archive SHA-256: `EFAFDFC8ED44F2B0ADE27C0246E11A2474CE3A793B4F8E16DBE7403824F6E77B`.

Only four original PNG files were imported. Their image pixels are unchanged; file names were shortened and Unity slicing/import settings were added.

| Source path inside archive | Project path |
|---|---|
| `Free/Main Characters/Virtual Guy/Idle (32x32).png` | `Assets/_Game/Art/Characters/VirtualGuy/Idle.png` |
| `Free/Main Characters/Virtual Guy/Run (32x32).png` | `Assets/_Game/Art/Characters/VirtualGuy/Run.png` |
| `Free/Main Characters/Virtual Guy/Jump (32x32).png` | `Assets/_Game/Art/Characters/VirtualGuy/Jump.png` |
| `Free/Main Characters/Virtual Guy/Fall (32x32).png` | `Assets/_Game/Art/Characters/VirtualGuy/Fall.png` |

These PNGs are retained as the previous player art. The Player prefab now uses Adventurer (below). The former integration used the creator's stated 20 FPS.

## Kings and Pigs — Pig

- Creator: **Pixel Frog**, also the creator of the previously used Virtual Guy.
- Original asset pack: [Kings and Pigs on itch.io](https://pixelfrog-assets.itch.io/kings-and-pigs).
- License: [Creative Commons Zero v1.0 Universal (CC0)](https://creativecommons.org/publicdomain/zero/1.0/), as stated on the creator's page. Commercial use, modification and redistribution are permitted; attribution is optional.
- Retrieved from the creator's official free download on **2026-09-13**.
- Original archive: `Kings and Pigs.zip`, 285,652 bytes.
- Archive SHA-256: `4D61A9C48D5EB1EC5EF5585359D3800205349AF813AF67030A719BFD6371D373`.

The Pig's idle, run, attack, hit and death PNG sheets were imported. Attack, Hit and Dead were added on 2026-09-16 from the same locally retained archive. Image pixels are unchanged; filenames were shortened and Unity slicing/import metadata was added.

| Source path inside archive | Project path |
|---|---|
| `Sprites/03-Pig/Idle (34x28).png` | `Assets/_Game/Art/Characters/Pig/Idle.png` |
| `Sprites/03-Pig/Run (34x28).png` | `Assets/_Game/Art/Characters/Pig/Run.png` |
| `Sprites/03-Pig/Attack (34x28).png` | `Assets/_Game/Art/Characters/Pig/Attack.png` |
| `Sprites/03-Pig/Hit (34x28).png` | `Assets/_Game/Art/Characters/Pig/Hit.png` |
| `Sprites/03-Pig/Dead (34x28).png` | `Assets/_Game/Art/Characters/Pig/Dead.png` |

Each frame is 34×28 pixels. Idle has 11 frames, Run 6, Attack 5, Hit 2 and Dead 4. Animation clips and controller in `Assets/_Game/Animations/Enemies/PatrolEnemy` are project-authored integration data authored at **10 FPS**. Combat clips are sampled according to the FSM's configured timings; Idle/Run retain native timing. Other pack content is not imported.

## Animated Pixel Adventurer — rvros

- Creator and official source: [rvros — Animated Pixel Adventurer](https://rvros.itch.io/animated-pixel-hero).
- Retrieved through the creator's free download on **2026-09-25**.
- Original archive: `Adventurer-1.5.zip`, **204,405 bytes**.
- Archive SHA-256: `0BF3F9253FCF77F6BF23AF2EBB83C7776490414223B3E37AE798D6135E134A7C`.
- The creator permits personal/commercial use and modification; credit is optional. The asset may not be resold or redistributed as a standalone asset. This is the creator's custom license, not CC0.
- Imported from `Individual Sprites/` into `Assets/_Game/Art/Characters/Adventurer/`, retaining filenames and original PNG bytes: `adventurer-idle-2-*`, `adventurer-run-*`, `adventurer-jump-*`, `adventurer-fall-*`, `adventurer-attack*`, `adventurer-air-attack*`, `adventurer-hurt-*`, `adventurer-die-*`.
- Unity import: 50×37 pixels, Single Sprite, 18 PPU, Point filtering, uncompressed, no mipmaps, pivot `(25/50, 1/37)`. Physics dimensions and the Visual's existing foot position are preserved.
- Project-authored clips and Animator integration are in `Assets/_Game/Animations/Player`. Ground attack 1/2/3 use their corresponding source frames. Ordinary air combo uses air-attack1, air-attack2, then air-attack1 again; air-attack3 is reserved for the slam's ready/loop/end sequence. Lift and emergence reuse the upward ground attack1 swing.
- Attack clips sample preparation, impact and recovery according to combat's phase progress. This retiming is integration metadata; original image pixels are unchanged. The original archive and unused art are not included in Assets.


## New enemy patterns — 2026-09-30

All three archives were downloaded for free from their creators’ official itch.io pages. Each archive contains an explicit Creative Commons Zero (CC0) license; copies are retained as `License.txt` beside the imported PNGs. No source gameplay scripts, backgrounds, audio, or unused character art were imported. PNG bytes are unchanged. Unity slicing and animation/controller data are project-authored.

### LuizMelo — Hero Knight 2

- Official source: [LuizMelo — Hero Knight 2](https://luizmelo.itch.io/hero-knight-2).
- License: [CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/).
- Downloaded archive: `knight.zip` (local filename), 26,855 bytes. SHA-256: `960213A89F1401F1B050432E9EC80E067A7A9EB9C3CC2BE1070C532860ED55DF`.
- Source folder: `Hero Knight 2/Sprites/`. Used files: `Idle.png`, `Run.png`, `Dash.png`, `Death.png`.
- Imported folder: `Assets/_Game/Art/Characters/ShieldKnight`.

### Foozle — Sci-fi Lab Droids (art commissioned from Baldur)

- Official source: [Foozle — Sci-fi Lab Droids (art commissioned from Baldur)](https://foozlecc.itch.io/sci-fi-lab-droids).
- License: [CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/).
- Downloaded archive: `droids.zip` (local filename), 723,701 bytes. SHA-256: `D3B21180C6BEC2300A5CF8C73790C594958E6D61B4C4BB7CF22D70C777A8B840`.
- Source folder: `Foozle_2DC0006_Sci_Fi_Lab_Droids_Pack/Droid01/Png/`. Used files: `Droid01Idle.png`, `Droid01Shoot.png`, `Droid01Death.png`.
- Imported folder: `Assets/_Game/Art/Characters/SurveillanceDroid`.

### Foozle — Mecha Boss + Exploding Drone (art commissioned from aimen23b)

- Official source: [Foozle — Mecha Boss + Exploding Drone (art commissioned from aimen23b)](https://foozlecc.itch.io/sci-fi-lab-mecha-boss).
- License: [CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/).
- Downloaded archive: `drone.zip` (local filename), 98,871 bytes. SHA-256: `AD755AC11D83A99ECF2A14EF8FBCF1168ABCCB60F1BE9A5CB129AFA01FE1456D`.
- Source folder: `Foozle_2DC0008_Sci_Fi_Lab_Mecha_Boss_Plus_Drone/exploding_drone/`. Used files: `drone idle_Sheet.png`, `drone run-Sheet.png`, `drone death explosion-Sheet.png`.
- Imported folder: `Assets/_Game/Art/Characters/FlyingDrone`.

Hero Knight uses 140×140 cells, 25 PPU, pivot (0.5,57/140), Idle 11 / Run 8 / Dash 4 / Death 9 frames. Droid01 uses 32×32 cells at 23 PPU with foot pivot (0.5,2/32); Shoot uses 48×48 cells and matching foot pivot (0.5,10/48), Idle 4 / Shoot 12 / Death 9 frames. Exploding Drone uses 32×32 cells at 20 PPU, body pivot (16.5/32,15.5/32), Idle 8 / Run 10 / Death 12 frames. All textures use Point filtering, no mipmaps and no compression. Idle/Run use 10 FPS; action/death clips are sampled by gameplay phase timing.

## Noto Sans KR — cinematic dialogue font (2026-10-07)

- Source: https://github.com/notofonts/noto-cjk/tree/main/Sans/SubsetOTF/KR
- Original file: https://raw.githubusercontent.com/notofonts/noto-cjk/main/Sans/SubsetOTF/KR/NotoSansKR-Regular.otf
- Copyright: Adobe / Noto font contributors; the original font copyright metadata is preserved.
- License: SIL Open Font License 1.1. The unchanged license is included at `Assets/_Game/UI/Cinematics/Fonts/OFL.txt`.
- Local file: `Assets/_Game/UI/Cinematics/Fonts/NotoSansKR-Regular.otf`, unmodified.
- SHA-256: `69975a0ac8472717870aefeab0a4d52739308d90856b9955313b2ad5e0148d68`.

- 2026-10-07: CoreLoopStage의 안내 NPC는 위 Adventurer의 기존 Idle 스프라이트/클립을 색상 변경하여 재사용한다. 새 외부 에셋이나 라이선스는 추가하지 않았다.


## Project-supplied player art — 2026-10-15

- The user supplied the character PNGs and animation assets already present under `Assets/Art`; this integration downloaded no additional art and does not infer an author or license for those files.
- Original image bytes and import settings are unchanged. Player-specific integration clips live in `Assets/_Game/Animations/Player/ArtCharacter` and reuse these sprites with gameplay-aligned timing.
- These clips replace the player's Adventurer presentation. The original Adventurer assets and their notices remain because the guide NPC still uses its Idle clip.

## Project-supplied monster art — 2026-10-21

- Supplied by the user in the desktop `몬스터 애님` folder. No additional art was downloaded; author and license are not inferred from the filenames. Original PNG bytes are unchanged.
- Monster1 replaces SurveillanceTurret, Monster2 replaces FlyingDrone, and Monster3 replaces ShieldSoldier. Existing Pig art and all gameplay settings remain unchanged. Previous enemy art and its notices are retained.
- Imported 130 individual 64×64 PNGs into `Assets/_Game/Art/Characters/Monster1`, `Monster2`, and `Monster3`. Import settings: Single Sprite, Point, uncompressed, no mipmaps, full rectangle mesh. Monster1 uses 32 PPU and pivot `(32/64,13/64)`; Monster2 uses 32 PPU and pivot `(32/64,32/64)`; Monster3 uses 30 PPU and pivot `(32/64,7/64)`. Source frames face left; the existing visual component mirrors them for right-facing actions.
- Existing Idle/Run/Attack/Death clips and controllers retain their GUIDs. Idle/Run loop at 10 FPS. Attack/Death retain the existing FSM-driven normalized playback and timing; clips change only the SpriteRenderer sprite and contain no animation events.

| Enemy | Idle / Run | Attack | Death |
|---|---|---|---|
| SurveillanceTurret | `Monster1_PNG.zip: PNG/normal/Idle` (frame 1 held for Idle/Run; frames 1→5 in IdleTurn; frame 5 held in IdleOpposite; frames 5→1 in IdleTurnBack); `PNG/angry/Idle` (frame 1 for aimed AngryIdle) | `PNG/angry/Attack_Left` (10) | `Monster1Death_PNG.zip: PNG/angry/Death_Base` (10) |
| FlyingDrone | `Monster2_Idle.zip: Monster2_Idle/Monster2_Idle/Idle_Normal` (32, shared for Run) | `Monster2AttackPNG.zip: PNG/Attack_Angry_Left` (10) | `Monster2Death_PNG.zip: PNG/Death` (8) |
| ShieldSoldier | `Monster3Left_Idle+Walk_PNG.zip: PNG/Normal/Idle` (10) / `PNG/Normal/Walk` (16) | `PNG/Angry/Walk` (16), used for the existing shield dash | `PNG/Angry/WalkStopB` (8), stop/hold fallback until the existing death timer deactivates the enemy |

`Monster3Hit_PNG.zip` was visually inspected and contains UFO/drone frames despite its name, so it is not used for ShieldSoldier. The initial archives had no dedicated shield death sequence; the 2026-10-22 follow-up below replaces that fallback. The right-facing archive, pose banks, duplicate Monster2 death archive, and bundled scripts/shader were not imported.

The turret follow-up imported 20 additional unchanged PNGs into `Monster1/AngryIdle` and `Monster1/AngryDeath` (32 PPU, original 64×64 layout, pivot 32/64,13/64). The previous normal death frames remain available. Project-authored `SurveillanceTurretVisual` slices the referenced textures into a stationary pedestal and aiming upper part at runtime; source image bytes are unchanged. The source package's Unity scripts/shader and its variable frame timing were not imported.

Archive SHA-256:

| Archive | SHA-256 |
|---|---|
| Monster1_PNG.zip | `de58e355917a550f6c65ec95d4131360d2a564eeab9f3da1c1617c76adcaae1d` |
| Monster1Death_PNG.zip | `bd84ef94957397f2faf2a887836c6a54fdbf31dff6ee11769c043ed37d94bd2c` |
| Monster2_Idle.zip | `5881cfdb3ac689d589c613ecc12f48daefd1f1436826d4a666e7822d701c2185` |
| Monster2AttackPNG.zip | `1e52ea5ae55d9aa8375fc51b1cbf3cabb66f74365f3962b273cdb01fde9ece7b` |
| Monster2Death_PNG.zip | `7592c171cff7ae22b90afabecbc8565c63d1292486347a1155f001097f6dac1b` |
| Monster3Left_Idle+Walk_PNG.zip | `929032f0105d1ea84a33a4283768868922c831a18ef58b469813ce305e133610` |

## Project-supplied shield animations — 2026-10-22

- Supplied by the user in the Desktop `벽,바닥` folder. No download or author/license inference was made. Imported 66 unchanged PNGs into additional `Monster3` subfolders, retaining 30 PPU, Point, no compression/mipmaps, Full Rect and pivot `(32/64,7/64)`.
- Attack uses `Monster3Attack_PNG.zip: PNG/Left/AttackFromIdle` (14). Death uses `Monster3Death_PNG.zip: PNG/Left/Angry` (10), replacing the stop-pose fallback. Hit uses the new **`Monster3_Hit_PNG.zip`** (underscore included), `PNG/Left` (10); the old incorrectly named drone archive remains excluded.
- `Monster3Siled_PNG.zip: Monster3Siled_PNG/Core/Left/Angry` supplies Enter/Hold/Exit/BlockImpact (8 each). Right-facing copies and alternative walk-entry transitions are not imported; existing SpriteRenderer flipping handles direction. Original Idle/Run remain unchanged.
- Existing Attack/Death clip GUIDs are retained. Five clips/states are added for Hit, GuardEnter, GuardHold, GuardExit and Block. Source PNGs are not recolored, rescaled or cropped. The 2026-10-21 attack/death mappings above describe the prior integration.

| Archive | SHA-256 |
|---|---|
| Monster3Attack_PNG.zip | `4b7625be1d77eede68bc30d6aaac1feb94f375069c24fe1ccd2be74e1b861147` |
| Monster3Death_PNG.zip | `ef76f5b2c67f9fd56cf53fe32f6df0de4a638184372ac8974560fc6a9ec96463` |
| Monster3_Hit_PNG.zip | `7ad541209d19fede8c4b55704248b64d059a3e9c6525bb215bef760102a4d708` |
| Monster3Siled_PNG.zip | `1494a43df7abacf7aa2d2345ceb9c5dc56606ed5daf9bbeaafa8c9b2061a0a0a` |

## Combat FX Free — player slash and glitch spark (2026-10-21)

- Creator/source: [heyheythere — Combat FX Free](https://heyheythere.itch.io/combat-fx-free), version 1.1.1. This is the pack matching the user's reference image and `white slash` / `white spark` names. The supplied ParticleFX Digital Explosion URL describes a different effect and was not imported.
- License: [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/). Original `LICENSE.txt` is retained beside the sheets. Credit: **Combat FX Free by heyheythere - https://heyheythere.itch.io/combat-fx-free - CC BY 4.0**. Retain this attribution in distributed game credits.
- Downloaded archive: `combat-fx-free-assets.zip`, 1,661,356 bytes, SHA-256 `fead596e08728bf69ab1385ca2822dac7da93160aec07dc5e31fea69d43c0755`.
- Only `sheets/128/white/white_slash.png` and `white_spark.png` were imported into `Assets/_Game/Art/Effects/CombatFxFree`. Original PNG bytes are unchanged. No bundled Godot scripts or unrelated effects were imported.
- Each sheet has 16 frames, 128×128 cells, 8 columns, left-to-right/top-to-bottom order, centered anchor. Unity uses Multiple Sprite, 128 PPU, full rectangle, Bilinear, no mipmaps/compression. The provided HD artwork matches the reference's smooth edges.
- Project-authored presentation retimes the slash to the existing basic attack Active/Recovery phases. Spark plays once at the source 30 FPS at successful glitch departure/arrival points. No gameplay timing or damage values are changed.
- Project-authored `PlayerActionTwoTone.shader` / `PlayerActionTealRed.mat` recolor the rendered effects with teal `#79D9DF` and red `#D84255`, plus a subdued white highlight. This is a presentation modification; the original PNG files, frame geometry, and transparency remain unchanged.

## User-supplied industrial terrain and background (2026-10-21)

- Source: the user's Desktop `벽,바닥` folder. `terrain_tiles.zip` contains 32 terrain PNGs; `Background_runtime.zip` contains `01_base.png`, `02_far.png`, and `03_mid.png`. Imported into `Assets/_Game/Art/Environment/Industrial` for CoreLoopStage.
- All PNG bytes are preserved. Import settings and scene placement are project-authored. The archives contain no creator or license document; no third-party license has been inferred.
- `terrain_tiles.zip` SHA-256: `6fd1f72204b2bf21aa8c506ca98bbe7d90899ba504bbe6640b2dd22726de5f56`.
- `Background_runtime.zip` SHA-256: `4bf229103940edfaff5d8f932f3c1f33548aef7ba71d6b432dc9c13c2af6d200`.

## ARCBIT Lite — enemy lasers (2026-10-25)

- Creator/source: [gennai — ARCBIT Lite](https://gennai.itch.io/arcbit-lite), download version 1.0.2, `arcbit-lite-lite.zip`, SHA-256 `10915e07e80a6cbbe3375a3240c4bd58e66b9b17e1c850227bb5b59c63832585`.
- Free use and modification in commercial/non-commercial games, apps and media; attribution optional. The pack and its parts may not be redistributed on their own or in another asset pack, template or tool, resold, used to train machine-learning models, or claimed as original work. Exported product builds are permitted. Original `LICENSE.txt` and `CREDITS.txt` are retained in `Assets/_Game/Art/Effects/ArcbitLite`.
- Imported only the six unchanged `sprites/laser/laser_{start,body,end}_32x32_{blue,red}.png` strips and their source JSON. Turret uses blue, FlyingDrone uses red. No Godot scripts/project, orb or zap effects were imported.
- Unity slices 21 start frames and 13 body/end frames per color (94 sprites total), 32 PPU, Point, Full Rect, no mipmaps/compression. Pivots are converted from the source top-left coordinates: start `(12,17)`, body `(0,17)`, end `(17,17)` in Unity pixel coordinates.
- Project-authored `EnemyLaserVisual` uses the original palettes with an unlit sprite material. Charge follows existing preparation/lock; on/loop/off frames are retimed to the existing 0.16-second beam display. Tiled body fills the gap between source/impact caps; native source dimensions are retained and render scale fits the existing beam width. Very short clipped beams shrink caps together.
- Enemy targeting, aim lock, muzzle position, wall clipping, attack timing, damage, and the player's glitch spark effects are unchanged. FlyingDrone's existing bidirectional vertical shot is displayed as two segments from the emitter to the already-calculated endpoints.
