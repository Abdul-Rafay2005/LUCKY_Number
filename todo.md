# Lucky Number implementation checklist

Updated: 2026-11-11. Phase 1 is complete; Phase 2 implementation is substantially complete, with integrated verification still open. Follow plan.md for findings G01–G22 and progression.md for current status. Each numbered section implements the corresponding delivery phase, in dependency order.

## 1 Establish baseline and lock the jam rules

- [x] 1.1 Inspect git status, preserve existing settings, package-lock and font edits, and create `codex/phase-1-baseline`. See progression.md for the observed pre-existing differences. Confirm their purpose with the team before any commit or revert. Keep generated Library/Logs/build artifacts out of commits.
- [x] 1.2 Import and compile the baseline snapshot in Unity 6000.3.23f1. Batch exit code 0 with no C# warnings or errors. Manifest and lockfile retained.
- [x] 1.3 Open Assets/Scenes/LuckyNumberMaster.unity and inspect its manager, transport, game NetworkObject, player prefab, EventSystem and settings. Record the initial menu and the pre-edit host/two-client run through discussion, including observed defects.
- [x] 1.4 Select Windows x64 desktop, keyboard and mouse, 1280×720, 1920×1080 and a narrower 4:3 check. Use Lobby and Relay for release rooms and localhost for same-machine validation.
- [x] 1.5 Write docs/rules.md with the eight decision groups and provisional defaults.
- [x] 1.6 Synchronize GameSettings.cs and DefaultGameSettings.asset with adopted rules. Unity verified policy defaults, positive timers, role counts, nonnegative charges and valid shift bounds. Later phases will consume the staged policies.
- [x] 1.7 Retain master-scene navigation and document the placeholder scenes. Static reference inspection found no dependencies on them. Unity verified only the master is enabled after editing.
- [x] 1.8 Record reproducible editor and runtime checks, expected behavior and actual baseline defects in docs/validation.md. Use docs/rules.md as the provisional rules for subsequent phases.

## 2 Repair authoritative multiplayer state

Implementation items 2.3–2.6 and 2.8–2.18 are present and compiled. Item 2.7 remains open because the final rehost stamp fix has not passed the integrated authority run; item 2.19 is the outstanding Phase 2 test matrix. See progression.md and docs/validation.md.

### 2A Public roster and private state (G06–G08, G14)

- [x] 2.1 Add a client-visible roster keyed by client ID and maintained by LuckyNetworkPlayer's spawn/despawn lifecycle. All three registries cleared on tested shutdown.
- [x] 2.2 Use spawned players for client UI lookups and keep server connection checks for authority. A host and two localhost clients saw the same IDs, profiles, roster count, and three lobby cards.
- [x] 2.3 Add explicit server match-player state for roles, remaining ability charges, pending effects and per-round ability usage. Initialize every player on every match; never use owner-local display fields as the authoritative budget.
- [x] 2.4 Deliver role, private dice and budget snapshots only to the intended owner. Deliver any shared imposter-team charge updates only to imposters. Decide and implement teammate-identity visibility instead of discarding otherImposters silently.
- [x] 2.5 Remove public pending-block state. If indistinguishable skips are adopted, keep remaining personal skips private too. Audit public fields, feed entries, debugging output and UI for unintended role/effect disclosure.
- [x] 2.6 Add server acknowledgments/rejection reasons for actions and abilities. Update private counts from server state; play success feedback only after acceptance.

### 2B Validate every state mutation (G07, G10, G15, G17)

- [ ] 2.7 Define session/match/turn identifiers and a single path to validate phase, sender membership, alive status, active turn and allowed action. Consume the authorized roll once. Prevent stale requests from a previous turn/match being reused.
- [x] 2.8 Guard start/rematch with host authority, allowed phase, minimum/maximum players and adopted readiness policy. Prevent mid-match restart RPCs.
- [x] 2.9 Apply caster alive/role/phase/budget/round-cap checks to all ability RPCs; validate target and timing before spending a charge. Return actionable private rejection feedback.
- [x] 2.10 Make MatchEnded terminal. Block subsequent moves, effects, votes and timer transitions; separate explicit rematch initialization from ordinary phase updates.
- [x] 2.11 Replace anonymous subscriptions with removable handlers. Clear handlers/state on despawn and rebind when a new manager/player instance appears. Ensure UI can read current state after missing an event.

