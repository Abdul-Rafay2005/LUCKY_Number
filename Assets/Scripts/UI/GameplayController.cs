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
    public class GameplayController : MonoBehaviour
    {
        [Header("Score & Target Header")]
        [SerializeField] private TMP_Text luckyTargetText;
        [SerializeField] private TMP_Text currentScoreText;
        [SerializeField] private TMP_Text remainingMovesText;
        [SerializeField] private TMP_Text roundInfoText;
        [SerializeField] private TMP_Text timerText;
        [SerializeField] private Image scoreboardImage;

        [Header("Turn Indicator")]
        [SerializeField] private TMP_Text turnBannerText;

        [Header("Private Dice Panel (Active Player Only)")]
        [SerializeField] private GameObject dicePanel;
        [SerializeField] private Button[] diceButtons; // 3 dice
        [SerializeField] private Image[] diceImages;
        [SerializeField] private Sprite[] diceFaceSprites; // 1 to 6
        [SerializeField] private Sprite cursedDiceSprite;
        [SerializeField] private Button addOperationButton;
        [SerializeField] private Button subtractOperationButton;
        [SerializeField] private Button voluntarySkipButton;
        [SerializeField] private Button commitMoveButton;
        [SerializeField] private TMP_Text selectedOperationPreviewText;

        [Header("Imposter Abilities Panel")]
        [SerializeField] private GameObject imposterDrawer;
        [SerializeField] private Button forcedBlockButton;
        [SerializeField] private TMP_Text forcedBlockText;
        [SerializeField] private Button luckyShiftButton;
        [SerializeField] private TMP_Text luckyShiftText;
        [SerializeField] private Slider luckyShiftSlider;
        [SerializeField] private TMP_Text luckyShiftDeltaText;
        [SerializeField] private Button cursedDiceButton;
        [SerializeField] private TMP_Text cursedDiceText;
        [SerializeField] private TMP_Dropdown abilityTargetDropdown;

        [Header("Move History Feed")]
        [SerializeField] private TMP_Text moveHistoryText;

        private int selectedDieIndex = -1;
        private bool isAddition = true;
        private int[] localRoll = new int[3];
        private bool isCurrentRollCursed = false;

        private void OnEnable()
        {
            SetupDiceListeners();
            SetupAbilityListeners();

            if (NetworkGameManager.Instance != null)
            {
                NetworkGameManager.Instance.OnPhaseChanged += HandlePhaseChanged;
                NetworkGameManager.Instance.OnScoreUpdated += UpdateScoreboard;
                NetworkGameManager.Instance.LastCommittedMove.OnValueChanged += HandleNewMoveCommitted;
            }

            var localPlayer = GetLocalPlayer();
            if (localPlayer != null)
            {
                localPlayer.OnPrivateDiceReceived += HandlePrivateDiceReceived;
                localPlayer.OnSecretRoleReceived += HandleSecretRoleReceived;
            }

            UpdateScoreboard();
        }

        private void OnDisable()
        {
            if (NetworkGameManager.Instance != null)
            {
                NetworkGameManager.Instance.OnPhaseChanged -= HandlePhaseChanged;
                NetworkGameManager.Instance.OnScoreUpdated -= UpdateScoreboard;
                NetworkGameManager.Instance.LastCommittedMove.OnValueChanged -= HandleNewMoveCommitted;
            }

            var localPlayer = GetLocalPlayer();
            if (localPlayer != null)
            {
                localPlayer.OnPrivateDiceReceived -= HandlePrivateDiceReceived;
                localPlayer.OnSecretRoleReceived -= HandleSecretRoleReceived;
            }
        }

        private void Update()
        {
            if (NetworkGameManager.Instance == null) return;

            // Update timer
            float t = NetworkGameManager.Instance.PhaseTimer.Value;
            if (timerText != null) timerText.text = $"{Mathf.CeilToInt(t)}s";

            // Update turn visibility
            ulong activeId = NetworkGameManager.Instance.ActivePlayerClientId.Value;
            bool isMyTurn = NetworkManager.Singleton != null && NetworkManager.Singleton.LocalClientId == activeId && NetworkGameManager.Instance.CurrentGamePhase.Value == GamePhase.PlayerTurn;

            if (dicePanel != null)
            {
                dicePanel.SetActive(isMyTurn);
            }

            if (turnBannerText != null)
            {
                if (isMyTurn)
                {
                    turnBannerText.text = "🎯 YOUR TURN! Pick a die & operation";
                }
                else
                {
                    var activeP = NetworkGameManager.Instance.GetPlayer(activeId);
                    string name = activeP != null ? activeP.PlayerName.Value.ToString() : $"Player {activeId}";
                    turnBannerText.text = $"Waiting for {name} to move...";
                }
            }

            UpdateAbilityDrawer();
        }

        private void SetupDiceListeners()
        {
            for (int i = 0; i < diceButtons.Length; i++)
            {
                int index = i;
                diceButtons[i].onClick.AddListener(() => SelectDie(index));
            }

            if (addOperationButton != null) addOperationButton.onClick.AddListener(() => SetOperation(true));
            if (subtractOperationButton != null) subtractOperationButton.onClick.AddListener(() => SetOperation(false));
            if (voluntarySkipButton != null) voluntarySkipButton.onClick.AddListener(OnVoluntarySkip);
            if (commitMoveButton != null) commitMoveButton.onClick.AddListener(OnCommitMove);

            SetOperation(true);
        }

        private void SetupAbilityListeners()
        {
            if (forcedBlockButton != null) forcedBlockButton.onClick.AddListener(OnUseForcedBlock);
            if (luckyShiftButton != null) luckyShiftButton.onClick.AddListener(OnUseLuckyShift);
            if (cursedDiceButton != null) cursedDiceButton.onClick.AddListener(OnUseCursedDice);

            if (luckyShiftSlider != null)
            {
                luckyShiftSlider.minValue = -5;
                luckyShiftSlider.maxValue = 5;
                luckyShiftSlider.wholeNumbers = true;
                luckyShiftSlider.onValueChanged.AddListener(val => {
                    int delta = (int)val;
                    if (delta == 0) delta = 1; // Non-zero
                    if (luckyShiftDeltaText != null) luckyShiftDeltaText.text = $"Shift: {delta:+#;-#;0}";
                });
            }
        }

        private void SelectDie(int index)
        {
            AudioManager.Instance?.PlayClick();
            selectedDieIndex = index;
            UpdateDiceVisuals();
            UpdateMovePreview();
        }

        private void SetOperation(bool addition)
        {
            AudioManager.Instance?.PlayClick();
            isAddition = addition;
            if (addOperationButton != null) addOperationButton.image.color = addition ? Color.yellow : Color.white;
            if (subtractOperationButton != null) subtractOperationButton.image.color = !addition ? Color.yellow : Color.white;
            UpdateMovePreview();
        }

        private void UpdateMovePreview()
        {
            if (selectedDieIndex >= 0 && selectedDieIndex < localRoll.Length)
            {
                int val = localRoll[selectedDieIndex];
                string sign = isAddition ? "+" : "-";
                if (selectedOperationPreviewText != null)
                {
                    selectedOperationPreviewText.text = $"Selected: {sign}{val}";
                }
                if (commitMoveButton != null) commitMoveButton.interactable = true;
            }
            else
            {
                if (selectedOperationPreviewText != null) selectedOperationPreviewText.text = "Select a die...";
                if (commitMoveButton != null) commitMoveButton.interactable = false;
            }
        }

        private void OnCommitMove()
        {
            if (selectedDieIndex < 0 || selectedDieIndex >= localRoll.Length) return;

            int dieValue = localRoll[selectedDieIndex];
            AudioManager.Instance?.PlayPositive();

            NetworkGameManager.Instance?.SubmitMoveServerRpc(dieValue, isAddition);

            selectedDieIndex = -1;
            if (dicePanel != null) dicePanel.SetActive(false);
        }

        private void OnVoluntarySkip()
        {
            AudioManager.Instance?.PlayClick();
            NetworkGameManager.Instance?.RequestVoluntarySkipServerRpc();
            selectedDieIndex = -1;
            if (dicePanel != null) dicePanel.SetActive(false);
        }

        private void HandlePrivateDiceReceived(int[] dice, bool cursed)
        {
            localRoll = dice;
            isCurrentRollCursed = cursed;
            selectedDieIndex = -1;

            AudioManager.Instance?.PlayDiceRoll();
            UpdateDiceVisuals();
            UpdateMovePreview();
        }

        private void UpdateDiceVisuals()
        {
            for (int i = 0; i < diceImages.Length && i < localRoll.Length; i++)
            {
                int val = localRoll[i];
                if (val >= 1 && val <= 6 && diceFaceSprites != null && val <= diceFaceSprites.Length)
                {
                    diceImages[i].sprite = (isCurrentRollCursed && cursedDiceSprite != null)
                        ? cursedDiceSprite
                        : diceFaceSprites[val - 1];
                }

                // Highlight selected
                var outline = diceImages[i].GetComponent<Outline>();
                if (outline != null) outline.enabled = (i == selectedDieIndex);
            }
        }

        private void UpdateScoreboard()
        {
            if (NetworkGameManager.Instance == null) return;

            if (luckyTargetText != null) luckyTargetText.text = $"TARGET: {NetworkGameManager.Instance.TargetLuckyNumber.Value}";
            if (currentScoreText != null) currentScoreText.text = $"SCORE: {NetworkGameManager.Instance.CurrentScore.Value}";
            if (remainingMovesText != null)
            {
                int moves = NetworkGameManager.Instance.RemainingMoves.Value;
                remainingMovesText.text = $"MOVES: {moves}";
                remainingMovesText.color = moves <= 3 ? Color.red : Color.white;
            }
            if (roundInfoText != null) roundInfoText.text = $"ROUND {NetworkGameManager.Instance.CurrentRound.Value}";
        }

        private void HandleNewMoveCommitted(Unity.Collections.FixedString128Bytes prev, Unity.Collections.FixedString128Bytes next)
        {
            if (moveHistoryText != null)
            {
                moveHistoryText.text += $"\n• {next}";
            }
            UpdateScoreboard();
        }

        private void HandleSecretRoleReceived(PlayerRole role)
        {
            UpdateAbilityDrawer();
        }

        private void UpdateAbilityDrawer()
        {
            var localPlayer = GetLocalPlayer();
            bool isImposter = localPlayer != null && localPlayer.LocalRole == PlayerRole.Imposter;

            if (imposterDrawer != null) imposterDrawer.SetActive(isImposter);
            if (!isImposter) return;

            // Populate target dropdown with living other players
            if (abilityTargetDropdown != null && abilityTargetDropdown.options.Count != NetworkGameManager.Instance?.GetAllLivingPlayers().Count)
            {
                abilityTargetDropdown.ClearOptions();
                var options = new List<string>();
                foreach (var p in NetworkGameManager.Instance.GetAllLivingPlayers())
                {
                    if (p.OwnerClientId != NetworkManager.Singleton.LocalClientId)
                    {
                        options.Add($"{p.PlayerName.Value} (#{p.OwnerClientId})");
                    }
                }
                abilityTargetDropdown.AddOptions(options);
            }

            if (forcedBlockText != null) forcedBlockText.text = $"Forced Block ({localPlayer.ForcedBlockCharges})";
            if (luckyShiftText != null) luckyShiftText.text = $"Lucky Shift ({localPlayer.LuckyShiftCharges})";
            if (cursedDiceText != null) cursedDiceText.text = $"Cursed Dice ({localPlayer.CursedDiceCharges})";

            if (forcedBlockButton != null) forcedBlockButton.interactable = localPlayer.ForcedBlockCharges > 0;
            if (luckyShiftButton != null) luckyShiftButton.interactable = localPlayer.LuckyShiftCharges > 0;
            if (cursedDiceButton != null) cursedDiceButton.interactable = localPlayer.CursedDiceCharges > 0;
        }

        private void OnUseForcedBlock()
        {
            ulong targetId = GetSelectedDropdownTargetId();
            if (targetId == ulong.MaxValue) return;

            AudioManager.Instance?.PlayAbility();
            NetworkGameManager.Instance?.ActivateForcedBlockServerRpc(targetId);
        }

        private void OnUseLuckyShift()
        {
            int delta = (int)(luckyShiftSlider != null ? luckyShiftSlider.value : 1);
            if (delta == 0) delta = 1;

            AudioManager.Instance?.PlayAbility();
            NetworkGameManager.Instance?.ActivateLuckyShiftServerRpc(delta);
        }

        private void OnUseCursedDice()
        {
            ulong targetId = GetSelectedDropdownTargetId();
            if (targetId == ulong.MaxValue) return;

            AudioManager.Instance?.PlayAbility();
            NetworkGameManager.Instance?.ActivateCursedDiceServerRpc(targetId);
        }

        private ulong GetSelectedDropdownTargetId()
        {
            if (abilityTargetDropdown == null || abilityTargetDropdown.options.Count == 0) return ulong.MaxValue;
            string selectedText = abilityTargetDropdown.options[abilityTargetDropdown.value].text;
            int hashIndex = selectedText.IndexOf('#');
            if (hashIndex >= 0 && ulong.TryParse(selectedText.Substring(hashIndex + 1).Trim(')'), out ulong id))
            {
                return id;
            }
            return ulong.MaxValue;
        }

        private void HandlePhaseChanged(GamePhase phase)
        {
            switch (phase)
            {
                case GamePhase.Discussion:
                    LuckyUIManager.Instance?.ShowDiscussion();
                    break;
                case GamePhase.Voting:
                    LuckyUIManager.Instance?.ShowVoting();
                    break;
                case GamePhase.Elimination:
                    LuckyUIManager.Instance?.ShowElimination();
                    break;
                case GamePhase.MatchEnded:
                    LuckyUIManager.Instance?.ShowResults();
                    break;
                case GamePhase.PlayerTurn:
                    LuckyUIManager.Instance?.ShowGameplay();
                    break;
            }
        }

        private LuckyNetworkPlayer GetLocalPlayer()
        {
            return NetworkManager.Singleton?.LocalClient?.PlayerObject?.GetComponent<LuckyNetworkPlayer>();
        }
    }
}

