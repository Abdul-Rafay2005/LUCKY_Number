# Phase 1 validation

Run date 2026-11-09. The baseline uses the workspace files as they stood before Phase 1 edits, including the existing local project and package changes.

## Reproduce the baseline

1. Use Unity 6000.3.23f1 with Windows Build Support. Open the project, allow package resolution and script compilation, and open `Assets/Scenes/LuckyNumberMaster.unity`. Do not regenerate scenes from the setup menu.
2. Verify the scene contains one NetworkManager with Unity Transport and the LuckyNetworkPlayer prefab, a NetworkGameManager with a NetworkObject, and an EventSystem with InputSystemUIInputModule. In the Phase 1 version, confirm the game manager references `Assets/ScriptableObjects/DefaultGameSettings.asset` and the master scene is the only enabled build scene.
3. Build Windows x86-64, then launch the built player in three separate windows. Use the menu's local host button in the first window and local join in the other two. The direct path uses `127.0.0.1:7777` and requires no room code. The three windows exercise separate player processes on one machine.
4. Compare the host and client lobby rosters, start a match from the host, and observe the initial phase and turn UI in each window. A working baseline should show three connected players, allow the host to start, show all clients the same public phase and score, and reveal dice only to the active player.

To compile without opening the Unity UI, run the installed editor with `-batchmode -nographics -quit -projectPath "<isolated-project>" -logFile "<editor-log>"` and inspect the exit status and C# diagnostics.

## Pre-edit editor baseline

- Imported and compiled the copied `Assets`, `Packages` and `ProjectSettings` with Unity 6000.3.23f1. The editor exited with code 0 and no C# errors or warnings. Netcode for GameObjects 2.2.0, Unity Transport 2.4.0, Lobby 1.3.0 and Relay 1.2.0 resolved. The package lock remained byte-identical to the staged baseline.
- A first attempt in the restricted process environment stalled during licensing and ended in an editor crash. A host-access retry completed import and compilation with the existing Unity license. This was an environment failure before compilation.
- Unity opened the master scene and confirmed the NetworkManager, transport, networked player prefab, NetworkGameManager/NetworkObject, EventSystem and input module. The manager enables background operation on network start. The settings asset existed with a four-second role reveal, but the game manager's serialized settings reference was empty. The baseline manager creates a transient settings instance in that case. The master scene was the first of six enabled scenes.
- Validation artifacts and raw logs are under the ignored `builds/phase1-validation` directory on the machine that ran the checks.

## Pre-edit Windows runtime

- Built a fresh Windows x86-64 player from the isolated pre-edit snapshot. The build succeeded with zero errors and one Unity Gaming Services project-link warning. The initial 1280×720 menu rendered the player profile, online room controls, Host Local and Join Local, How to Play, Settings and Exit.
- One host and two clients joined via the menu over localhost. All three displayed three lobby cards. They shared the same default profile name on this machine. Start was enabled for the host with all players marked waiting, and the host successfully started the match.
- All three logged RoleReveal then PlayerTurn. During role reveal the host remained on the lobby screen. In the first turn all three displayed target 31, score 0 and 15 moves. Both observers saw a waiting banner and no dice. The active client saw three blank dice, and selecting one showed +0. The host rejected the submission as outside the authorized roll. The score and move count were unchanged.
- The first turn timed out and advanced without spending a move. Dice rendered for subsequent turns. The host and a client each committed +1. All three reached discussion at score 2 with 13 moves remaining. No observer's dice panel appeared on the sampled turns.
- The clients' match logs omitted the second committed move during the transition to discussion, even though their score and move count were correct. Discussion also showed stale waiting text and the subtract button appeared blank on active turns. Runtime TextMesh Pro logs reported missing glyphs, visible as square placeholders in the lobby and turn/discussion banners.
- Closed all three test windows after the discussion check.

## Post-edit editor verification

- Applied the Phase 1 files to the isolated editor copy, recompiled and reopened the master scene. The editor exited with code 0 and no C# diagnostics. It verified the game manager references DefaultGameSettings and the master scene is the only enabled build scene.
- Verified the staged timeout, personal skip, team shift, curse charge, round cap and reconsideration defaults deserialize. Invoked editor validation on a temporary in-memory settings instance with invalid timers, player and role counts, charges, and shift bounds. The values were repaired to the expected bounds and defaults.
- The localhost smoke used the pre-edit build. A post-edit standalone build, Relay room creation/join, cross-machine connections, full matches, voting outcomes and rematches were outside this run.

