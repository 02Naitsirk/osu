// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using MathNet.Numerics;
using MathNet.Numerics.Distributions;
using MathNet.Numerics.RootFinding;
using osu.Framework.Audio.Track;
using osu.Framework.Extensions.IEnumerableExtensions;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;

namespace osu.Game.Rulesets.Osu.Difficulty
{
    public class OsuPerformanceCalculator : PerformanceCalculator
    {
        private double accuracy;
        private int scoreMaxCombo;
        private int countGreat;
        private int countOk;
        private int countMeh;
        private int countMiss;

        private double effectiveMissCount;
        private double deviation;
        private double speedDeviation;

        public OsuPerformanceCalculator()
            : base(new OsuRuleset())
        {
        }

        protected override PerformanceAttributes CreatePerformanceAttributes(ScoreInfo score, DifficultyAttributes attributes)
        {
            var osuAttributes = (OsuDifficultyAttributes)attributes;

            accuracy = score.Accuracy;
            scoreMaxCombo = score.MaxCombo;
            countGreat = score.Statistics.GetValueOrDefault(HitResult.Great);
            countOk = score.Statistics.GetValueOrDefault(HitResult.Ok);
            countMeh = score.Statistics.GetValueOrDefault(HitResult.Meh);
            countMiss = score.Statistics.GetValueOrDefault(HitResult.Miss);
            effectiveMissCount = calculateEffectiveMissCount(osuAttributes);
            deviation = calculateDeviation(score, osuAttributes);
            speedDeviation = calculateSpeedDeviation(score, osuAttributes);

            const double multiplier = 1.0; // This is being adjusted to keep the final pp value scaled around what it used to be when changing things.

            double aimValue = computeAimValue(score, osuAttributes);
            double speedValue = computeSpeedValue(score, osuAttributes);
            double accuracyValue = computeAccuracyValue(score);
            double flashlightValue = computeFlashlightValue(score, osuAttributes);
            double totalValue = multiplier * (aimValue + speedValue + accuracyValue + flashlightValue);

            return new OsuPerformanceAttributes
            {
                Aim = aimValue,
                Speed = speedValue,
                Accuracy = accuracyValue,
                Flashlight = flashlightValue,
                EffectiveMissCount = effectiveMissCount,
                Deviation = deviation,
                SpeedDeviation = speedDeviation,
                Total = totalValue
            };
        }

        private double computeAimValue(ScoreInfo score, OsuDifficultyAttributes attributes)
        {
            double aimDifficulty = attributes.AimDifficulty;

            if (totalSuccessfulHits == 0)
                return 0;

            // Penalize misses. This is an approximation of skill level derived from assuming all objects have equal hit probabilities.
            if (effectiveMissCount > 0)
            {
                // Since star rating is difficulty^0.829842642, we should raise the miss penalty to this power as well.
                aimDifficulty *= Math.Pow(calculateMissPenalty(), 0.829842642);
            }

            double aimValue = Math.Pow(aimDifficulty, 3);

            // Temporarily handling of slider-only maps:
            if (attributes.HitCircleCount - countMiss == 0)
                return aimValue;

            if (score.Mods.Any(h => h is OsuModHidden))
            {
                // We want to give more reward for lower AR when it comes to aim and HD. This nerfs high AR and buffs lower AR.
                aimValue *= 1.0 + 0.04 * (12.0 - attributes.ApproachRate);
            }

            // Scale the aim value with deviation
            aimValue *= SpecialFunctions.Erf(50 / (Math.Sqrt(2) * deviation));

            return aimValue;
        }

        private double computeSpeedValue(ScoreInfo score, OsuDifficultyAttributes attributes)
        {
            if (score.Mods.Any(h => h is OsuModRelax) || totalSuccessfulHits == 0)
                return 0.0;

            double speedValue = Math.Pow(attributes.SpeedDifficulty, 3);

            if (score.Mods.Any(m => m is OsuModHidden))
            {
                // We want to give more reward for lower AR when it comes to aim and HD. This nerfs high AR and buffs lower AR.
                speedValue *= 1.0 + 0.04 * (12.0 - attributes.ApproachRate);
            }

            double lengthBonus = 0.95 + 0.4 * Math.Min(1.0, totalHits / 2000.0) +
                                 (totalHits > 2000 ? Math.Log10(totalHits / 2000.0) * 0.5 : 0.0);
            speedValue *= lengthBonus;

            // Scale the speed value with speed deviation
            speedValue *= 120.289 / 108 * Math.Pow(SpecialFunctions.Erf(26 / (Math.Sqrt(2) * speedDeviation)), 2);

            return speedValue;
        }

