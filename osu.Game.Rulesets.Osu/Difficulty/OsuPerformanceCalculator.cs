// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;

namespace osu.Game.Rulesets.Osu.Difficulty
{
    public class OsuPerformanceCalculator : PerformanceCalculator
    {
        public new OsuDifficultyAttributes Attributes => (OsuDifficultyAttributes)base.Attributes;

        private Mod[] mods;

        private double accuracy;
        private int scoreMaxCombo;
        private int countGreat;
        private int countOk;
        private int countMeh;
        private int countMiss;

        public OsuPerformanceCalculator(Ruleset ruleset, DifficultyAttributes attributes, ScoreInfo score)
            : base(ruleset, attributes, score)
        {
        }

        public override double Calculate(Dictionary<string, double> categoryRatings = null)
        {
            mods = Score.Mods;
            accuracy = Score.Accuracy;
            scoreMaxCombo = Score.MaxCombo;
            countGreat = Score.Statistics.GetValueOrDefault(HitResult.Great);
            countOk = Score.Statistics.GetValueOrDefault(HitResult.Ok);
            countMeh = Score.Statistics.GetValueOrDefault(HitResult.Meh);
            countMiss = Score.Statistics.GetValueOrDefault(HitResult.Miss);

            // Custom multipliers for NoFail and SpunOut.
            double multiplier = 1.12; // This is being adjusted to keep the final pp value scaled around what it used to be when changing things

            if (mods.Any(m => m is OsuModNoFail))
                multiplier *= Math.Max(0.90, 1.0 - 0.02 * countMiss);

            if (mods.Any(m => m is OsuModSpunOut))
                multiplier *= 1.0 - Math.Pow((double)Attributes.SpinnerCount / totalHits, 0.85);

            double aimValue = computeAimValue();
            double speedValue = computeSpeedValue();
            double accuracyValue = computeAccuracyValue();
            double totalValue =
                Math.Pow(
                    Math.Pow(aimValue, 1.1) +
                    Math.Pow(speedValue, 1.1) +
                    Math.Pow(accuracyValue, 1.1), 1.0 / 1.1
                ) * multiplier;

            if (categoryRatings != null)
            {
                categoryRatings.Add("Aim", aimValue);
                categoryRatings.Add("Speed", speedValue);
                categoryRatings.Add("Accuracy", accuracyValue);
                categoryRatings.Add("OD", Attributes.OverallDifficulty);
                categoryRatings.Add("AR", Attributes.ApproachRate);
                categoryRatings.Add("Max Combo", Attributes.MaxCombo);
            }

            return totalValue;
        }

        private double computeAimValue()
        {
            double rawAim = Attributes.AimStrain;

            if (mods.Any(m => m is OsuModTouchDevice))
                rawAim = Math.Pow(rawAim, 0.8);

            double aimValue = Math.Pow(5.0 * Math.Max(1.0, rawAim / 0.0675) - 4.0, 3.0) / 100000.0;

            // Longer maps are worth more
            double lengthBonus = 0.95 + 0.4 * Math.Min(1.0, totalHits / 2000.0) +
                                 (totalHits > 2000 ? Math.Log10(totalHits / 2000.0) * 0.5 : 0.0);

            aimValue *= lengthBonus;

            // Penalize misses by assessing # of misses relative to the total # of objects. Default a 3% reduction for any # of misses.
            if (countMiss > 0)
                aimValue *= 0.97 * Math.Pow(1 - Math.Pow((double)countMiss / totalHits, 0.775), countMiss);

            // Combo scaling
            if (Attributes.MaxCombo > 0)
                aimValue *= Math.Min(Math.Pow(scoreMaxCombo, 0.8) / Math.Pow(Attributes.MaxCombo, 0.8), 1.0);

            double approachRateFactor = 0.0;
            if (Attributes.ApproachRate > 10.33)
                approachRateFactor = Attributes.ApproachRate - 10.33;
            else if (Attributes.ApproachRate < 8.0)
                approachRateFactor = 0.025 * (8.0 - Attributes.ApproachRate);

            double approachRateTotalHitsFactor = 1.0 / (1.0 + Math.Exp(-(0.007 * (totalHits - 400))));

            double approachRateBonus = 1.0 + (0.03 + 0.37 * approachRateTotalHitsFactor) * approachRateFactor;

            // We want to give more reward for lower AR when it comes to aim and HD. This nerfs high AR and buffs lower AR.
            if (mods.Any(h => h is OsuModHidden))
                aimValue *= 1.0 + 0.04 * (12.0 - Attributes.ApproachRate);

            double flashlightBonus = 1.0;

            if (mods.Any(h => h is OsuModFlashlight))
            {
                // Apply object-based bonus for flashlight.
                flashlightBonus = 1.0 + 0.35 * Math.Min(1.0, totalHits / 200.0) +
                                  (totalHits > 200
                                      ? 0.3 * Math.Min(1.0, (totalHits - 200) / 300.0) +
                                        (totalHits > 500 ? (totalHits - 500) / 1200.0 : 0.0)
                                      : 0.0);
            }

            aimValue *= Math.Max(flashlightBonus, approachRateBonus);

            // Scale the aim value with accuracy _slightly_
            aimValue *= 0.5 + accuracy / 2.0;
            // It is important to also consider accuracy difficulty when doing that
            aimValue *= 0.98 + Math.Pow(Attributes.OverallDifficulty, 2) / 2500;

            return aimValue;
        }

