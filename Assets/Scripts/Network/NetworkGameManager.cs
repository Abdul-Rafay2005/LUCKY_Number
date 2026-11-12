using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using LuckyNumber.Core;
using LuckyNumber.Gameplay;

namespace LuckyNumber.Network
{
    /// <summary>
    /// Authoritative game manager controlling game state, turn flow,
    /// validations, imposter abilities, voting, and win conditions.
    /// </summary>
    public class NetworkGameManager : NetworkBehaviour
    {
        public static NetworkGameManager Instance { get; private set; }

        [Header("Settings")]
        [SerializeField] private GameSettings settings;

        [Header("Public Authoritative Network Variables")]
        public NetworkVariable<int> TargetLuckyNumber = new NetworkVariable<int>(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public NetworkVariable<int> CurrentScore = new NetworkVariable<int>(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public NetworkVariable<int> RemainingMoves = new NetworkVariable<int>(
            15, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public NetworkVariable<int> CurrentRound = new NetworkVariable<int>(
            1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public NetworkVariable<ulong> ActivePlayerClientId = new NetworkVariable<ulong>(
            ulong.MaxValue, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public NetworkVariable<GamePhase> CurrentGamePhase = new NetworkVariable<GamePhase>(
            GamePhase.Lobby, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public NetworkVariable<float> PhaseTimer = new NetworkVariable<float>(
            0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public NetworkVariable<Team> WinningTeam = new NetworkVariable<Team>(
            Team.None, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public NetworkVariable<WinReason> MatchWinReason = new NetworkVariable<WinReason>(
            WinReason.None, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public NetworkVariable<FixedString128Bytes> LastCommittedMove = new NetworkVariable<FixedString128Bytes>(
            string.Empty, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public NetworkVariable<FixedString64Bytes> LastEliminatedPlayerName = new NetworkVariable<FixedString64Bytes>(
            string.Empty, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public NetworkVariable<PlayerRole> LastEliminatedRole = new NetworkVariable<PlayerRole>(
            PlayerRole.Normal, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public NetworkVariable<VoteOutcomeSnapshot> LastVoteOutcome = new NetworkVariable<VoteOutcomeSnapshot>(
            default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public readonly NetworkVariable<ActionStamp> ActionContext = new NetworkVariable<ActionStamp>();

        private sealed class MatchPlayer
        {
            public PlayerSecret Secret;
            public bool Alive = true;
            public bool BlockPending;
            public bool CursePending;
        }

        private readonly List<ulong> turnOrder = new List<ulong>();
        private int nextTurnIndex;
        private PrivateRoll activeRoll;
        private bool voteOpen;
        private bool spawnedBefore;
        private int teamShiftCharges;
        private bool shiftGraceUsedThisTurn;
        private uint nextPublicEventSequence;
        private const int PublicHistoryLimit = 64;
        private readonly NetworkList<PublicGameEvent> publicHistory = new NetworkList<PublicGameEvent>(
            null, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkList<ulong> votingCandidateIds = new NetworkList<ulong>(
            null, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly VoteRunoffTracker runoffTracker = new VoteRunoffTracker();
        private int runoffBallotsPlayed;
        private readonly Dictionary<ulong, MatchPlayer> serverPlayers = new Dictionary<ulong, MatchPlayer>();
        private readonly Dictionary<ulong, ulong> serverVotes = new Dictionary<ulong, ulong>();
        private readonly HashSet<ulong> serverAbstainVotes = new HashSet<ulong>();
        private readonly HashSet<ulong> serverSkipVotes = new HashSet<ulong>();

        public GameSettings Settings => settings;
        public NetworkList<PublicGameEvent> PublicHistory => publicHistory;
        public NetworkList<ulong> VotingCandidateIds => votingCandidateIds;
        public int RunoffBallotsPlayed => LastVoteOutcome.Value.RunoffBallot;

        public event Action<GamePhase> OnPhaseChanged;
        public event Action OnScoreUpdated;
        public event Action OnPublicHistoryChanged;
        public event Action<ActionStamp, MatchAction, bool, string> OnActionFeedback;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<GameSettings>();
            }
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (IsServer)
            {
                if (spawnedBefore) ReinitializeSceneVariables();
                ActionContext.Value = new ActionStamp { SessionId = BitConverter.ToUInt64(Guid.NewGuid().ToByteArray(), 0) };
                ResetMatch();
                CurrentGamePhase.Value = GamePhase.Lobby;
                NetworkManager.OnClientDisconnectCallback += HandleServerClientDisconnect;
            }
            spawnedBefore = true;

            CurrentGamePhase.OnValueChanged += NotifyPhaseChanged;
            CurrentScore.OnValueChanged += NotifyScoreChanged;
            TargetLuckyNumber.OnValueChanged += NotifyScoreChanged;
            RemainingMoves.OnValueChanged += NotifyScoreChanged;
            CurrentRound.OnValueChanged += NotifyScoreChanged;
            publicHistory.OnListChanged += NotifyPublicHistoryChanged;
        }

        private void ReinitializeSceneVariables()
        {
            // NGO retains send timestamps on reused scene objects, but each session has a new network clock.
            var variables = new NetworkVariableBase[]
            {
                ActionContext, TargetLuckyNumber, CurrentScore, RemainingMoves, CurrentRound, ActivePlayerClientId,
                CurrentGamePhase, PhaseTimer, WinningTeam, MatchWinReason, LastCommittedMove,
                LastEliminatedPlayerName, LastEliminatedRole, LastVoteOutcome, publicHistory, votingCandidateIds
            };
            foreach (var variable in variables)
                variable.Initialize(this);
        }

        public override void OnNetworkDespawn()
        {
            if (NetworkManager != null)
                NetworkManager.OnClientDisconnectCallback -= HandleServerClientDisconnect;
            CurrentGamePhase.OnValueChanged -= NotifyPhaseChanged;
            CurrentScore.OnValueChanged -= NotifyScoreChanged;
            TargetLuckyNumber.OnValueChanged -= NotifyScoreChanged;
            RemainingMoves.OnValueChanged -= NotifyScoreChanged;
            CurrentRound.OnValueChanged -= NotifyScoreChanged;
            publicHistory.OnListChanged -= NotifyPublicHistoryChanged;
            base.OnNetworkDespawn();
        }

        private void NotifyPhaseChanged(GamePhase previous, GamePhase next) => OnPhaseChanged?.Invoke(next);
        private void NotifyScoreChanged(int previous, int next) => OnScoreUpdated?.Invoke();
        private void NotifyPublicHistoryChanged(NetworkListEvent<PublicGameEvent> changeEvent) => OnPublicHistoryChanged?.Invoke();

        public override void OnDestroy()
        {
            if (Instance == this) Instance = null;
            base.OnDestroy();
        }

        private void Update()
        {
            if (!IsSpawned || !IsServer || CurrentGamePhase.Value == GamePhase.MatchEnded) return;

            if (PhaseTimer.Value > 0f)
            {
                PhaseTimer.Value -= Time.deltaTime;
                if (PhaseTimer.Value <= 0f)
                {
                    PhaseTimer.Value = 0f;
                    HandlePhaseTimerExpired();
                }
            }
        }

        #region Match Initialization (Server Only)

        [ServerRpc(RequireOwnership = false)]
        public void RequestStartMatchServerRpc(ActionStamp stamp, ServerRpcParams rpcParams = default)
        {
            TryStartMatch(rpcParams.Receive.SenderClientId, stamp, GamePhase.Lobby, MatchAction.Start);
        }

        [ServerRpc(RequireOwnership = false)]
        public void RequestRematchServerRpc(ActionStamp stamp, ServerRpcParams rpcParams = default)
        {
            TryStartMatch(rpcParams.Receive.SenderClientId, stamp, GamePhase.MatchEnded, MatchAction.Rematch);
        }

        private void ResetMatch()
        {
            WinningTeam.Value = Team.None;
            MatchWinReason.Value = WinReason.None;
            CurrentScore.Value = 0;
            RemainingMoves.Value = settings.startingMoves;
            CurrentRound.Value = 1;
            TargetLuckyNumber.Value = 0;
            PhaseTimer.Value = 0;
            ActivePlayerClientId.Value = ulong.MaxValue;
            LastEliminatedPlayerName.Value = string.Empty;
            LastEliminatedRole.Value = PlayerRole.Normal;
            LastVoteOutcome.Value = default;
            LastCommittedMove.Value = string.Empty;
            publicHistory.Clear();
            nextPublicEventSequence = 0;
            votingCandidateIds.Clear();
            runoffBallotsPlayed = 0;
            runoffTracker.Reset();
            activeRoll = default;
            voteOpen = false;
            serverPlayers.Clear();
            serverVotes.Clear();
            serverAbstainVotes.Clear();
            serverSkipVotes.Clear();
            teamShiftCharges = 0;
            shiftGraceUsedThisTurn = false;
            turnOrder.Clear();
            nextTurnIndex = 0;
        }

        private void TryStartMatch(ulong sender, ActionStamp stamp, GamePhase requiredPhase, MatchAction action)
        {
            if (!IsServer) return;
            if (sender != Unity.Netcode.NetworkManager.ServerClientId || !IsHost)
            {
                SendFeedback(sender, stamp, action, false, "Only the host can start a match.");
                return;
            }
            if (CurrentGamePhase.Value != requiredPhase || !stamp.Equals(ActionContext.Value))
            {
                SendFeedback(sender, stamp, action, false, "This match cannot be started now.");
                return;
            }
            var clients = NetworkManager.ConnectedClientsIds;
            if (clients.Count < settings.minPlayers || clients.Count > settings.maxPlayers)
            {
                SendFeedback(sender, stamp, action, false, $"A match needs {settings.minPlayers}–{settings.maxPlayers} players.");
                return;
            }
            if (clients.Any(client => GetPlayer(client) == null))
            {
                SendFeedback(sender, stamp, action, false, "Wait for all players to finish joining.");
                return;
            }

            ResetMatch();
            var newStamp = ActionContext.Value;
            newStamp.MatchId++;
            newStamp.ActionId = 0;
            ActionContext.Value = newStamp;
            TargetLuckyNumber.Value = UnityEngine.Random.Range(settings.minLuckyTarget, settings.maxLuckyTarget + 1);

            turnOrder.AddRange(clients.OrderBy(_ => UnityEngine.Random.value));
            var imposters = clients.OrderBy(_ => UnityEngine.Random.value)
                .Take(settings.GetImposterCount(clients.Count)).ToList();
            teamShiftCharges = settings.luckyShiftUsesPerMatch;
            int personalSkips = settings.personalSkipPolicy == PersonalSkipPolicy.OnePerFourStartingPlayersRoundedUp
                ? Mathf.CeilToInt(clients.Count / 4f)
                : settings.skipsPerPlayer;
            foreach (ulong client in turnOrder)
            {
                bool isImposter = imposters.Contains(client);
                var secret = new PlayerSecret
                {
                    HasRole = true,
                    MatchId = newStamp.MatchId,
                    Role = isImposter ? PlayerRole.Imposter : PlayerRole.Normal,
                    BlockCharges = isImposter ? settings.forcedBlockUsesPerMatch : 0,
                    ShiftCharges = isImposter ? settings.luckyShiftUsesPerMatch : 0,
                    CurseCharges = isImposter ? settings.jamCursedDiceUsesPerMatch : 0
                };
                if (isImposter)
                {
                    foreach (ulong teammate in imposters)
                    {
                        if (teammate != client) secret.KnownImposters.Add(teammate);
                    }
                }
                serverPlayers.Add(client, new MatchPlayer { Secret = secret });
                var player = GetPlayer(client);
                player.IsAlive.Value = true;
                player.LastScheduledRound.Value = 0;
                player.RemainingSkips.Value = personalSkips;
                player.HasVoted.Value = false;
                player.SetSecret(secret);
                player.SetPrivateRoll(default);
            }

            PhaseTimer.Value = settings.roleRevealDurationSeconds;
            LastCommittedMove.Value = $"Match started! Target Lucky Number is {TargetLuckyNumber.Value}.";
            CurrentGamePhase.Value = GamePhase.RoleReveal;
            SendFeedback(sender, stamp, action, true, "Match started.");
        }

        #endregion

        #region Turn Management (Server Only)

        private void StartNextTurn()
        {
            if (!IsServer || CurrentGamePhase.Value == GamePhase.MatchEnded) return;
            while (nextTurnIndex < turnOrder.Count)
            {
                ulong activeId = turnOrder[nextTurnIndex++];
                if (!IsPlayerAlive(activeId)) continue;

                var state = serverPlayers[activeId];
                var player = GetPlayer(activeId);
                player.LastScheduledRound.Value = CurrentRound.Value;
                if (state.BlockPending)
                {
                    state.BlockPending = false;
                    RecordSkip(player);
                    continue;
                }

                bool cursed = state.CursePending;
                state.CursePending = false;
                int[] dice = DiceRollGenerator.RollThreeDice(cursed, CurrentScore.Value, TargetLuckyNumber.Value, settings.cursedBiasStrength);
                activeRoll = new PrivateRoll
                {
                    Stamp = AdvanceActionContext(),
                    Die1 = dice[0],
                    Die2 = dice[1],
                    Die3 = dice[2],
                    IsCursed = cursed
                };
                shiftGraceUsedThisTurn = false;
                ActivePlayerClientId.Value = activeId;
                player.SetPrivateRoll(activeRoll);
                PhaseTimer.Value = settings.turnDurationSeconds;
                CurrentGamePhase.Value = GamePhase.PlayerTurn;
                return;
            }
            StartDiscussionPhase();
        }

        private void ClearActiveTurn()
        {
            activeRoll = default;
            GetPlayer(ActivePlayerClientId.Value)?.SetPrivateRoll(default);
            ActivePlayerClientId.Value = ulong.MaxValue;
        }

        private void RecordSkip(LuckyNetworkPlayer player)
        {
            LastCommittedMove.Value = $"{player.PlayerName.Value} skipped their turn.";
            RecordPublicEvent(PublicEventKind.Skip, player.OwnerClientId, 0);
        }

        #endregion

        #region Move Submissions & Skips (Authoritative ServerRpc)

        [ServerRpc(RequireOwnership = false)]
        public void SubmitMoveServerRpc(int dieValue, bool isAddition, ActionStamp stamp, ServerRpcParams rpcParams = default)
        {
            ulong senderId = rpcParams.Receive.SenderClientId;
            if (!ValidateTurnAction(senderId, stamp, MatchAction.Move)) return;
            if (dieValue != activeRoll.Die1 && dieValue != activeRoll.Die2 && dieValue != activeRoll.Die3)
            {
                SendFeedback(senderId, stamp, MatchAction.Move, false, "Choose a die from the current roll.");
                return;
            }

            if (RemainingMoves.Value <= 0)
            {
                SendFeedback(senderId, stamp, MatchAction.Move, false, "No shared moves remain.");
                return;
            }

            ResolveNumberMove(senderId, dieValue, isAddition, false);
            SendFeedback(senderId, stamp, MatchAction.Move, true, "Move accepted.");
        }

        [ServerRpc(RequireOwnership = false)]
        public void RequestVoluntarySkipServerRpc(ActionStamp stamp, ServerRpcParams rpcParams = default)
        {
            ulong senderId = rpcParams.Receive.SenderClientId;
            if (!ValidateTurnAction(senderId, stamp, MatchAction.Skip)) return;
            var player = GetPlayer(senderId);
            if (player.RemainingSkips.Value <= 0)
            {
                SendFeedback(senderId, stamp, MatchAction.Skip, false, "No personal skips remain.");
                return;
            }

            ClearActiveTurn();
            player.RemainingSkips.Value--;
            RecordSkip(player);
            SendFeedback(senderId, stamp, MatchAction.Skip, true, "Skip accepted.");
            StartNextTurn();
        }

        private bool ValidateTurnAction(ulong sender, ActionStamp stamp, MatchAction action)
        {
            if (!ValidateAction(sender, stamp, action, GamePhase.PlayerTurn)) return false;
            if (activeRoll.HasDice && ActivePlayerClientId.Value == sender) return true;
            SendFeedback(sender, stamp, action, false, "Wait for your current roll.");
            return false;
        }

        private void ResolveNumberMove(ulong senderId, int dieValue, bool isAddition, bool timedOut)
        {
            ClearActiveTurn();
            var resolution = MoveRules.ResolveMove(CurrentScore.Value, RemainingMoves.Value,
                TargetLuckyNumber.Value, dieValue, isAddition);
            CurrentScore.Value = resolution.Score;
            RemainingMoves.Value = resolution.RemainingMoves;
            var player = GetPlayer(senderId);
            string sign = isAddition ? "+" : "-";
            LastCommittedMove.Value = timedOut
                ? $"{player.PlayerName.Value} timed out and played {sign}{dieValue} (Score: {CurrentScore.Value})"
                : $"{player.PlayerName.Value} played {sign}{dieValue} (Score: {CurrentScore.Value})";
            RecordPublicEvent(PublicEventKind.NumberMove, senderId, isAddition ? dieValue : -dieValue);

            switch (resolution.WinReason)
            {
                case WinReason.TargetReached:
                    EndMatch(Team.NormalTeam, WinReason.TargetReached);
                    break;
                case WinReason.OutOfMoves:
                    EndMatch(Team.ImposterTeam, WinReason.OutOfMoves);
                    break;
                default:
                    StartNextTurn();
                    break;
            }
        }

        private void ResolveTimedOutTurn()
        {
            if (!activeRoll.HasDice || !IsPlayerAlive(ActivePlayerClientId.Value))
            {
                ClearActiveTurn();
                StartNextTurn();
                return;
            }

            if (settings.turnTimeoutPolicy == TurnTimeoutPolicy.SkipTurn)
            {
                RecordSkip(GetPlayer(ActivePlayerClientId.Value));
                ClearActiveTurn();
                StartNextTurn();
                return;
            }

            int dieIndex = UnityEngine.Random.Range(0, 3);
            int die = MoveRules.SelectTimeoutDie(activeRoll.Die1, activeRoll.Die2, activeRoll.Die3, dieIndex);
            ResolveNumberMove(ActivePlayerClientId.Value, die, UnityEngine.Random.value < 0.5f, true);
        }

        #endregion

        #region Imposter Abilities (Authoritative ServerRpc)

        [ServerRpc(RequireOwnership = false)]
        public void ActivateForcedBlockServerRpc(ulong targetClientId, ActionStamp stamp, ServerRpcParams rpcParams = default)
        {
            ScheduleEffect(rpcParams.Receive.SenderClientId, targetClientId, stamp, MatchAction.ForcedBlock);
        }

        [ServerRpc(RequireOwnership = false)]
        public void ActivateLuckyShiftServerRpc(int delta, ActionStamp stamp, ServerRpcParams rpcParams = default)
        {
            ulong senderId = rpcParams.Receive.SenderClientId;
            const MatchAction action = MatchAction.LuckyShift;
            if (!ValidateAbility(senderId, stamp, action, out var imposter)) return;
            long target = (long)TargetLuckyNumber.Value + delta;
            if (delta == 0 || delta < settings.minShiftDelta || delta > settings.maxShiftDelta ||
                target < int.MinValue || target > int.MaxValue || target == CurrentScore.Value)
            {
                SendFeedback(senderId, stamp, action, false, "Choose a nonzero shift that keeps the target different from the score.");
                return;
            }
            if (!MatchActionRules.TrySpendCharge(ref teamShiftCharges))
            {
                SendFeedback(senderId, stamp, action, false, "The imposter team has no Lucky Shift charges remaining.");
                return;
            }
            foreach (var playerState in serverPlayers)
            {
                if (playerState.Value.Secret.Role != PlayerRole.Imposter) continue;
                var secret = playerState.Value.Secret;
                secret.ShiftCharges = teamShiftCharges;
                playerState.Value.Secret = secret;
                GetPlayer(playerState.Key)?.SetSecret(secret);
            }
            SpendAbility(senderId, imposter);
            TargetLuckyNumber.Value = (int)target;
            LastCommittedMove.Value = $"[LUCKY SHIFT] Target altered by {delta:+#;-#;0}! New Target: {TargetLuckyNumber.Value}";
            RecordPublicEvent(PublicEventKind.TargetShift, senderId, delta);
            if (!shiftGraceUsedThisTurn)
            {
                PhaseTimer.Value = Mathf.Max(PhaseTimer.Value, settings.shiftReconsiderationSeconds);
                shiftGraceUsedThisTurn = true;
            }
            SendFeedback(senderId, stamp, action, true, $"Target shifted by {delta}.");
        }

        [ServerRpc(RequireOwnership = false)]
        public void ActivateCursedDiceServerRpc(ulong targetClientId, ActionStamp stamp, ServerRpcParams rpcParams = default)
        {
            ScheduleEffect(rpcParams.Receive.SenderClientId, targetClientId, stamp, MatchAction.CursedDice);
        }

        private void ScheduleEffect(ulong sender, ulong targetId, ActionStamp stamp, MatchAction action)
        {
            if (!ValidateAbility(sender, stamp, action, out var imposter)) return;
            if (!IsPlayerAlive(targetId))
            {
                SendFeedback(sender, stamp, action, false, "Choose a living player.");
                return;
            }
            var target = serverPlayers[targetId];
            int targetTurnIndex = turnOrder.IndexOf(targetId);
            if (targetTurnIndex < nextTurnIndex)
            {
                SendFeedback(sender, stamp, action, false, "Choose a player whose turn has not started this round.");
                return;
            }
            bool isBlock = action == MatchAction.ForcedBlock;
            if (isBlock ? target.BlockPending : target.CursePending)
            {
                SendFeedback(sender, stamp, action, false, "That effect is already pending for this player.");
                return;
            }

            if (isBlock)
            {
                if (!MatchActionRules.TrySpendCharge(ref imposter.Secret.BlockCharges))
                {
                    SendFeedback(sender, stamp, action, false, "No Forced Block charges remain.");
                    return;
                }
                target.BlockPending = true;
            }
            else
            {
                if (!MatchActionRules.TrySpendCharge(ref imposter.Secret.CurseCharges))
                {
                    SendFeedback(sender, stamp, action, false, "No Cursed Dice charges remain.");
                    return;
                }
                target.CursePending = true;
            }
            SpendAbility(sender, imposter);
            SendFeedback(sender, stamp, action, true, $"Effect queued for {GetPlayer(targetId).PlayerName.Value}'s next unrolled turn.");
        }

        private bool ValidateAbility(ulong sender, ActionStamp stamp, MatchAction action, out MatchPlayer imposter)
        {
            imposter = null;
            if (!ValidateAction(sender, stamp, action, GamePhase.PlayerTurn)) return false;
            imposter = serverPlayers[sender];
            if (!activeRoll.HasDice || imposter.Secret.Role != PlayerRole.Imposter)
            {
                SendFeedback(sender, stamp, action, false, "Only living imposters can use abilities during a dice turn.");
                return false;
            }
            int usesThisRound = imposter.Secret.LastAbilityRound == CurrentRound.Value
                ? imposter.Secret.AbilityUsesThisRound
                : 0;
            if (usesThisRound >= settings.abilityActivationsPerImposterPerRound)
            {
                SendFeedback(sender, stamp, action, false, "You already used an ability this round.");
                return false;
            }
            int charges = action switch
            {
                MatchAction.ForcedBlock => imposter.Secret.BlockCharges,
                MatchAction.LuckyShift => teamShiftCharges,
                _ => imposter.Secret.CurseCharges
            };
            if (charges > 0) return true;
            SendFeedback(sender, stamp, action, false, "No charges remain for that ability.");
            return false;
        }

        private void SpendAbility(ulong sender, MatchPlayer imposter)
        {
            if (imposter.Secret.LastAbilityRound != CurrentRound.Value)
                imposter.Secret.AbilityUsesThisRound = 0;
            imposter.Secret.LastAbilityRound = CurrentRound.Value;
            imposter.Secret.AbilityUsesThisRound++;
            GetPlayer(sender).SetSecret(imposter.Secret);
        }

        private void RecordPublicEvent(PublicEventKind kind, ulong actorId, int signedValue)
        {
            if (!IsServer) return;
            // Anonymous sabotage must not identify its caster in the payload either.
            bool anonymous = kind == PublicEventKind.TargetShift;
            var actor = anonymous ? null : GetPlayer(actorId);
            var entry = new PublicGameEvent
            {
                Sequence = nextPublicEventSequence++,
                Round = CurrentRound.Value,
                ActorClientId = anonymous ? ulong.MaxValue : actorId,
                ActorName = actor != null ? actor.PlayerName.Value : default,
                Kind = kind,
                SignedValue = signedValue,
                ResultingScore = CurrentScore.Value,
                ResultingTarget = TargetLuckyNumber.Value,
                RemainingMoves = RemainingMoves.Value
            };
            if (publicHistory.Count >= PublicHistoryLimit) publicHistory.RemoveAt(0);
            publicHistory.Add(entry);
        }

        #endregion

        #region Discussion & Voting (Server Only)

        private void StartDiscussionPhase()
        {
            ClearActiveTurn();
            AdvanceActionContext();
            PhaseTimer.Value = settings.discussionDurationSeconds;
            CurrentGamePhase.Value = GamePhase.Discussion;
        }

        private void StartVotingPhase()
        {
            AdvanceActionContext();
            serverVotes.Clear();
            serverAbstainVotes.Clear();
            serverSkipVotes.Clear();
            votingCandidateIds.Clear();
            foreach (var candidate in serverPlayers.Where(pair => pair.Value.Alive).Select(pair => pair.Key).OrderBy(id => id))
                votingCandidateIds.Add(candidate);
            runoffBallotsPlayed = 0;
            runoffTracker.Reset();
            foreach (var p in GetAllLivingPlayers())
            {
                p.HasVoted.Value = false;
            }
            voteOpen = true;
            SetVoteOutcome(VoteOutcomeKind.None, 0, string.Empty);
            PhaseTimer.Value = settings.votingDurationSeconds;
            LastCommittedMove.Value = "Voting phase started! Cast your vote.";
            CurrentGamePhase.Value = GamePhase.Voting;
        }

        [ServerRpc(RequireOwnership = false)]
        public void SubmitVoteServerRpc(ulong suspectClientId, ActionStamp stamp, ServerRpcParams rpcParams = default)
        {
            SubmitBallot(rpcParams.Receive.SenderClientId, suspectClientId, stamp);
        }

        [ServerRpc(RequireOwnership = false)]
        public void SkipVoteServerRpc(ActionStamp stamp, ServerRpcParams rpcParams = default)
        {
            SubmitBallot(rpcParams.Receive.SenderClientId, null, stamp);
        }

        private void SubmitBallot(ulong voter, ulong? suspect, ActionStamp stamp)
        {
            const MatchAction action = MatchAction.Vote;
            if (!ValidateAction(voter, stamp, action, GamePhase.Voting)) return;
            if (!voteOpen || serverVotes.ContainsKey(voter) || serverSkipVotes.Contains(voter) || serverAbstainVotes.Contains(voter))
            {
                SendFeedback(voter, stamp, action, false, "This ballot is closed or you already voted.");
                return;
            }
            if (suspect.HasValue && (!IsPlayerAlive(suspect.Value) || !votingCandidateIds.Contains(suspect.Value)))
            {
                SendFeedback(voter, stamp, action, false, "Choose an eligible living candidate.");
                return;
            }

            if (suspect.HasValue)
                serverVotes.Add(voter, suspect.Value);
            else
                serverSkipVotes.Add(voter);

            GetPlayer(voter).HasVoted.Value = true;
            SendFeedback(voter, stamp, action, true, "Ballot accepted.");
            CheckAllVotesSubmitted();
        }

        private void CheckAllVotesSubmitted()
        {
            int livingCount = serverPlayers.Values.Count(player => player.Alive);
            int totalVotes = serverVotes.Count + serverSkipVotes.Count + serverAbstainVotes.Count;

            if (totalVotes >= livingCount)
            {
                ResolveVoting();
            }
        }

        private void ResolveVoting()
        {
            if (!IsServer || !voteOpen || CurrentGamePhase.Value != GamePhase.Voting) return;
            voteOpen = false;
            LastEliminatedPlayerName.Value = string.Empty;
            LastEliminatedRole.Value = PlayerRole.Normal;

            var livingVotersAndCandidates = serverPlayers.Where(pair => pair.Value.Alive)
                .Select(pair => pair.Key).ToList();
            var result = VoteRules.Tally(serverVotes, serverSkipVotes,
                livingVotersAndCandidates, livingVotersAndCandidates);
            if (result.Kind != VoteResolutionKind.TiedCandidates)
                runoffTracker.Reset();
            switch (result.Kind)
            {
                case VoteResolutionKind.NoVotes:
                    LastCommittedMove.Value = "No votes cast. No one was eliminated.";
                    SetVoteOutcome(VoteOutcomeKind.NoVotes, 0, LastCommittedMove.Value.ToString());
                    break;
                case VoteResolutionKind.SkipEjection:
                    LastCommittedMove.Value = "Skip Ejection won or tied the vote. No one was eliminated.";
                    SetVoteOutcome(VoteOutcomeKind.SkipEjection, 0, LastCommittedMove.Value.ToString());
                    break;
                case VoteResolutionKind.TiedCandidates:
                    var followup = runoffTracker.RegisterTie(result.LeadingCandidates, runoffBallotsPlayed);
                    if (followup == TieFollowup.RandomEjection)
                    {
                        ulong selected = result.LeadingCandidates[UnityEngine.Random.Range(0, result.LeadingCandidates.Count)];
                        Eliminate(selected);
                        LastCommittedMove.Value = $"{LastEliminatedPlayerName.Value} ejected by tie-break. Role: {LastEliminatedRole.Value}.";
                        SetVoteOutcome(VoteOutcomeKind.RandomEjection, selected, LastCommittedMove.Value.ToString());
                    }
                    else if (followup == TieFollowup.StartRunoff)
                    {
                        BeginRunoff(result.LeadingCandidates);
                        return;
                    }
                    else
                    {
                        LastCommittedMove.Value = $"Tie vote ({result.VotesAtLead} votes each) after the final runoff. No one was eliminated.";
                        SetVoteOutcome(VoteOutcomeKind.Tie, 0, LastCommittedMove.Value.ToString());
                    }
                    break;
                case VoteResolutionKind.Eliminate:
                    Eliminate(result.LeadingCandidates[0]);
                    SetVoteOutcome(VoteOutcomeKind.Ejection, result.LeadingCandidates[0], LastCommittedMove.Value.ToString());
                    break;
            }

            if (CheckRoleWins(showElimination: true)) return;
            PhaseTimer.Value = settings.eliminationDurationSeconds;
            CurrentGamePhase.Value = GamePhase.Elimination;
        }

        private void BeginRunoff(IReadOnlyList<ulong> tiedCandidates)
        {
            runoffBallotsPlayed++;
            AdvanceActionContext();
            serverVotes.Clear();
            serverSkipVotes.Clear();
            serverAbstainVotes.Clear();
            votingCandidateIds.Clear();
            foreach (ulong candidate in tiedCandidates) votingCandidateIds.Add(candidate);
            foreach (var player in GetAllLivingPlayers()) player.HasVoted.Value = false;
            voteOpen = true;
            PhaseTimer.Value = settings.votingDurationSeconds;
            LastCommittedMove.Value = $"Runoff ballot {runoffBallotsPlayed}: vote among the tied candidates.";
            SetVoteOutcome(VoteOutcomeKind.RunoffStarted, 0, LastCommittedMove.Value.ToString());
        }

        private void Eliminate(ulong clientId)
        {
            var player = GetPlayer(clientId);
            if (player == null) return;
            var state = serverPlayers[clientId];
            state.Alive = false;
            state.BlockPending = false;
            state.CursePending = false;
            player.IsAlive.Value = false;
            LastEliminatedPlayerName.Value = player.PlayerName.Value;
            LastEliminatedRole.Value = state.Secret.Role;
            LastCommittedMove.Value = $"{player.PlayerName.Value} was eliminated! They were {state.Secret.Role}.";
        }

        private void SetVoteOutcome(VoteOutcomeKind kind, ulong playerId, string message)
        {
            bool hasEjectedPlayer = kind == VoteOutcomeKind.Ejection || kind == VoteOutcomeKind.RandomEjection;
            var player = hasEjectedPlayer ? GetPlayer(playerId) : null;
            var outcome = new VoteOutcomeSnapshot
            {
                Kind = kind,
                RunoffBallot = runoffBallotsPlayed,
                Stamp = ActionContext.Value,
                PlayerId = hasEjectedPlayer ? playerId : 0,
                PlayerName = player != null ? player.PlayerName.Value : default,
                PlayerRole = hasEjectedPlayer && serverPlayers.TryGetValue(playerId, out var state)
                    ? state.Secret.Role
                    : PlayerRole.Normal,
                Message = message
            };
            LastVoteOutcome.Value = outcome;
        }

        #endregion

        #region Win Conditions & Phase Resolution

        private bool CheckRoleWins(bool showElimination = false)
        {
            int livingImposters = serverPlayers.Values.Count(player => player.Alive && player.Secret.Role == PlayerRole.Imposter);
            int livingNormals = serverPlayers.Values.Count(player => player.Alive && player.Secret.Role == PlayerRole.Normal);
            Team winner = WinRules.GetRoleCountWinner(livingImposters, livingNormals);
            if (winner == Team.NormalTeam)
            {
                EndMatch(Team.NormalTeam, WinReason.AllImpostersEliminated, showElimination);
                return true;
            }
            if (winner == Team.ImposterTeam)
            {
                EndMatch(Team.ImposterTeam, WinReason.ImposterParity, showElimination);
                return true;
            }

            return false;
        }

        private void EndMatch(Team winner, WinReason reason, bool showElimination = false)
        {
            if (WinningTeam.Value != Team.None) return;
            ClearActiveTurn();
            voteOpen = false;
            WinningTeam.Value = winner;
            MatchWinReason.Value = reason;
            PhaseTimer.Value = 0f;

            string reasonText = reason switch
            {
                WinReason.TargetReached => "Lucky Number target reached exactly!",
                WinReason.AllImpostersEliminated => "All Imposters have been eliminated!",
                WinReason.OutOfMoves => "Team ran out of moves!",
                WinReason.ImposterParity => "Imposters achieved parity with normal players!",
                _ => "Match completed."
            };

            LastCommittedMove.Value = $"Game Over! {winner} Wins — {reasonText}";
            // Freeze the outcome now, but let everyone see the final ejection first.
            PhaseTimer.Value = showElimination ? settings.eliminationDurationSeconds : 0f;
            CurrentGamePhase.Value = showElimination ? GamePhase.Elimination : GamePhase.MatchEnded;
        }

        private void HandlePhaseTimerExpired()
        {
            if (!IsServer) return;

            switch (CurrentGamePhase.Value)
            {
                case GamePhase.RoleReveal:
                    StartNextTurn();
                    break;

                case GamePhase.PlayerTurn:
                    ResolveTimedOutTurn();
                    break;

                case GamePhase.Discussion:
                    StartVotingPhase();
                    break;

                case GamePhase.Voting:
                    ResolveVoting();
                    break;

                case GamePhase.Elimination:
                    if (WinningTeam.Value != Team.None)
                    {
                        CurrentGamePhase.Value = GamePhase.MatchEnded;
                        break;
                    }
                    CurrentRound.Value++;
                    nextTurnIndex = 0;
                    StartNextTurn();
                    break;
            }
        }

        #endregion

        #region Helpers & RPCs

        private void HandleServerClientDisconnect(ulong clientId)
        {
            var phase = CurrentGamePhase.Value;
            if (!IsServer || WinningTeam.Value != Team.None || NetworkManager.ShutdownInProgress || phase == GamePhase.Lobby ||
                phase == GamePhase.MatchEnded || !serverPlayers.Remove(clientId, out var departed))
                return;

            bool ballotCancelled = serverVotes.Remove(clientId);
            ballotCancelled |= serverSkipVotes.Remove(clientId);
            ballotCancelled |= serverAbstainVotes.Remove(clientId);
            for (int i = votingCandidateIds.Count - 1; i >= 0; i--)
            {
                if (votingCandidateIds[i] == clientId)
                {
                    votingCandidateIds.RemoveAt(i);
                    ballotCancelled = true;
                }
            }
            foreach (ulong voterId in serverVotes.Where(vote => vote.Value == clientId).Select(vote => vote.Key).ToList())
            {
                serverVotes.Remove(voterId);
                ballotCancelled = true;
                var voter = GetPlayer(voterId);
                if (voter != null)
                {
                    voter.HasVoted.Value = false;
                    if (phase == GamePhase.Voting)
                        SendFeedback(voterId, ActionContext.Value, MatchAction.Vote, false, "Your candidate left. Choose another player or skip.");
                }
            }

            if (phase == GamePhase.Voting && ballotCancelled)
            {
                LastCommittedMove.Value = "A ballot or candidate was canceled because a player disconnected.";
                SetVoteOutcome(VoteOutcomeKind.DisconnectCancellation, 0, LastCommittedMove.Value.ToString());
            }

            if (departed.Alive && CheckRoleWins()) return;
            if (phase == GamePhase.PlayerTurn && ActivePlayerClientId.Value == clientId)
            {
                ClearActiveTurn();
                StartNextTurn();
            }
            else if (phase == GamePhase.Voting)
                CheckAllVotesSubmitted();
        }

        internal bool IsInCurrentMatch(ulong clientId) => serverPlayers.ContainsKey(clientId);

        public LuckyNetworkPlayer GetPlayer(ulong clientId)
        {
            if (IsServer && !NetworkManager.ConnectedClients.ContainsKey(clientId)) return null;
            return LuckyNetworkPlayer.FindSpawnedPlayer(clientId);
        }

        public List<LuckyNetworkPlayer> GetAllLivingPlayers()
        {
            return LuckyNetworkPlayer.SpawnedPlayers.Values
                .Where(player => player.IsAlive.Value && (!IsServer || NetworkManager.ConnectedClients.ContainsKey(player.OwnerClientId)))
                .ToList();
        }

        private bool IsPlayerAlive(ulong clientId)
        {
            return serverPlayers.TryGetValue(clientId, out var player) && player.Alive && GetPlayer(clientId) != null;
        }

        private ActionStamp AdvanceActionContext()
        {
            var stamp = ActionContext.Value;
            stamp.ActionId++;
            ActionContext.Value = stamp;
            return stamp;
        }

        private bool ValidateAction(ulong sender, ActionStamp stamp, MatchAction action, GamePhase phase)
        {
            if (!IsServer || !NetworkManager.ConnectedClients.ContainsKey(sender)) return false;
            if (!MatchActionRules.CanApplyInPhase(CurrentGamePhase.Value, phase) || !stamp.Equals(ActionContext.Value))
            {
                SendFeedback(sender, stamp, action, false, "That action window has ended.");
                return false;
            }
            if (IsPlayerAlive(sender)) return true;
            SendFeedback(sender, stamp, action, false, "Only living players can take this action.");
            return false;
        }

        private void SendFeedback(ulong recipient, ActionStamp stamp, MatchAction action, bool accepted, string message)
        {
            if (!NetworkManager.ConnectedClients.ContainsKey(recipient)) return;
            var rpcParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams { TargetClientIds = new[] { recipient } }
            };
            ReceiveActionFeedbackClientRpc(stamp, action, accepted, message, rpcParams);
        }

        [ClientRpc]
        private void ReceiveActionFeedbackClientRpc(ActionStamp stamp, MatchAction action, bool accepted, string message, ClientRpcParams rpcParams = default)
        {
            OnActionFeedback?.Invoke(stamp, action, accepted, message);
        }

        #endregion
    }
}

