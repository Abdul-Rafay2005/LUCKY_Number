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
    public class DiscussionAndVotingController : MonoBehaviour
    {
        [Header("Discussion Phase UI")]
        [SerializeField] private TMP_Text discussionTimerText;
        [SerializeField] private Transform livingPlayersContainer;

        [Header("Voting Phase UI")]
        [SerializeField] private TMP_Text votingTimerText;
        [SerializeField] private Transform votingCandidatesContainer;
        [SerializeField] private GameObject voteCandidateCardPrefab;
        [SerializeField] private Button skipVoteButton;
        [SerializeField] private TMP_Text voteSubmittedStatusText;
        [SerializeField] private Sprite[] characterPortraits;

        [Header("Elimination Reveal Modal")]
        [SerializeField] private GameObject eliminationModalPanel;
        [SerializeField] private TMP_Text eliminatedPlayerNameText;
        [SerializeField] private TMP_Text revealedRoleText;
        [SerializeField] private Image revealedRoleIcon;
        [SerializeField] private Sprite imposterMaskSprite;
        [SerializeField] private Sprite normalBadgeSprite;

        private readonly List<GameObject> spawnedVoteCards = new List<GameObject>();
        private bool hasVotedThisRound = false;

        private void OnEnable()
        {
            if (skipVoteButton != null) skipVoteButton.onClick.AddListener(OnSkipVote);

            if (NetworkGameManager.Instance != null)
            {
                NetworkGameManager.Instance.OnPhaseChanged += HandlePhaseChanged;
            }

            hasVotedThisRound = false;
            if (voteSubmittedStatusText != null) voteSubmittedStatusText.gameObject.SetActive(false);
            if (eliminationModalPanel != null) eliminationModalPanel.SetActive(false);

            RefreshUI();
        }

        private void OnDisable()
        {
            if (skipVoteButton != null) skipVoteButton.onClick.RemoveListener(OnSkipVote);

            if (NetworkGameManager.Instance != null)
            {
                NetworkGameManager.Instance.OnPhaseChanged -= HandlePhaseChanged;
            }
        }

        private void Update()
        {
            if (NetworkGameManager.Instance == null) return;

            float remaining = Mathf.Max(0f, NetworkGameManager.Instance.PhaseTimer.Value);
            string timerStr = $"{Mathf.CeilToInt(remaining)}s";

            if (discussionTimerText != null) discussionTimerText.text = timerStr;
            if (votingTimerText != null) votingTimerText.text = timerStr;
        }

        private void HandlePhaseChanged(GamePhase phase)
        {
            if (phase == GamePhase.Discussion)
            {
                hasVotedThisRound = false;
                if (eliminationModalPanel != null) eliminationModalPanel.SetActive(false);
                if (voteSubmittedStatusText != null) voteSubmittedStatusText.gameObject.SetActive(false);
            }
            else if (phase == GamePhase.Voting)
            {
                BuildVotingCards();
            }
            else if (phase == GamePhase.Elimination)
            {
                ShowEliminationReveal();
            }
        }

        private void RefreshUI()
        {
            if (NetworkGameManager.Instance == null) return;

            var phase = NetworkGameManager.Instance.CurrentGamePhase.Value;
            if (phase == GamePhase.Voting)
            {
                BuildVotingCards();
            }
            else if (phase == GamePhase.Elimination)
            {
                ShowEliminationReveal();
            }
        }

        private void BuildVotingCards()
        {
            foreach (var card in spawnedVoteCards)
            {
                if (card != null) Destroy(card);
            }
            spawnedVoteCards.Clear();

            if (votingCandidatesContainer == null || voteCandidateCardPrefab == null) return;

            var livingPlayers = NetworkGameManager.Instance.GetAllLivingPlayers();
            ulong localClientId = NetworkManager.Singleton.LocalClientId;

            foreach (var p in livingPlayers)
            {
                // Can vote for anyone including self or other living players
                var cardObj = Instantiate(voteCandidateCardPrefab, votingCandidatesContainer);
                spawnedVoteCards.Add(cardObj);

                var texts = cardObj.GetComponentsInChildren<TMP_Text>();
                var images = cardObj.GetComponentsInChildren<Image>();
                var btn = cardObj.GetComponentInChildren<Button>();

                string pName = p.PlayerName.Value.ToString();
                if (p.OwnerClientId == localClientId) pName += " (You)";

                if (texts.Length > 0) texts[0].text = pName;

                int charIdx = p.CharacterIndex.Value;
                if (images.Length > 1 && characterPortraits != null && charIdx >= 0 && charIdx < characterPortraits.Length)
                {
                    images[1].sprite = characterPortraits[charIdx];
                }

                ulong targetId = p.OwnerClientId;
                if (btn != null)
                {
                    btn.interactable = !hasVotedThisRound;
                    btn.onClick.AddListener(() => OnCastVote(targetId));
                }
            }

            if (skipVoteButton != null) skipVoteButton.interactable = !hasVotedThisRound;
        }

        private void OnCastVote(ulong suspectClientId)
        {
            if (hasVotedThisRound) return;
            hasVotedThisRound = true;

            AudioManager.Instance?.PlayClick();
            NetworkGameManager.Instance?.SubmitVoteServerRpc(suspectClientId);

            if (voteSubmittedStatusText != null)
            {
                voteSubmittedStatusText.gameObject.SetActive(true);
                voteSubmittedStatusText.text = "✓ Vote submitted! Waiting for others...";
            }

            DisableAllVoteButtons();
        }

        private void OnSkipVote()
        {
            if (hasVotedThisRound) return;
            hasVotedThisRound = true;

            AudioManager.Instance?.PlayClick();
            NetworkGameManager.Instance?.SkipVoteServerRpc();

            if (voteSubmittedStatusText != null)
            {
                voteSubmittedStatusText.gameObject.SetActive(true);
                voteSubmittedStatusText.text = "✓ Abstain vote submitted! Waiting for others...";
            }

            DisableAllVoteButtons();
        }

        private void DisableAllVoteButtons()
        {
            if (skipVoteButton != null) skipVoteButton.interactable = false;
            foreach (var card in spawnedVoteCards)
            {
                if (card != null)
                {
                    var btn = card.GetComponentInChildren<Button>();
                    if (btn != null) btn.interactable = false;
                }
            }
        }

        private void ShowEliminationReveal()
        {
            if (eliminationModalPanel != null) eliminationModalPanel.SetActive(true);

            string elimName = NetworkGameManager.Instance.LastEliminatedPlayerName.Value.ToString();
            PlayerRole elimRole = NetworkGameManager.Instance.LastEliminatedRole.Value;

            if (string.IsNullOrEmpty(elimName))
            {
                if (eliminatedPlayerNameText != null) eliminatedPlayerNameText.text = "TIE VOTE / NO ELIMINATION";
                if (revealedRoleText != null) revealedRoleText.text = "The team could not agree. No one was eliminated!";
                if (revealedRoleIcon != null) revealedRoleIcon.gameObject.SetActive(false);
            }
            else
            {
                AudioManager.Instance?.PlayAlarm();
                if (eliminatedPlayerNameText != null) eliminatedPlayerNameText.text = $"{elimName} WAS ELIMINATED!";
                if (revealedRoleText != null)
                {
                    revealedRoleText.text = elimRole == PlayerRole.Imposter
                        ? "THEY WERE AN IMPOSTER! 🎉"
                        : "THEY WERE A NORMAL PLAYER... 😢";
                    revealedRoleText.color = elimRole == PlayerRole.Imposter ? Color.red : Color.cyan;
                }

                if (revealedRoleIcon != null)
                {
                    revealedRoleIcon.gameObject.SetActive(true);
                    revealedRoleIcon.sprite = (elimRole == PlayerRole.Imposter) ? imposterMaskSprite : normalBadgeSprite;
                }
            }
        }
    }
}

