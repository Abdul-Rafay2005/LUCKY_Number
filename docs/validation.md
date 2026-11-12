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

## Phase 2 matrix defined at the original checkpoint

| Area | Check |
| --- | --- |
| Private state | Confirm only the owner can read their role, dice, charges and skips. Confirm observers see the same public phase and score. Verify the first active client can commit a real roll. |
| Action authority | Reject out-of-turn, duplicate, stale, unauthorized, dead-player and terminal requests. Confirm rejected requests leave score, move count, budgets and effects unchanged, and that acknowledgments go to the caller. |
| Match lifecycle | Verify host-only start and rematch, lobby player limits, late-join rejection and no reset from a mid-match start request. Verify match state reinitializes for rematch. |
| Session lifecycle | Exercise failed connection, duplicate connection attempt, leave/rejoin and host loss. Check that lobby UI opens only after connection and returns to the menu on exit. |
| Departures | Check lobby departure without applying a game win, and active/inactive departures during play. Use a fourth player for nonterminal continuation because a living departure from a three-player match can immediately satisfy the adopted role-count conditions. |

Relay and cross-machine connections are outside the localhost matrix.




## Historical session run and resume point

- The session run observed a pending localhost connection with the main menu visible, lobby hidden, and zero join events. A transient Windows sharing violation reading the test mailbox stopped the run before the connection failure completed. No capacity, late-join, departure, host-loss, or spectator result was obtained.
- The implementer updated `Assets/Scripts/Network/NetworkGameManager.cs` to reinitialize scene network variables for a second host session. That edit is newer than the final integrated test build and still needs a Unity rebuild and repeated rehost check.
- The ignored PowerShell launcher now retries transient mailbox read failures. Its syntax parsed successfully. The retry has not run in a live player scenario.
- Resume by copying the updated `NetworkGameManager.cs` into the warm isolated `builds/phase1-validation/host-project` at the same relative path. Run Unity 6000.3.23f1 there with `-batchmode -nographics -quit -executeMethod PhaseTwoEditor.Build -phase2BuildPath "<new Windows player path>" -logFile "<new editor log>"`. Launch `builds/phase2-validation/Run-PhaseTwoIntegration.ps1` with `-PlayerPath`, a new `-OutputDirectory`, an unused localhost `-Port`, and `-Scenario Authority`. Verify a second room advances the stamp on host and both clients. Run `Session`, `Departure`, and `Spectator` in new output directories after that passes.

# Review repair validation (2026-11-11)

This checkpoint supersedes the earlier resume instructions. The baseline source is commit 9dcca3f; the repair diff is currently uncommitted. Sync all current production scripts, scene/settings and tests into an isolated project before rerunning, rather than copying only NetworkGameManager.cs from the historical instructions.

## Build and focused integration

- Unity 6000.3.23f1 compiled and built the isolated Windows player successfully: zero build errors and one existing Unity Services project-link warning. The initial compile caught an unsupported LINQ call in the test-only driver; it was corrected before the successful rebuild. No production compiler errors remained.
- Tools/Validation contains the test-only driver, editor build entry point, PowerShell launcher and instructions. These stay outside production Assets and are copied only into the isolated test project.
- Run-ReviewRegression.ps1 passed against three hidden localhost player processes, twice in the same processes with leave/rehost between rooms.
- All peers agreed on roles/turn stamps and private views; ordinary clients did not receive other owners' secrets/rolls. Each peer received a target-shift event with empty actor name and the anonymous actor sentinel, and displayed anonymous shift history.
- Actual production vote RPCs were accepted in the initial ballot and two runoffs. The third identical tied result selected a random ejection. All peers received the runoff number and reached the winning elimination reveal with role text visible before results. An ability during the locked reveal was rejected. Timer completion then moved everyone to results.
- The harness uses host-only fixture commands to open voting and shorten timers. This is focused integration evidence, not a complete natural match or balance playtest. UI assertions inspect active components and text in headless players, not rendered layout.
- Players were shut down by the harness; the run completed with exit code 0. Local artifacts: builds/review-build-retry.log, builds/review-validation, builds/review-run-1. Those artifacts are ignored; reproduction instructions are tracked.

## Acceptance pending at the review checkpoint

The focused review left the authority, session, departure, spectator, shared-budget, rematch, and UI checks for the continuation below. Relay, cross-machine networking, and release packaging are tracked in Phase 6.

## Edit Mode and final checks

- The existing Edit Mode suite passed 22/22 tests, zero failures and zero skipped, in the isolated current project (builds/review-editmode.xml). Unity exited with code 0.
- git diff --check passed. No production scene regeneration, package upgrade or art modification occurred in this repair. The working tree contains uncommitted source/documentation repairs and the new Tools/Validation harness.
- The focused player logs contained no exception or C# error matches in the final log scan. All launched validation players and Unity build/test processes finished.

# Multiplayer continuation checks (2026-11-11)

The maintained localhost harness is `Tools/Validation/Run-ReviewRegression.ps1`. The following scenarios passed with Unity 6000.3.23f1 Windows players:

