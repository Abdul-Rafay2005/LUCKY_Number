# Phase 5 art inventory

Source archive `gameassets.zip` contains 15 JPEGs. The existing project JPEGs are smaller copies of the corresponding source artwork. The archive and project JPEGs were preserved. The team confirmed that all archive art is original, collectively created by the Lucky Number team, and authorized for use in the project. Credit the Lucky Number team for the archive artwork.

| Archive entry | Project art under `Assets/Art` | Screen or intended use | Cleanup and usage |
| --- | --- | --- | --- |
| `BG.jpg` | — | Alternate lounge backdrop | Full-scene JPEG. Kept in the archive. |
| `game bg (2).jpg` | `Backgrounds/GameLoungeBackground.jpeg` | Main menu and shared backdrop | Reuse the existing opaque background. UI contrast is controlled separately. |
| `buttons for bg.jpg` | `UI/MainMenuButtonsSheet.jpeg`, `UI/Buttons/Btn_*.png` | Main menu | Recropped the normal-state illustrations to remove clipped edges, checkerboards, and the adjacent hover row. Illustrations still contain English labels. Use the tintable rounded sprite for controls with live TMP labels and state colors. |
| `characters.jpg` | `Characters/Portraits/Portrait_0.png`–`Portrait_5.png` | Lobby cards and player UI | Recropped six consistent bust portraits from the lineup, including the red-haired athlete previously mostly outside the crop. Removed the off-white source matte. |
| `game asset 1.jpg` | `Props/ScoreboardGadgetSheet.jpeg`, `Props/Scoreboards/Scoreboard_*.png` | Gameplay score header | Removed checkerboards from normal, goal, and warning gadgets. These illustrations contain fixed digits and markings, so use them as small emblems alongside authoritative TMP score and target text. |
| `game asset 2.jpg` | `Dice/DiceSheet.jpeg`, `Dice/Slices/*.png`, `Dice/DiceRollSprites.png` | Private gameplay dice and roll animation | Recut all six face values, six roll poses, cursed and golden dice. Removed checkerboards, pose frames, and baked captions. Restored clipped silhouettes and corrected the top of the six die from four pips to six. Animated poses are transitional art. Live numbers accompany the resolved dice. |
| `gameassets.jpg` | — | Alternate tokens, dice, ability badges, and clock | White-matte concept sheet with static numbers. Kept in the archive. |
| `Gemini_Generated_Image_90gs4190gs4190gs.jpg` | `Props/IsometricPropsSheet.jpeg` | Isometric lounge props | Reference for future prop work. No new runtime slices. |
| `Gemini_Generated_Image_wsi83hwsi83hwsi8.jpg` | `Characters/Char1_BoyGlasses.jpeg` | Boy with glasses pose reference | The individual full-body pose grid remains source art. |
| `Gemini_Generated_Image_wsi83hwsi83hwsi8 (1).jpg` | `Characters/Char2_FortuneTeller.jpeg` | Fortune-teller pose reference | The individual full-body pose grid remains source art. |
| `Gemini_Generated_Image_wsi83hwsi83hwsi8 (2).jpg` | `Characters/Char3_StylishGirl.jpeg` | Stylish girl pose reference | The individual full-body pose grid remains source art. |
| `Gemini_Generated_Image_wsi83hwsi83hwsi8 (3).jpg` | `Characters/Char4_CapBoy.jpeg` | Boy with cap pose reference | The individual full-body pose grid remains source art. |
| `Gemini_Generated_Image_wsi83hwsi83hwsi8 (4).jpg` | `Characters/Char5_DreadlocksMan.jpeg` | Man with dreadlocks pose reference | The individual full-body pose grid remains source art. |
| `Gemini_Generated_Image_wsi83hwsi83hwsi8 (5).jpg` | `Characters/Char6_SportyGirl.jpeg` | Sporty girl pose reference | The individual full-body pose grid remains source art. |
| `UI asset.jpg` | `UI/GameplayUISheet.jpeg`, `UI/Icons/Icon_*.png` | Gameplay operations, player status, and role reveal | Recut clock, plus, minus, skip, alive, dead, and imposter mask from complete source shapes. Removed checkerboards, adjacent art, and source captions. |
| `vfx effects.jpg` | `VFX/VFXSheet.jpeg`, `VFX/MatchEffectSprites.png` | Local roll and voting feedback, public target changes and role reveals | Five frames each of sparkle, vote, shift, target, and reveal. Soft effect opacity was reconstructed from the flattened source; numeric and curse-specific effects stay in the source sheet. |
| Project-authored | `UI/Panels/RoundedPanel.png` | Tintable panels and controls | Neutral 64×64 rounded rectangle with a 12 px border on all four sides for Unity sliced images. |

The cleaned sprites use RGBA, centered pivots, full-rectangle geometry, bilinear filtering, and uncompressed textures with mipmaps disabled. Individual dice and roll frames use the same 192×192 transparent cell. The roll sheet names are `DieRoll_1` through `DieRoll_6`. The 160×160 effect cells are named `Shift_1` through `Shift_5`, `Target_1` through `Target_5`, `Vote_1` through `Vote_5`, `Reveal_1` through `Reveal_5`, and `Sparkle_1` through `Sparkle_5`. Preserve the existing individual sprite GUIDs when updating crops. Inspect any new crops on both dark and light UI backgrounds before replacing runtime sprites.
