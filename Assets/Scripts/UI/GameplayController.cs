using System;
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
        private int[] localRoll = Array.Empty<int>();
        private PrivateRoll renderedRoll;
        private NetworkGameManager game;
        private ActionStamp renderedAction;
        private bool movePending;
        private bool abilityPending;
        private string privateFeedback;
        private float feedbackUntil;

        private void Awake()
        {
            SetupDiceListeners();
            SetupAbilityListeners();
        }

        private void OnEnable() => Update();

        private void OnDisable()
        {
            BindGame(null);
            renderedAction = default;
            renderedRoll = default;
            localRoll = Array.Empty<int>();
            selectedDieIndex = -1;
            movePending = false;
            abilityPending = false;
            privateFeedback = null;
        }

        private void BindGame(NetworkGameManager next)
        {
            if (ReferenceEquals(game, next)) return;
            if (!ReferenceEquals(game, null))
            {
                game.OnScoreUpdated -= UpdateScoreboard;
                game.LastCommittedMove.OnValueChanged -= HandleNewMoveCommitted;
                game.OnActionFeedback -= HandleActionFeedback;
            }
            game = next;
            if (game != null)
            {
                game.OnScoreUpdated += UpdateScoreboard;
                game.LastCommittedMove.OnValueChanged += HandleNewMoveCommitted;
                game.OnActionFeedback += HandleActionFeedback;
                UpdateScoreboard();
            }
        }

        private void Update()
        {
            var manager = NetworkGameManager.Instance;
            BindGame(manager != null && manager.IsSpawned ? manager : null);
            if (game == null) return;
            var stamp = game.ActionContext.Value;
            if (!renderedAction.Equals(stamp))
            {
                if (renderedAction.SessionId != stamp.SessionId || renderedAction.MatchId != stamp.MatchId)
                {
                    if (moveHistoryText != null) moveHistoryText.text = game.LastCommittedMove.Value.ToString();
                    privateFeedback = null;
                }
                renderedAction = stamp;
                movePending = false;
                abilityPending = false;
            }

            float t = game.PhaseTimer.Value;
            if (timerText != null) timerText.text = $"{Mathf.CeilToInt(t)}s";

            var local = LuckyNetworkPlayer.LocalPlayer;
            ulong activeId = game.ActivePlayerClientId.Value;
            bool playerTurn = game.CurrentGamePhase.Value == GamePhase.PlayerTurn;
            bool isMyTurn = playerTurn && local != null && local.IsAlive.Value && local.OwnerClientId == activeId;
            var roll = isMyTurn ? local.CurrentRoll : default;
            if (!roll.Stamp.Equals(stamp)) roll = default;
            if (!renderedRoll.Equals(roll)) RenderRoll(roll);

            if (dicePanel != null) dicePanel.SetActive(roll.HasDice);
            if (voluntarySkipButton != null)
                voluntarySkipButton.interactable = roll.HasDice && !movePending && local.RemainingSkips.Value > 0;

            if (turnBannerText != null)
            {
                if (!playerTurn)
                    turnBannerText.text = string.Empty;
                else if (isMyTurn)
                {
                    turnBannerText.text = roll.HasDice ? "YOUR TURN! Pick a die & operation" : "Waiting for your dice...";
                }
                else
                {
                    var activeP = game.GetPlayer(activeId);
                    turnBannerText.text = activeP == null ? "Waiting for the next turn..." : $"Waiting for {activeP.PlayerName.Value} to move...";
                }
                if (!string.IsNullOrEmpty(privateFeedback) && Time.unscaledTime < feedbackUntil)
                    turnBannerText.text += $"\n{privateFeedback}";
            }

            UpdateAbilityDrawer(local, playerTurn);
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
            if (movePending || index >= localRoll.Length) return;
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
                if (commitMoveButton != null) commitMoveButton.interactable = !movePending;
            }
            else
            {
                if (selectedOperationPreviewText != null) selectedOperationPreviewText.text = "Select a die...";
                if (commitMoveButton != null) commitMoveButton.interactable = false;
            }
        }

        private void OnCommitMove()
        {
            if (game == null || movePending || !renderedRoll.HasDice ||
                selectedDieIndex < 0 || selectedDieIndex >= localRoll.Length) return;
            movePending = true;
            UpdateMovePreview();
            game.SubmitMoveServerRpc(localRoll[selectedDieIndex], isAddition, renderedRoll.Stamp);
        }

        private void OnVoluntarySkip()
        {
            if (game == null || movePending || !renderedRoll.HasDice) return;
            movePending = true;
            UpdateMovePreview();
            game.RequestVoluntarySkipServerRpc(renderedRoll.Stamp);
        }

        private void RenderRoll(PrivateRoll roll)
        {
            renderedRoll = roll;
            localRoll = roll.HasDice ? new[] { roll.Die1, roll.Die2, roll.Die3 } : Array.Empty<int>();
            selectedDieIndex = -1;
            if (roll.HasDice) AudioManager.Instance?.PlayDiceRoll();
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
                    diceImages[i].sprite = diceFaceSprites[val - 1];
                }

                // Highlight selected
                var outline = diceImages[i].GetComponent<Outline>();
                if (outline != null) outline.enabled = (i == selectedDieIndex);
            }
        }

        private void UpdateScoreboard()
        {
            if (game == null) return;

            if (luckyTargetText != null) luckyTargetText.text = $"TARGET: {game.TargetLuckyNumber.Value}";
            if (currentScoreText != null) currentScoreText.text = $"SCORE: {game.CurrentScore.Value}";
            if (remainingMovesText != null)
            {
                int moves = game.RemainingMoves.Value;
                remainingMovesText.text = $"MOVES: {moves}";
                remainingMovesText.color = moves <= 3 ? Color.red : Color.white;
            }
            if (roundInfoText != null) roundInfoText.text = $"ROUND {game.CurrentRound.Value}";
        }

        private void HandleNewMoveCommitted(Unity.Collections.FixedString128Bytes prev, Unity.Collections.FixedString128Bytes next)
        {
            if (moveHistoryText != null && next.Length > 0)
            {
                moveHistoryText.text += $"\n• {next}";
            }
            UpdateScoreboard();
        }

        private void UpdateAbilityDrawer(LuckyNetworkPlayer localPlayer, bool playerTurn)
        {
            bool isImposter = localPlayer != null && localPlayer.HasSecretRole && localPlayer.LocalRole == PlayerRole.Imposter;

            if (imposterDrawer != null) imposterDrawer.SetActive(isImposter);
            if (!isImposter) return;

            // Populate target dropdown with living other players
            if (abilityTargetDropdown != null && abilityTargetDropdown.options.Count != game.GetAllLivingPlayers().Count)
            {
                abilityTargetDropdown.ClearOptions();
                var options = new List<string>();
                foreach (var p in game.GetAllLivingPlayers())
                {
                    options.Add($"{p.PlayerName.Value} (#{p.OwnerClientId})");
                }
                abilityTargetDropdown.AddOptions(options);
            }

            if (forcedBlockText != null) forcedBlockText.text = $"Forced Block ({localPlayer.ForcedBlockCharges})";
            if (luckyShiftText != null) luckyShiftText.text = $"Lucky Shift ({localPlayer.LuckyShiftCharges})";
            if (cursedDiceText != null) cursedDiceText.text = $"Cursed Dice ({localPlayer.CursedDiceCharges})";

            bool canUse = playerTurn && localPlayer.IsAlive.Value && !abilityPending &&
                localPlayer.LastAbilityRound != game.CurrentRound.Value;
            if (forcedBlockButton != null) forcedBlockButton.interactable = canUse && localPlayer.ForcedBlockCharges > 0;
            if (luckyShiftButton != null) luckyShiftButton.interactable = canUse && localPlayer.LuckyShiftCharges > 0;
            if (cursedDiceButton != null) cursedDiceButton.interactable = canUse && localPlayer.CursedDiceCharges > 0;
        }

        private void OnUseForcedBlock()
        {
            ulong targetId = GetSelectedDropdownTargetId();
            if (game == null || abilityPending || targetId == ulong.MaxValue) return;
            abilityPending = true;
            game.ActivateForcedBlockServerRpc(targetId, renderedAction);
        }

        private void OnUseLuckyShift()
        {
            int delta = (int)(luckyShiftSlider != null ? luckyShiftSlider.value : 1);
            if (delta == 0) delta = 1;

            if (game == null || abilityPending) return;
            abilityPending = true;
            game.ActivateLuckyShiftServerRpc(delta, renderedAction);
        }

        private void OnUseCursedDice()
        {
            ulong targetId = GetSelectedDropdownTargetId();
            if (game == null || abilityPending || targetId == ulong.MaxValue) return;
            abilityPending = true;
            game.ActivateCursedDiceServerRpc(targetId, renderedAction);
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

        private void HandleActionFeedback(ActionStamp stamp, MatchAction action, bool accepted, string message)
        {
            if (game == null || stamp.SessionId != renderedAction.SessionId || stamp.MatchId != renderedAction.MatchId) return;
            bool sameWindow = stamp.Equals(renderedAction);
            switch (action)
            {
                case MatchAction.Move:
                case MatchAction.Skip:
                    if (sameWindow && !accepted)
                    {
                        movePending = false;
                        UpdateMovePreview();
                    }
                    if (accepted) AudioManager.Instance?.PlayPositive();
                    break;
                case MatchAction.ForcedBlock:
                case MatchAction.LuckyShift:
                case MatchAction.CursedDice:
                    if (sameWindow && !accepted) abilityPending = false;
                    if (accepted) AudioManager.Instance?.PlayAbility();
                    break;
                default:
                    return;
            }
            if (sameWindow || accepted)
            {
                privateFeedback = message;
                feedbackUntil = Time.unscaledTime + 4f;
            }
        }
    }
}

