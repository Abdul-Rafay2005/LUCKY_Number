using System.Collections.Generic;
using System.Linq;
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
        [SerializeField] private SpriteBurst acceptedVoteEffect;
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
        private readonly Dictionary<ulong, TMP_Text> candidateStatuses = new Dictionary<ulong, TMP_Text>();
        private NetworkGameManager game;
        private bool votePending;
        private bool voteAccepted;
        private ActionStamp soundedEjection;
        private string candidateSignature = string.Empty;
        private ActionStamp renderedVotingAction;

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
                var stamp = game.ActionContext.Value;
                if (!renderedVotingAction.Equals(stamp))
                {
                    renderedVotingAction = stamp;
                    votePending = false;
                    voteAccepted = false;
                    ShowVoteStatus(game.RunoffBallotsPlayed > 0
                        ? $"Runoff ballot {game.RunoffBallotsPlayed}: choose among the tied candidates."
                        : null);
                }
                var candidates = GetEligibleCandidates();
                string nextSignature = CandidateSignature(candidates);
                if (nextSignature != candidateSignature) BuildVotingCards(candidates);
                foreach (var candidate in candidates)
                    if (candidateStatuses.TryGetValue(candidate.OwnerClientId, out var label))
                        label.text = candidate.IsOwner
                            ? (candidate.HasVoted.Value ? "YOU · VOTED" : "YOU")
                            : (candidate.HasVoted.Value ? "VOTED" : "");
                var local = LuckyNetworkPlayer.LocalPlayer;
                if (local != null && !local.IsAlive.Value)
                    ShowVoteStatus("Spectating");
                else if (local != null && local.HasVoted.Value && !voteAccepted)
                    ShowVoteStatus("Ballot submitted.");
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
                game.LastVoteOutcome.OnValueChanged -= HandleVoteOutcomeChanged;
            }
            game = next;
            if (game != null)
            {
                game.OnPhaseChanged += HandlePhaseChanged;
                game.OnActionFeedback += HandleActionFeedback;
                game.LastVoteOutcome.OnValueChanged += HandleVoteOutcomeChanged;
            }
            HandlePhaseChanged(game != null ? game.CurrentGamePhase.Value : GamePhase.Lobby);
        }

        private void HandlePhaseChanged(GamePhase phase)
        {
            votePending = false;
            voteAccepted = false;
            renderedVotingAction = game != null ? game.ActionContext.Value : default;
            ShowVoteStatus(null);
            if (phase == GamePhase.Voting)
                BuildVotingCards(GetEligibleCandidates());
            else if (phase == GamePhase.Elimination)
                ShowEliminationReveal();
        }

        private void BuildVotingCards(List<LuckyNetworkPlayer> livingPlayers)
        {
            foreach (var card in spawnedVoteCards)
            {
                if (card == null) continue;
                card.SetActive(false);
                Destroy(card);
            }
            spawnedVoteCards.Clear();
            candidateStatuses.Clear();
            candidateSignature = CandidateSignature(livingPlayers);

            if (votingCandidatesContainer == null || voteCandidateCardPrefab == null) return;
            foreach (var p in livingPlayers)
            {
                // Can vote for anyone including self or other living players
                var cardObj = Instantiate(voteCandidateCardPrefab, votingCandidatesContainer);
                spawnedVoteCards.Add(cardObj);

                var texts = cardObj.GetComponentsInChildren<TMP_Text>();
                var images = cardObj.GetComponentsInChildren<Image>();
                var btn = cardObj.GetComponentInChildren<Button>();

                if (texts.Length > 0) texts[0].text = PlayerNameLabel.Compact(p.PlayerName.Value.ToString());
                if (texts.Length > 1) candidateStatuses[p.OwnerClientId] = texts[1];

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

        private static string CandidateSignature(List<LuckyNetworkPlayer> candidates) =>
            string.Join("\n", candidates.Select(player => $"{player.OwnerClientId}/{player.CharacterIndex.Value}/{player.PlayerName.Value}"));

        private List<LuckyNetworkPlayer> GetEligibleCandidates()
        {
            if (game == null) return new List<LuckyNetworkPlayer>();
            var eligible = new HashSet<ulong>();
            for (int i = 0; i < game.VotingCandidateIds.Count; i++)
                eligible.Add(game.VotingCandidateIds[i]);
            return game.GetAllLivingPlayers().Where(player => eligible.Contains(player.OwnerClientId)).ToList();
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
            if (accepted)
            {
                AudioManager.Instance?.PlayVote();
                acceptedVoteEffect?.Play();
            }
            ShowVoteStatus(message);
        }

        private void HandleVoteOutcomeChanged(VoteOutcomeSnapshot previous, VoteOutcomeSnapshot next)
        {
            if (game != null && game.CurrentGamePhase.Value == GamePhase.Voting &&
                (next.Kind == VoteOutcomeKind.DisconnectCancellation || next.Kind == VoteOutcomeKind.RunoffStarted))
                ShowVoteStatus(next.Message.ToString());
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
            var outcome = game.LastVoteOutcome.Value;
            if (outcome.Kind == VoteOutcomeKind.Ejection || outcome.Kind == VoteOutcomeKind.RandomEjection)
            {
                if (!soundedEjection.Equals(outcome.Stamp))
                {
                    AudioManager.Instance?.PlayEjection();
                    soundedEjection = outcome.Stamp;
                }
                string eliminatedName = outcome.PlayerName.ToString();
                PlayerRole eliminatedRole = outcome.PlayerRole;
                if (eliminatedPlayerNameText != null) eliminatedPlayerNameText.text = $"{eliminatedName} WAS ELIMINATED!";
                if (revealedRoleText != null)
                {
                    revealedRoleText.text = eliminatedRole == PlayerRole.Imposter
                        ? "THEY WERE AN IMPOSTER!"
                        : "THEY WERE A NORMAL PLAYER...";
                    revealedRoleText.color = eliminatedRole == PlayerRole.Imposter ? new Color32(255, 135, 125, 255) : Color.cyan;
                }
                if (revealedRoleIcon != null)
                {
                    revealedRoleIcon.gameObject.SetActive(true);
                    revealedRoleIcon.sprite = eliminatedRole == PlayerRole.Imposter ? imposterMaskSprite : normalBadgeSprite;
                }
            }
            else
            {
                string heading;
                string details = outcome.Message.ToString();
                switch (outcome.Kind)
                {
                    case VoteOutcomeKind.NoVotes:
                        heading = "NO VOTES CAST";
                        break;
                    case VoteOutcomeKind.SkipEjection:
                        heading = "SKIP EJECTION";
                        break;
                    case VoteOutcomeKind.DisconnectCancellation:
                        heading = "BALLOT CANCELED";
                        break;
                    default:
                        heading = "NO ELIMINATION";
                        details = string.IsNullOrEmpty(details)
                            ? "The team could not agree. No one was eliminated."
                            : details;
                        break;
                }
                if (eliminatedPlayerNameText != null) eliminatedPlayerNameText.text = heading;
                if (revealedRoleText != null)
                {
                    revealedRoleText.text = details;
                    revealedRoleText.color = Color.white;
                }
                if (revealedRoleIcon != null)
                    revealedRoleIcon.gameObject.SetActive(false);
            }
        }
    }
}

