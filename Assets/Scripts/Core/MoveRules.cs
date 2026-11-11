using System;

namespace LuckyNumber.Core
{
    public struct MoveResolution
    {
        public int Score;
        public int RemainingMoves;
        public WinReason WinReason;
    }

    public static class MoveRules
    {
        public static int SelectTimeoutDie(int die1, int die2, int die3, int selectedIndex)
        {
            if (selectedIndex < 0 || selectedIndex > 2)
                throw new ArgumentOutOfRangeException(nameof(selectedIndex));

            return selectedIndex == 0 ? die1 : selectedIndex == 1 ? die2 : die3;
        }

        public static MoveResolution ResolveMove(int score, int remainingMoves, int target, int dieValue, bool add)
        {
            if (remainingMoves <= 0) throw new ArgumentOutOfRangeException(nameof(remainingMoves));
            if (dieValue < 1 || dieValue > 6) throw new ArgumentOutOfRangeException(nameof(dieValue));

            int nextScore = score + (add ? dieValue : -dieValue);
            int nextMoves = remainingMoves - 1;
            WinReason reason = nextScore == target
                ? WinReason.TargetReached
                : nextMoves == 0 ? WinReason.OutOfMoves : WinReason.None;

            return new MoveResolution
            {
                Score = nextScore,
                RemainingMoves = nextMoves,
                WinReason = reason
            };
        }
    }
}
