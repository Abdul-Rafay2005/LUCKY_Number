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
        [SerializeField] private TMP_Text winReasonText;
        [SerializeField] private TMP_Text finalScoreSummaryText;

        [Header("Controls")]
        [SerializeField] private Button playAgainButton;
        [SerializeField] private Button returnToMenuButton;

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
            if (NetworkGameManager.Instance == null) return;

            Team winner = NetworkGameManager.Instance.WinningTeam.Value;
            WinReason reason = NetworkGameManager.Instance.MatchWinReason.Value;
            int score = NetworkGameManager.Instance.CurrentScore.Value;
            int target = NetworkGameManager.Instance.TargetLuckyNumber.Value;

            if (winnerTitleText != null)
            {
                if (winner == Team.NormalTeam)
                {
                    winnerTitleText.text = "🎉 TEAM VICTORY! 🎉";
                    winnerTitleText.color = Color.green;
                    AudioManager.Instance?.PlayVictory();
                }
                else
                {
                    winnerTitleText.text = "😈 IMPOSTERS WIN! 😈";
                    winnerTitleText.color = Color.red;
                    AudioManager.Instance?.PlayAlarm();
                }
            }

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
                // Only host can trigger Play Again
                playAgainButton.gameObject.SetActive(NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost);
            }
        }

        private void OnPlayAgain()
        {
            AudioManager.Instance?.PlayClick();
            if (NetworkGameManager.Instance != null && NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost)
            {
                NetworkGameManager.Instance.StartMatch();
            }
        }

        private void OnReturnToMenu()
        {
            AudioManager.Instance?.PlayClick();
            NetworkLobbyManager.Instance?.LeaveRoom();
            LuckyUIManager.Instance?.ShowMainMenu();
        }
    }
}

