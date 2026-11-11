# Progression

Updated: 2026-11-09

## Objective and status
Phase 1 is complete. Phase 2 remains partial on `codex/phase-1-baseline`. The account reached the chat's 5% reserve, so implementation and tests stopped. Items 2.1–2.2 passed. The remaining Phase 2 source changes are present, with final validation outstanding.

## Completed
- Read the full concept brief text and inventoried all 16 JPEG entries in gameassets.zip; visually reviewed an archive contact sheet.
- Reviewed all custom scripts, master scene references, placeholder-scene generation, player prefab, settings and package/editor configuration.
- Inspected installed Netcode connection source to flag client roster assumptions for runtime verification.
- plan.md contains the game/current-project summaries, architecture, 22 comparison findings, provisional decisions, asset assessment, screen requirements, six delivery phases and acceptance criteria.
- todo.md translates the plan into ordered implementation and validation checklists. It records only verified items as complete.
- The user approved provisional rules from plan.md, Windows desktop with keyboard and mouse, external voice, and Relay for online rooms. Localhost is the baseline test path.
- The tester staged an isolated copy of Assets, Packages and ProjectSettings before any Phase 1 source edits.
- Created the dedicated `codex/phase-1-baseline` branch with the existing working tree intact. The project-version and virtual-project edits select Unity 6000.3.23f1, and the existing lockfile differs in the resolved Wire and Searcher versions. The remaining pre-existing asset/settings entries showed no content diff after Git line-ending normalization. No existing changes were committed or reverted.
- `docs/rules.md` records the eight rule groups and the release scope. `Assets/Scripts/Core/GameSettings.cs` and `Assets/ScriptableObjects/DefaultGameSettings.asset` stage the adopted balance policies and editor validation.
- `Assets/Scenes/LuckyNumberMaster.unity` now references the default settings asset. Its role reveal uses four seconds instead of the previous transient fallback's five. The release scene list and setup generator enable only the master scene.
- `docs/validation.md` records reproducible editor and localhost run instructions, the baseline defects, and the post-edit editor results. All eight Phase 1 checklist items are checked.
- Phase 2 items 2.1–2.2 are implemented in `Assets/Scripts/Network/LuckyNetworkPlayer.cs`, `Assets/Scripts/Network/NetworkGameManager.cs`, `Assets/Scripts/UI/LobbyController.cs` and `Assets/Scripts/UI/GameplayController.cs`. Spawned player objects populate the client roster. Lobby, gameplay and discussion lookups use it. A fresh headless Windows build verified all three localhost peers showed the same roster and lobby cards and cleared their registries on shutdown.
- The authority snapshot includes owner-only roles, rolls and budgets, stamped action requests, private acknowledgments, independent role and turn shuffles, stable turn scheduling, and phase-aware departure handling. Source is in `Assets/Scripts/Network/MatchAction.cs`, `LuckyNetworkPlayer.cs`, `NetworkGameManager.cs`, `Assets/Prefabs/LuckyNetworkPlayer.prefab` and the gameplay, voting, lobby and result controllers under `Assets/Scripts/UI`. Static whitespace check passed before the final rehost change.
- The integrated snapshot adds confirmed connection state, connection approval, serialized cleanup, host loss handling and guarded singleton destruction in `Assets/Scripts/Network/NetworkLobbyManager.cs`. The menu controller, master scene and setup generator expose the connection status and label the direct path as localhost testing.

## Validation and limits
Static inspection and `git diff --check` passed. The first isolated Unity launch hit licensing initialization errors under the sandbox. The host-access retry imported and compiled the original snapshot in Unity 6000.3.23f1 with exit code 0 and no C# warnings or errors. The manifest and package lock were unchanged. The baseline Windows build succeeded with one Unity Services project-link warning. The post-edit Unity check passed for settings defaults and validation, scene settings wiring, and the sole enabled scene.

The fresh baseline opened its main menu at 1280×720. A host and two separate localhost clients joined, displayed three roster cards, and started round one with matching public state. The first active remote client displayed blank dice and selected +0. Committing left the score and move count unchanged. That turn timed out without spending a move. The other two players each committed +1. All peers reached discussion at target 31, score 2 and 13 moves. Both remote clients missed the last move in their history, and the discussion banner overlapped stale waiting text. Observer dice were hidden during sampled turns. The three test players were closed. Relay, full matches and release resolution checks have not run.

## Preserved work
Pre-existing modified files in Assets/Settings, the TextMesh Pro fallback font, Packages/packages-lock.json and ProjectSettings were preserved. The only intentional overlap is the five placeholder scene flags in `ProjectSettings/EditorBuildSettings.asset`. All other pre-existing modified files retain their original hashes. Confirm the purpose of pre-existing changes with their owner before any commit or revert.

## Next actions
1. Recheck the applicable quota windows before resuming. Review docs/validation.md and preserve the current working tree.
2. Use the ignored `builds/phase2-validation/Run-PhaseTwoIntegration.ps1` harness with its syntax-checked response-file retry. Copy the latest `Assets/Scripts/Network/NetworkGameManager.cs` to the isolated Unity copy at `builds/phase1-validation/host-project` and rebuild the integrated Windows player. The final patch reinitializes reused scene NetworkVariables on repeated server spawn to rebase NGO send timing. It has not compiled or run.
3. Run the authority case first. After leaving and rehosting, verify the host and both clients converge on the new match/action stamp and the active client can submit a valid die. Before the patch, the host was at 1/1 while a remote stayed at 0/0 despite receiving phase and private role.
4. Run the session, departure and spectator cases. Record actual outcomes in docs/validation.md before checking remaining items. Validate Unity Services linking and Relay separately before release.

## Pending and blockers
The roster, authority and integrated Windows builds passed before the final rehost patch, with the existing Unity Services link warning. The authority run verified owner-only role and real first-turn dice plus nonhost start rejection, then reproduced the stale action stamp. The session harness aborted on a Windows mailbox file-sharing violation before completing assertions. The launcher closed its players. No further builds or tests started after the reserve was reached. Full authority, session, departure and spectator checks remain open. Relay and cross-machine play were outside the localhost run.