### 2C Sessions and departures (G17–G18, G22)

- [x] 2.12 Implement explicit Idle/Connecting/InRoom/Leaving/Error connection state. Disable duplicate requests; handle StartHost/StartClient return values and connection/disconnection callbacks. Show lobby only after successful connection.
- [x] 2.13 Add connection approval for capacity and late-join policy. Lock the cloud lobby on match start where appropriate; maintain server validation independently of the cloud listing.
- [x] 2.14 On partial create/join failure, remove the cloud membership or created lobby and stop networking as appropriate. Prevent overlapping initialization/heartbeat/leave operations and handle host loss with a clear return-to-menu reason.
- [x] 2.15 Fix duplicate NetworkLobbyManager instance handling so destroying a duplicate does not call LeaveRoom on the active session. Clear singleton ownership deliberately on destruction.
- [x] 2.16 For LAN support, provide an IP input and deliberate listen address rather than only 127.0.0.1; otherwise label the existing option as same-machine testing. Verify separately from Relay.
- [x] 2.17 Use a stable round schedule/cursor or remaining-turn queue. Removing a player must not skip the next living player's opportunity. Clear departed players' rolls, votes and pending effects according to docs/rules.md.
- [x] 2.18 Handle departures only in relevant phases: lobby roster update, active-turn continuation, voting re-evaluation, and defined match-end policy. Avoid applying normal match wins while still in lobby or after a result.
- [ ] 2.19 Test host plus two clients: matching rosters, targeted role/dice privacy, repeated requests, dead caster, failed connection, active/inactive departure, leave/rejoin and host loss. Exit: record actual outcomes and unresolved defects.

## 3 Align the gameplay rules

Implementation checkboxes reflect source-level completion; runtime checks called out in item text remain open unless independently verified.

### 3A Moves skips and wins (G01, G04–G05, G10, G12)

- [x] 3.1 Extract a server move-resolution helper used by both player submission and timeout. Inputs identify the active turn and a server-authorized die/sign; mutate score and spend exactly one move atomically.
- [x] 3.2 Implement timeout selection of one existing die and a server-picked sign; resolve through the shared move path, clear the roll and advance once.
- [x] 3.2a Test repeated timeout move resolutions exhaust the shared move pool and that each selected die belongs to the authorized roll.
- [x] 3.3 Compute personal skip budgets at match setup from the adopted policy; do not recompute after ejections. Voluntary skip spends only personal budget; block spends neither budget. Both consume the scheduled turn.
- [x] 3.4 Split win checks by cause: committed move checks target before exhaustion; vote resolution checks last imposter/parity; disconnect uses its explicit policy. Reject or otherwise handle shift-to-current-score as documented.
- [x] 3.5 Add an ordered public event model with sequence, round, actor, kind, selected value/sign, resulting score and remaining moves. Include only publicly permitted information and use a bounded replicated history or reliable event stream with snapshot recovery.
- [x] 3.6 Clear match history and all pending state on rematch. Retain final result long enough for every client to render it.

### 3B Abilities (G06–G11)

- [x] 3.7 Schedule block and curse for a specific eligible upcoming turn, including self-targets. Define current-player-after-roll behavior and duplicate-effect stacking. Reject invalid windows without spending charges.
- [x] 3.8 Emit the same public skipped-turn event for blocked and voluntary skips. The victim sees the ordinary skip outcome; no block-specific public event is emitted.
- [x] 3.9 Implement the team-wide Lucky Shift budget, ten legal deltas, active-roll preservation, and one-time reconsideration grace per active turn.
- [x] 3.10 Serialize shift/commit processing on the authority; if the move has already won, phase validation rejects a later shift.
- [x] 3.10a Edit Mode tests cover shift-before-move, a target-winning move before a later shift, and two imposter attempts to consume the final team charge. Live RPC timing remains in 6.4.
- [x] 3.11 Refactor DiceRollGenerator to accept a seeded random source. Cursed rolls use two biasable dice plus one fair die, shuffled before delivery; all values remain 1–6 and curse state is not published.
- [x] 3.12 Rank unfavorable faces by the best signed move, covering overshoot and near-target cases. Seeded tests check bias frequency; the system does not promise a reachable target.
- [x] 3.13 Initialize one block and two curse charges per imposter, one shared shift charge, and enforce the configured per-round cap; reset budgets on match initialization.
- [ ] 3.13a Verify those budgets and rematch resets for host and remote imposters in a multiplayer run.

