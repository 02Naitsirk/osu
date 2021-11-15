﻿// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using Newtonsoft.Json;
using osu.Game.Rulesets.Difficulty;

namespace osu.Game.Rulesets.Mania.Difficulty
{
    public class ManiaDifficultyAttributes : DifficultyAttributes
    {
        [JsonProperty("great_hit_window")]
        public double GreatHitWindow { get; set; }

        [JsonProperty("score_multiplier")]
        public double ScoreMultiplier { get; set; }

        public override IEnumerable<(int attributeId, object value)> ToDatabaseAttributes()
        {
            foreach (var v in base.ToDatabaseAttributes())
                yield return v;

            // Todo: Mania doesn't output MaxCombo attribute for some reason.
            yield return (11, StarRating);
            yield return (13, GreatHitWindow);
            yield return (15, ScoreMultiplier);
        }

        public override void FromDatabaseAttributes(IReadOnlyDictionary<int, double> values)
        {
            base.FromDatabaseAttributes(values);

            StarRating = values[11];
            GreatHitWindow = values[13];
            ScoreMultiplier = values[15];
        }
    }
}
