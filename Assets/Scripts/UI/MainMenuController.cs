using UnityEngine;
using UnityEngine.UI;
using TMPro;
using LuckyNumber.Network;
using LuckyNumber.Audio;

namespace LuckyNumber.UI
{
    public class MainMenuController : MonoBehaviour
    {
        [Header("Inputs")]
        [SerializeField] private TMP_InputField playerNameInput;
        [SerializeField] private TMP_InputField roomCodeInput;
        [SerializeField] private TMP_InputField localIpInput;

        [Header("Character Selection")]
        [SerializeField] private Image characterPreviewImage;
        [SerializeField] private Sprite[] characterPortraits;
        [SerializeField] private Button prevCharButton;
        [SerializeField] private Button nextCharButton;

        [Header("Buttons")]
        [SerializeField] private Button createOnlineRoomButton;
        [SerializeField] private Button joinOnlineRoomButton;
        [SerializeField] private Button createLocalRoomButton;
        [SerializeField] private Button joinLocalRoomButton;
        [SerializeField] private Button howToPlayButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button exitButton;

        [Header("Feedback")]
        [SerializeField] private TMP_Text statusText;

        private int selectedCharIndex = 0;

        private void Start()
        {
            // Load saved preferences
            string defaultName = "Player_" + Random.Range(100, 999);
            string savedName = PlayerPrefs.GetString("PlayerName", defaultName);
            if (playerNameInput != null) playerNameInput.text = savedName;

            selectedCharIndex = PlayerPrefs.GetInt("SelectedCharacter", 0);
            UpdateCharacterPreview();

            // Setup button listeners
            if (prevCharButton != null) prevCharButton.onClick.AddListener(SelectPrevCharacter);
            if (nextCharButton != null) nextCharButton.onClick.AddListener(SelectNextCharacter);

            if (createOnlineRoomButton != null) createOnlineRoomButton.onClick.AddListener(OnCreateOnlineRoom);
            if (joinOnlineRoomButton != null) joinOnlineRoomButton.onClick.AddListener(OnJoinOnlineRoom);
            if (createLocalRoomButton != null) createLocalRoomButton.onClick.AddListener(OnCreateLocalRoom);
            if (joinLocalRoomButton != null) joinLocalRoomButton.onClick.AddListener(OnJoinLocalRoom);

            if (howToPlayButton != null) howToPlayButton.onClick.AddListener(() => {
                AudioManager.Instance?.PlayClick();
                LuckyUIManager.Instance?.ToggleHowToPlay(true);
            });

            if (settingsButton != null) settingsButton.onClick.AddListener(() => {
                AudioManager.Instance?.PlayClick();
                LuckyUIManager.Instance?.ToggleSettings(true);
            });

            if (exitButton != null) exitButton.onClick.AddListener(OnExitGame);

            if (NetworkLobbyManager.Instance != null)
            {
                NetworkLobbyManager.Instance.OnStatusChanged += UpdateStatus;
            }
        }

        private void OnDestroy()
        {
            if (NetworkLobbyManager.Instance != null)
            {
                NetworkLobbyManager.Instance.OnStatusChanged -= UpdateStatus;
            }
        }

        private void SavePlayerProfile()
        {
            string pName = playerNameInput != null && !string.IsNullOrWhiteSpace(playerNameInput.text)
                ? playerNameInput.text.Trim()
                : "Player_" + Random.Range(100, 999);

            PlayerPrefs.SetString("PlayerName", pName);
            PlayerPrefs.SetInt("SelectedCharacter", selectedCharIndex);
            PlayerPrefs.Save();
        }

        private void SelectPrevCharacter()
        {
            AudioManager.Instance?.PlayClick();
            if (characterPortraits != null && characterPortraits.Length > 0)
            {
                selectedCharIndex = (selectedCharIndex - 1 + characterPortraits.Length) % characterPortraits.Length;
                UpdateCharacterPreview();
            }
        }

        private void SelectNextCharacter()
        {
            AudioManager.Instance?.PlayClick();
            if (characterPortraits != null && characterPortraits.Length > 0)
            {
                selectedCharIndex = (selectedCharIndex + 1) % characterPortraits.Length;
                UpdateCharacterPreview();
            }
        }

        private void UpdateCharacterPreview()
        {
            if (characterPreviewImage != null && characterPortraits != null && selectedCharIndex < characterPortraits.Length)
            {
                characterPreviewImage.sprite = characterPortraits[selectedCharIndex];
            }
        }

        private async void OnCreateOnlineRoom()
        {
            AudioManager.Instance?.PlayClick();
            SavePlayerProfile();
            if (NetworkLobbyManager.Instance != null)
            {
                UpdateStatus("Creating online room via Relay...");
                await NetworkLobbyManager.Instance.CreateRelayRoomAsync(8);
            }
        }

        private async void OnJoinOnlineRoom()
        {
            AudioManager.Instance?.PlayClick();
            SavePlayerProfile();
            string code = roomCodeInput != null ? roomCodeInput.text : string.Empty;
            if (string.IsNullOrWhiteSpace(code))
            {
                UpdateStatus("Please enter a room code.");
                return;
            }

            if (NetworkLobbyManager.Instance != null)
            {
                UpdateStatus($"Joining room {code}...");
                await NetworkLobbyManager.Instance.JoinRelayRoomAsync(code);
            }
        }

        private void OnCreateLocalRoom()
        {
            AudioManager.Instance?.PlayClick();
            SavePlayerProfile();
            NetworkLobbyManager.Instance?.StartLocalHost();
        }

        private void OnJoinLocalRoom()
        {
            AudioManager.Instance?.PlayClick();
            SavePlayerProfile();
            string ip = localIpInput != null && !string.IsNullOrWhiteSpace(localIpInput.text) ? localIpInput.text : "127.0.0.1";
            NetworkLobbyManager.Instance?.JoinLocalClient(ip);
        }

        private void UpdateStatus(string message)
        {
            if (statusText != null) statusText.text = message;
        }

        private void OnExitGame()
        {
            AudioManager.Instance?.PlayClick();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}

