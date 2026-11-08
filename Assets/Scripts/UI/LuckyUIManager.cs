using UnityEngine;
using LuckyNumber.Core;
using LuckyNumber.Network;

namespace LuckyNumber.UI
{
    /// <summary>
    /// Master coordinator for UI panels across match lifecycle.
    /// Listens to NetworkGameManager phase changes and switches panels accordingly.
    /// </summary>
    public class LuckyUIManager : MonoBehaviour
    {
        public static LuckyUIManager Instance { get; private set; }

        [Header("Panels")]
        [SerializeField] private GameObject mainMenuPanel;
        [SerializeField] private GameObject lobbyPanel;
        [SerializeField] private GameObject gameplayPanel;
        [SerializeField] private GameObject discussionPanel;
        [SerializeField] private GameObject votingPanel;
        [SerializeField] private GameObject eliminationModal;
        [SerializeField] private GameObject resultsPanel;
        [SerializeField] private GameObject howToPlayModal;
        [SerializeField] private GameObject settingsModal;

        private bool _subscribedToGameManager = false;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            ShowMainMenu();

            if (NetworkLobbyManager.Instance != null)
            {
                NetworkLobbyManager.Instance.OnRoomJoined += HandleRoomJoined;
                NetworkLobbyManager.Instance.OnLeftRoom += ShowMainMenu;
            }
        }

        private void Update()
        {
            // NetworkGameManager spawns after network starts — subscribe once available
            if (!_subscribedToGameManager && NetworkGameManager.Instance != null)
            {
                NetworkGameManager.Instance.OnPhaseChanged += HandlePhaseChanged;
                _subscribedToGameManager = true;
                Debug.Log("[LuckyUIManager] Subscribed to NetworkGameManager phase changes.");
            }
        }

        private void HandlePhaseChanged(GamePhase newPhase)
        {
            Debug.Log($"[LuckyUIManager] Phase changed to: {newPhase}");
            switch (newPhase)
            {
                case GamePhase.Lobby:
                    ShowLobby();
                    break;
                case GamePhase.RoleReveal:
                    // Stay on lobby briefly while role reveal plays, then gameplay shows
                    ShowLobby();
                    break;
                case GamePhase.PlayerTurn:
                    ShowGameplay();
                    break;
                case GamePhase.Discussion:
                    ShowGameplay(); // Keep gameplay panel visible under discussion
                    ShowDiscussion();
                    break;
                case GamePhase.Voting:
                    ShowVoting();
                    break;
                case GamePhase.Elimination:
                    ShowElimination();
                    break;
                case GamePhase.MatchEnded:
                    ShowResults();
                    break;
            }
        }

        private void OnDestroy()
        {
            if (NetworkLobbyManager.Instance != null)
            {
                NetworkLobbyManager.Instance.OnRoomJoined -= HandleRoomJoined;
                NetworkLobbyManager.Instance.OnLeftRoom -= ShowMainMenu;
            }
            if (NetworkGameManager.Instance != null)
            {
                NetworkGameManager.Instance.OnPhaseChanged -= HandlePhaseChanged;
            }
        }

        public void ShowMainMenu()
        {
            HideAll();
            if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
        }

        public void ShowLobby()
        {
            HideAll();
            if (lobbyPanel != null) lobbyPanel.SetActive(true);
        }

        public void ShowGameplay()
        {
            HideAll();
            if (gameplayPanel != null) gameplayPanel.SetActive(true);
        }

        public void ShowDiscussion()
        {
            HideAll();
            if (gameplayPanel != null) gameplayPanel.SetActive(true);
            if (discussionPanel != null) discussionPanel.SetActive(true);
            if (votingPanel != null) votingPanel.SetActive(false);
            if (eliminationModal != null) eliminationModal.SetActive(false);
        }

        public void ShowVoting()
        {
            HideAll();
            if (gameplayPanel != null) gameplayPanel.SetActive(true);
            if (discussionPanel != null) discussionPanel.SetActive(false);
            if (votingPanel != null) votingPanel.SetActive(true);
            if (eliminationModal != null) eliminationModal.SetActive(false);
        }

        public void ShowElimination()
        {
            if (eliminationModal != null) eliminationModal.SetActive(true);
        }

        public void ShowResults()
        {
            HideAll();
            if (resultsPanel != null) resultsPanel.SetActive(true);
        }

        public void ToggleHowToPlay(bool show)
        {
            if (howToPlayModal != null) howToPlayModal.SetActive(show);
        }

        public void ToggleSettings(bool show)
        {
            if (settingsModal != null) settingsModal.SetActive(show);
        }

        private void HideAll()
        {
            if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
            if (lobbyPanel != null) lobbyPanel.SetActive(false);
            if (gameplayPanel != null) gameplayPanel.SetActive(false);
            if (discussionPanel != null) discussionPanel.SetActive(false);
            if (votingPanel != null) votingPanel.SetActive(false);
            if (eliminationModal != null) eliminationModal.SetActive(false);
            if (resultsPanel != null) resultsPanel.SetActive(false);
            if (howToPlayModal != null) howToPlayModal.SetActive(false);
            if (settingsModal != null) settingsModal.SetActive(false);
        }

        private void HandleRoomJoined(string roomCode)
        {
            ShowLobby();
        }
    }
}