# Phase 2 validation

## Roster checkpoint

- Built the 2.1–2.2 snapshot with Unity 6000.3.23f1 in the isolated editor project from Phase 1. The Windows player build exited successfully with zero errors and the existing Unity Gaming Services project-link warning.
- Ran one host and two separate clients against localhost. Every peer reported the same three client IDs and distinct player profiles. The spawned registry, game manager lookups and living-player queries agreed. All three active lobby panels showed three cards with those profiles.
- The roster cleared on all peers after leaving and all three processes exited successfully. This was an automated check in Windows player processes with graphics disabled. It inspected live Unity UI components for the count and card names.
- The isolated harness and raw logs are under the ignored `builds/phase2-validation` directory. The harness used the game's local connection methods, set test profile names through player RPCs, and bypassed Unity Services initialization for these localhost-only checks.

## Integrated build

- The initial authority compile found an invalid Unity metadata GUID for the new action types. After it was fixed, the authority and final integrated Windows builds succeeded in Unity 6000.3.23f1 with zero errors and the existing Unity Gaming Services project-link warning.
- The integrated test launcher uses real localhost player processes and production RPCs. It sends fixture commands and observations through local files. It can advance phase timers, choose a test seed, and lower capacity for an approval test. Product sources in the workspace do not contain the harness.

## Authority run observations

- The first localhost room reached the first turn with the same action stamp on the host and both clients. The active remote client received a usable three-die roll and its owner-only role. On the ordinary clients, the other players' underlying secret-role and roll variables stayed unset. A nonhost start request was rejected.
- The second room reused the same processes after explicit leave. Its two clients got new IDs and saw PlayerTurn, the new private roles, and public score/move/target state. One client retained the new session's lobby action stamp (match 0, action 0) while the host advanced to match 1, action 1. The harness stopped before duplicate, stale, budget-spend and terminal assertions. A stale public stamp after rehosting can prevent valid actions.
- The first preliminary launch exceeded the harness's 20-second player startup budget before running any assertions. The integrated rerun used an 80-second startup budget.

## Remaining Phase 2 matrix

| Area | Check |
| --- | --- |
| Private state | Confirm only the owner can read their role, dice, charges and skips. Confirm observers see the same public phase and score. Verify the first active client can commit a real roll. |
| Action authority | Reject out-of-turn, duplicate, stale, unauthorized, dead-player and terminal requests. Confirm rejected requests leave score, move count, budgets and effects unchanged, and that acknowledgments go to the caller. |
| Match lifecycle | Verify host-only start and rematch, lobby player limits, late-join rejection and no reset from a mid-match start request. Verify match state reinitializes for rematch. |
| Session lifecycle | Exercise failed connection, duplicate connection attempt, leave/rejoin and host loss. Check that lobby UI opens only after connection and returns to the menu on exit. |
| Departures | Check lobby departure without applying a game win, and active/inactive departures during play. Use a fourth player for nonterminal continuation because a living departure from a three-player match can immediately satisfy the adopted role-count conditions. |

Relay and cross-machine connections are outside the localhost matrix.




## Session run and resume point

- The session run observed a pending localhost connection with the main menu visible, lobby hidden, and zero join events. A transient Windows sharing violation reading the test mailbox stopped the run before the connection failure completed. No capacity, late-join, departure, host-loss, or spectator result was obtained.
- The implementer updated `Assets/Scripts/Network/NetworkGameManager.cs` to reinitialize scene network variables for a second host session. That edit is newer than the final integrated test build and still needs a Unity rebuild and repeated rehost check.
- The ignored PowerShell launcher now retries transient mailbox read failures. Its syntax parsed successfully. The retry has not run in a live player scenario.
- Resume by copying the updated `NetworkGameManager.cs` into the warm isolated `builds/phase1-validation/host-project` at the same relative path. Run Unity 6000.3.23f1 there with `-batchmode -nographics -quit -executeMethod PhaseTwoEditor.Build -phase2BuildPath "<new Windows player path>" -logFile "<new editor log>"`. Launch `builds/phase2-validation/Run-PhaseTwoIntegration.ps1` with `-PlayerPath`, a new `-OutputDirectory`, an unused localhost `-Port`, and `-Scenario Authority`. Verify a second room advances the stamp on host and both clients. Run `Session`, `Departure`, and `Spectator` in new output directories after that passes.
