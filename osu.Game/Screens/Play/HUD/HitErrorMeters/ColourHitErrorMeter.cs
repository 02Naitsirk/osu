// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Configuration;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Scoring;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Play.HUD.HitErrorMeters
{
    public class ColourHitErrorMeter : HitErrorMeter
    {
        private const int animation_duration = 200;
        private const int drawable_judgement_size = 8;

        [SettingSource("Hit error amount", "Number of hit error shapes")]
        public BindableNumber<int> HitShapeCount { get; } = new BindableNumber<int>(20)
        {
            MinValue = 1,
            MaxValue = 30,
            Precision = 1
        };

        [SettingSource("Opacity", "Visibility of object")]
        public BindableNumber<float> HitShapeOpacity { get; } = new BindableNumber<float>(1)
        {
            MinValue = 0.01f,
            MaxValue = 1,
            Precision = 0.01f,
        };

        [SettingSource("Spacing", "Space between hit error shapes")]
        public BindableNumber<float> HitShapeSpacing { get; } = new BindableNumber<float>(2)
        {
            MinValue = 0,
            MaxValue = 10,
            Precision = 0.1f
        };

        [SettingSource("Shape", "The shape of each displayed error")]
        public Bindable<ShapeStyle> HitShape { get; } = new Bindable<ShapeStyle>();

        private readonly JudgementFlow judgementsFlow;

        public ColourHitErrorMeter()
        {
            AutoSizeAxes = Axes.Both;
            InternalChild = judgementsFlow = new JudgementFlow();
        }

        protected override void OnNewJudgement(JudgementResult judgement)
        {
            if (!judgement.Type.IsScorable() || judgement.Type.IsBonus())
                return;

            judgementsFlow.Push(GetColourForHitResult(judgement.Type), HitShapeCount.Value);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            HitShapeOpacity.BindValueChanged(_ => judgementsFlow.Alpha = HitShapeOpacity.Value, true);
            HitShapeSpacing.BindValueChanged(_ =>
            {
                judgementsFlow.Height = HitShapeCount.Value * (drawable_judgement_size + HitShapeSpacing.Value) - HitShapeSpacing.Value;
                judgementsFlow.Spacing = new Vector2(0, HitShapeSpacing.Value);
            }, true);
            HitShapeCount.BindValueChanged(_ =>
            {
                judgementsFlow.Clear();
                judgementsFlow.Height = HitShapeCount.Value * (drawable_judgement_size + HitShapeSpacing.Value) - HitShapeSpacing.Value;
            }, true);
        }

        public override void Clear() => judgementsFlow.Clear();

        private class JudgementFlow : FillFlowContainer<HitErrorShape>
        {
            public override IEnumerable<Drawable> FlowingChildren => base.FlowingChildren.Reverse();

            public readonly Bindable<ShapeStyle> Shape = new Bindable<ShapeStyle>();

            public JudgementFlow()
            {
                Width = drawable_judgement_size;
                Direction = FillDirection.Vertical;
                LayoutDuration = animation_duration;
                LayoutEasing = Easing.OutQuint;
            }

            public void Push(Color4 colour, int maxErrorShapeCount)
            {
                Add(new HitErrorShape(colour, drawable_judgement_size)
                {
                    Shape = { BindTarget = Shape },
                });

                if (Children.Count > maxErrorShapeCount)
                    Children.FirstOrDefault(c => !c.IsRemoved)?.Remove();
            }
        }

        public class HitErrorShape : Container
        {
            public bool IsRemoved { get; private set; }

            public readonly Bindable<ShapeStyle> Shape = new Bindable<ShapeStyle>();

            private readonly Color4 colour;

            public HitErrorShape(Color4 colour, int size)
            {
                this.colour = colour;
                Size = new Vector2(size);
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();

                Shape.BindValueChanged(shape =>
                {
                    switch (shape.NewValue)
                    {
                        case ShapeStyle.Circle:
                            Child = new Circle
                            {
                                RelativeSizeAxes = Axes.Both,
                                Alpha = 0,
                                Colour = colour
                            };
                            break;

                        case ShapeStyle.Square:
                            Child = new Box
                            {
                                RelativeSizeAxes = Axes.Both,
                                Alpha = 0,
                                Colour = colour
                            };
                            break;
                    }
                }, true);

                Child.FadeInFromZero(animation_duration, Easing.OutQuint);
                Child.MoveToY(-DrawSize.Y);
                Child.MoveToY(0, animation_duration, Easing.OutQuint);
            }

            public void Remove()
            {
                IsRemoved = true;

                this.FadeOut(animation_duration, Easing.OutQuint).Expire();
            }
        }

        public enum ShapeStyle
        {
            Circle,
            Square
        }
    }
}
