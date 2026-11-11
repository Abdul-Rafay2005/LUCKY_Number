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
        private NetworkGameManager game;
        private bool votePending;
        private bool voteAccepted;
        private bool playedRevealSound;
        private int candidateCount;

        private void OnEnable()
        {
            if (skipVoteButton != null) skipVoteButton.onClick.AddListener(OnSkipVote);

            Update();
        }

        private void OnDisable()
        {
            if (skipVoteButton != null) skipVoteButton.onClick.RemoveListener(OnSkipVote);

            BindGame(null);
        }

        private void Update()
        {
            var manager = NetworkGameManager.Instance;
            BindGame(manager != null && manager.IsSpawned ? manager : null);
            if (game == null) return;

            float remaining = Mathf.Max(0f, game.PhaseTimer.Value);
            string timerStr = $"{Mathf.CeilToInt(remaining)}s";

            if (discussionTimerText != null) discussionTimerText.text = timerStr;
            if (votingTimerText != null) votingTimerText.text = timerStr;
            if (game.CurrentGamePhase.Value == GamePhase.Voting)
            {
                var candidates = game.GetAllLivingPlayers();
                if (candidates.Count != candidateCount) BuildVotingCards(candidates);
                SetVoteButtons(CanVote());
            }
            else if (game.CurrentGamePhase.Value == GamePhase.Elimination)
                ShowEliminationReveal();
        }

        private void BindGame(NetworkGameManager next)
        {
            if (ReferenceEquals(game, next)) return;
            if (!ReferenceEquals(game, null))
            {
                game.OnPhaseChanged -= HandlePhaseChanged;
                game.OnActionFeedback -= HandleActionFeedback;
            }
            game = next;
            if (game != null)
            {
                game.OnPhaseChanged += HandlePhaseChanged;
                game.OnActionFeedback += HandleActionFeedback;
            }
            HandlePhaseChanged(game != null ? game.CurrentGamePhase.Value : GamePhase.Lobby);
        }

        private void HandlePhaseChanged(GamePhase phase)
        {
            votePending = false;
            voteAccepted = false;
            playedRevealSound = false;
            ShowVoteStatus(null);
            if (phase == GamePhase.Voting)
                BuildVotingCards(game.GetAllLivingPlayers());
            else if (phase == GamePhase.Elimination)
                ShowEliminationReveal();
        }

        private void BuildVotingCards(List<LuckyNetworkPlayer> livingPlayers)
        {
            foreach (var card in spawnedVoteCards)
            {
                if (card != null) Destroy(card);
            }
            spawnedVoteCards.Clear();
            candidateCount = livingPlayers.Count;

            if (votingCandidatesContainer == null || voteCandidateCardPrefab == null) return;
            var localPlayer = LuckyNetworkPlayer.LocalPlayer;

            foreach (var p in livingPlayers)
            {
                // Can vote for anyone including self or other living players
                var cardObj = Instantiate(voteCandidateCardPrefab, votingCandidatesContainer);
                spawnedVoteCards.Add(cardObj);

                var texts = cardObj.GetComponentsInChildren<TMP_Text>();
                var images = cardObj.GetComponentsInChildren<Image>();
                var btn = cardObj.GetComponentInChildren<Button>();

                string pName = p.PlayerName.Value.ToString();
                if (p == localPlayer) pName += " (You)";

                if (texts.Length > 0) texts[0].text = pName;

                int charIdx = p.CharacterIndex.Value;
                if (images.Length > 1 && characterPortraits != null && charIdx >= 0 && charIdx < characterPortraits.Length)
                {
                    images[1].sprite = characterPortraits[charIdx];
                }

                ulong targetId = p.OwnerClientId;
                if (btn != null)
                {
                    btn.interactable = CanVote();
                    btn.onClick.AddListener(() => OnCastVote(targetId));
                }
            }

            if (skipVoteButton != null) skipVoteButton.interactable = CanVote();
        }

        private bool CanVote()
        {
            var local = LuckyNetworkPlayer.LocalPlayer;
            return game != null && game.CurrentGamePhase.Value == GamePhase.Voting &&
                local != null && local.IsAlive.Value && !local.HasVoted.Value && !votePending && !voteAccepted;
        }

        private void OnCastVote(ulong suspectClientId) => SendBallot(suspectClientId);
        private void OnSkipVote() => SendBallot(null);

        private void SendBallot(ulong? suspect)
        {
            if (!CanVote()) return;
            votePending = true;
            SetVoteButtons(false);
            ShowVoteStatus("Sending ballot...");
            var stamp = game.ActionContext.Value;
            if (suspect.HasValue)
                game.SubmitVoteServerRpc(suspect.Value, stamp);
            else
                game.SkipVoteServerRpc(stamp);
        }

        private void HandleActionFeedback(ActionStamp stamp, MatchAction action, bool accepted, string message)
        {
            if (action != MatchAction.Vote || game.CurrentGamePhase.Value != GamePhase.Voting ||
                !stamp.Equals(game.ActionContext.Value)) return;
            votePending = false;
            voteAccepted = accepted;
            if (accepted) AudioManager.Instance?.PlayClick();
            ShowVoteStatus(message);
        }

        private void ShowVoteStatus(string message)
        {
            if (voteSubmittedStatusText == null) return;
            voteSubmittedStatusText.text = message;
            voteSubmittedStatusText.gameObject.SetActive(!string.IsNullOrEmpty(message));
        }

        private void SetVoteButtons(bool enabled)
        {
            if (skipVoteButton != null) skipVoteButton.interactable = enabled;
            foreach (var card in spawnedVoteCards)
            {
                if (card != null)
                {
                    var btn = card.GetComponentInChildren<Button>();
                    if (btn != null) btn.interactable = enabled;
                }
            }
        }

        private void ShowEliminationReveal()
        {
            string elimName = game.LastEliminatedPlayerName.Value.ToString();
            PlayerRole elimRole = game.LastEliminatedRole.Value;

            if (string.IsNullOrEmpty(elimName))
            {
                if (eliminatedPlayerNameText != null) eliminatedPlayerNameText.text = "TIE VOTE / NO ELIMINATION";
                if (revealedRoleText != null) revealedRoleText.text = "The team could not agree. No one was eliminated!";
                if (revealedRoleIcon != null) revealedRoleIcon.gameObject.SetActive(false);
            }
            else
            {
                if (!playedRevealSound)
                {
                    AudioManager.Instance?.PlayAlarm();
                    playedRevealSound = true;
                }
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

