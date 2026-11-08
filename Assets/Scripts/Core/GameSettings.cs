using UnityEngine;

namespace LuckyNumber.Core
{
    [CreateAssetMenu(fileName = "GameSettings", menuName = "LuckyNumber/GameSettings")]
    public class GameSettings : ScriptableObject
    {
        [Header("Player & Match Limits")]
        [Tooltip("Minimum players required to start a game.")]
        public int minPlayers = 3;

        [Tooltip("Maximum players allowed in a room.")]
        public int maxPlayers = 8;

        [Tooltip("Number of imposters for 3 to 5 players.")]
        public int imposterCountSmallGame = 1;

        [Tooltip("Number of imposters for 6 or more players.")]
        public int imposterCountLargeGame = 2;

        [Header("Target & Move Pool")]
        [Tooltip("Minimum possible Lucky Number target.")]
        public int minLuckyTarget = 25;

        [Tooltip("Maximum possible Lucky Number target.")]
        public int maxLuckyTarget = 55;

        [Tooltip("Shared moves available for the team.")]
        public int startingMoves = 15;

        [Tooltip("Number of voluntary skips allowed per player per match.")]
        public int skipsPerPlayer = 1;

        [Header("Timers (in seconds)")]
        [Tooltip("Seconds per player dice turn.")]
        public float turnDurationSeconds = 30f;

        [Tooltip("Seconds for discussion phase.")]
        public float discussionDurationSeconds = 45f;

        [Tooltip("Seconds for voting phase.")]
        public float votingDurationSeconds = 25f;

        [Tooltip("Brief pause for role reveal splash at start.")]
        public float roleRevealDurationSeconds = 5f;

        [Tooltip("Seconds to display elimination result.")]
        public float eliminationDurationSeconds = 5f;

        [Header("Imposter Abilities")]
        [Tooltip("Forced Block uses per imposter.")]
        public int forcedBlockUsesPerMatch = 1;

        [Tooltip("Lucky Shift uses per imposter.")]
        public int luckyShiftUsesPerMatch = 1;

        [Tooltip("Cursed Dice uses per imposter.")]
        public int cursedDiceUsesPerMatch = 1;

        [Tooltip("Minimum delta for Lucky Shift (nonzero).")]
        public int minShiftDelta = -5;

        [Tooltip("Maximum delta for Lucky Shift (nonzero).")]
        public int maxShiftDelta = 5;

        [Header("Cursed Dice Bias")]
        [Tooltip("Chance (0 to 1) that cursed die roll favors unfavorable value.")]
        [Range(0f, 1f)]
        public float cursedBiasStrength = 0.85f;

        /// <summary>
        /// Calculates the number of imposters based on total player count.
        /// </summary>
        public int GetImposterCount(int playerCount)
        {
            if (playerCount <= 0) return 0;
            if (playerCount < 6) return imposterCountSmallGame;
            return imposterCountLargeGame;
        }
    }
}
