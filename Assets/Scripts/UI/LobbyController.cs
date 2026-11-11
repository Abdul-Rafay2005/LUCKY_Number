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

        private readonly List<GameObject> activePlayerCards = new List<GameObject>();

        private void OnEnable()
        {
            if (copyCodeButton != null) copyCodeButton.onClick.AddListener(OnCopyCode);
            if (readyButton != null) readyButton.onClick.AddListener(OnToggleReady);
            if (startMatchButton != null) startMatchButton.onClick.AddListener(OnStartMatchClicked);
            if (leaveRoomButton != null) leaveRoomButton.onClick.AddListener(OnLeaveRoom);

            UpdateLobbyView();
        }

        private void OnDisable()
        {
            if (copyCodeButton != null) copyCodeButton.onClick.RemoveListener(OnCopyCode);
            if (readyButton != null) readyButton.onClick.RemoveListener(OnToggleReady);
            if (startMatchButton != null) startMatchButton.onClick.RemoveListener(OnStartMatchClicked);
            if (leaveRoomButton != null) leaveRoomButton.onClick.RemoveListener(OnLeaveRoom);

        }

        private void Update()
        {
            // Regularly refresh lobby stats and readiness
            UpdateLobbyView();
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
            var local = LuckyNetworkPlayer.LocalPlayer;
            bool inLobby = game != null && game.IsSpawned && game.CurrentGamePhase.Value == GamePhase.Lobby;
            if (readyButton != null) readyButton.interactable = inLobby && local != null;
            if (readyButtonText != null) readyButtonText.text = local != null && local.IsReady.Value ? "UNREADY" : "READY";

            bool isHost = NetworkManager.Singleton.IsHost;

            if (startMatchButton != null)
            {
                startMatchButton.gameObject.SetActive(isHost);
                bool canStart = inLobby && connectedCount >= minNeeded && connectedCount <= maxAllowed;
                startMatchButton.interactable = canStart;

                if (isHost && statusMessageText != null)
                {
                    statusMessageText.text = !inLobby ? "Starting match..." : canStart
                        ? "Ready to start match!"
                        : $"Need {minNeeded}–{maxAllowed} players ({connectedCount} connected).";
                }
            }

            RefreshPlayerCards();
        }

        private void RefreshPlayerCards()
        {
            // Clear existing
            foreach (var card in activePlayerCards)
            {
                if (card != null) Destroy(card);
            }
            activePlayerCards.Clear();

            if (playerListContainer == null || playerCardPrefab == null) return;

            foreach (var netPlayer in LuckyNetworkPlayer.SpawnedPlayers.Values)
            {
                var cardObj = Instantiate(playerCardPrefab, playerListContainer);
                activePlayerCards.Add(cardObj);

                var texts = cardObj.GetComponentsInChildren<TMP_Text>();
                var images = cardObj.GetComponentsInChildren<Image>();

                string pName = netPlayer.PlayerName.Value.ToString();
                bool isCardHost = netPlayer.OwnerClientId == NetworkManager.ServerClientId;
                bool isReady = netPlayer.IsReady.Value;

                // Set player name text
                if (texts.Length > 0) texts[0].text = isCardHost ? $"{pName} 👑 (Host)" : pName;
                if (texts.Length > 1) texts[1].text = isReady ? "READY" : "WAITING";

                // Set portrait image
                int charIdx = netPlayer.CharacterIndex.Value;
                if (images.Length > 1 && characterPortraits != null && charIdx >= 0 && charIdx < characterPortraits.Length)
                {
                    images[1].sprite = characterPortraits[charIdx];
                }
            }
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

