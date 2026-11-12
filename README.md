# Lucky Number

Lucky Number is an online social deduction game for 3–8 players. Choose a private die, add or subtract its value from the shared score, and work toward the exact target while hidden imposters try to derail the group.

## Play on Windows

1. Download the Windows x64 ZIP and extract the entire archive. Open `LuckyNumber.exe` from the extracted folder. Keep the executable, data folder, and supporting files together.
2. Everyone should use the same build and connect to the internet. One player chooses **Create Online Room** and shares the code shown in the lobby.
3. Everyone else enters that code and chooses **Join Online Room**. The host can start when at least three players have connected. Keep the host's game running for the duration of the match.
4. Arrange a separate voice call for discussion. Ejected players can spectate and should stay silent during play.

Use the mouse to select dice, operations, abilities, and ballots. Keyboard arrows or WASD move focus, Enter activates the focused control, and the mouse wheel scrolls match history. The main-menu **Settings** panel has audio, display, and keyboard binding options. **How to Play** explains the rules in-game.

If a code stops working, verify that the host is still in the lobby and the match has not started. If the host closes the game, create a new room and share its new code.

## Build from source

Open the project in Unity **6000.3.23f1** with Windows Build Support. Build the enabled `Assets/Scenes/LuckyNumberMaster.unity` scene for Windows x64. Online play uses the configured Unity project with Authentication, Lobby, and Relay. For a fork deployed under a different Unity project, link that project and enable those services before building.

See [rules](docs/rules.md), [validation](docs/validation.md), and [art inventory](docs/asset-inventory.md). Artwork by the Lucky Number team.