### 3C Voting and reveal (G02–G03, G16, G19)

- [x] 3.14 Represent Skip Ejection as an explicit ballot choice, distinct from abstention/no response, and count it against suspect candidates.
- [x] 3.14a Test two skip ballots versus one suspect ballot.
- [x] 3.15 Extract a pure plurality tally with skip-tie/no-vote handling; server validation checks living voters, eligible candidates, and one ballot per window.
- [x] 3.16 Retain sorted tied candidates, restrict each runoff to that set, reset ballots/timer, reset repeat counts when the set changes, and choose randomly after three identical tied results. Stop after three runoff ballots.
- [x] 3.17 Publish a synchronized vote outcome snapshot before phase changes; distinguish no votes, Skip Ejection, ties/runoffs, ejections, random ejections, and disconnect cancellation. UI reads the snapshot for reveals and cancellation feedback.
- [x] 3.18 Apply ejection and role reveal once; check terminal role-count wins in the adopted order. Eliminated players become spectators and server validation rejects their influence requests.
- [x] 3.19 How to Play explains discussion timing, external voice, ejected-player silence, and that the game cannot mute external voice.
- [x] 3.20 Edit Mode tests cover no votes, all skip, unique suspect, mixed skip/suspect, ties, changed runoff sets, third repeated tie, departed voter/candidate filtering, last imposter and parity. Live disconnect/session behavior remains in 6.3–6.4.

## 4 Make the complete match operable through UI

- [x] 4.1 Make LuckyUIManager the single phase router. GameplayController no longer navigates phase panels; it manages only its private dice/ability controls.
- [ ] 4.2 Bind click callbacks once or remove exact handlers on disable. Hydrate dice, role, charges, roster, phase and history from current state whenever a panel/player binds. Clear dice after the turn and on session exit.
- [ ] 4.3 Build a private RoleReveal panel with role objective/countdown and connect it to RoleReveal phase. Test event delivery before and after panel activation.
- [ ] 4.4 Add the ability target picker with stable client-ID data, not IDs parsed from displayed names. Include self when legal. Refresh only when eligibility/profile changes, retaining valid selection.
- [ ] 4.5 Add the ten-value shift selector and preview; wire every serialized reference in LuckyNumberMaster.unity. Use GameSettings bounds rather than hardcoded UI assumptions.
- [ ] 4.6 Keep each die value readable even when cursed; use an overlay only if private curse notification is adopted. Show selected value/sign, predicted score, skip balance and disabled-state reason.
- [ ] 4.7 Lock pending move/ability/vote inputs until server response. Reflect rejection without falsely claiming success; do not let local UI state substitute for authority.
- [ ] 4.8 Replace lobby per-frame destruction/recreation with stable player-card bindings. Refresh ready/profile state on events; use configured limits, not hardcoded eight.
- [ ] 4.9 Add scrollable public history available during discussion and voting. Create candidate cards with correct names/portraits, voted status, runoff eligibility and read-only spectator state.
- [ ] 4.10 Present elimination/results from current outcome snapshots. Prevent click-through, hide illegal controls, and implement clean replay/menu transitions. Test two consecutive rematches and a new connection after leaving.
- [ ] 4.11 Add visible connection errors and real IP input if LAN remains in scope. Exit: a remote player can complete every action without Inspector access.

## 5 Prepare art and rebuild presentation

