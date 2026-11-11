namespace LuckyNumber.Core
{
    public static class MatchActionRules
    {
        public static bool CanApplyInPhase(GamePhase currentPhase, GamePhase requiredPhase) =>
            currentPhase == requiredPhase && currentPhase != GamePhase.MatchEnded;

        public static bool TrySpendCharge(ref int remainingCharges)
        {
            if (remainingCharges <= 0) return false;
            remainingCharges--;
            return true;
        }
    }
}
