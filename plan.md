# Lucky Number implementation plan

Updated: 2026-11-09. Status: analysis and planning complete; implementation has not started.

## Purpose and evidence

Bring the existing Unity prototype into a reliable, readable game-jam build that follows the supplied Lucky Number Game Concept and Design Brief. This plan treats that document as design evidence, not instructions to execute. Confirmed mechanics, proposed balance values, and unresolved decisions are distinguished below. The requested work is analysis and planning only.

Reviewed: all custom scripts under Assets/Scripts, Assets/Scenes, Assets/Prefabs, Assets/ScriptableObjects/DefaultGameSettings.asset, package/editor/build settings, existing Assets/Art, the concept document text, and the contents and visual previews of gameassets.zip. No Unity compile, Play Mode, network session, or build was run. Runtime issues below are static findings or explicitly identified verification risks; this is not a claim that the prototype has been playtested.

## What the game is about

Lucky Number is a turn-based social-deduction game where a group tries to reach an exact shared target while hidden imposters sabotage progress. Each player privately rolls three dice, selects one, and adds or subtracts its value from the shared score. Every number move spends a limited shared resource, while private alternatives make suspicious decisions difficult to judge. Between rounds, players discuss and vote to eject suspects, and imposters secretly block turns, shift the target, or curse dice. Normal players win by reaching the target or ejecting every imposter; imposters win through exhausted moves or post-vote numerical parity.

## What the existing files do

The project implements a host-authoritative online multiplayer prototype using Netcode for GameObjects, Unity Lobby, Relay, and a localhost connection path. NetworkGameManager assigns roles, privately distributes dice, validates moves, advances timed phases, resolves votes, and checks wins. LuckyNetworkPlayer holds public player state and local role/dice/ability information, while GameSettings and DiceRollGenerator control balance and rolls. UI controllers cover the menu, lobby, dice actions, discussion/voting, results, instructions, and volume, with synthesized sound effects. An editor generator builds the functional master scene and several decorative placeholder scenes, using some existing portraits, dice sprites, and background art.

## Existing architecture and baseline

