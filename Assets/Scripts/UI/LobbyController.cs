using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;
using LuckyNumber.Core;
using LuckyNumber.Network;
using LuckyNumber.Audio;

namespace LuckyNumber.UI
{
    public class LobbyController : MonoBehaviour
    {
        [Header("Room Header")]
        [SerializeField] private TMP_Text roomCodeText;
        [SerializeField] private Button copyCodeButton;
        [SerializeField] private TMP_Text playerCountText;

        [Header("Player List Display")]
        [SerializeField] private Transform playerListContainer;
        [SerializeField] private GameObject playerCardPrefab;
        [SerializeField] private Sprite[] characterPortraits;

        [Header("Controls")]
        [SerializeField] private Button readyButton;
        [SerializeField] private TMP_Text readyButtonText;
        [SerializeField] private Button startMatchButton;
        [SerializeField] private Button leaveRoomButton;

        [Header("Status Feedback")]
        [SerializeField] private TMP_Text statusMessageText;

        private sealed class PlayerCard
        {
            public GameObject View;
            public LuckyNetworkPlayer Player;
            public TMP_Text[] Labels;
            public Image Portrait;
            public System.Action Refresh;
        }

        private readonly Dictionary<ulong, PlayerCard> activePlayerCards = new Dictionary<ulong, PlayerCard>();
        private readonly List<ulong> removedPlayers = new List<ulong>();

        private void OnEnable()
        {
            if (copyCodeButton != null) copyCodeButton.onClick.AddListener(OnCopyCode);
            if (readyButton != null) readyButton.onClick.AddListener(OnToggleReady);
            if (startMatchButton != null) startMatchButton.onClick.AddListener(OnStartMatchClicked);
            if (leaveRoomButton != null) leaveRoomButton.onClick.AddListener(OnLeaveRoom);

            LuckyNetworkPlayer.RosterChanged += UpdateLobbyView;
            UpdateLobbyView();
        }

        private void OnDisable()
        {
            if (copyCodeButton != null) copyCodeButton.onClick.RemoveListener(OnCopyCode);
            if (readyButton != null) readyButton.onClick.RemoveListener(OnToggleReady);
            if (startMatchButton != null) startMatchButton.onClick.RemoveListener(OnStartMatchClicked);
            if (leaveRoomButton != null) leaveRoomButton.onClick.RemoveListener(OnLeaveRoom);
            LuckyNetworkPlayer.RosterChanged -= UpdateLobbyView;
            foreach (var card in activePlayerCards.Values)
                RemoveCard(card);
            activePlayerCards.Clear();
        }

        private void UpdateLobbyView()
        {
            if (NetworkLobbyManager.Instance != null && roomCodeText != null)
            {
                roomCodeText.text = $"ROOM CODE: {NetworkLobbyManager.Instance.CurrentRoomCode}";
            }

            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening) return;

            int connectedCount = LuckyNetworkPlayer.SpawnedPlayers.Count;
            var game = NetworkGameManager.Instance;
            var settings = game != null ? game.Settings : null;
            int minNeeded = settings != null ? settings.minPlayers : 3;
            int maxAllowed = settings != null ? settings.maxPlayers : 8;

            if (playerCountText != null)
            {
                playerCountText.text = $"Players: {connectedCount} / {maxAllowed} (Min: {minNeeded})";
            }
            RefreshReadyLabel();
            var local = LuckyNetworkPlayer.LocalPlayer;
            bool inLobby = game != null && game.IsSpawned && game.CurrentGamePhase.Value == GamePhase.Lobby;
            if (readyButton != null) readyButton.interactable = inLobby && local != null;

            bool isHost = NetworkManager.Singleton.IsHost;

            if (startMatchButton != null)
            {
                startMatchButton.gameObject.SetActive(isHost);
                bool canStart = inLobby && connectedCount >= minNeeded && connectedCount <= maxAllowed;
                startMatchButton.interactable = canStart;

                if (statusMessageText != null)
                {
                    statusMessageText.text = !inLobby ? "Starting match..." : !isHost
                        ? "Waiting for the host to start."
                        : canStart
                        ? "Ready to start match!"
                        : $"Need {minNeeded}–{maxAllowed} players ({connectedCount} connected).";
                }
            }

