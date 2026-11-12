# Progression

Updated: 2026-11-12

## Current objective

Phase 5 is complete through `todo.md` item 5.14. Phase 6 covers Relay and cross-machine validation, playtesting, balance, and release.

## Completed

- Phases 2–4 passed localhost authority, session, departure, spectator, budget, privacy, and functional match UI scenarios. Historical results are in `docs/validation.md`.
- Phase 5 cleaned 33 source crops with stable Unity GUIDs, added sliced dice and effect sheets and a nine-slice UI surface, and integrated them in `Assets/Scenes/LuckyNumberMaster.unity` and `Assets/Prefabs/UI`. The team confirmed original authorship and authorized project use. See `docs/asset-inventory.md`.
- The UI uses the lounge, portraits, dice, and effects with fewer large panels. Added private roll, selection, curse, and vote feedback, public target-shift and ejection effects, procedural sound, and saved volume. The generator is an explicit development command and guards the authored scene.
- Unity built the final isolated Windows player with zero errors and one existing Unity Services project-link warning. The three-player UI and eight-player presentation scenarios passed at 1280×720, 1920×1080, and 1024×768. The existing Edit Mode suite passed 22 of 22 on the Phase 5 snapshot before the final layout and modal changes. See `docs/validation.md` for screenshots and validation scope.

## Next actions

- Recheck account usage before starting Phase 6. Use `todo.md` to resume at 6.1. Compare existing Edit Mode coverage to 6.1 before adding tests, then extend Play Mode and Relay/cross-machine coverage.
- Audible balance, Windows relaunch persistence, Relay rooms, cross-machine behavior, human playtests, and release packaging are pending.
- Keep `Tools/Validation` outside production Assets. Workspace changes remain uncommitted.

## Settings follow-up

- The main-menu Settings panel has Audio, Display, and Controls tabs. Audio includes separate master and effects sliders and a sample. Display supports windowed, borderless fullscreen, and exclusive fullscreen with timed confirmation. Controls documents mouse and gamepad input and supports remapping five keyboard controls.
- The final Windows validation build had zero errors and the existing Unity Services warning. The rendered 1024×768 settings and eight-player scenario passed. It checked both volume previews, display options and manual revert, navigation and activation remapping, duplicate keys, reset, and discard.
- Save paths are implemented. Preferences across a Windows relaunch, display rollback on timer expiry, and each fullscreen mode on physical displays were not independently verified. Resume Phase 6 at TODO 6.1.

## Distribution follow-up

- Added Windows launch, room-code and voice instructions in `README.md`.
- Three Windows peers passed a live Unity Authentication, Lobby, and Relay smoke test using the configured Unity project. They played a move, voted, rematched, and left. All processes ran on one machine. Cross-machine acceptance and human playtesting remain open.
- Production Windows package output is local under `builds/release`. Keep validation fixtures outside production Assets.
- Production source was pushed to GitHub main in commit 46876eb. The extracted Windows x64 ZIP launched successfully. The next acceptance step is to share the ZIP with another Windows machine and verify a room-code join across networks.
