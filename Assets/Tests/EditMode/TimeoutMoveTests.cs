using NUnit.Framework;
using LuckyNumber.Core;
using LuckyNumber.Gameplay;

namespace LuckyNumber.Tests
{
    public class TimeoutMoveTests
    {
        [TestCase(0, 2)]
        [TestCase(1, 4)]
        [TestCase(2, 6)]
        public void TimeoutSelectionUsesOneOfTheAuthorizedDice(int index, int expected)
        {
            Assert.AreEqual(expected, MoveRules.SelectTimeoutDie(2, 4, 6, index));
        }

        [Test]
        public void RepeatedTimeoutMovesExhaustTheSharedMovePool()
        {
            int score = 0;
            int remainingMoves = 4;
            WinReason result = WinReason.None;

            for (int turn = 0; turn < 4; turn++)
            {
                int die = MoveRules.SelectTimeoutDie(1, 2, 3, turn % 3);
                var resolution = MoveRules.ResolveMove(score, remainingMoves, 100, die, true);
                score = resolution.Score;
                remainingMoves = resolution.RemainingMoves;
                result = resolution.WinReason;
            }

            Assert.AreEqual(7, score);
            Assert.AreEqual(0, remainingMoves);
            Assert.AreEqual(WinReason.OutOfMoves, result);
        }

        [Test]
        public void FairRollsStayWithinDieBoundsAndRemainApproximatelyUniform()
        {
            var random = new System.Random(41293);
            var counts = new int[7];
            for (int i = 0; i < 4000; i++)
            {
                foreach (int die in DiceRollGenerator.RollThreeDice(false, 0, 30, 0f, random))
                {
                    Assert.That(die, Is.InRange(1, 6));
                    counts[die]++;
                }
            }

            for (int face = 1; face <= 6; face++)
                Assert.That(counts[face], Is.InRange(1800, 2200), $"Unexpected frequency for face {face}.");
        }

        [Test]
        public void CursedRollHasTwoBiasedDiceAndOneFairDieThenShuffles()
        {
            const int score = 0;
            const int target = 5;
            var dice = DiceRollGenerator.RollThreeDice(true, score, target, 1f, new System.Random(917));
            var leastHelpful = DiceRollGenerator.GetLeastHelpfulFaces(score, target);
            int biasedDiceCount = 0;
            foreach (int die in dice)
            {
                Assert.That(die, Is.InRange(1, 6));
                if (System.Array.IndexOf(leastHelpful, die) >= 0) biasedDiceCount++;
            }
            Assert.That(biasedDiceCount, Is.GreaterThanOrEqualTo(2));

            var repeated = DiceRollGenerator.RollThreeDice(true, score, target, 1f, new System.Random(917));
            CollectionAssert.AreEqual(dice, repeated);
        }

        [Test]
        public void UnfavorableFaceRankingAccountsForOvershootAndBothSigns()
        {
            CollectionAssert.AreEqual(new[] { 1 }, DiceRollGenerator.GetLeastHelpfulFaces(10, 5));
        }

        [Test]
        public void CursedBiasFrequencyMatchesConfiguredStrengthWithinSamplingTolerance()
        {
            const int rolls = 4000;
            const int leastHelpfulFace = 1; // At score 0 and target 5, face 1 is worst under either sign.
            int unfavorableCount = 0;
            var random = new System.Random(28931);
            for (int i = 0; i < rolls; i++)
            {
                foreach (int die in DiceRollGenerator.RollThreeDice(true, 0, 5, 0.85f, random))
                    if (die == leastHelpfulFace) unfavorableCount++;
            }

            Assert.That(unfavorableCount, Is.InRange(7500, 7850));
        }
    }
}
