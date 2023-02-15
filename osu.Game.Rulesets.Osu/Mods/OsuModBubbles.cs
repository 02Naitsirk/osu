// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Diagnostics;
using System.Linq;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Effects;
using osu.Framework.Graphics.Pooling;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Localisation;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Objects.Drawables;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.UI;
using osu.Game.Scoring;
using osuTK;

namespace osu.Game.Rulesets.Osu.Mods
{
    public partial class OsuModBubbles : Mod, IApplicableToDrawableRuleset<OsuHitObject>, IApplicableToDrawableHitObject, IApplicableToScoreProcessor
    {
        public override string Name => "Bubbles";

        public override string Acronym => "BU";

        public override LocalisableString Description => "Don't let their popping distract you!";

        public override double ScoreMultiplier => 1;

        public override ModType Type => ModType.Fun;

        // Compatibility with these seems potentially feasible in the future, blocked for now because they don't work as one would expect
        public override Type[] IncompatibleMods => new[] { typeof(OsuModBarrelRoll), typeof(OsuModMagnetised), typeof(OsuModRepel) };

        private PlayfieldAdjustmentContainer bubbleContainer = null!;

        private readonly Bindable<int> currentCombo = new BindableInt();

        private float maxSize;
        private float bubbleRadius;
        private double bubbleFade;

        private readonly DrawablePool<BubbleDrawable> bubblePool = new DrawablePool<BubbleDrawable>(100);

        public ScoreRank AdjustRank(ScoreRank rank, double accuracy) => rank;

        public void ApplyToScoreProcessor(ScoreProcessor scoreProcessor)
        {
            currentCombo.BindTo(scoreProcessor.Combo);
            currentCombo.BindValueChanged(combo =>
                maxSize = Math.Min(1.75f, (float)(1.25 + 0.005 * combo.NewValue)), true);
        }

        public void ApplyToDrawableRuleset(DrawableRuleset<OsuHitObject> drawableRuleset)
        {
            // Multiplying by 2 results in an initial size that is too large, hence 1.90 has been chosen
            // Also avoids the HitObject bleeding around the edges of the bubble drawable at minimum size
            bubbleRadius = (float)(drawableRuleset.Beatmap.HitObjects.OfType<HitCircle>().First().Radius * 1.90f);
            bubbleFade = drawableRuleset.Beatmap.HitObjects.OfType<HitCircle>().First().TimePreempt * 2;

            // We want to hide the judgements since they are obscured by the BubbleDrawable (due to layering)
            drawableRuleset.Playfield.DisplayJudgements.Value = false;

            bubbleContainer = drawableRuleset.CreatePlayfieldAdjustmentContainer();

            drawableRuleset.Overlays.Add(bubbleContainer);
        }

        public void ApplyToDrawableHitObject(DrawableHitObject drawableObject)
        {
            drawableObject.OnNewResult += (drawable, _) =>
            {
                if (drawable is not DrawableOsuHitObject drawableOsuHitObject) return;

                switch (drawableOsuHitObject.HitObject)
                {
                    case Slider:
                    case SpinnerTick:
                        break;

                    default:
                        addBubble();
                        break;
                }

                void addBubble()
                {
                    BubbleDrawable bubble = bubblePool.Get();

                    bubble.DrawableOsuHitObject = drawableOsuHitObject;
                    bubble.InitialSize = new Vector2(bubbleRadius);
                    bubble.FadeTime = bubbleFade;
                    bubble.MaxSize = maxSize;

                    bubbleContainer.Add(bubble);
                }
            };

            drawableObject.OnRevertResult += (drawable, _) =>
            {
                if (drawable.HitObject is SpinnerTick or Slider) return;

                BubbleDrawable? lastBubble = bubbleContainer.OfType<BubbleDrawable>().LastOrDefault();

                lastBubble?.ClearTransforms();
                lastBubble?.Expire();
            };
        }

        #region Pooled Bubble drawable

        private partial class BubbleDrawable : PoolableDrawable
        {
            public DrawableOsuHitObject? DrawableOsuHitObject { get; set; }

            public Vector2 InitialSize { get; set; }
            public double FadeTime { get; set; }
            public float MaxSize { get; set; }

            private readonly Box colourBox;
            private readonly CircularContainer content;

            public BubbleDrawable()
            {
                Origin = Anchor.Centre;
                InternalChild = content = new CircularContainer
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    RelativeSizeAxes = Axes.Both,
                    MaskingSmoothness = 2,
                    BorderThickness = 0,
                    BorderColour = Colour4.White,
                    Masking = true,
                    EdgeEffect = new EdgeEffectParameters
                    {
                        Type = EdgeEffectType.Shadow,
                        Radius = 3,
                        Colour = Colour4.Black.Opacity(0.05f),
                    },
                    Child = colourBox = new Box { RelativeSizeAxes = Axes.Both, }
                };
            }

            protected override void PrepareForUse()
            {
                Debug.Assert(DrawableOsuHitObject.IsNotNull());

                Colour = DrawableOsuHitObject.IsHit ? Colour4.White : Colour4.Black;
                Alpha = 1;
                Scale = new Vector2(1);
                Position = getPosition();
                Size = InitialSize;
                content.BorderThickness = InitialSize.X / 3.5f;
                content.BorderColour = Colour4.White;

                //We want to fade to a darker colour to avoid colours such as white hiding the "ripple" effect.
                ColourInfo colourDarker = DrawableOsuHitObject.AccentColour.Value.Darken(0.1f);

                // The absolute length of the bubble's animation, can be used in fractions for animations of partial length
                double getAnimationDuration = 1700 + Math.Pow(FadeTime, 1.07f);

                // Main bubble scaling based on combo
                this.ScaleTo(MaxSize, getAnimationDuration * 0.8f)
                    .Then()
                    // Pop at the end of the bubbles life time
                    .ScaleTo(MaxSize * 1.5f, getAnimationDuration * 0.2f, Easing.OutQuint)
                    .FadeOut(getAnimationDuration * 0.2f, Easing.OutCirc).Expire();

                if (!DrawableOsuHitObject.IsHit) return;

                colourBox.FadeColour(colourDarker);

                content.TransformTo(nameof(BorderColour), colourDarker, getAnimationDuration * 0.3f, Easing.OutQuint);
                // Ripple effect utilises the border to reduce drawable count
                content.TransformTo(nameof(BorderThickness), 2f, getAnimationDuration * 0.3f, Easing.OutQuint)
                       // Avoids transparency overlap issues during the bubble "pop"
                       .Then().Schedule(() => content.BorderThickness = 0);

                Vector2 getPosition()
                {
                    switch (DrawableOsuHitObject)
                    {
                        // SliderHeads are derived from HitCircles,
                        // so we must handle them before to avoid them using the wrong positioning logic
                        case DrawableSliderHead:
                            return DrawableOsuHitObject.HitObject.Position;

                        // Using hitobject position will cause issues with HitCircle placement due to stack leniency.
                        case DrawableHitCircle:
                            return DrawableOsuHitObject.Position;

                        default:
                            return DrawableOsuHitObject.HitObject.Position;
                    }
                }
            }
        }

        #endregion
    }
}
