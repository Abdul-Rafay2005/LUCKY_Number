using System;
using System.Collections.Generic;

namespace LuckyNumber.Gameplay
{
    /// <summary>
    /// Generates authoritative dice rolls. Supplying a random source makes rolls reproducible in tests.
    /// </summary>
    public static class DiceRollGenerator
    {
        private static readonly Random DefaultRandom = new Random();

        /// <summary>
        /// A cursed roll contains two biased dice and one fair die, shuffled before delivery.
        /// Each biased die uses the configured chance to draw from the least helpful faces.
        /// </summary>
        public static int[] RollThreeDice(bool isCursed, int currentScore, int targetScore,
            float biasStrength, Random random = null)
        {
            Random rng = random ?? DefaultRandom;
            var dice = new int[3];
            if (isCursed)
            {
                dice[0] = GenerateBiasedDie(currentScore, targetScore, biasStrength, rng);
                dice[1] = GenerateBiasedDie(currentScore, targetScore, biasStrength, rng);
                dice[2] = rng.Next(1, 7);
                Shuffle(dice, rng);
            }
            else
            {
                for (int i = 0; i < dice.Length; i++) dice[i] = rng.Next(1, 7);
            }
            return dice;
        }

        /// <summary>
        /// Returns the faces whose best signed move leaves the greatest distance to target.
        /// </summary>
        public static int[] GetLeastHelpfulFaces(int currentScore, int targetScore)
        {
            long worstDistance = long.MinValue;
            var faces = new List<int>();
            for (int face = 1; face <= 6; face++)
            {
                long afterAdd = (long)currentScore + face;
                long afterSubtract = (long)currentScore - face;
                long distance = Math.Min(Math.Abs((long)targetScore - afterAdd),
                    Math.Abs((long)targetScore - afterSubtract));
                if (distance > worstDistance)
                {
                    worstDistance = distance;
                    faces.Clear();
                    faces.Add(face);
                }
                else if (distance == worstDistance)
                {
                    faces.Add(face);
                }
            }
            return faces.ToArray();
        }

        private static int GenerateBiasedDie(int currentScore, int targetScore, float biasStrength, Random rng)
        {
            if (rng.NextDouble() >= Math.Clamp(biasStrength, 0f, 1f))
                return rng.Next(1, 7);

            int[] leastHelpful = GetLeastHelpfulFaces(currentScore, targetScore);
            return leastHelpful[rng.Next(0, leastHelpful.Length)];
        }

        private static void Shuffle(int[] values, Random rng)
        {
            for (int i = values.Length - 1; i > 0; i--)
            {
                int j = rng.Next(0, i + 1);
                (values[i], values[j]) = (values[j], values[i]);
            }
        }
    }
}