        private double computeAccuracyValue(ScoreInfo score)
        {
            if (score.Mods.Any(h => h is OsuModRelax) || totalSuccessfulHits == 0)
                return 0.0;

            // This formula is based on the previous accuracy formula to keep values similar, but it caps SS pp.
            // Eventually, this should be changed to a power law to make SS pp uncapped.
            double accuracyValue = 763.087 * Math.Exp(-0.230237 * deviation);

            // Increasing the accuracy value by object count for Blinds isn't ideal, so the minimum buff is given.
            if (score.Mods.Any(m => m is OsuModBlinds))
                accuracyValue *= 1.14;
            else if (score.Mods.Any(m => m is OsuModHidden))
                accuracyValue *= 1.08;

            if (score.Mods.Any(m => m is OsuModFlashlight))
                accuracyValue *= 1.02;

            accuracyValue *= 1 - (double)countMiss / totalHits;

            return accuracyValue;
        }

        private double computeFlashlightValue(ScoreInfo score, OsuDifficultyAttributes attributes)
        {
            if (!score.Mods.Any(h => h is OsuModFlashlight))
                return 0.0;

            double rawFlashlight = attributes.FlashlightDifficulty;

            if (score.Mods.Any(m => m is OsuModTouchDevice))
                rawFlashlight = Math.Pow(rawFlashlight, 0.8);

            double flashlightValue = Math.Pow(rawFlashlight, 2.0) * 25.0;

            // Penalize misses by assessing # of misses relative to the total # of objects. Default a 3% reduction for any # of misses.
            if (effectiveMissCount > 0)
                flashlightValue *= 0.97 * Math.Pow(1 - Math.Pow(effectiveMissCount / totalHits, 0.775), Math.Pow(effectiveMissCount, .875));

            flashlightValue *= getComboScalingFactor(attributes);

            // Account for shorter maps having a higher ratio of 0 combo/100 combo flashlight radius.
            flashlightValue *= 0.7 + 0.1 * Math.Min(1.0, totalHits / 200.0) +
                               (totalHits > 200 ? 0.2 * Math.Min(1.0, (totalHits - 200) / 200.0) : 0.0);

            // Scale the flashlight value with accuracy _slightly_.
            flashlightValue *= 0.5 + accuracy / 2.0;
            // It is important to also consider accuracy difficulty when doing that.
            flashlightValue *= 0.98 + Math.Pow(attributes.OverallDifficulty, 2) / 2500;

            return flashlightValue;
        }

        private double calculateDeviation(ScoreInfo score, OsuDifficultyAttributes attributes)
        {
            if (totalSuccessfulHits == 0)
                return double.PositiveInfinity;

            int inaccuracies = countMeh + countOk;
            int hitCirclesMinusMisses = attributes.HitCircleCount - countMiss;
            double hitWindow300 = 80 - 6 * attributes.OverallDifficulty;

            if (hitCirclesMinusMisses - inaccuracies <= 0)
            {
                return double.PositiveInfinity;
            }

            double getDeviationAt(double d)
            {
                double p = SpecialFunctions.Erfc(hitWindow300 / (Math.Sqrt(2) * d));
                return Binomial.CDF(p, hitCirclesMinusMisses, inaccuracies);
            }

            double expectedDeviation = Integrate.DoubleExponential(getDeviationAt, 0, double.PositiveInfinity, 1e-4);
            return expectedDeviation;
        }

