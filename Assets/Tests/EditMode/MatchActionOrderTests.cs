using NUnit.Framework;
using LuckyNumber.Core;

namespace LuckyNumber.Tests
{
    public class MatchActionOrderTests
    {
        [Test]
        public void ShiftProcessedBeforeMoveChangesTheTargetUsedForThatMove()
        {
            int target = 10;
            Assert.IsTrue(MatchActionRules.CanApplyInPhase(GamePhase.PlayerTurn, GamePhase.PlayerTurn));
            target += 1;

            var move = MoveRules.ResolveMove(4, 2, target, 6, true);
            Assert.AreEqual(10, move.Score);
            Assert.AreEqual(WinReason.None, move.WinReason);
        }

        [Test]
        public void MoveThatEndsMatchMakesLaterShiftInvalid()
        {
            var move = MoveRules.ResolveMove(4, 2, 10, 6, true);
            Assert.AreEqual(WinReason.TargetReached, move.WinReason);

            Assert.IsFalse(MatchActionRules.CanApplyInPhase(GamePhase.MatchEnded, GamePhase.PlayerTurn));
        }

        [Test]
        public void OnlyOneImposterCanSpendTheFinalSharedShiftCharge()
        {
            int teamCharges = 1;

            Assert.IsTrue(MatchActionRules.TrySpendCharge(ref teamCharges));
            Assert.IsFalse(MatchActionRules.TrySpendCharge(ref teamCharges));
            Assert.AreEqual(0, teamCharges);
        }
    }
}
