// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.Game.Rulesets.Osu.Difficulty.Utils
{
    public static class Chandrupatla
    {
        public static double FindRoot(Func<double, double> f, double x0, double x1, double epsilon, double expansionFactor = 2)
        {
            int maxIterations = 25;
            double a = x0;
            double b = x1;
            double fa = f(a);
            double fb = f(b);

            while (fa * fb > 0)
            {
                a = b;
                b *= expansionFactor;
                fa = f(a);
                fb = f(b);
            }

            double t = 0.5;

            for (int i = 0; i < maxIterations; i++)
            {
                double xt = a + t * (b - a);
                double ft = f(xt);

                double c;
                double fc;
                if (Math.Sign(ft) == Math.Sign(fa))
                {
                    c = a;
                    fc = fa;
                }
                else
                {
                    c = b;
                    b = a;
                    fc = fb;
                    fb = fa;
                }
                a = xt;
                fa = ft;

                double xm, fm;

                if (Math.Abs(fa) < Math.Abs(fb))
                {
                    xm = a;
                    fm = fa;
                }
                else
                {
                    xm = b;
                    fm = fb;
                }

                if (fm == 0)
                    return xm;

                double tol = 2 * epsilon * Math.Abs(xm) + 2 * epsilon;
                double tlim = tol / Math.Abs(b - c);

                if (tlim > 0.5)
                {
                    return xm;
                }

                double chi = (a - b) / (c - b);
                double phi = (fa - fb) / (fc - fb);
                bool iqi = phi * phi < chi && (1 - phi) * (1 - phi) < chi;

                if (iqi)
                    t = fa / (fb - fa) * fc / (fb - fc) + (c - a) / (b - a) * fa / (fc - fa) * fb / (fc - fb);
                else
                    t = 0.5;

                t = Math.Min(1 - tlim, Math.Max(tlim, t));
            }

            return 0;
        }
    }
}