            RefreshPlayerCards();
        }

        private void RefreshPlayerCards()
        {
            removedPlayers.Clear();
            foreach (var entry in activePlayerCards)
            {
                if (LuckyNetworkPlayer.FindSpawnedPlayer(entry.Key) == entry.Value.Player) continue;
                RemoveCard(entry.Value);
                removedPlayers.Add(entry.Key);
            }
            foreach (ulong id in removedPlayers)
                activePlayerCards.Remove(id);

            if (playerListContainer == null || playerCardPrefab == null) return;

            foreach (var netPlayer in LuckyNetworkPlayer.SpawnedPlayers.Values)
            {
                if (activePlayerCards.ContainsKey(netPlayer.OwnerClientId)) continue;
                var cardObj = Instantiate(playerCardPrefab, playerListContainer);
                var images = cardObj.GetComponentsInChildren<Image>();
                var card = new PlayerCard
                {
                    View = cardObj,
                    Player = netPlayer,
                    Labels = cardObj.GetComponentsInChildren<TMP_Text>(),
                    Portrait = images.Length > 1 ? images[1] : null
                };
                var button = cardObj.GetComponent<Button>();
                if (button != null) button.enabled = false;
                card.Refresh = () => RefreshCard(card);
                netPlayer.ProfileChanged += card.Refresh;
                activePlayerCards.Add(netPlayer.OwnerClientId, card);
                RefreshCard(card);
            }
        }

        private void RefreshCard(PlayerCard card)
        {
            var player = card.Player;
            if (card.Labels.Length > 0)
                card.Labels[0].text = PlayerNameLabel.Compact(player.PlayerName.Value.ToString());
            if (card.Labels.Length > 1)
            {
                string readiness = player.IsReady.Value ? "READY" : "WAITING";
                card.Labels[1].text = player.OwnerClientId == NetworkManager.ServerClientId ? $"HOST · {readiness}" : readiness;
                card.Labels[1].color = player.IsReady.Value
                    ? new Color32(147, 230, 200, 255)
                    : new Color32(250, 237, 209, 255);
            }
            int index = player.CharacterIndex.Value;
            if (card.Portrait != null && characterPortraits != null && index >= 0 && index < characterPortraits.Length)
                card.Portrait.sprite = characterPortraits[index];
            if (player.IsOwner) RefreshReadyLabel();
        }

        private void RefreshReadyLabel()
        {
            var local = LuckyNetworkPlayer.LocalPlayer;
            if (readyButtonText != null) readyButtonText.text = local != null && local.IsReady.Value ? "UNREADY" : "READY";
        }

        private void RemoveCard(PlayerCard card)
        {
            card.Player.ProfileChanged -= card.Refresh;
            card.View.SetActive(false);
            Destroy(card.View);
        }
        private void OnCopyCode()
        {
            AudioManager.Instance?.PlayClick();
            if (NetworkLobbyManager.Instance != null && !string.IsNullOrEmpty(NetworkLobbyManager.Instance.CurrentRoomCode))
            {
                GUIUtility.systemCopyBuffer = NetworkLobbyManager.Instance.CurrentRoomCode;
                if (statusMessageText != null) statusMessageText.text = "Room code copied to clipboard!";
            }
        }

        private void OnToggleReady()
        {
            AudioManager.Instance?.PlayClick();
            var localPlayer = LuckyNetworkPlayer.LocalPlayer;
            if (localPlayer != null)
            {
                localPlayer.SetReadyServerRpc(!localPlayer.IsReady.Value);
            }
        }

        private void OnStartMatchClicked()
        {
            AudioManager.Instance?.PlayClick();
            var game = NetworkGameManager.Instance;
            if (game != null && game.IsSpawned)
                game.RequestStartMatchServerRpc(game.ActionContext.Value);
        }

        private void OnLeaveRoom()
        {
            AudioManager.Instance?.PlayClick();
            NetworkLobbyManager.Instance?.LeaveRoom();
        }

    }
}