- **Authority** verified host-only match start and rematch, owner-only role and dice delivery, caller-only rejection, one accepted move from a duplicate, rejection of previous-turn and previous-match stamps, personal skip privacy, and immutable results.
- **Session** verified failed connection recovery, duplicate connect and duplicate lobby-manager isolation, minimum player count, leave/rejoin, capacity and late-join rejection, and return to menu after abrupt host loss.
- **Departure** verified that an inactive departure preserved the current actor and the remaining schedule, and that an active departure advanced exactly once without spending a move.
- **Spectator** verified private teammate visibility in a six-player match and that an ejected imposter retained a block charge but could neither use it nor affect an active round.
- **Budget** verified one block and two curses per imposter, one activation per round, one shared shift, host and remote owners spending charges, and full budget restoration on two rematches. The six-player rerun also verified that the role panel revealed teammate names only to imposters.
- **Review** verified that a cached move stamp from the prior room was rejected after rehosting without consuming a roll. It repeated the anonymous shift, runoff, winning elimination, and results checks in both rooms.

## Phase 4 functional UI

- **UI** exercised the lobby ready button, private role and dice, gameplay panel reactivation, a negative shift, subtraction, a voluntary skip, block and curse, skip and candidate votes, elimination, two consecutive rematches, and return to menu through the actual scene button callbacks. It verified stable lobby cards and restored history on panel reactivation.
- The rendered 1280×720 pass exposed low-contrast operation labels, overflowing lobby/vote names, and unsupported result glyphs. Those were fixed and the rebuilt screenshots were inspected. Baked checkerboards remain visible in the existing dice and reveal sprites for Phase 5 asset cleanup.
- The final three-player UI scenario passed with graphics enabled at 1280×720, 1024×768, and 1920×1080. The narrower and 1080p runs exercised a remote player's negative move and voluntary skip. The captured screens support a layout smoke check, with the eight-player, long-history, keyboard, and full presentation checks tracked in Phase 5.
- The final isolated Windows validation player built with zero errors and one existing Unity Services project-link warning. The Edit Mode rerun passed all 22 tests with zero failures and zero skipped.

The harness controls phase pacing with fixtures. Human playtests, Relay, cross-machine connectivity, and the broader Phase 5–6 acceptance checks remain open.

## Phase 5 presentation acceptance (2026-11-12)

- Unity 6000.3.23f1 built the isolated Windows player with zero errors and one existing Unity Services project-link warning. The authored master scene was copied back and matched the isolated scene by hash. The existing Edit Mode suite passed 22 of 22 tests on the Phase 5 code before the final layout and modal input changes.
- The three-player UI scenario passed at 1280×720, 1920×1080, and 1024×768. It exercised ready, die and sign selection, commit, skip, all three abilities, voting, two rematches, and leave. It verified private role and dice delivery, role-specific controls, and history hydration on reactivation.
- The eight-player Presentation scenario passed at all three sizes. It exercised keyboard focus assignment and return, modal raycast isolation, volume preview and restoration on dismiss, a representative join error, eight long names, 35 public history entries, discussion, a full ballot, and ejection. The error message and long history used test fixtures.
- Inspected 16 captures per size. First-pass defects included cramped sabotage labels, indistinguishable end-truncated names, a menu title icon overlap, and incomplete modal input isolation. Widened controls, kept name suffixes visible, moved the title artwork, and disabled menu interaction and raycasts under modals. Long names wrap within the history scroll area.
- Procedural audio checks validated all 11 cues and the volume save/restore code with Unity audio stubs. The Unity menu scenario verified slider preview and restoration. Subjective listening and a Windows relaunch persistence check remain pending.
- Screenshot contact sheets (local build artifacts): [1280×720](../builds/phase5-screens-1280.png), [1920×1080](../builds/phase5-screens-1920.png), [1024×768](../builds/phase5-screens-1024.png). Captures and logs are in `builds/phase5-verified-ui-*` and `builds/phase5-verified-capacity-*`.

## Settings follow-up (2026-11-12)

- Unity 6000.3.23f1 built the final Windows player with zero errors and the existing Unity Services warning. The 1024×768 rendered Presentation scenario passed settings, modal focus, and the eight-player ballot flow.
- Settings assertions covered both volume previews, borderless resolution selection, confirmation and manual revert, keyboard navigation and activation rebinding, duplicate binding rejection, reset, and discarding changes. Rebinding exposed a default Submit action in the scene, so the master EventSystem was wired to the project input asset and retested.
- Save and startup restore use PlayerPrefs. Windows relaunch, timed expiration, and each physical fullscreen mode were not independently verified.
- Captures: [Audio](../builds/settings-complete-1024/ValidationClient1-settings-audio.png), [Display](../builds/settings-complete-1024/ValidationClient1-settings-display.png), [Controls](../builds/settings-complete-1024/ValidationClient1-settings-controls.png).

## Windows distribution and live Relay smoke (2026-11-12)

- Three Windows test players used real anonymous Unity Authentication, Lobby room creation and code join, and Relay transport through the main menu. They started a match, verified private role and dice delivery, committed a move through the UI, voted, reached results, rematched, and left. The three processes ran on one machine. Cross-machine acceptance remains open.
- The Unity editor printed its project-link warning in batch mode. Live Authentication, Lobby, and Relay worked with the configured project during the smoke test.
- The shareable Windows x64 package is generated from the production master scene. Validation driver source stays in `Tools/Validation` outside the production Assets tree.
