using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
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
        [SerializeField] private GameObject roleRevealPanel;
        [SerializeField] private TMP_Text roleNameText;
        [SerializeField] private TMP_Text roleObjectiveText;
        [SerializeField] private TMP_Text roleTimerText;
        [SerializeField] private Image roleIcon;
        [SerializeField] private Sprite normalRoleSprite;
        [SerializeField] private Sprite imposterRoleSprite;
        [SerializeField] private GameObject gameplayPanel;
        [SerializeField] private GameObject discussionPanel;
        [SerializeField] private GameObject votingPanel;
        [SerializeField] private GameObject eliminationModal;
        [SerializeField] private SpriteBurst ejectionEffect;
        [SerializeField] private GameObject resultsPanel;
        [SerializeField] private GameObject howToPlayModal;
        [SerializeField] private GameObject settingsModal;

        private NetworkGameManager game;
        private NetworkLobbyManager room;
        private bool inRoom;
        private ActionStamp presentedEjection;
        private GameObject menuFocus;
        private CanvasGroup menuControls;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            if (mainMenuPanel != null)
                menuControls = mainMenuPanel.GetComponent<CanvasGroup>();
        }

        private void Start()
        {
            SettingsModal.RestoreBindings();
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
            if (roleRevealPanel != null && roleRevealPanel.activeSelf)
                RefreshRoleReveal();
            if (game != null && eliminationModal != null && eliminationModal.activeSelf)
            {
                var outcome = game.LastVoteOutcome.Value;
                if ((outcome.Kind == VoteOutcomeKind.Ejection || outcome.Kind == VoteOutcomeKind.RandomEjection) &&
                    !presentedEjection.Equals(outcome.Stamp))
                {
                    presentedEjection = outcome.Stamp;
                    ejectionEffect?.Play();
                }
            }
        }

        private void LateUpdate()
        {
            var events = EventSystem.current;
            if (events == null) return;
            var selected = events.currentSelectedGameObject;
            if (selected != null && selected.activeInHierarchy) return;
            if (howToPlayModal != null && howToPlayModal.activeSelf) FocusPanel(howToPlayModal);
            else if (settingsModal != null && settingsModal.activeSelf) FocusPanel(settingsModal);
            else if (mainMenuPanel != null && mainMenuPanel.activeSelf) FocusPanel(mainMenuPanel);
            else if (lobbyPanel != null && lobbyPanel.activeSelf) FocusPanel(lobbyPanel);
            else if (votingPanel != null && votingPanel.activeSelf) FocusPanel(votingPanel);
            else if (resultsPanel != null && resultsPanel.activeSelf) FocusPanel(resultsPanel);
            else if (gameplayPanel != null && gameplayPanel.activeSelf) FocusPanel(gameplayPanel);
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
                    SetPanels(roleReveal: true);
                    RefreshRoleReveal();
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

        private void RefreshRoleReveal()
        {
            var local = LuckyNetworkPlayer.LocalPlayer;
            bool hasRole = game != null && game.CurrentGamePhase.Value == GamePhase.RoleReveal &&
                local != null && local.HasSecretRole;
            bool imposter = hasRole && local.LocalRole == PlayerRole.Imposter;
            if (roleIcon != null)
            {
                roleIcon.enabled = hasRole;
                roleIcon.sprite = imposter ? imposterRoleSprite : normalRoleSprite;
            }
            if (roleNameText != null)
                roleNameText.text = hasRole ? (imposter ? "IMPOSTER" : "NORMAL PLAYER") : "Assigning your role…";
            if (roleObjectiveText != null)
            {
                string objective = !hasRole ? string.Empty : imposter
                    ? "Stop the team from reaching the target. Block turns, curse dice, or shift the target."
                    : "Reach the target exactly or vote out every imposter.";
                if (imposter)
                {
                    foreach (ulong teammateId in local.KnownImposters)
                    {
                        var teammate = LuckyNetworkPlayer.FindSpawnedPlayer(teammateId);
                        if (teammate != null) objective += $"\nTeammate · {teammate.PlayerName.Value}";
                    }
                }
                roleObjectiveText.text = objective;
            }
            if (roleTimerText != null)
                roleTimerText.text = game != null ? $"{Mathf.CeilToInt(game.PhaseTimer.Value)}s" : string.Empty;
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
            SetPanels(elimination: true);
        }

        public void ShowResults()
        {
            SetPanels(results: true);
        }

        public void ToggleHowToPlay(bool show)
        {
            ToggleMenuModal(howToPlayModal, show);
        }

        public void ToggleSettings(bool show)
        {
            ToggleMenuModal(settingsModal, show);
        }

        private void ToggleMenuModal(GameObject modal, bool show)
        {
            if (modal == null) return;
            var events = EventSystem.current;
            if (show && events != null) menuFocus = events.currentSelectedGameObject;
            modal.SetActive(show);
            bool modalOpen = (howToPlayModal != null && howToPlayModal.activeSelf) ||
                (settingsModal != null && settingsModal.activeSelf);
            if (menuControls != null)
            {
                menuControls.interactable = !modalOpen;
                menuControls.blocksRaycasts = !modalOpen;
            }
            if (show) FocusPanel(modal);
            else if (!modalOpen && events != null && menuFocus != null && menuFocus.activeInHierarchy)
                events.SetSelectedGameObject(menuFocus);
        }

        private static void FocusPanel(GameObject panel)
        {
            var events = EventSystem.current;
            if (events == null) return;
            if (panel != null)
            {
                foreach (var selectable in panel.GetComponentsInChildren<Selectable>())
                {
                    if (!selectable.IsInteractable()) continue;
                    events.SetSelectedGameObject(selectable.gameObject);
                    return;
                }
            }
            events.SetSelectedGameObject(null);
        }

        private void SetPanels(bool menu = false, bool lobby = false, bool roleReveal = false, bool gameplay = false,
            bool discussion = false, bool voting = false, bool elimination = false, bool results = false)
        {
            if (mainMenuPanel != null) mainMenuPanel.SetActive(menu);
            if (lobbyPanel != null) lobbyPanel.SetActive(lobby);
            if (roleRevealPanel != null) roleRevealPanel.SetActive(roleReveal);
            if (gameplayPanel != null) gameplayPanel.SetActive(gameplay);
            if (discussionPanel != null) discussionPanel.SetActive(discussion);
            if (votingPanel != null) votingPanel.SetActive(voting);
            if (eliminationModal != null) eliminationModal.SetActive(elimination);
            if (resultsPanel != null) resultsPanel.SetActive(results);
            ToggleHowToPlay(false);
            ToggleSettings(false);
            FocusPanel(menu ? mainMenuPanel : lobby ? lobbyPanel : voting ? votingPanel : results ? resultsPanel : null);
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