- [ ] 5.1 Create docs/asset-inventory.md mapping archive image, existing asset, intended screen, cleanup needed and usage/license attribution status. Verify source rights/credits with the team; do not infer them from generated-looking filenames.
- [ ] 5.2 Inspect existing portraits/dice/buttons/icons at actual display size. Reuse clean crops; list baked checkerboards, clipped edges, incorrect pips or illegible text needing replacement.
- [ ] 5.3 Prepare transparent PNG elements with clean alpha and padding using an appropriate image editor. Preserve originals outside runtime imports where useful. JPEG checkerboards cannot be fixed by simply enabling alpha import.
- [ ] 5.4 Slice consistent sprite bounds/pivots; set Sprite (2D and UI), appropriate filtering/compression, and nine-slice borders for scalable panels/buttons. Create separate numeric TMP labels where source artwork contains unreliable text.
- [ ] 5.5 Create reusable UI prefabs under Assets/Prefabs/UI for buttons, panels, player cards, dice controls, score header and modals. Define shared colors/type/spacing and normal/hover/pressed/disabled/selected states.
- [ ] 5.6 Rebuild menu and lobby using plan.md screen requirements. Keep the lounge background subdued behind text; use clean portrait crops and meaningful primary-action hierarchy.
- [ ] 5.7 Rebuild gameplay header, public player strip/history, private dice region and sabotage drawer. Wire scoreboard art instead of leaving scoreboardImage unused; maintain live numeric text.
- [ ] 5.8 Rebuild role, discussion, voting, ejection and result views. Preserve history during discussion, enforce modal layering, and distinguish team outcome from local victory/defeat where useful.
- [ ] 5.9 Add small local roll/selection feedback and public target-change/ejection effects. Never show private curse or ability-user effects to observers. Keep animations from delaying authoritative resolution or input unnecessarily.
- [ ] 5.10 Retain procedural audio where adequate; trigger accepted-action feedback consistently. Persist volume through PlayerPrefs and restore on launch; update the SAVE & CLOSE behavior/label accordingly.
- [ ] 5.11 Rewrite How to Play from docs/rules.md, including last-move priority, exact target, skips, abilities, voting and voice policy. Replace unsupported emoji with supplied icons or verified font glyphs.
- [ ] 5.12 Update LuckyNumberSetupEditor to preserve authored prefabs/scenes or make generation an explicit development-only operation. Ensure it cannot silently overwrite the UI. Validate all serialized references after changes.
- [ ] 5.13 Inspect every screen at 1280x720, 1920x1080 and a narrower desktop aspect; test eight players, long names, long history and errors. Check keyboard focus, readable text, non-color-only selection, no overlap, no background raycast blocking and no click-through.
- [ ] 5.14 Record screenshots and defects in docs/validation.md. Exit: all required controls remain readable and usable across supported layouts.

## 6 Validate balance and release

- [ ] 6.1 Add focused Edit Mode tests under Assets/Tests/EditMode for tally/runoffs, budget rules, timeout, target priority, dice invariants and terminal guards, with appropriate test assembly definitions.
- [ ] 6.2 Add Play Mode/integration coverage under Assets/Tests/PlayMode for spawn/despawn subscriptions, private delivery, stale RPCs, pending controls and phase/outcome rendering. Prefer assertions on observable behavior over implementation mirroring.
- [ ] 6.3 Run host plus two clients and real remote connectivity. Test six-player shared-ability behavior and eight-player capacity; distinguish real network runs from editor-only simulations.
- [ ] 6.4 Exercise every win reason, final-move target hit, overshoot/negative score, two abilities racing, late/duplicate input, dropped client in every relevant phase, failed join, host exit, replay twice and leave/rejoin.
- [ ] 6.5 Playtest with new players. Record time to connect, rules confusion, turn delays, full match length, win rates, vote usefulness and sabotage impact. Compare pacing to the brief's provisional 5–20-minute goal.
- [ ] 6.6 Tune one balance group at a time in DefaultGameSettings.asset, update docs/rules.md, and repeat affected scenarios. Do not treat one successful match as balance proof.
- [ ] 6.7 Optional only: implement feature-flagged adaptive dice and shift reachability protection after core acceptance. Define feasible distances over remaining signed moves; test zero moves, zero gap, close targets and sabotage interactions. Preserve at least one viable offered option when enabled without forcing players to choose it; document guided randomness honestly.
- [ ] 6.8 Build the chosen platform with only intended scenes. Test a clean launch outside the editor and actual room connection between machines. Record exact build version and results.
- [ ] 6.9 Prepare README with launch/join steps, controls, host requirements, external voice rules if used, known limitations, and credits. Exclude credentials and private machine paths from shared docs.
- [ ] 6.10 Review git diff, update progression.md with actual completed/partial work, unresolved defects, tests run and next actions. Ship only after plan.md's jam completion criteria pass, or explicitly document agreed cuts.

## Working discipline

After each small milestone, check account usage and update progression.md. If any applicable active quota window reaches 10% remaining, stop implementation and write the handoff. Check boxes only after implementation and the listed validation have actually completed. Failed or unrun checks remain visible; optional/deferred work is marked as such rather than checked off.