        private double calculateSpeedDeviation(ScoreInfo score, OsuDifficultyAttributes attributes)
        {
            if (totalSuccessfulHits == 0)
                return double.PositiveInfinity;

            var track = new TrackVirtual(10000);
            score.Mods.OfType<IApplicableToTrack>().ForEach(m => m.ApplyToTrack(track));
            double clockRate = track.Rate;

            double hitWindow300 = 80 - 6 * attributes.OverallDifficulty;
            double hitWindow50 = (200 - 10 * ((80 - hitWindow300 * clockRate) / 6)) / clockRate;
            double root2 = Math.Sqrt(2);

            double relevantTotalDiff = totalHits - attributes.SpeedNoteCount;
            double relevantCountGreat = Math.Max(0, countGreat - relevantTotalDiff);
            double relevantCountOk = Math.Max(0, countOk - Math.Max(0, relevantTotalDiff - countGreat)) + 1;
            double relevantCountMeh = Math.Max(0, countMeh - Math.Max(0, relevantTotalDiff - countGreat - countOk));

            // Derivative of erf(x)
            double erfPrime(double x) => 2 / Math.Sqrt(Math.PI) * Math.Exp(-x * x);

            // Let f(x) = erf(x). To find the deviation, we have to maximize the log-likelihood function,
            // which is the same as finding the zero of the derivative of the log-likelihood function.
            double logLikelihoodGradient(double u)
            {
                double t1 = -hitWindow300 * relevantCountGreat * erfPrime(hitWindow300 / (root2 * u)) / SpecialFunctions.Erf(hitWindow300 / (root2 * u));
                double t2 = (relevantCountOk + relevantCountMeh) * (-hitWindow50 * erfPrime(hitWindow50 / (root2 * u)) + hitWindow300 * erfPrime(hitWindow300 / (root2 * u))) / (SpecialFunctions.Erfc(hitWindow300 / (root2 * u)) - SpecialFunctions.Erfc(hitWindow50 / (root2 * u)));
                return (t1 + t2) / (root2 * u * u);
            }

            return Brent.FindRootExpand(logLikelihoodGradient, 2, 20, 1e-6, expandFactor: 2);
        }

        private double calculateEffectiveMissCount(OsuDifficultyAttributes attributes)
        {
            // Guess the number of misses + slider breaks from combo
            double comboBasedMissCount = 0.0;

            if (attributes.SliderCount > 0)
            {
                double fullComboThreshold = attributes.MaxCombo - 0.1 * attributes.SliderCount;
                if (scoreMaxCombo < fullComboThreshold)
                    comboBasedMissCount = fullComboThreshold / Math.Max(1.0, scoreMaxCombo);
            }

            // Clamp miss count since it's derived from combo and can be higher than total hits and that breaks some calculations
            comboBasedMissCount = Math.Min(comboBasedMissCount, totalHits);

            return Math.Max(countMiss, comboBasedMissCount);
        }

        /// <summary>
        /// Imagine a map with n objects, where all objects have equal difficulty d.
        /// d * sqrt(2) * s(n,0) will return the FC difficulty of that map.
        /// d * sqrt(2) * s(n,m) will return the m-miss difficulty of that map.
        /// Since we are given FC difficulty, for a score with m misses, we can obtain
        /// the difficulty for m misses by multiplying the difficulty by s(n,m) / s(n,0).
        /// Note that the term d * sqrt(2) gets canceled when taking the ratio.
        /// </summary>
        private double calculateMissPenalty()
        {
            int n = totalHits;

            if (n == 0)
                return 0;

            double s(double m)
            {
                double y = SpecialFunctions.ErfInv((n - m) / (n + 1));
                // Derivatives of ErfInv:
                double y1 = Math.Exp(y * y) * Math.Sqrt(Math.PI) / 2;
                double y2 = 2 * y * y1 * y1;
                double y3 = 2 * y1 * (y * y2 + (2 * (y * y) + 1) * (y1 * y1));
                double y4 = 2 * y1 * (y * y3 + (6 * (y * y) + 3) * y1 * y2 + (4 * (y * y * y) + 6 * y) * (y1 * y1 * y1));
                // Central moments of Beta distribution:
                double a = n - m;
                double b = m + 1;
                double u2 = a * b / ((a + b) * (a + b) * (a + b + 1));
                double u3 = 2 * (b - a) * a * b / ((a + b + 2) * (a + b) * (a + b) * (a + b) * (a + b + 1));
                double u4 = (3 + 6 * ((a - b) * (a + b + 1) - a * b * (a + b + 2)) / (a * b * (a + b + 2) * (a + b + 3))) * (u2 * u2);
                return Math.Sqrt(2) * (y + 0.5 * y2 * u2 + 1 / 6.0 * y3 * u3 + 1 / 24.0 * y4 * u4);
            }

            return s(effectiveMissCount) / s(0);
        }

        private double getComboScalingFactor(OsuDifficultyAttributes attributes) => attributes.MaxCombo <= 0 ? 1.0 : Math.Min(Math.Pow(scoreMaxCombo, 0.8) / Math.Pow(attributes.MaxCombo, 0.8), 1.0);
        private int totalHits => countGreat + countOk + countMeh + countMiss;
        private int totalSuccessfulHits => countGreat + countOk + countMeh;
    }
}
