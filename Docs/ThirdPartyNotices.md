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
