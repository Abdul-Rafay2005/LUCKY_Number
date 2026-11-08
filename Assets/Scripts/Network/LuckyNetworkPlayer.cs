using System;
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
            1,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public NetworkVariable<bool> IsBlockedNextTurn = new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public NetworkVariable<bool> HasVoted = new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        [Header("Local Private States (Not replicated to other clients)")]
        [SerializeField] private PlayerRole localRole = PlayerRole.Normal;
        [SerializeField] private int forcedBlockCharges = 1;
        [SerializeField] private int luckyShiftCharges = 1;
        [SerializeField] private int cursedDiceCharges = 1;
        [SerializeField] private int[] currentDice = new int[3];
        [SerializeField] private bool isCursedThisTurn = false;

        public PlayerRole LocalRole => localRole;
        public int ForcedBlockCharges => forcedBlockCharges;
        public int LuckyShiftCharges => luckyShiftCharges;
        public int CursedDiceCharges => cursedDiceCharges;
        public int[] CurrentDice => currentDice;
        public bool IsCursedThisTurn => isCursedThisTurn;

        public event Action<PlayerRole> OnSecretRoleReceived;
        public event Action<int[], bool> OnPrivateDiceReceived;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (IsOwner)
            {
                // Send player profile to server
                string savedName = PlayerPrefs.GetString("PlayerName", "Player_" + UnityEngine.Random.Range(100, 999));
                int savedChar = PlayerPrefs.GetInt("SelectedCharacter", (int)(OwnerClientId % 6));
                SetProfileServerRpc(savedName, savedChar);
            }
        }

        [ServerRpc]
        public void SetProfileServerRpc(FixedString64Bytes name, int charIndex)
        {
            PlayerName.Value = name;
            CharacterIndex.Value = Mathf.Clamp(charIndex, 0, 5);
        }

        [ServerRpc]
        public void SetReadyServerRpc(bool ready)
        {
            IsReady.Value = ready;
        }

        /// <summary>
        /// Called only on the specific client to assign their secret role.
        /// </summary>
        public void SetLocalRole(PlayerRole role, int blockCharges, int shiftCharges, int cursedCharges)
        {
            localRole = role;
            forcedBlockCharges = blockCharges;
            luckyShiftCharges = shiftCharges;
            cursedDiceCharges = cursedCharges;
            OnSecretRoleReceived?.Invoke(role);
        }

        /// <summary>
        /// Called on the active player's client when receiving their secret dice.
        /// </summary>
        public void SetLocalDice(int[] dice, bool cursed)
        {
            currentDice = dice;
            isCursedThisTurn = cursed;
            OnPrivateDiceReceived?.Invoke(dice, cursed);
        }

        public void DecrementBlockCharge() => forcedBlockCharges = Mathf.Max(0, forcedBlockCharges - 1);
        public void DecrementShiftCharge() => luckyShiftCharges = Mathf.Max(0, luckyShiftCharges - 1);
        public void DecrementCursedCharge() => cursedDiceCharges = Mathf.Max(0, cursedDiceCharges - 1);
    }
}

