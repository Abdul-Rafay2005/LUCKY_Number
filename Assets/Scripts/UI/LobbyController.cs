using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;
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
        private bool isLocalReady = false;

        private void OnEnable()
        {
            if (copyCodeButton != null) copyCodeButton.onClick.AddListener(OnCopyCode);
            if (readyButton != null) readyButton.onClick.AddListener(OnToggleReady);
            if (startMatchButton != null) startMatchButton.onClick.AddListener(OnStartMatchClicked);
            if (leaveRoomButton != null) leaveRoomButton.onClick.AddListener(OnLeaveRoom);

            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback += HandleClientListChanged;
                NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientListChanged;
            }

            UpdateLobbyView();
        }

        private void OnDisable()
        {
            if (copyCodeButton != null) copyCodeButton.onClick.RemoveListener(OnCopyCode);
            if (readyButton != null) readyButton.onClick.RemoveListener(OnToggleReady);
            if (startMatchButton != null) startMatchButton.onClick.RemoveListener(OnStartMatchClicked);
            if (leaveRoomButton != null) leaveRoomButton.onClick.RemoveListener(OnLeaveRoom);

            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientListChanged;
                NetworkManager.Singleton.OnClientDisconnectCallback -= HandleClientListChanged;
            }
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

            int connectedCount = NetworkManager.Singleton.ConnectedClients.Count;
            int minNeeded = NetworkGameManager.Instance != null && NetworkGameManager.Instance.Settings != null
                ? NetworkGameManager.Instance.Settings.minPlayers
                : 3;

            if (playerCountText != null)
            {
                playerCountText.text = $"Players: {connectedCount} / 8 (Min: {minNeeded})";
            }

            bool isHost = NetworkManager.Singleton.IsHost;

            if (startMatchButton != null)
            {
                startMatchButton.gameObject.SetActive(isHost);
                // Enabled only if minimum players met
                bool canStart = connectedCount >= minNeeded;
                startMatchButton.interactable = canStart;

                if (!canStart && isHost && statusMessageText != null)
                {
                    statusMessageText.text = $"Waiting for players... ({connectedCount}/{minNeeded})";
                }
                else if (canStart && isHost && statusMessageText != null)
                {
                    statusMessageText.text = "Ready to start match!";
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

            foreach (var client in NetworkManager.Singleton.ConnectedClients.Values)
            {
                if (client.PlayerObject == null) continue;
                var netPlayer = client.PlayerObject.GetComponent<LuckyNetworkPlayer>();
                if (netPlayer == null) continue;

                var cardObj = Instantiate(playerCardPrefab, playerListContainer);
                activePlayerCards.Add(cardObj);

                var texts = cardObj.GetComponentsInChildren<TMP_Text>();
                var images = cardObj.GetComponentsInChildren<Image>();

                string pName = netPlayer.PlayerName.Value.ToString();
                bool isCardHost = client.ClientId == NetworkManager.ServerClientId;
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
            isLocalReady = !isLocalReady;

            if (readyButtonText != null)
            {
                readyButtonText.text = isLocalReady ? "UNREADY" : "READY";
            }

            var localPlayer = NetworkManager.Singleton?.LocalClient?.PlayerObject?.GetComponent<LuckyNetworkPlayer>();
            if (localPlayer != null)
            {
                localPlayer.SetReadyServerRpc(isLocalReady);
            }
        }

        private void OnStartMatchClicked()
        {
            AudioManager.Instance?.PlayClick();
            if (NetworkGameManager.Instance != null)
            {
                NetworkGameManager.Instance.RequestStartMatchServerRpc();
            }
        }

        private void OnLeaveRoom()
        {
            AudioManager.Instance?.PlayClick();
            NetworkLobbyManager.Instance?.LeaveRoom();
        }

        private void HandleClientListChanged(ulong clientId)
        {
            UpdateLobbyView();
        }
    }
}

