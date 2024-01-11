// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Runtime.InteropServices;
using osu.Framework.Allocation;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Shaders;
using osu.Framework.Graphics.Shaders.Types;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Play.HUD.ArgonHealthDisplayParts
{
    public partial class ArgonHealthDisplayBar : Box
    {
        private float endProgress = 1f;

        public float EndProgress
        {
            get => endProgress;
            set
            {
                if (endProgress == value)
                    return;

                endProgress = value;
                Invalidate(Invalidation.DrawNode);
            }
        }

        private float startProgress;

        public float StartProgress
        {
            get => startProgress;
            set
            {
                if (startProgress == value)
                    return;

                startProgress = value;
                Invalidate(Invalidation.DrawNode);
            }
        }

        private float radius = 10f;

        public float PathRadius
        {
            get => radius;
            set
            {
                if (radius == value)
                    return;

                radius = value;
                Invalidate(Invalidation.DrawNode);
            }
        }

        private float padding = 10f;

        public float PathPadding
        {
            get => padding;
            set
            {
                if (padding == value)
                    return;

                padding = value;
                Invalidate(Invalidation.DrawNode);
            }
        }

        private float glowPortion;

        public float GlowPortion
        {
            get => glowPortion;
            set
            {
                if (glowPortion == value)
                    return;

                glowPortion = value;
                Invalidate(Invalidation.DrawNode);
            }
        }

        private Colour4 barColour = Color4.White;

        public Colour4 BarColour
        {
            get => barColour;
            set
            {
                if (barColour == value)
                    return;

                barColour = value;
                Invalidate(Invalidation.DrawNode);
            }
        }

        private Colour4 glowColour = Color4.White.Opacity(0);

        public Colour4 GlowColour
        {
            get => glowColour;
            set
            {
                if (glowColour == value)
                    return;

                glowColour = value;
                Invalidate(Invalidation.DrawNode);
            }
        }

        [BackgroundDependencyLoader]
        private void load(ShaderManager shaders)
        {
            TextureShader = shaders.Load(VertexShaderDescriptor.TEXTURE_2, "ArgonBarPath");
        }

        protected override void Update()
        {
            base.Update();
            Invalidate(Invalidation.DrawNode);
        }

        protected override DrawNode CreateDrawNode() => new ArgonBarPathDrawNode(this);

        private class ArgonBarPathDrawNode : SpriteDrawNode
        {
            protected new ArgonHealthDisplayBar Source => (ArgonHealthDisplayBar)base.Source;

            public ArgonBarPathDrawNode(ArgonHealthDisplayBar source)
                : base(source)
            {
            }

            private Vector2 size;
            private float startProgress;
            private float endProgress;
            private float pathRadius;
            private float padding;
            private float glowPortion;
            private Color4 barColour;
            private Color4 glowColour;

            public override void ApplyState()
            {
                base.ApplyState();

                size = Source.DrawSize;
                endProgress = Source.endProgress;
                startProgress = Math.Min(Source.startProgress, endProgress);
                pathRadius = Source.PathRadius;
                padding = Source.PathPadding;
                glowPortion = Source.GlowPortion;
                barColour = Source.barColour;
                glowColour = Source.glowColour;
            }

            protected override void Draw(IRenderer renderer)
            {
                if (pathRadius == 0)
                    return;

                base.Draw(renderer);
            }

            private IUniformBuffer<ArgonBarPathParameters> parametersBuffer;

            protected override void BindUniformResources(IShader shader, IRenderer renderer)
            {
                base.BindUniformResources(shader, renderer);

                parametersBuffer ??= renderer.CreateUniformBuffer<ArgonBarPathParameters>();
                parametersBuffer.Data = new ArgonBarPathParameters
                {
                    BarColour = new Vector4(barColour.R, barColour.G, barColour.B, barColour.A),
                    GlowColour = new Vector4(glowColour.R, glowColour.G, glowColour.B, glowColour.A),
                    GlowPortion = glowPortion,
                    Size = size,
                    StartProgress = startProgress,
                    EndProgress = endProgress,
                    PathRadius = pathRadius,
                    Padding = padding
                };

                shader.BindUniformBlock("m_ArgonBarPathParameters", parametersBuffer);
            }

            protected override bool CanDrawOpaqueInterior => false;

            protected override void Dispose(bool isDisposing)
            {
                base.Dispose(isDisposing);
                parametersBuffer?.Dispose();
            }

            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            private record struct ArgonBarPathParameters
            {
                public UniformVector4 BarColour;
                public UniformVector4 GlowColour;
                public UniformVector2 Size;
                public UniformFloat StartProgress;
                public UniformFloat EndProgress;
                public UniformFloat PathRadius;
                public UniformFloat Padding;
                public UniformFloat GlowPortion;
                private readonly UniformPadding4 pad;
            }
        }
    }
}
