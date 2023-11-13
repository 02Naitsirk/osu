﻿// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.Game.Rulesets.Objects.Types
{
    public enum SplineType
    {
        Catmull,
        BSpline,
        Linear,
        PerfectCurve
    }

    public readonly struct PathType : IEquatable<PathType>
    {
        public static readonly PathType CATMULL = new PathType(SplineType.Catmull);
        public static readonly PathType BEZIER = new PathType(SplineType.BSpline);
        public static readonly PathType LINEAR = new PathType(SplineType.Linear);
        public static readonly PathType PERFECT_CURVE = new PathType(SplineType.PerfectCurve);

        /// <summary>
        /// The type of the spline that should be used to interpret the control points of the path.
        /// </summary>
        public SplineType Type { get; init; }

        /// <summary>
        /// The degree of a BSpline. Unused if <see cref="Type"/> is not <see cref="SplineType.BSpline"/>.
        /// Null means the degree is equal to the number of control points, 1 means linear, 2 means quadratic, etc.
        /// </summary>
        public int? Degree { get; init; }

        public PathType(SplineType splineType)
        {
            Type = splineType;
            Degree = null;
        }

        public override int GetHashCode()
            => HashCode.Combine(Type, Degree);

        public override bool Equals(object? obj)
            => obj is PathType pathType && this == pathType;

        public static bool operator ==(PathType a, PathType b)
            => a.Type == b.Type && a.Degree == b.Degree;

        public static bool operator !=(PathType a, PathType b)
            => a.Type != b.Type || a.Degree != b.Degree;

        public static PathType BSpline(int degree)
        {
            if (degree <= 0)
                throw new ArgumentOutOfRangeException(nameof(degree), "The degree of a B-Spline path must be greater than zero.");

            return new PathType { Type = SplineType.BSpline, Degree = degree };
        }

        public bool Equals(PathType other)
            => Type == other.Type && Degree == other.Degree;
    }
}
