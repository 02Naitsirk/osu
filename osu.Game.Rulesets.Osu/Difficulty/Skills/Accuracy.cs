// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using MathNet.Numerics;
using MathNet.Numerics.RootFinding;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty.Evaluators;

namespace osu.Game.Rulesets.Osu.Difficulty.Skills
{
    public class Accuracy : Skill
    {
        private readonly List<double> effectiveHitWindows = new List<double>();

        public Accuracy(Mod[] mods)
            : base(mods)
        {
        }

        private double logLikelihood(double t)
        {
            double p(double hj) => SpecialFunctions.Erf(hj * t);
            double oneMinusP(double hj) => SpecialFunctions.Erfc(hj * t);
            double pPrime(double hj) => Math.Sqrt(2) / Math.PI * hj * Math.Exp(-(hj * t) * (hj * t));

            double x = 0;
            double y = 0;
            double z = 0;

            foreach (double effectiveHitWindow in effectiveHitWindows)
            {
                x += pPrime(effectiveHitWindow) / p(effectiveHitWindow);
                y += oneMinusP(effectiveHitWindow);
                z += pPrime(effectiveHitWindow);
            }

            return x * y - z;
        }

        public override void Process(DifficultyHitObject current)
        {
            double effectiveHitWindow = AccuracyEvaluator.EvaluateEffectiveHitWindow(current);

            if (double.IsFinite(effectiveHitWindow))
            {
                effectiveHitWindows.Add(effectiveHitWindow);
            }
        }

        public override double DifficultyValue()
        {
            double tau = Bisection.FindRoot(logLikelihood, 0, 0.5, 1e-4);
            double sigma = 1 / (Math.Sqrt(2) * tau);

            return sigma;
        }
    }
}
