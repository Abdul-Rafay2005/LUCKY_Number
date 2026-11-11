using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using LuckyNumber.Core;

namespace LuckyNumber.Network
{
    /// <summary>
    /// Represents a networked player in Lucky Number.
    /// Manages synchronized public states and local private role/ability info.
    /// </summary>
    public class LuckyNetworkPlayer : NetworkBehaviour
    {
        private static readonly SortedDictionary<ulong, LuckyNetworkPlayer> spawnedPlayers =
            new SortedDictionary<ulong, LuckyNetworkPlayer>();

        public static IReadOnlyDictionary<ulong, LuckyNetworkPlayer> SpawnedPlayers => spawnedPlayers;

        public static LuckyNetworkPlayer FindSpawnedPlayer(ulong clientId) =>
            spawnedPlayers.TryGetValue(clientId, out var player) ? player : null;

        public static LuckyNetworkPlayer LocalPlayer
        {
            get
            {
                var manager = NetworkManager.Singleton;
                return manager != null && manager.IsListening ? FindSpawnedPlayer(manager.LocalClientId) : null;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void ClearSpawnedPlayers() => spawnedPlayers.Clear();

        [Header("Public Networked States")]
        public NetworkVariable<FixedString64Bytes> PlayerName = new NetworkVariable<FixedString64Bytes>(
            string.Empty,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public NetworkVariable<int> CharacterIndex = new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public NetworkVariable<bool> IsReady = new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public NetworkVariable<bool> IsAlive = new NetworkVariable<bool>(
            true,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public NetworkVariable<int> RemainingSkips = new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Owner,
            NetworkVariableWritePermission.Server);

        public NetworkVariable<bool> HasVoted = new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<PlayerSecret> secret =
            new NetworkVariable<PlayerSecret>(default, NetworkVariableReadPermission.Owner);
        private readonly NetworkVariable<PrivateRoll> roll =
            new NetworkVariable<PrivateRoll>(default, NetworkVariableReadPermission.Owner);

        public bool HasSecretRole => IsOwner && secret.Value.HasRole;
        public PlayerRole LocalRole => IsOwner ? secret.Value.Role : PlayerRole.Normal;
        public int ForcedBlockCharges => IsOwner ? secret.Value.BlockCharges : 0;
        public int LuckyShiftCharges => IsOwner ? secret.Value.ShiftCharges : 0;
        public int CursedDiceCharges => IsOwner ? secret.Value.CurseCharges : 0;
        public int LastAbilityRound => IsOwner ? secret.Value.LastAbilityRound : 0;
        public int AbilityUsesThisRound
        {
            get
            {
                if (!IsOwner) return 0;
                var game = NetworkGameManager.Instance;
                return game != null && secret.Value.LastAbilityRound == game.CurrentRound.Value
                    ? secret.Value.AbilityUsesThisRound
                    : 0;
            }
        }
        public FixedList128Bytes<ulong> KnownImposters => IsOwner ? secret.Value.KnownImposters : default;
        public PrivateRoll CurrentRoll => IsOwner ? roll.Value : default;

        internal void SetSecret(PlayerSecret value) => secret.Value = value;
        internal void SetPrivateRoll(PrivateRoll value) => roll.Value = value;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            spawnedPlayers[OwnerClientId] = this;

            if (IsOwner)
            {
                // Send player profile to server
                string savedName = PlayerPrefs.GetString("PlayerName", "Player_" + UnityEngine.Random.Range(100, 999));
                int savedChar = PlayerPrefs.GetInt("SelectedCharacter", (int)(OwnerClientId % 6));
                SetProfileServerRpc(savedName, savedChar);
            }
        }

        public override void OnNetworkDespawn()
        {
            if (FindSpawnedPlayer(OwnerClientId) == this)
                spawnedPlayers.Remove(OwnerClientId);

            base.OnNetworkDespawn();
        }

        [ServerRpc]
        public void SetProfileServerRpc(FixedString64Bytes name, int charIndex)
        {
            var game = NetworkGameManager.Instance;
            if (game != null && game.IsSpawned && game.CurrentGamePhase.Value != GamePhase.Lobby) return;
            PlayerName.Value = name;
            CharacterIndex.Value = Mathf.Clamp(charIndex, 0, 5);
        }

        [ServerRpc]
        public void SetReadyServerRpc(bool ready)
        {
            if (NetworkGameManager.Instance?.CurrentGamePhase.Value != GamePhase.Lobby) return;
            IsReady.Value = ready;
        }
    }
}

