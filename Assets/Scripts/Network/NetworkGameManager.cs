using System;
using System.Collections;
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

        // Server-only internal state
        private readonly List<ulong> turnOrder = new List<ulong>();
        private int currentTurnOrderIndex = 0;
        private readonly Dictionary<ulong, PlayerRole> serverRoles = new Dictionary<ulong, PlayerRole>();
        private readonly Dictionary<ulong, int[]> serverActiveDice = new Dictionary<ulong, int[]>();
        private readonly HashSet<ulong> serverCursedPlayers = new HashSet<ulong>();
        private readonly Dictionary<ulong, ulong> serverVotes = new Dictionary<ulong, ulong>(); // voterId -> targetId
        private readonly HashSet<ulong> serverAbstainVotes = new HashSet<ulong>();

        public GameSettings Settings => settings;

        public event Action<string> OnPublicLogMessage;
        public event Action<GamePhase> OnPhaseChanged;
        public event Action OnScoreUpdated;

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

            CurrentGamePhase.OnValueChanged += (prev, next) => OnPhaseChanged?.Invoke(next);
            CurrentScore.OnValueChanged += (prev, next) => OnScoreUpdated?.Invoke();
            TargetLuckyNumber.OnValueChanged += (prev, next) => OnScoreUpdated?.Invoke();

            if (IsServer)
            {
                NetworkManager.Singleton.OnClientDisconnectCallback += HandleServerClientDisconnect;
            }
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer && NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientDisconnectCallback -= HandleServerClientDisconnect;
            }
            base.OnNetworkDespawn();
        }

        private void Update()
        {
            if (!IsServer) return;

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
        public void RequestStartMatchServerRpc(ServerRpcParams rpcParams = default)
        {
            if (!IsServer) return;
            if (rpcParams.Receive.SenderClientId != NetworkManager.ServerClientId)
            {
                Debug.LogWarning("[NetworkGameManager] Only host can start match.");
                return;
            }

            var connectedClients = NetworkManager.Singleton.ConnectedClientsIds;
            if (connectedClients.Count < settings.minPlayers)
            {
                Debug.LogWarning($"[NetworkGameManager] Need at least {settings.minPlayers} players to start.");
                return;
            }

            StartMatch();
        }

        public void StartMatch()
        {
            if (!IsServer) return;

            WinningTeam.Value = Team.None;
            MatchWinReason.Value = WinReason.None;
            CurrentScore.Value = 0;
            RemainingMoves.Value = settings.startingMoves;
            CurrentRound.Value = 1;
            TargetLuckyNumber.Value = UnityEngine.Random.Range(settings.minLuckyTarget, settings.maxLuckyTarget + 1);

            serverRoles.Clear();
            serverCursedPlayers.Clear();
            serverVotes.Clear();
            serverAbstainVotes.Clear();
            serverActiveDice.Clear();

            // Assign roles
            var clients = NetworkManager.Singleton.ConnectedClientsIds.ToList();
            int imposterCount = settings.GetImposterCount(clients.Count);

            // Shuffle clients
            var shuffled = clients.OrderBy(x => UnityEngine.Random.value).ToList();
            var imposters = shuffled.Take(imposterCount).ToList();
            var normals = shuffled.Skip(imposterCount).ToList();

            foreach (var imp in imposters) serverRoles[imp] = PlayerRole.Imposter;
            foreach (var norm in normals) serverRoles[norm] = PlayerRole.Normal;

            // Initialize player states
            foreach (var client in clients)
            {
                var netPlayer = GetPlayer(client);
                if (netPlayer != null)
                {
                    netPlayer.IsAlive.Value = true;
                    netPlayer.RemainingSkips.Value = settings.skipsPerPlayer;
                    netPlayer.IsBlockedNextTurn.Value = false;
                    netPlayer.HasVoted.Value = false;
                }
            }

            // Privately notify each client of their secret role
            ulong[] imposterArray = imposters.ToArray();
            foreach (var client in clients)
            {
                PlayerRole role = serverRoles[client];
                var rpcParams = new ClientRpcParams
                {
                    Send = new ClientRpcSendParams { TargetClientIds = new[] { client } }
                };

                // Imposters know who the other imposters are; normals do not
                ulong[] knownImposters = (role == PlayerRole.Imposter) ? imposterArray : Array.Empty<ulong>();

                ReceiveSecretRoleClientRpc(
                    role,
                    knownImposters,
                    settings.forcedBlockUsesPerMatch,
                    settings.luckyShiftUsesPerMatch,
                    settings.cursedDiceUsesPerMatch,
                    rpcParams);
            }

            // Build initial turn order of living players
            turnOrder.Clear();
            turnOrder.AddRange(shuffled);
            currentTurnOrderIndex = 0;

            CurrentGamePhase.Value = GamePhase.RoleReveal;
            PhaseTimer.Value = settings.roleRevealDurationSeconds;
            LastCommittedMove.Value = $"Match started! Target Lucky Number is {TargetLuckyNumber.Value}.";
        }

        #endregion

        #region Turn Management (Server Only)

        private void StartNextTurn()
        {
            if (!IsServer) return;

            // Check if win condition met
            if (CheckWinCondition()) return;

            // Filter living players
            var livingPlayers = turnOrder.Where(id => IsPlayerAlive(id)).ToList();
            if (livingPlayers.Count == 0)
            {
                EndMatch(Team.ImposterTeam, WinReason.OutOfMoves);
                return;
            }

            // If we completed a full round of living players -> go to Discussion & Voting
            if (currentTurnOrderIndex >= livingPlayers.Count)
            {
                currentTurnOrderIndex = 0;
                StartDiscussionPhase();
                return;
            }

            ulong activeId = livingPlayers[currentTurnOrderIndex];
            ActivePlayerClientId.Value = activeId;
            CurrentGamePhase.Value = GamePhase.PlayerTurn;
            PhaseTimer.Value = settings.turnDurationSeconds;

            var activePlayer = GetPlayer(activeId);

            // Check if player is blocked by Forced Block
            if (activePlayer != null && activePlayer.IsBlockedNextTurn.Value)
            {
                activePlayer.IsBlockedNextTurn.Value = false;
                LastCommittedMove.Value = $"{activePlayer.PlayerName.Value} was blocked and skips their turn!";
                // Forced block does not consume shared moves
                currentTurnOrderIndex++;
                StartNextTurn();
                return;
            }

            // Generate authoritative dice
            bool isCursed = serverCursedPlayers.Contains(activeId);
            serverCursedPlayers.Remove(activeId); // Curse is consumed

            int[] dice = DiceRollGenerator.RollThreeDice(isCursed, CurrentScore.Value, TargetLuckyNumber.Value, settings.cursedBiasStrength);
            serverActiveDice[activeId] = dice;

            // Send dice PRIVATELY to active player
            var rpcParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams { TargetClientIds = new[] { activeId } }
            };
            ReceivePrivateDiceClientRpc(dice[0], dice[1], dice[2], isCursed, rpcParams);
        }

        #endregion

        #region Move Submissions & Skips (Authoritative ServerRpc)

        [ServerRpc(RequireOwnership = false)]
        public void SubmitMoveServerRpc(int dieValue, bool isAddition, ServerRpcParams rpcParams = default)
        {
            if (!IsServer) return;
            ulong senderId = rpcParams.Receive.SenderClientId;

            // Validation 1: Phase
            if (CurrentGamePhase.Value != GamePhase.PlayerTurn)
            {
                Debug.LogWarning("[NetworkGameManager] Move rejected: Not player turn phase.");
                return;
            }

            // Validation 2: Active player
            if (senderId != ActivePlayerClientId.Value)
            {
                Debug.LogWarning($"[NetworkGameManager] Move rejected: Sender {senderId} is not active player {ActivePlayerClientId.Value}.");
                return;
            }

            // Validation 3: Alive
            if (!IsPlayerAlive(senderId)) return;

            // Validation 4: Move pool
            if (RemainingMoves.Value <= 0)
            {
                EndMatch(Team.ImposterTeam, WinReason.OutOfMoves);
                return;
            }

            // Validation 5: Die belongs to server roll
            if (!serverActiveDice.TryGetValue(senderId, out var roll) || !roll.Contains(dieValue))
            {
                Debug.LogWarning($"[NetworkGameManager] Move rejected: Die value {dieValue} not in authorized roll.");
                return;
            }

            // Execute move
            int delta = isAddition ? dieValue : -dieValue;
            CurrentScore.Value += delta;
            RemainingMoves.Value--;

            var player = GetPlayer(senderId);
            string pName = player != null ? player.PlayerName.Value.ToString() : $"Player {senderId}";
            string sign = isAddition ? "+" : "-";
            LastCommittedMove.Value = $"{pName} played {sign}{dieValue} (Score: {CurrentScore.Value})";

            serverActiveDice.Remove(senderId);

            // Check if game ended immediately
            if (CheckWinCondition()) return;

            // Next turn
            currentTurnOrderIndex++;
            StartNextTurn();
        }

        [ServerRpc(RequireOwnership = false)]
        public void RequestVoluntarySkipServerRpc(ServerRpcParams rpcParams = default)
        {
            if (!IsServer) return;
            ulong senderId = rpcParams.Receive.SenderClientId;

            if (CurrentGamePhase.Value != GamePhase.PlayerTurn || senderId != ActivePlayerClientId.Value) return;

            var player = GetPlayer(senderId);
            if (player == null || player.RemainingSkips.Value <= 0)
            {
                Debug.LogWarning("[NetworkGameManager] Voluntary skip rejected: No skips remaining.");
                return;
            }

            // Consume skip allowance, does not consume shared moves
            player.RemainingSkips.Value--;
            LastCommittedMove.Value = $"{player.PlayerName.Value} voluntarily skipped their turn.";

            serverActiveDice.Remove(senderId);
            currentTurnOrderIndex++;
            StartNextTurn();
        }

        #endregion

        #region Imposter Abilities (Authoritative ServerRpc)

        [ServerRpc(RequireOwnership = false)]
        public void ActivateForcedBlockServerRpc(ulong targetClientId, ServerRpcParams rpcParams = default)
        {
            if (!IsServer) return;
            ulong senderId = rpcParams.Receive.SenderClientId;

            if (!IsImposter(senderId)) return;
            var imposter = GetPlayer(senderId);
            if (imposter == null || imposter.ForcedBlockCharges <= 0) return;

            var target = GetPlayer(targetClientId);
            if (target == null || !target.IsAlive.Value) return;

            imposter.DecrementBlockCharge();
            target.IsBlockedNextTurn.Value = true;

            // Acknowledge to imposter only
            SendAbilityFeedback(senderId, $"Forced Block primed on {target.PlayerName.Value}!");
        }

        [ServerRpc(RequireOwnership = false)]
        public void ActivateLuckyShiftServerRpc(int delta, ServerRpcParams rpcParams = default)
        {
            if (!IsServer) return;
            ulong senderId = rpcParams.Receive.SenderClientId;

            if (!IsImposter(senderId)) return;
            var imposter = GetPlayer(senderId);
            if (imposter == null || imposter.LuckyShiftCharges <= 0) return;

            // Nonzero delta within bounds
            if (delta == 0 || delta < settings.minShiftDelta || delta > settings.maxShiftDelta) return;

            imposter.DecrementShiftCharge();
            TargetLuckyNumber.Value += delta;

            LastCommittedMove.Value = $"[LUCKY SHIFT] Target altered by {delta:+#;-#;0}! New Target: {TargetLuckyNumber.Value}";
            SendAbilityFeedback(senderId, $"Target shifted by {delta}.");

            CheckWinCondition();
        }

        [ServerRpc(RequireOwnership = false)]
        public void ActivateCursedDiceServerRpc(ulong targetClientId, ServerRpcParams rpcParams = default)
        {
            if (!IsServer) return;
            ulong senderId = rpcParams.Receive.SenderClientId;

            if (!IsImposter(senderId)) return;
            var imposter = GetPlayer(senderId);
            if (imposter == null || imposter.CursedDiceCharges <= 0) return;

            var target = GetPlayer(targetClientId);
            if (target == null || !target.IsAlive.Value) return;

            imposter.DecrementCursedCharge();
            serverCursedPlayers.Add(targetClientId);

            SendAbilityFeedback(senderId, $"Cursed Dice placed on {target.PlayerName.Value}!");
        }

        private void SendAbilityFeedback(ulong imposterId, string msg)
        {
            var rpcParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams { TargetClientIds = new[] { imposterId } }
            };
            ReceiveAbilityFeedbackClientRpc(msg, rpcParams);
        }

        #endregion

        #region Discussion & Voting (Server Only)

        private void StartDiscussionPhase()
        {
            CurrentGamePhase.Value = GamePhase.Discussion;
            PhaseTimer.Value = settings.discussionDurationSeconds;
            LastCommittedMove.Value = "Round finished. Discussion phase started!";
        }

        private void StartVotingPhase()
        {
            CurrentGamePhase.Value = GamePhase.Voting;
            PhaseTimer.Value = settings.votingDurationSeconds;
            serverVotes.Clear();
            serverAbstainVotes.Clear();

            foreach (var p in GetAllLivingPlayers())
            {
                p.HasVoted.Value = false;
            }

            LastCommittedMove.Value = "Voting phase started! Cast your vote.";
        }

        [ServerRpc(RequireOwnership = false)]
        public void SubmitVoteServerRpc(ulong suspectClientId, ServerRpcParams rpcParams = default)
        {
            if (!IsServer || CurrentGamePhase.Value != GamePhase.Voting) return;
            ulong voterId = rpcParams.Receive.SenderClientId;

            if (!IsPlayerAlive(voterId)) return;
            if (serverVotes.ContainsKey(voterId) || serverAbstainVotes.Contains(voterId)) return;

            // Target must be alive
            if (!IsPlayerAlive(suspectClientId)) return;

            serverVotes[voterId] = suspectClientId;
            var voter = GetPlayer(voterId);
            if (voter != null) voter.HasVoted.Value = true;

            CheckAllVotesSubmitted();
        }

        [ServerRpc(RequireOwnership = false)]
        public void SkipVoteServerRpc(ServerRpcParams rpcParams = default)
        {
            if (!IsServer || CurrentGamePhase.Value != GamePhase.Voting) return;
            ulong voterId = rpcParams.Receive.SenderClientId;

            if (!IsPlayerAlive(voterId)) return;
            if (serverVotes.ContainsKey(voterId) || serverAbstainVotes.Contains(voterId)) return;

            serverAbstainVotes.Add(voterId);
            var voter = GetPlayer(voterId);
            if (voter != null) voter.HasVoted.Value = true;

            CheckAllVotesSubmitted();
        }

        private void CheckAllVotesSubmitted()
        {
            int livingCount = GetAllLivingPlayers().Count;
            int totalVotes = serverVotes.Count + serverAbstainVotes.Count;

            if (totalVotes >= livingCount)
            {
                ResolveVoting();
            }
        }

        private void ResolveVoting()
        {
            if (!IsServer) return;

            CurrentGamePhase.Value = GamePhase.Elimination;
            PhaseTimer.Value = settings.eliminationDurationSeconds;

            // Tally votes
            var voteTally = new Dictionary<ulong, int>();
            foreach (var kvp in serverVotes)
            {
                voteTally[kvp.Value] = voteTally.GetValueOrDefault(kvp.Value, 0) + 1;
            }

            if (voteTally.Count == 0)
            {
                LastCommittedMove.Value = "No votes cast. No one was eliminated.";
                LastEliminatedPlayerName.Value = string.Empty;
                return;
            }

            int maxVotes = voteTally.Values.Max();
            var topSuspects = voteTally.Where(kvp => kvp.Value == maxVotes).Select(kvp => kvp.Key).ToList();

            // Tie handling: No elimination if tie
            if (topSuspects.Count > 1)
            {
                LastCommittedMove.Value = $"Tie vote ({maxVotes} votes each). No one was eliminated.";
                LastEliminatedPlayerName.Value = string.Empty;
            }
            else
            {
                ulong eliminatedId = topSuspects[0];
                var targetPlayer = GetPlayer(eliminatedId);
                PlayerRole revealedRole = serverRoles.GetValueOrDefault(eliminatedId, PlayerRole.Normal);

                if (targetPlayer != null)
                {
                    targetPlayer.IsAlive.Value = false;
                    LastEliminatedPlayerName.Value = targetPlayer.PlayerName.Value;
                    LastEliminatedRole.Value = revealedRole;
                    LastCommittedMove.Value = $"{targetPlayer.PlayerName.Value} was eliminated! They were {revealedRole}.";
                }
            }

            // Check win conditions after elimination
            if (CheckWinCondition()) return;
        }

        #endregion

        #region Win Conditions & Phase Resolution

        private bool CheckWinCondition()
        {
            if (!IsServer) return false;

            // Win Condition 1: Target Reached exactly -> Normal Team wins!
            if (CurrentScore.Value == TargetLuckyNumber.Value)
            {
                EndMatch(Team.NormalTeam, WinReason.TargetReached);
                return true;
            }

            // Win Condition 2: Remaining moves reached zero -> Imposter Team wins!
            if (RemainingMoves.Value <= 0)
            {
                EndMatch(Team.ImposterTeam, WinReason.OutOfMoves);
                return true;
            }

            // Win Condition 3: All Imposters eliminated -> Normal Team wins!
            int livingImposters = GetAllLivingPlayers().Count(p => serverRoles.GetValueOrDefault(p.OwnerClientId, PlayerRole.Normal) == PlayerRole.Imposter);
            if (livingImposters == 0)
            {
                EndMatch(Team.NormalTeam, WinReason.AllImpostersEliminated);
                return true;
            }

            // Win Condition 4: Imposter parity (living imposters >= living normals) -> Imposters win!
            int livingNormals = GetAllLivingPlayers().Count(p => serverRoles.GetValueOrDefault(p.OwnerClientId, PlayerRole.Normal) == PlayerRole.Normal);
            if (livingImposters >= livingNormals)
            {
                EndMatch(Team.ImposterTeam, WinReason.ImposterParity);
                return true;
            }

            return false;
        }

        private void EndMatch(Team winner, WinReason reason)
        {
            WinningTeam.Value = winner;
            MatchWinReason.Value = reason;
            CurrentGamePhase.Value = GamePhase.MatchEnded;
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
                    // Auto-skip on turn timeout
                    LastCommittedMove.Value = $"Player turn timed out. Automatically skipped.";
                    currentTurnOrderIndex++;
                    StartNextTurn();
                    break;

                case GamePhase.Discussion:
                    StartVotingPhase();
                    break;

                case GamePhase.Voting:
                    ResolveVoting();
                    break;

                case GamePhase.Elimination:
                    if (!CheckWinCondition())
                    {
                        CurrentRound.Value++;
                        currentTurnOrderIndex = 0;
                        StartNextTurn();
                    }
                    break;
            }
        }

        #endregion

        #region Helpers & RPCs

        private void HandleServerClientDisconnect(ulong clientId)
        {
            if (!IsServer) return;
            Debug.Log($"[NetworkGameManager] Client {clientId} disconnected.");

            if (ActivePlayerClientId.Value == clientId && CurrentGamePhase.Value == GamePhase.PlayerTurn)
            {
                currentTurnOrderIndex++;
                StartNextTurn();
            }

            CheckWinCondition();
        }

        public LuckyNetworkPlayer GetPlayer(ulong clientId)
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client))
            {
                return client.PlayerObject != null ? client.PlayerObject.GetComponent<LuckyNetworkPlayer>() : null;
            }
            return null;
        }

        public List<LuckyNetworkPlayer> GetAllLivingPlayers()
        {
            var list = new List<LuckyNetworkPlayer>();
            if (NetworkManager.Singleton == null) return list;

            foreach (var client in NetworkManager.Singleton.ConnectedClients.Values)
            {
                if (client.PlayerObject != null)
                {
                    var p = client.PlayerObject.GetComponent<LuckyNetworkPlayer>();
                    if (p != null && p.IsAlive.Value) list.Add(p);
                }
            }
            return list;
        }

        private bool IsPlayerAlive(ulong clientId)
        {
            var p = GetPlayer(clientId);
            return p != null && p.IsAlive.Value;
        }

        private bool IsImposter(ulong clientId) => serverRoles.GetValueOrDefault(clientId, PlayerRole.Normal) == PlayerRole.Imposter;

        #endregion

        #region Targeted ClientRpc Implementations

        [ClientRpc]
        private void ReceiveSecretRoleClientRpc(
            PlayerRole role,
            ulong[] otherImposters,
            int blockCharges,
            int shiftCharges,
            int cursedCharges,
            ClientRpcParams rpcParams = default)
        {
            var localPlayer = NetworkManager.Singleton?.LocalClient?.PlayerObject?.GetComponent<LuckyNetworkPlayer>();
            if (localPlayer != null)
            {
                localPlayer.SetLocalRole(role, blockCharges, shiftCharges, cursedCharges);
            }
            Debug.Log($"[NetworkGameManager] Received Secret Role: {role}");
        }

        [ClientRpc]
        private void ReceivePrivateDiceClientRpc(int d1, int d2, int d3, bool isCursed, ClientRpcParams rpcParams = default)
        {
            var localPlayer = NetworkManager.Singleton?.LocalClient?.PlayerObject?.GetComponent<LuckyNetworkPlayer>();
            if (localPlayer != null)
            {
                localPlayer.SetLocalDice(new[] { d1, d2, d3 }, isCursed);
            }
        }

        [ClientRpc]
        private void ReceiveAbilityFeedbackClientRpc(string message, ClientRpcParams rpcParams = default)
        {
            Debug.Log($"[NetworkGameManager] Ability Feedback: {message}");
            OnPublicLogMessage?.Invoke(message);
        }

        #endregion
    }
}

