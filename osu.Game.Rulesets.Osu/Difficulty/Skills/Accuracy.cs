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

        private double ssProbability(double deviation)
        {
            if (deviation == 0)
                return 1;

            double p = 1.0;
            foreach (double effectiveHitWindow in effectiveHitWindows)
                p *= SpecialFunctions.Erf(effectiveHitWindow / (Math.Sqrt(2) * deviation));

            return p;
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
            double threshold = 0.01;
            return Brent.FindRootExpand(d => ssProbability(d) - threshold, 0, 20, 1e-4);
        }
    }
}
