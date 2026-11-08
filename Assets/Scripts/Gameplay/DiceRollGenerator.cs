using System;
using UnityEngine;
using LuckyNumber.Core;

namespace LuckyNumber.Gameplay
{
    /// <summary>
    /// Generates authoritative dice rolls for players.
    /// Handles both fair uniform rolls and Cursed Dice rolls.
    /// </summary>
    public static class DiceRollGenerator
    {
        private static readonly System.Random Rng = new System.Random();

        /// <summary>
        /// Generates a roll of three 6-sided dice (values 1 to 6).
        /// </summary>
        public static int[] RollThreeDice(bool isCursed, int currentScore, int targetScore, float biasStrength)
        {
            int[] dice = new int[3];

            for (int i = 0; i < 3; i++)
            {
                if (isCursed && Rng.NextDouble() < biasStrength)
                {
                    dice[i] = GenerateBiasedDie(currentScore, targetScore);
                }
                else
                {
                    dice[i] = Rng.Next(1, 7);
                }
            }

            return dice;
        }

        /// <summary>
        /// Biases a die away from the optimal move towards an unfavorable outcome:
        /// - If team needs a specific number (e.g., target - current = 3), biased away from 3.
        /// - If current < target, favors 1 (stagnation) or 6 (causes overshoot if gap is small).
        /// - If current > target, favors 1 (cannot quickly recover with subtraction).
        /// </summary>
        private static int GenerateBiasedDie(int currentScore, int targetScore)
        {
            int gap = targetScore - currentScore;

            if (gap > 0)
            {
                // If close to target (gap <= 5), roll numbers that cannot hit the target
                if (gap >= 1 && gap <= 6)
                {
                    // Avoid gap! Pick something else
                    int roll = Rng.Next(1, 6);
                    if (roll >= gap) roll++;
                    return Mathf.Clamp(roll, 1, 6);
                }

                // Far from target: favor rolling 1
                return (Rng.Next(100) < 65) ? 1 : Rng.Next(1, 7);
            }
            else if (gap < 0)
            {
                // Over-target: team needs to subtract. Favor 1 (minimal recovery)
                return (Rng.Next(100) < 70) ? 1 : Rng.Next(1, 7);
            }

            return Rng.Next(1, 7);
        }
    }
}

