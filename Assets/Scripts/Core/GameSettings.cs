using UnityEngine;

namespace LuckyNumber.Core
{
    public enum TurnTimeoutPolicy
    {
        SkipTurn,
        RandomSignedMove
    }

    public enum PersonalSkipPolicy
    {
        Fixed,
        OnePerFourStartingPlayersRoundedUp
    }

    public enum ShiftBudgetScope
    {
        PerImposter,
        ImposterTeam
    }

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
        public float roleRevealDurationSeconds = 4f;

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

        [Header("Jam Rule Policies (Phases 2–3)")]
        public TurnTimeoutPolicy turnTimeoutPolicy = TurnTimeoutPolicy.RandomSignedMove;
        public PersonalSkipPolicy personalSkipPolicy = PersonalSkipPolicy.OnePerFourStartingPlayersRoundedUp;
        public ShiftBudgetScope shiftBudgetScope = ShiftBudgetScope.ImposterTeam;
        [Tooltip("Cursed Dice charges after the server-owned ability budgets are implemented.")]
        public int jamCursedDiceUsesPerMatch = 2;
        public int abilityActivationsPerImposterPerRound = 1;
        public float shiftReconsiderationSeconds = 10f;

        private void OnValidate()
        {
            minPlayers = Mathf.Max(3, minPlayers);
            maxPlayers = Mathf.Max(minPlayers, maxPlayers);
            // Bound each role count at the smallest player count in its tier.
            imposterCountSmallGame = Mathf.Clamp(imposterCountSmallGame, 1, (Mathf.Min(minPlayers, 5) - 1) / 2);
            imposterCountLargeGame = Mathf.Clamp(imposterCountLargeGame, 1, (Mathf.Max(minPlayers, 6) - 1) / 2);

            minLuckyTarget = Mathf.Clamp(minLuckyTarget, 1, int.MaxValue - 1);
            maxLuckyTarget = Mathf.Clamp(maxLuckyTarget, minLuckyTarget, int.MaxValue - 1);
            startingMoves = Mathf.Max(1, startingMoves);
            skipsPerPlayer = Mathf.Max(0, skipsPerPlayer);

            turnDurationSeconds = PositiveSeconds(turnDurationSeconds, 30f);
            discussionDurationSeconds = PositiveSeconds(discussionDurationSeconds, 45f);
            votingDurationSeconds = PositiveSeconds(votingDurationSeconds, 25f);
            roleRevealDurationSeconds = PositiveSeconds(roleRevealDurationSeconds, 4f);
            eliminationDurationSeconds = PositiveSeconds(eliminationDurationSeconds, 5f);
            shiftReconsiderationSeconds = PositiveSeconds(shiftReconsiderationSeconds, 10f);

            forcedBlockUsesPerMatch = Mathf.Max(0, forcedBlockUsesPerMatch);
            luckyShiftUsesPerMatch = Mathf.Max(0, luckyShiftUsesPerMatch);
            cursedDiceUsesPerMatch = Mathf.Max(0, cursedDiceUsesPerMatch);
            jamCursedDiceUsesPerMatch = Mathf.Max(0, jamCursedDiceUsesPerMatch);
            abilityActivationsPerImposterPerRound = Mathf.Max(0, abilityActivationsPerImposterPerRound);
            minShiftDelta = Mathf.Clamp(minShiftDelta, -5, -1);
            maxShiftDelta = Mathf.Clamp(maxShiftDelta, 1, 5);
        }

        private static float PositiveSeconds(float seconds, float fallback)
        {
            return seconds > 0f && !float.IsInfinity(seconds) ? seconds : fallback;
        }

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
