using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;
using Unity.Collections;
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
        [SerializeField] private SpriteBurst targetEffect;
        [SerializeField] private TMP_Text[] publicRosterSlots;
        [SerializeField] private Image[] publicRosterPortraits;
        [SerializeField] private Sprite[] characterPortraits;

        [Header("Turn Indicator")]
        [SerializeField] private TMP_Text turnBannerText;

        [Header("Private Dice Panel (Active Player Only)")]
        [SerializeField] private GameObject dicePanel;
        [SerializeField] private Button[] diceButtons; // 3 dice
        [SerializeField] private Image[] diceImages;
        [SerializeField] private Sprite[] diceFaceSprites; // 1 to 6
        [SerializeField] private Sprite[] diceRollFrames;
        [SerializeField] private Sprite cursedDiceSprite;
        [SerializeField] private Image privateCurseIcon;
        [SerializeField] private SpriteBurst dieSelectionEffect;
        [SerializeField] private Button addOperationButton;
        [SerializeField] private Button subtractOperationButton;
        [SerializeField] private Button voluntarySkipButton;
        [SerializeField] private Button commitMoveButton;
        [SerializeField] private TMP_Text selectedOperationPreviewText;

        [Header("Imposter Abilities Panel")]
        [SerializeField] private GameObject imposterDrawer;
        [SerializeField] private TMP_Text abilityStatusText;
        [SerializeField] private Button forcedBlockButton;
        [SerializeField] private TMP_Text forcedBlockText;
        [SerializeField] private Button luckyShiftButton;
        [SerializeField] private TMP_Text luckyShiftText;
        [SerializeField] private TMP_Dropdown luckyShiftDropdown;
        [SerializeField] private TMP_Text luckyShiftDeltaText;
        [SerializeField] private Button cursedDiceButton;
        [SerializeField] private TMP_Text cursedDiceText;
        [SerializeField] private TMP_Dropdown abilityTargetDropdown;

        [Header("Move History Feed")]
        [SerializeField] private TMP_Text moveHistoryText;
        [SerializeField] private ScrollRect moveHistoryScroll;

        private int selectedDieIndex = -1;
        private bool isAddition = true;
        private int[] localRoll = Array.Empty<int>();
        private PrivateRoll renderedRoll;
        private NetworkGameManager game;
        private ActionStamp renderedAction;
        private bool movePending;
        private bool abilityPending;
        private bool abilityAccepted;
        private int expectedAbilityUses;
        private readonly List<ulong> abilityTargetIds = new List<ulong>();
        private readonly List<int> shiftDeltas = new List<int>();
        private readonly List<ulong> eligibleTargetIds = new List<ulong>();
        private readonly List<string> eligibleTargetLabels = new List<string>();
        private string privateFeedback;
        private float feedbackUntil;
        private TMP_Text skipLabel;
        private float rollStarted = float.NegativeInfinity;
        private float selectionStarted = float.NegativeInfinity;
        private uint? presentedShift;
        private readonly FixedString64Bytes[] rosterNames = new FixedString64Bytes[8];
        private readonly byte[] rosterStates = new byte[8];

        private void Awake()
        {
            SetupDiceListeners();
            SetupAbilityListeners();
            if (voluntarySkipButton != null) skipLabel = voluntarySkipButton.GetComponentInChildren<TMP_Text>(true);
        }

        private void OnEnable() => Update();

        private void OnDisable()
        {
            BindGame(null);
            renderedAction = default;
            renderedRoll = default;
            localRoll = Array.Empty<int>();
            selectedDieIndex = -1;
            rollStarted = selectionStarted = float.NegativeInfinity;
            movePending = false;
            abilityPending = false;
            abilityAccepted = false;
            privateFeedback = null;
            abilityTargetIds.Clear();
            if (abilityTargetDropdown != null) abilityTargetDropdown.ClearOptions();
            if (dicePanel != null) dicePanel.SetActive(false);
            if (imposterDrawer != null) imposterDrawer.SetActive(false);
            if (moveHistoryText != null) moveHistoryText.text = string.Empty;
        }

        private void BindGame(NetworkGameManager next)
        {
            if (ReferenceEquals(game, next)) return;
            if (!ReferenceEquals(game, null))
            {
                game.OnScoreUpdated -= UpdateScoreboard;
                game.OnPublicHistoryChanged -= HydratePublicHistory;
                game.OnActionFeedback -= HandleActionFeedback;
            }
            game = next;
            if (game != null)
            {
                game.OnScoreUpdated += UpdateScoreboard;
                game.OnPublicHistoryChanged += HydratePublicHistory;
                game.OnActionFeedback += HandleActionFeedback;
                ConfigureShiftOptions();
                UpdateScoreboard();
                presentedShift = null;
                HydratePublicHistory(false);
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
                    HydratePublicHistory(false);
                    privateFeedback = null;
                }
                renderedAction = stamp;
                movePending = false;
                abilityPending = false;
                abilityAccepted = false;
            }

            float t = game.PhaseTimer.Value;
            if (timerText != null) timerText.text = $"{Mathf.CeilToInt(t)}s";

            var local = LuckyNetworkPlayer.LocalPlayer;
            ulong activeId = game.ActivePlayerClientId.Value;
            bool playerTurn = game.CurrentGamePhase.Value == GamePhase.PlayerTurn;
            UpdatePublicRoster(playerTurn, activeId);
            bool isMyTurn = playerTurn && local != null && local.IsAlive.Value && local.OwnerClientId == activeId;
            var roll = isMyTurn ? local.CurrentRoll : default;
            if (!roll.Stamp.Equals(stamp)) roll = default;
            if (!renderedRoll.Equals(roll)) RenderRoll(roll);

            if (dicePanel != null) dicePanel.SetActive(roll.HasDice);
            if (roll.HasDice) AnimateDice();
            if (voluntarySkipButton != null && roll.HasDice)
            {
                int skips = local.RemainingSkips.Value;
                voluntarySkipButton.interactable = !movePending && skips > 0;
                if (skipLabel != null) skipLabel.text = $"SKIP ({skips})";
            }

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

            if (addOperationButton != null) addOperationButton.onClick.AddListener(() => SelectOperation(true));
            if (subtractOperationButton != null) subtractOperationButton.onClick.AddListener(() => SelectOperation(false));
            if (voluntarySkipButton != null) voluntarySkipButton.onClick.AddListener(OnVoluntarySkip);
            if (commitMoveButton != null) commitMoveButton.onClick.AddListener(OnCommitMove);

            SetOperation(true);
        }

        private void SetupAbilityListeners()
        {
            if (forcedBlockButton != null) forcedBlockButton.onClick.AddListener(OnUseForcedBlock);
            if (luckyShiftButton != null) luckyShiftButton.onClick.AddListener(OnUseLuckyShift);
            if (cursedDiceButton != null) cursedDiceButton.onClick.AddListener(OnUseCursedDice);

            if (luckyShiftDropdown != null)
                luckyShiftDropdown.onValueChanged.AddListener(UpdateShiftPreview);
        }

        private void ConfigureShiftOptions()
        {
            if (luckyShiftDropdown == null) return;
            shiftDeltas.Clear();
            var labels = new List<string>();
            for (int delta = game.Settings.minShiftDelta; delta <= game.Settings.maxShiftDelta; delta++)
            {
                if (delta == 0) continue;
                shiftDeltas.Add(delta);
                labels.Add(delta.ToString("+#;-#;0"));
            }
            luckyShiftDropdown.ClearOptions();
            luckyShiftDropdown.AddOptions(labels);
            luckyShiftDropdown.SetValueWithoutNotify(Mathf.Max(0, shiftDeltas.IndexOf(1)));
            UpdateShiftPreview(luckyShiftDropdown.value);
        }

        private void SelectDie(int index)
        {
            if (movePending || index >= localRoll.Length) return;
            AudioManager.Instance?.PlaySelect();
            selectedDieIndex = index;
            selectionStarted = Time.unscaledTime;
            if (dieSelectionEffect != null)
            {
                var effectRect = (RectTransform)dieSelectionEffect.transform;
                var dieRect = diceImages[index].rectTransform;
                effectRect.anchorMin = effectRect.anchorMax = dieRect.anchorMin;
                effectRect.anchoredPosition = dieRect.anchoredPosition;
                dieSelectionEffect.Play();
            }
            UpdateDiceVisuals();
            UpdateMovePreview();
        }

        private void SelectOperation(bool addition)
        {
            if (movePending) return;
            AudioManager.Instance?.PlayClick();
            SetOperation(addition);
        }

        private void SetOperation(bool addition)
        {
            isAddition = addition;
            StyleOperation(addOperationButton, addition, "+ ADD");
            StyleOperation(subtractOperationButton, !addition, "− SUB");
            UpdateMovePreview();
        }

        private static void StyleOperation(Button button, bool selected, string caption)
        {
            if (button == null) return;
            button.image.color = selected ? new Color32(247, 205, 101, 255) : new Color32(27, 86, 103, 255);
            var label = button.GetComponentInChildren<TMP_Text>(true);
            if (label == null) return;
            label.color = selected ? new Color32(17, 32, 42, 255) : Color.white;
            label.text = selected ? $"[{caption}]" : caption;
        }

        private void UpdateMovePreview()
        {
            foreach (var die in diceButtons)
                die.interactable = !movePending;
            if (addOperationButton != null) addOperationButton.interactable = !movePending;
            if (subtractOperationButton != null) subtractOperationButton.interactable = !movePending;
            if (voluntarySkipButton != null && movePending) voluntarySkipButton.interactable = false;
            if (selectedDieIndex >= 0 && selectedDieIndex < localRoll.Length)
            {
                int val = localRoll[selectedDieIndex];
                string sign = isAddition ? "+" : "-";
                if (selectedOperationPreviewText != null)
                {
                    int predictedScore = game.CurrentScore.Value + (isAddition ? val : -val);
                    selectedOperationPreviewText.text = movePending ? "Sending action…" : $"{sign}{val}  →  Score {predictedScore}";
                }
                if (commitMoveButton != null) commitMoveButton.interactable = !movePending;
            }
            else
            {
                if (selectedOperationPreviewText != null)
                    selectedOperationPreviewText.text = movePending ? "Sending skip…" : "Select a die…";
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
            rollStarted = Time.unscaledTime;
            selectionStarted = float.NegativeInfinity;
            if (privateCurseIcon != null)
            {
                privateCurseIcon.sprite = cursedDiceSprite;
                privateCurseIcon.enabled = roll.HasDice && roll.IsCursed;
            }
            SetOperation(true);
            if (roll.HasDice) AudioManager.Instance?.PlayDiceRoll();
            UpdateDiceVisuals();
            UpdateMovePreview();
        }

        private void UpdateDiceVisuals()
        {
            for (int i = 0; i < diceImages.Length && i < localRoll.Length; i++)
            {
                int val = localRoll[i];
                var label = diceImages[i].GetComponentInChildren<TMP_Text>(true);
                if (label != null) label.text = i == selectedDieIndex ? $"[{val}]" : val.ToString();
                if (val >= 1 && val <= 6 && diceFaceSprites != null && val <= diceFaceSprites.Length)
                {
                    diceImages[i].sprite = diceFaceSprites[val - 1];
                }

                // Highlight selected
                var outline = diceImages[i].GetComponent<Outline>();
                if (outline != null) outline.enabled = (i == selectedDieIndex);
            }
        }

        private void AnimateDice()
        {
            float elapsed = Time.unscaledTime - rollStarted;
            float selection = Mathf.Clamp01((Time.unscaledTime - selectionStarted) / .18f);
            for (int i = 0; i < diceImages.Length; i++)
            {
                var image = diceImages[i];
                float rollPulse = Mathf.Sin(Mathf.Clamp01(elapsed / .21f) * Mathf.PI);
                float selectPulse = i == selectedDieIndex ? Mathf.Sin(selection * Mathf.PI) : 0f;
                image.rectTransform.localScale = Vector3.one * (1f + .09f * rollPulse + .08f * selectPulse);
                int value = localRoll[i];
                var face = diceFaceSprites[value - 1];
                if (elapsed < .21f && diceRollFrames is { Length: > 0 })
                {
                    int frame = ((int)(elapsed * 28) + i) % diceRollFrames.Length;
                    face = diceRollFrames[frame] ?? face;
                }
                image.sprite = face;
            }
        }

        private void UpdatePublicRoster(bool playerTurn, ulong activeId)
        {
            if (publicRosterSlots == null || publicRosterSlots.Length == 0) return;
            int slot = 0;
            foreach (var player in LuckyNetworkPlayer.SpawnedPlayers.Values)
            {
                if (slot == publicRosterSlots.Length) break;
                bool active = playerTurn && player.OwnerClientId == activeId;
                var name = player.PlayerName.Value;
                byte state = active ? (byte)3 : player.IsAlive.Value ? (byte)2 : (byte)1;
                var portrait = publicRosterPortraits[slot];
                int character = player.CharacterIndex.Value;
                portrait.sprite = character >= 0 && character < characterPortraits.Length ? characterPortraits[character] : null;
                portrait.enabled = portrait.sprite != null;
                portrait.color = state == 1 ? new Color32(126, 139, 141, 255) : Color.white;
                portrait.rectTransform.localScale = active ? Vector3.one * 1.17f : Vector3.one;
                if (rosterStates[slot] != state || !rosterNames[slot].Equals(name))
                {
                    rosterStates[slot] = state;
                    rosterNames[slot] = name;
                    var label = publicRosterSlots[slot];
                    string compactName = PlayerNameLabel.Compact(name.ToString(), 15);
                    label.text = active ? $"> {compactName}" : state == 2 ? compactName : $"{compactName} (Out)";
                    label.color = active ? new Color32(247, 205, 118, 255) :
                        state == 2 ? new Color32(250, 237, 209, 255) : new Color32(173, 184, 187, 255);
                }
                slot++;
            }
            for (; slot < publicRosterSlots.Length; slot++)
            {
                if (rosterStates[slot] == 0) continue;
                rosterStates[slot] = 0;
                publicRosterSlots[slot].text = string.Empty;
                publicRosterPortraits[slot].enabled = false;
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
                remainingMovesText.color = moves <= 3 ? new Color32(255, 147, 133, 255) : new Color32(250, 237, 209, 255);
            }
            if (roundInfoText != null) roundInfoText.text = $"ROUND {game.CurrentRound.Value}";
            UpdateMovePreview();
            UpdateShiftPreview(luckyShiftDropdown != null ? luckyShiftDropdown.value : -1);
        }

        private void HydratePublicHistory() => HydratePublicHistory(true);

        private void HydratePublicHistory(bool animate)
        {
            if (moveHistoryText == null || game == null) return;
            bool followLatest = moveHistoryScroll != null && (string.IsNullOrEmpty(moveHistoryText.text) ||
                moveHistoryScroll.verticalNormalizedPosition <= 0.01f);
            var lines = new List<string>(game.PublicHistory.Count);
            uint? latestShift = null;
            foreach (var entry in game.PublicHistory)
            {
                if (entry.Kind == PublicEventKind.TargetShift) latestShift = entry.Sequence;
                string name = entry.ActorName.ToString();
                string line = entry.Kind switch
                {
                    PublicEventKind.NumberMove => $"{name} played {entry.SignedValue:+#;-#;0} (Score: {entry.ResultingScore}, Moves: {entry.RemainingMoves})",
                    PublicEventKind.Skip => $"{name} skipped (Score: {entry.ResultingScore}, Moves: {entry.RemainingMoves})",
                    PublicEventKind.TargetShift => $"Target shifted by {entry.SignedValue:+#;-#;0} to {entry.ResultingTarget}",
                    _ => name
                };
                lines.Add($"R{entry.Round}  {line}");
            }
            if (animate && latestShift.HasValue && latestShift != presentedShift)
            {
                targetEffect?.Play();
                AudioManager.Instance?.PlayTargetChanged();
            }
            presentedShift = latestShift;
            moveHistoryText.text = string.Join("\n", lines);
            if (followLatest && moveHistoryScroll.isActiveAndEnabled)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(moveHistoryScroll.content);
                moveHistoryScroll.verticalNormalizedPosition = 0;
            }
        }

        private void UpdateAbilityDrawer(LuckyNetworkPlayer localPlayer, bool playerTurn)
        {
            bool isImposter = playerTurn && localPlayer != null && localPlayer.IsAlive.Value &&
                localPlayer.HasSecretRole && localPlayer.LocalRole == PlayerRole.Imposter;

            if (imposterDrawer != null) imposterDrawer.SetActive(isImposter);
            if (!isImposter) return;

            RefreshAbilityTargets(localPlayer);
            if (abilityAccepted && localPlayer.AbilityUsesThisRound >= expectedAbilityUses)
                abilityAccepted = false;

            if (forcedBlockText != null) forcedBlockText.text = $"Forced Block ({localPlayer.ForcedBlockCharges})";
            if (luckyShiftText != null) luckyShiftText.text = $"Lucky Shift ({localPlayer.LuckyShiftCharges})";
            if (cursedDiceText != null) cursedDiceText.text = $"Cursed Dice ({localPlayer.CursedDiceCharges})";

            bool canUse = !abilityPending && !abilityAccepted &&
                localPlayer.AbilityUsesThisRound < game.Settings.abilityActivationsPerImposterPerRound;
            if (abilityStatusText != null)
                abilityStatusText.text = abilityPending ? "Sending ability…" : canUse ? "SABOTAGE" : "Ability used this round";
            bool hasTarget = GetSelectedDropdownTargetId() != ulong.MaxValue;
            if (forcedBlockButton != null) forcedBlockButton.interactable = canUse && hasTarget && localPlayer.ForcedBlockCharges > 0;
            if (luckyShiftButton != null) luckyShiftButton.interactable = canUse && GetSelectedShiftDelta() != 0 &&
                (long)game.TargetLuckyNumber.Value + GetSelectedShiftDelta() != game.CurrentScore.Value && localPlayer.LuckyShiftCharges > 0;
            if (cursedDiceButton != null) cursedDiceButton.interactable = canUse && hasTarget && localPlayer.CursedDiceCharges > 0;
            if (abilityTargetDropdown != null) abilityTargetDropdown.interactable = canUse && hasTarget;
            if (luckyShiftDropdown != null) luckyShiftDropdown.interactable = canUse && localPlayer.LuckyShiftCharges > 0;
        }

        private void RefreshAbilityTargets(LuckyNetworkPlayer localPlayer)
        {
            if (abilityTargetDropdown == null) return;
            eligibleTargetIds.Clear();
            eligibleTargetLabels.Clear();
            int round = game.CurrentRound.Value;
            foreach (var player in LuckyNetworkPlayer.SpawnedPlayers.Values)
            {
                if (!player.IsAlive.Value || player.LastScheduledRound.Value >= round ||
                    player.OwnerClientId == game.ActivePlayerClientId.Value) continue;
                eligibleTargetIds.Add(player.OwnerClientId);
                string name = PlayerNameLabel.Compact(player.PlayerName.Value.ToString(), 18);
                eligibleTargetLabels.Add(player == localPlayer ? $"{name} (You)" : name);
            }
            bool changed = eligibleTargetIds.Count != abilityTargetIds.Count;
            for (int i = 0; !changed && i < eligibleTargetIds.Count; i++)
                changed = eligibleTargetIds[i] != abilityTargetIds[i] || eligibleTargetLabels[i] != abilityTargetDropdown.options[i].text;
            if (!changed)
            {
                if (abilityTargetIds.Count == 0)
                    abilityTargetDropdown.captionText.text = "No upcoming players";
                return;
            }

            ulong previous = GetSelectedDropdownTargetId();
            abilityTargetIds.Clear();
            abilityTargetIds.AddRange(eligibleTargetIds);
            abilityTargetDropdown.Hide();
            abilityTargetDropdown.ClearOptions();
            abilityTargetDropdown.AddOptions(eligibleTargetLabels);
            abilityTargetDropdown.SetValueWithoutNotify(Mathf.Max(0, abilityTargetIds.IndexOf(previous)));
            abilityTargetDropdown.RefreshShownValue();
        }

        private void OnUseForcedBlock()
        {
            ulong targetId = GetSelectedDropdownTargetId();
            if (targetId == ulong.MaxValue || !BeginAbilityRequest()) return;
            game.ActivateForcedBlockServerRpc(targetId, renderedAction);
        }

        private void OnUseLuckyShift()
        {
            int delta = GetSelectedShiftDelta();

            if (delta == 0 || !BeginAbilityRequest()) return;
            game.ActivateLuckyShiftServerRpc(delta, renderedAction);
        }

        private void OnUseCursedDice()
        {
            ulong targetId = GetSelectedDropdownTargetId();
            if (targetId == ulong.MaxValue || !BeginAbilityRequest()) return;
            game.ActivateCursedDiceServerRpc(targetId, renderedAction);
        }

        private bool BeginAbilityRequest()
        {
            var local = LuckyNetworkPlayer.LocalPlayer;
            if (game == null || local == null || abilityPending || abilityAccepted ||
                local.AbilityUsesThisRound >= game.Settings.abilityActivationsPerImposterPerRound) return false;
            expectedAbilityUses = local.AbilityUsesThisRound + 1;
            abilityPending = true;
            return true;
        }

        private ulong GetSelectedDropdownTargetId()
        {
            if (abilityTargetDropdown == null || abilityTargetDropdown.value < 0 ||
                abilityTargetDropdown.value >= abilityTargetIds.Count) return ulong.MaxValue;
            return abilityTargetIds[abilityTargetDropdown.value];
        }

        private void UpdateShiftPreview(int index)
        {
            if (luckyShiftDeltaText == null) return;
            if (game == null || index < 0 || index >= shiftDeltas.Count)
            {
                luckyShiftDeltaText.text = "Choose a shift";
                return;
            }
            long target = (long)game.TargetLuckyNumber.Value + shiftDeltas[index];
            luckyShiftDeltaText.text = target == game.CurrentScore.Value ? "Matches score" : $"Target → {target}";
        }

        private int GetSelectedShiftDelta() =>
            luckyShiftDropdown != null && luckyShiftDropdown.value < shiftDeltas.Count
                ? shiftDeltas[luckyShiftDropdown.value] : 0;

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
                    if (accepted && action == MatchAction.Move) AudioManager.Instance?.PlayPositive();
                    if (accepted && action == MatchAction.Skip) AudioManager.Instance?.PlaySkip();
                    break;
                case MatchAction.ForcedBlock:
                case MatchAction.LuckyShift:
                case MatchAction.CursedDice:
                    if (sameWindow)
                    {
                        abilityPending = false;
                        abilityAccepted = accepted;
                    }
                    if (accepted && action != MatchAction.LuckyShift) AudioManager.Instance?.PlayAbility();
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