        private double computeSpeedValue()
        {
            double speedValue = Math.Pow(5.0 * Math.Max(1.0, Attributes.SpeedStrain / 0.0675) - 4.0, 3.0) / 100000.0;

            // Longer maps are worth more
            double lengthBonus = 0.95 + 0.4 * Math.Min(1.0, totalHits / 2000.0) +
                                 (totalHits > 2000 ? Math.Log10(totalHits / 2000.0) * 0.5 : 0.0);
            speedValue *= lengthBonus;

            // Penalize misses by assessing # of misses relative to the total # of objects. Default a 3% reduction for any # of misses.
            if (countMiss > 0)
                speedValue *= 0.97 * Math.Pow(1 - Math.Pow((double)countMiss / totalHits, 0.775), Math.Pow(countMiss, .875));

            // Combo scaling
            if (Attributes.MaxCombo > 0)
                speedValue *= Math.Min(Math.Pow(scoreMaxCombo, 0.8) / Math.Pow(Attributes.MaxCombo, 0.8), 1.0);

            double approachRateFactor = 0.0;
            if (Attributes.ApproachRate > 10.33)
                approachRateFactor = Attributes.ApproachRate - 10.33;

            double approachRateTotalHitsFactor = 1.0 / (1.0 + Math.Exp(-(0.007 * (totalHits - 400))));

            speedValue *= 1.0 + (0.03 + 0.37 * approachRateTotalHitsFactor) * approachRateFactor;

            if (mods.Any(m => m is OsuModHidden))
                speedValue *= 1.0 + 0.04 * (12.0 - Attributes.ApproachRate);

            // Scale the speed value with accuracy and OD
            speedValue *= (0.95 + Math.Pow(Attributes.OverallDifficulty, 2) / 750) * Math.Pow(accuracy, (14.5 - Math.Max(Attributes.OverallDifficulty, 8)) / 2);
            // Scale the speed value with # of 50s to punish doubletapping.
            // speedValue *= Math.Pow(0.98, countMeh < totalHits / 500.0 ? 0 : countMeh - totalHits / 500.0);

            return speedValue;
        }

        private double computeAccuracyValue()
        {
            int circleCount = Attributes.HitCircleCount;

            if (circleCount == 0)
                return 0;

            int adjustedCountGreat = Math.Max(0, countGreat - (totalHits - circleCount));

            if (adjustedCountGreat + countOk + countMeh == 0)
                return 0;

            double greatHitWindow = 79.5 - 6 * Attributes.OverallDifficulty;
            double okHitWindow = 139.5 - 8 * Attributes.OverallDifficulty;
            double mehHitWindow = 199.5 - 10 * Attributes.OverallDifficulty;

            double variance = (adjustedCountGreat * greatHitWindow * greatHitWindow + countOk * okHitWindow * okHitWindow + countMeh * mehHitWindow * mehHitWindow)
                              / (adjustedCountGreat + countOk + countMeh);

            double deviation = Math.Sqrt(variance);

            double estimatedUnstableRate = 10 * deviation / (Math.Sqrt(Math.PI / 8)
                                                             * Math.Log((1 + Math.Exp(-1.0 / (adjustedCountGreat + countOk + countMeh))) / (1 - Math.Exp(-1.0 / (adjustedCountGreat + countOk + countMeh)))));

            double accuracyValue = 1 / estimatedUnstableRate / estimatedUnstableRate;

            if (mods.Any(m => m is OsuModHidden))
                accuracyValue *= 1.08;
            if (mods.Any(m => m is OsuModFlashlight))
                accuracyValue *= 1.02;

            return 2160.8437 * 100 * accuracyValue;
        }

        private int totalHits => countGreat + countOk + countMeh + countMiss;
        private int totalSuccessfulHits => countGreat + countOk + countMeh;
    }
}