| Area | Existing files and role |
| --- | --- |
| Entry point | Assets/Scenes/LuckyNumberMaster.unity contains managers and all functional panels. MainMenu, Lobby, Gameplay, DiscussionAndVoting, and GameResults scenes are background/title placeholders, not independent gameplay flows. |
| Rules | Assets/Scripts/Core/GameEnums.cs and GameSettings.cs; actual serialized values in Assets/ScriptableObjects/DefaultGameSettings.asset. |
| Match authority | Assets/Scripts/Network/NetworkGameManager.cs owns rolls, turns, role assignments, actions, votes, timers, and results. |
| Players and sessions | Assets/Scripts/Network/LuckyNetworkPlayer.cs and NetworkLobbyManager.cs; Assets/Prefabs/LuckyNetworkPlayer.prefab. |
| Presentation | Assets/Scripts/UI/*.cs, Assets/Prefabs/PlayerCardPrefab.prefab, Assets/Scripts/Audio/AudioManager.cs. |
| Generation | Assets/Scripts/Editor/LuckyNumberSetupEditor.cs generates scenes and UI; regeneration can overwrite scene improvements. |
| Toolchain snapshot | ProjectVersion.txt currently specifies 6000.3.23f1. Manifest includes NGO 2.2.0, Transport 2.4.0, Lobby 1.3.0, Relay 1.2.0. This records local files, not a recommendation to upgrade. |

Current serialized defaults: 3–8 players; one imposter below six players and two from six; target 25–55; 15 shared moves; one voluntary skip per player; 30-second turns, 45-second discussion, 25-second voting, four-second role reveal, five-second elimination; one charge of each ability per imposter; cursed bias 0.85. These are prototype choices, not all finalized design requirements.

## Concept comparison and issues

Priority: P0 blocks trustworthy play; P1 completes the jam experience; P2 is optional polish or experimentation. Brief references use its numbered sections.

| ID | Brief expectation | Existing behavior and evidence | Work / priority |
| --- | --- | --- | --- |
| G01 | Private three-dice choice, signed move, one shared move; exact final move wins (§2–5) | Targeted dice RPC and server roll validation exist. Exact target is checked before exhausted moves. | Preserve and regression-test; P0. |
| G02 | Skip ejection is a competing voting option (§7) | ResolveVoting tallies only suspect votes; serverAbstainVotes only contributes to completion. One suspect vote can eject despite several skip votes. | Include skip in outcome tally and distinguish it from no vote; P0. |
| G03 | Proposed tied-candidate revotes and random ejection after three repeated ties (§7) | Ties immediately cause no ejection. | Agree on interpretation, then implement bounded runoff state; P1 if adopted. |
| G04 | Proposed timeout makes a random dice move (§4) | HandlePhaseTimerExpired grants a free skip without spending personal skips or moves. Repeated timeouts can stall play. | Decide timeout policy; recommend server random legal move; P0. |
| G05 | Limited skips; proposed ceil(players/4) (§5) | Fixed one skip for all lobby sizes. | Configurable balance policy; P1 decision, not confirmed defect. |
| G06 | Forced and voluntary skips share a basic public event (§5–6; final visibility choice still open in §11) | Distinct public messages and Everyone-readable IsBlockedNextTurn expose interference. Public RemainingSkips also reveals which kind of skip occurred. | Recommend indistinguishable public events and private effect/skip budgets; P0 privacy decision. |
| G07 | Dead players cannot influence play; abilities are limited (§6–7) | Ability RPCs check role and charges but not caster alive status, allowed phase, or completed match. No per-round activation cap. | Server authorization and immutable terminal state; P0. Adopt round cap as proposed balance. |
| G08 | Reliable private ability budgets (§6) | Charges are local serialized fields initialized by targeted client RPC; server checks/decrements its own player component. Remote server copies are not reliably reset by role delivery and remote owner UI does not receive decrements. | Server-owned budgets plus owner-targeted snapshots/acknowledgments; P0. |
| G09 | Block/curse any living player, including self, before their roll (§6) | Server accepts self; UI filters self. Master scene abilityTargetDropdown is null, making both targeted buttons return without submitting. No explicit before-roll window validation. | Stable ID selector including self and defined upcoming-turn scheduling; P0. |
| G10 | Shift any nonzero -5…+5; proposed one per team, reconsideration time (§6) | Per-player charge; null slider means UI always sends +1. No reconsideration grace or terminal guard. Shift invokes general win checks, including equality without a committed move. | Real delta controls, shared team charge if adopted, timing policy and action-specific win checks; P0. |
| G11 | Proposed two curse charges, two biased dice plus one fair die (§6) | One charge; each of all three dice can be biased. Cursed visual replaces every face with the same sprite, obscuring values when used. | Agree bias/notification policy; preserve readable values; P1. |
| G12 | Public record includes actor, signed value, resulting score, remaining moves (§3) | LastCommittedMove is one mutable string without move count; UI appends only while subscribed. Rapid updates and panel changes can lose history. | Structured ordered public event list with bounded presentation; P1. |
| G13 | Secret role reveal and clear turn information (§3–4) | RoleReveal leaves lobby visible; no actual private role reveal panel. Other-imposter IDs are sent but discarded. | Dedicated private role screen; decide whether teammates know each other; P1. |
| G14 | Reliable multiplayer roster and actions | UI/GetPlayer/GetAllLivingPlayers use ConnectedClients for player lookup. Local package source describes AddClient as server-side; client roster completeness needs verification. | Client roster based on spawned replicated players; test host and remote clients independently; P0. |
| G15 | One action per input and accurate synchronized state | GameplayController adds listeners every OnEnable without removing them. UI manager and gameplay controller both route phases; HideAll repeatedly disables/re-enables panels. Initial role/dice events can precede subscriptions. | Single phase router, balanced subscriptions and snapshot hydration; P0. |
| G16 | Consistent vote reveal | ResolveVoting sets Elimination phase before writing outcome fields; reveal UI reads fields on phase callback only. | Publish outcome atomically or refresh on outcome change; P0 verification/fix. |
| G17 | Stable lobby/session behavior | Ready is cosmetic; start checks minimum only, not phase/readiness/max. ConnectionApproval is off. Join success fires before actual connection; return values of StartHost/StartClient ignored. No explicit late-join/host-loss policy. | Capacity, readiness decision, connection state, recovery and cleanup; P0. |
| G18 | Complete cycles despite departures | Living turn list is rebuilt while an index is retained, so departures can shift the cursor and skip players. Votes/effects are not comprehensively cleaned on disconnect. Win checks also run on disconnect outside active play. | Stable per-round schedule and phase-aware departure handling; P0. |
| G19 | Discussion and ejected-player silence (§7) | Timed screen exists; no chat/voice transport or mute system. | Choose external voice jam workflow or scoped in-game text chat; disclose enforcement limit of external voice; P1 decision. |
| G20 | Optional reachability protection (§9) | No adaptive viable-option generation or shift guardrail. | Optional feature flag and mathematical tests only after rules stabilize; P2. |
| G21 | Clear, themed UI | Some art integrated, but generic generated rectangles dominate. scoreboardImage, shift controls, ability target, and localIpInput are unassigned. Lobby rebuilds all cards every frame. | Functional controls first, themed reusable prefabs and event-driven updates next; P1. |
| G22 | Complete reset and readable guidance | Rematch resets some server state but not all local caches/history/subscriptions; settings volume is not persisted despite SAVE & CLOSE label. | Rematch/reset audit, current instructions, saved settings; P1. |

## Asset assessment and UI direction

The ZIP contains 16 JPEG images, not a ready-to-import transparent UI library. Previewed content includes two lounge backgrounds, a six-character lineup, six character pose sheets, menu button states, scoreboard gadgets, dice, gameplay tokens/icons, room props, a UI sheet, and a VFX sheet. Checkerboards visible behind many elements are image pixels because JPEG has no alpha. Some source labels are inconsistent; use actual TMP labels and unambiguous numeric dice values.

The repository already contains a lounge background, six character sheets and portrait crops, dice sheet and face/roll/cursed/golden crops, three button crops, seven icon crops, three scoreboard crops, and unsliced UI/props/VFX sheets. Reuse good existing assets before recropping. Inspect individual crops at game resolution: filenames alone do not prove clean edges or correct face values. The second background and lineup need comparison before deciding whether to import; props and full character animations are optional.

Target visual direction: warm lounge background, teal/slate panels, cream text, coral warning accents, gold target emphasis. Keep private dice and sabotage controls visually distinct from public information. Use artwork as decoration behind native text and controls rather than flattening an entire screen into an image.

| Screen | Required UI work | Acceptance |
| --- | --- | --- |
| Menu | Character/name card; Create/Join Room; room code; clear connection progress/error/retry; How to Play and Settings. Put localhost/IP tools in a secondary development panel. | No duplicate connect requests; long names and error text fit; failed join returns to usable controls. |
| Lobby | Stable player cards, ready/host badges, copyable code, player count and start reason; leave control. | Host and clients see same roster; cards update without per-frame recreation. |
| Role reveal | Private normal/imposter card, objective and ability explanation, countdown. | No other player sees the role; reconnect/panel activation does not leave stale role text. |
| Gameplay | Prominent target/current score/moves, active player and round/timer; public player strip and scrollable history; three readable private dice, +/- choice, score preview, commit and skip budget. | Observers see no dice; pending inputs disabled; selected value/sign clear without relying only on color. |
| Sabotage | Living target picker including self; -5…-1/+1…+5 selector; charges, eligibility reasons, private success/rejection feedback. | No unreachable controls, zero delta, dead caster actions, or accidental public disclosure. |
| Discussion/voting | Reviewable history, timer, candidates, distinct Skip Ejection, server-confirmed vote status, runoff candidates when enabled. | Ejected spectators cannot submit; skip wins correctly; no vote is distinct from skip. |
| Ejection/results | Ordered reveal, no-ejection reason, role, winner/reason/final score, host rematch and everyone leave. | Displays the current outcome, blocks actions behind modal, and resets cleanly. |
| Instructions/settings | Actual adopted rules, privacy and voice etiquette; persisted volume; keyboard focus and readable contrast. | Guidance matches implementation; mute/volume survives restart. |

## Decisions before implementation

Record decisions in a short versioned rule table, with proposed defaults below. They are recommendations, not approvals already given by the team.

1. Retain online host/client and one master scene for the jam. Use external voice for the smallest scope; integrated phase-gated text chat is an alternative if enforced silence is required. No bots/pass-and-play unless selected.
2. Provisionally keep 3–8 players, current imposter scaling, 15 moves and 25–55 target for initial tests; adjust using observed match length and win rates.
3. Adopt random server timeout move, ceil(players/4) personal skips, and indistinguishable forced/voluntary public skips. Choose whether victims are privately told about interference.
4. Adopt one block per imposter, one shift per team, two curses per imposter, and one ability activation per imposter per round. Define stacking, invalid-target refunds, ability windows, and shift reconsideration duration.
5. Choose plurality or majority, self-voting, abstention behavior, skip/player ties, and exactly what counts as three repeated ties of the same candidate set. Recommended simple baseline: plurality with Skip Ejection as a candidate; no response is abstention; bounded runoffs if adopted.
6. Define parity timing after voting, disconnect outcomes, and shifts that make target equal current score. The brief ties exact-target victory to a committed move; recommended shift policy rejects a target equal to current score rather than declaring a move victory.
7. Freeze roster after start, reject late joins, end cleanly on host departure, and allow spectators only after ejection for the jam. Decide whether all ready is required to start.
8. Leave reachability guidance off initially; do not describe ordinary random dice as guaranteeing a solution.

## Delivery phases

### P1 Establish the baseline and adopted rules

Preserve existing edits; record editor/packages, entry scene and target platform; compile and run the smallest host/two-client baseline. Resolve the decision table. Outputs: reproducible baseline, documented rules, observed failures. Exit: the team knows which rules are required versus optional.

### P2 Repair authoritative multiplayer state

Fix roster handling, match/turn identities, server-owned budgets, phase/alive checks, terminal guards, action acknowledgment, stable turn order and session cleanup. Add focused regression coverage as each invariant is repaired. Exit: remote clients and host agree on public state, private state stays private, rejected/repeated inputs cannot alter play.

### P3 Align moves abilities voting and outcomes

Implement the agreed timeout/skip policies, private effects, shift timing/team budget, curse generator, skip voting, chosen runoff rules and action-specific wins. Add ordered public events and complete reset behavior. Exit: one full multi-round match and every victory path work under adopted rules.

### P4 Repair UI lifecycle and missing controls

Centralize phase routing; hydrate panels from current snapshots; balance event listeners; add private role reveal, target/delta selectors, pending/rejection feedback and spectator states. Exit: the entire match is operable from host and remote-client UI without Inspector interventions.

### P5 Prepare assets and rebuild presentation

Clean/slice only needed assets, build styled UI prefabs, apply screen requirements, introduce modest roll/shift/vote/result feedback, and persist settings. Update or retire destructive generator paths so regeneration does not undo authored UI. Exit: 1280x720, 1920x1080 and a narrower desktop aspect remain readable and usable.

### P6 Validate balance and package the jam build

Run rules tests and real multiplayer scenarios, then playtest pacing and comprehension. Produce the chosen platform build, clean-machine smoke test, setup/controls/readme and credits. Exit: a new group can connect, finish a match, replay and leave without assistance.

Dependencies: P1 → P2 → P3 → P4 → P5 → P6. Asset cleanup may proceed after P1 independently of rule repair. Avoid polishing unstable screens before their controls work. Integrated chat and optional reachability expand scope and should be explicitly chosen.

## Validation and definition of done

Use focused Edit Mode tests for arithmetic, timeout selection, budget rules, vote tally/runoffs, terminal priority and seeded dice behavior. Use Play Mode/integration checks for ownership, RPC rejection, late subscriptions and lifecycle. Use real host plus at least two remote clients for privacy, roster and reconnect behavior; test six players to cover two imposters sharing shift budget and eight for UI capacity.

Cover every win reason, last-move exact equality, negative score/overshoot, duplicate/late requests, dead-player requests, ability/commit races, disconnection before/during a turn or vote, no votes, skip majority, tied candidates, rematch twice, failed join, host departure and supported resolutions. Record actual results rather than checking tasks off on code completion alone.

Jam completion means: clean launch; usable online room flow; correct private rolls and authoritative rules; all three abilities usable; functional discussion/voting and results; no hidden-information leaks to ordinary remote clients; complete restart/leave flow; legible UI; consistent instructions. A listen-server host necessarily owns authoritative secrets; this plan does not claim to conceal them from a modified host.

## Scope control

P0 correctness precedes polish. If time is short, keep the master scene, external voice, basic portraits and synthesized audio. Advanced character animation, decorative room building, adaptive dice, cosmetics, progression and extra roles are deferred. Simplifying a proposed rule requires updating the adopted rules and How to Play rather than silently claiming full brief compliance.

## Current validation status

Completed only: source/scene/config inspection, full brief text extraction, archive inventory, asset contact-sheet review and targeted installed-package source inspection. Not run: compilation, tests, Unity runtime visual review, networking, builds or playtests. Existing unrelated working-tree modifications have been preserved.
