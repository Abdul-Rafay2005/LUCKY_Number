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

        private NetworkGameManager game;
        private NetworkLobbyManager room;
        private bool inRoom;

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
            Update();
        }

        private void Update()
        {
            var nextRoom = NetworkLobbyManager.Instance;
            if (!ReferenceEquals(room, nextRoom))
            {
                BindRoom(nextRoom);
            }
            var next = NetworkGameManager.Instance;
            BindGame(next != null && next.IsSpawned ? next : null);
        }

        private void BindRoom(NetworkLobbyManager next)
        {
            if (!ReferenceEquals(room, null))
            {
                room.OnRoomJoined -= HandleRoomJoined;
                room.OnLeftRoom -= HandleRoomLeft;
            }
            room = next;
            if (room != null)
            {
                room.OnRoomJoined += HandleRoomJoined;
                room.OnLeftRoom += HandleRoomLeft;
                if (room.State == RoomConnectionState.InRoom)
                    HandleRoomJoined(room.CurrentRoomCode);
            }
            else if (inRoom)
            {
                HandleRoomLeft();
            }
        }

        private void BindGame(NetworkGameManager next)
        {
            if (ReferenceEquals(game, next)) return;
            if (!ReferenceEquals(game, null))
                game.OnPhaseChanged -= HandlePhaseChanged;
            game = next;
            if (game == null) return;
            game.OnPhaseChanged += HandlePhaseChanged;
            HandlePhaseChanged(game.CurrentGamePhase.Value);
        }

        private void HandlePhaseChanged(GamePhase newPhase)
        {
            if (!inRoom) return;
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
            BindRoom(null);
            BindGame(null);
            if (Instance == this) Instance = null;
        }

        public void ShowMainMenu()
        {
            SetPanels(menu: true);
        }

        public void ShowLobby()
        {
            SetPanels(lobby: true);
        }

        public void ShowGameplay()
        {
            SetPanels(gameplay: true);
        }

        public void ShowDiscussion()
        {
            SetPanels(gameplay: true, discussion: true);
        }

        public void ShowVoting()
        {
            SetPanels(gameplay: true, voting: true);
        }

        public void ShowElimination()
        {
            SetPanels(gameplay: true, voting: true, elimination: true);
        }

        public void ShowResults()
        {
            SetPanels(results: true);
        }

        public void ToggleHowToPlay(bool show)
        {
            if (howToPlayModal != null) howToPlayModal.SetActive(show);
        }

        public void ToggleSettings(bool show)
        {
            if (settingsModal != null) settingsModal.SetActive(show);
        }

        private void SetPanels(bool menu = false, bool lobby = false, bool gameplay = false,
            bool discussion = false, bool voting = false, bool elimination = false, bool results = false)
        {
            if (mainMenuPanel != null) mainMenuPanel.SetActive(menu);
            if (lobbyPanel != null) lobbyPanel.SetActive(lobby);
            if (gameplayPanel != null) gameplayPanel.SetActive(gameplay);
            if (discussionPanel != null) discussionPanel.SetActive(discussion);
            if (votingPanel != null) votingPanel.SetActive(voting);
            if (eliminationModal != null) eliminationModal.SetActive(elimination);
            if (resultsPanel != null) resultsPanel.SetActive(results);
            ToggleHowToPlay(false);
            ToggleSettings(false);
        }

        private void HandleRoomJoined(string roomCode)
        {
            inRoom = true;
            HandlePhaseChanged(game != null ? game.CurrentGamePhase.Value : GamePhase.Lobby);
        }

        private void HandleRoomLeft()
        {
            inRoom = false;
            ShowMainMenu();
        }
    }
}

