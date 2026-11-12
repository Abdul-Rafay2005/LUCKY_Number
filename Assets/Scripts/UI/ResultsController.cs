using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;
using LuckyNumber.Core;
using LuckyNumber.Network;
using LuckyNumber.Audio;

namespace LuckyNumber.UI
{
    public class ResultsController : MonoBehaviour
    {
        [Header("Results Header")]
        [SerializeField] private TMP_Text winnerTitleText;
        [SerializeField] private TMP_Text localOutcomeText;
        [SerializeField] private TMP_Text winReasonText;
        [SerializeField] private TMP_Text finalScoreSummaryText;

        [Header("Controls")]
        [SerializeField] private Button playAgainButton;
        [SerializeField] private Button returnToMenuButton;
        private ActionStamp soundedResult;

        private void OnEnable()
        {
            if (playAgainButton != null) playAgainButton.onClick.AddListener(OnPlayAgain);
            if (returnToMenuButton != null) returnToMenuButton.onClick.AddListener(OnReturnToMenu);

            DisplayResults();
        }

        private void OnDisable()
        {
            if (playAgainButton != null) playAgainButton.onClick.RemoveListener(OnPlayAgain);
            if (returnToMenuButton != null) returnToMenuButton.onClick.RemoveListener(OnReturnToMenu);
        }

        private void DisplayResults()
        {
            var game = NetworkGameManager.Instance;
            if (game == null) return;

            Team winner = game.WinningTeam.Value;
            WinReason reason = game.MatchWinReason.Value;
            int score = game.CurrentScore.Value;
            int target = game.TargetLuckyNumber.Value;

            if (winnerTitleText != null)
            {
                if (winner == Team.NormalTeam)
                {
                    winnerTitleText.text = "NORMAL PLAYERS WIN";
                    winnerTitleText.color = new Color32(145, 242, 195, 255);
                }
                else if (winner == Team.ImposterTeam)
                {
                    winnerTitleText.text = "IMPOSTERS WIN";
                    winnerTitleText.color = new Color32(255, 135, 125, 255);
                }
                else winnerTitleText.text = string.Empty;
            }

            DisplayLocalOutcome(winner, game.ActionContext.Value);

            if (winReasonText != null)
            {
                winReasonText.text = reason switch
                {
                    WinReason.TargetReached => $"The team successfully reached Lucky Number {target}!",
                    WinReason.AllImpostersEliminated => "All Imposters were successfully identified and eliminated!",
                    WinReason.OutOfMoves => $"Team ran out of moves without reaching {target}!",
                    WinReason.ImposterParity => "Imposters overpowered the remaining normal players!",
                    _ => "Match concluded."
                };
            }

            if (finalScoreSummaryText != null)
            {
                finalScoreSummaryText.text = $"Final Score: {score}  |  Target: {target}";
            }

            if (playAgainButton != null)
            {
                bool isHost = NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost;
                playAgainButton.gameObject.SetActive(isHost);
                var settings = game.Settings;
                int count = LuckyNetworkPlayer.SpawnedPlayers.Count;
                playAgainButton.interactable = isHost && count >= settings.minPlayers && count <= settings.maxPlayers;
            }
        }

        private void DisplayLocalOutcome(Team winner, ActionStamp stamp)
        {
            var local = LuckyNetworkPlayer.LocalPlayer;
            if (winner == Team.None || local == null || !local.HasSecretRole)
            {
                if (localOutcomeText != null) localOutcomeText.text = string.Empty;
                return;
            }

            Team localTeam = local.LocalRole == PlayerRole.Imposter ? Team.ImposterTeam : Team.NormalTeam;
            bool won = winner == localTeam;
            if (localOutcomeText != null)
            {
                localOutcomeText.text = won ? "VICTORY" : "DEFEAT";
                localOutcomeText.color = won ? new Color32(145, 242, 195, 255) : new Color32(255, 135, 125, 255);
            }

            if (soundedResult.SessionId == stamp.SessionId && soundedResult.MatchId == stamp.MatchId) return;
            soundedResult = stamp;
            if (won) AudioManager.Instance?.PlayVictory();
            else AudioManager.Instance?.PlayDefeat();
        }

        private void Update() => DisplayResults();

        private void OnPlayAgain()
        {
            AudioManager.Instance?.PlayClick();
            var game = NetworkGameManager.Instance;
            if (game != null && game.IsSpawned && NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost)
            {
                game.RequestRematchServerRpc(game.ActionContext.Value);
            }
        }

        private void OnReturnToMenu()
        {
            AudioManager.Instance?.PlayClick();
            NetworkLobbyManager.Instance?.LeaveRoom();
        }
    }
}

