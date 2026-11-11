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
        private Team displayedWinner;

        private void OnEnable()
        {
            if (playAgainButton != null) playAgainButton.onClick.AddListener(OnPlayAgain);
            if (returnToMenuButton != null) returnToMenuButton.onClick.AddListener(OnReturnToMenu);

            displayedWinner = Team.None;
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
                    if (displayedWinner != winner) AudioManager.Instance?.PlayVictory();
                }
                else if (winner == Team.ImposterTeam)
                {
                    winnerTitleText.text = "😈 IMPOSTERS WIN! 😈";
                    winnerTitleText.color = Color.red;
                    if (displayedWinner != winner) AudioManager.Instance?.PlayAlarm();
                }
                else winnerTitleText.text = string.Empty;
                displayedWinner = winner;
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
                bool isHost = NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost;
                playAgainButton.gameObject.SetActive(isHost);
                var settings = NetworkGameManager.Instance.Settings;
                int count = LuckyNetworkPlayer.SpawnedPlayers.Count;
                playAgainButton.interactable = isHost && count >= settings.minPlayers && count <= settings.maxPlayers;
            }
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

