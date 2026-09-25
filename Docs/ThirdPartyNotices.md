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
