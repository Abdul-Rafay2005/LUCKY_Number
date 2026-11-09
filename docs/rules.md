# Lucky Number jam rules

Revision 1, 2026-11-09. These are the provisional rules for implementation and playtesting. The Phase 1 baseline records the prototype's current behavior in [validation.md](validation.md). Authority, rule, and UI changes are scheduled in Phases 2–4 of [the checklist](../todo.md).

The master scene uses `DefaultGameSettings.asset`. Its four-second role reveal replaces the five-second transient fallback previously used by that scene. The asset's **Jam Rule Policies** group stores staged timeout, skip, and sabotage settings. The current prototype continues to use its existing skip and ability budget fields until the server rule changes are implemented.

## Release and entry point

Target Windows x64 desktop with mouse and keyboard. Verify 1280×720, 1920×1080, and a narrower 4:3 layout. Online rooms use Unity Lobby and Relay with a room code. The localhost path is for same-machine development checks.

`Assets/Scenes/LuckyNumberMaster.unity` is the entry scene and contains all functional panels. `MainMenu`, `Lobby`, `Gameplay`, `DiscussionAndVoting`, and `GameResults` are editor reference scenes excluded from release builds.

## 1. Lobby, roles, moves, and targets

- Start with 3–8 connected players. The host starts the match. Ready status is informational. Lock the roster on start and reject new joins until the room returns to lobby.
- Assign one imposter for 3–5 players and two for 6–8. Each living player gets one scheduled turn per round, followed by discussion and voting.
- Start at score 0 with a uniformly selected integer target from 25 through 55 and a shared pool of 15 moves. The active player privately receives three dice, then selects one face and adds or subtracts it. Every accepted number move spends one shared move. Negative scores and overshooting are legal.

## 2. Timers and skips

- Allow 30 seconds for a dice turn, 45 for discussion, 25 for each voting ballot, 4 for the role reveal, and 5 for the elimination reveal.
- On timeout, the server chooses one of the active player's three authorized dice and a sign uniformly at random and resolves it as a regular move.
- At match start, give each player one personal skip per four starting players, rounded up. A voluntary skip spends one personal skip and zero shared moves. A block spends neither budget. Both consume the scheduled turn.

## 3. Sabotage

- Each living imposter can activate one ability per round during an active player turn. Check all eligibility before spending a charge. Rejected requests leave budgets and effects intact.
- **Forced Block** has one charge per imposter. Target any living player, including the caster, for that player's next turn whose roll has yet to occur. The block consumes that turn before dice are dealt.
- **Cursed Dice** has two charges per imposter and uses the same targeting window. A target can have one pending block and one pending curse. Reapplying a pending effect of the same kind is rejected. If a block consumes a turn, the curse stays pending until the target actually rolls.
- A cursed roll has two biased dice and one fair die. Each biased die has an 85% chance to draw uniformly among the faces least helpful for reaching the current target and otherwise draws uniformly from all six faces. Rank each face by the smaller of the two distances to target after adding or subtracting it. Shuffle the three dice before delivery.
- **Lucky Shift** has one charge for the entire imposter team. Adjust the target by any integer from −5 through +5 except zero. Reject a shift that would make the target equal to the current score. Preserve the current roll and give the active player at least 10 seconds remaining to reconsider, at most once per active turn. Resolve commits and shifts in server processing order.

## 4. Information disclosure

- The owner sees their role, current dice, personal skip balance, and applicable ability budgets and feedback. Imposters privately learn teammate identities and their shared shift balance.
- The public history shows the actor, signed number move, resulting score, and moves remaining. Target changes are public. Voluntary skips and blocks share the same public skip event. A blocked player sees the ordinary skip outcome. A cursed player may see a private indicator that keeps each face readable.
- The listen-server host owns authoritative match data. Privacy acceptance checks compare ordinary client views.

## 5. Voting and runoffs

- Each living player may submit one ballot per voting window, including for themselves. The initial choices are each living candidate and Skip Ejection. A missing ballot is an abstention. The option with the most votes wins. Zero votes, a Skip Ejection win, or Skip Ejection tied for the lead all result in no ejection.
- A tie among leading suspects opens a runoff restricted to those suspects. A unique leader is ejected. Zero runoff votes result in no ejection. A changed tied set resets the identical-result count to one. Count the first tied result toward three consecutive identical tied results, then have the server choose uniformly from that set.
- Allow at most three runoff ballots after the initial ballot. If the final runoff still ties and the identical-result threshold has been met, use the random ejection rule. Other ties at the cap result in no ejection. A departed candidate or voter is removed from the active ballot. A living voter whose target departs can submit a replacement before the window closes.
- Announce the ejection outcome and reveal an ejected player's role. Ejected players stay connected as spectators and cannot submit gameplay actions or ballots.

## 6. Wins and disconnects

- After an accepted number move, check exact target equality before move exhaustion. Equality gives normal players the win, including on the last shared move. Exhaustion without equality gives imposters the win.
- After voting or an active-match player departure that changes living role counts, normal players win if no imposters remain. Otherwise imposters win if living imposters equal or outnumber living normal players. Apply role-count outcomes after resolving the vote or departure.
- Host departure ends the session. A nonhost departure removes the player and their pending roll, turn, effects, and ballot from future play. Apply the role-count check, then continue the match if neither team has won. Reject backfill or reconnect during an active match. Lobby departures only change the roster.

## 7. Communication

Use external voice for discussion. Ejected players are expected to stay silent during play and discussion. The game enforces spectator action permissions. Voice muting is managed outside the game.

## 8. Reachability

Keep adaptive dice and target reachability guardrails off for the jam. Ordinary rolls and a legal target shift can leave no path to the target within the remaining moves.

Playtesting may revise the balance values, timers, and ability grace period. Integrated chat, broader input/platform support, and adaptive dice are deferred.
