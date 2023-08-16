// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using MathNet.Numerics;
using MathNet.Numerics.RootFinding;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;

namespace osu.Game.Rulesets.Osu.Difficulty.Evaluators
{
    public static class AimEvaluator
    {
        private const double numerical_algorithm_accuracy = 1e-3;

        /// <summary>
        /// Evaluates the difficulty of successfully aiming at the current object.
        /// Aim difficulty is calculated by finding the average speed inside of the current circle.
        /// This is done by integrating the magnitude of velocity from the time the player enters the note to the time the player exits,
        /// and then dividing by the amount of time spent inside the note.
        /// </summary>
        public static double EvaluateDifficultyOf(DifficultyHitObject current)
        {
            var osuPrevObj = (OsuDifficultyHitObject)current.Previous(0);
            var osuCurrObj = (OsuDifficultyHitObject)current;
            var osuNextObj = (OsuDifficultyHitObject)current.Next(0);

            double aimDifficulty = 0;
            double timeInNote = 0;

            if (osuPrevObj == null)
            {
                timeInNote += osuCurrObj.HitWindowMeh / 2;
            }
            else
            {
                double currentNormalizedX = osuCurrObj.NormalizedX;
                double currentNormalizedY = osuCurrObj.NormalizedY;
                double previousNormalizedX = osuPrevObj.NormalizedX;
                double previousNormalizedY = osuPrevObj.NormalizedY;

                double xPosition(double t) => positionVectorAt(osuCurrObj, t)[0];
                double yPosition(double t) => positionVectorAt(osuCurrObj, t)[1];
                double xVelocity(double t) => velocityVectorAt(osuCurrObj, t)[0];
                double yVelocity(double t) => velocityVectorAt(osuCurrObj, t)[1];

                double speed(double t) => Math.Sqrt(Math.Pow(xVelocity(t), 2) + Math.Pow(yVelocity(t), 2));

                double realSquaredDistance = (currentNormalizedX - previousNormalizedX) * (currentNormalizedX - previousNormalizedX) +
                                             (currentNormalizedY - previousNormalizedY) * (currentNormalizedY - previousNormalizedY);

                // If the current and previous objects are overlapped by 50% or more, then hitting the previous object would imply the cursor is already inside the current object.
                // This means that the player enters the current note as soon as they clicked the previous note, so just add the current delta time.
                if (realSquaredDistance <= 1)
                {
                    aimDifficulty += Integrate.DoubleExponential(speed, 0, osuCurrObj.StrainTime, numerical_algorithm_accuracy);
                    timeInNote += osuCurrObj.StrainTime;
                }
                // Otherwise, the player takes time to travel from the previous object to the current object.
                // This finds when the player enters the current note.
                else
                {
                    double root(double t) => Math.Pow(xPosition(t) - osuCurrObj.NormalizedX, 2) + Math.Pow(yPosition(t) - osuCurrObj.NormalizedY, 2) - 1;
                    double timeEnterNote = Brent.FindRoot(root, 0, osuCurrObj.StrainTime, numerical_algorithm_accuracy);

                    aimDifficulty += Integrate.DoubleExponential(speed, timeEnterNote, osuCurrObj.StrainTime, numerical_algorithm_accuracy);
                    timeInNote += osuCurrObj.StrainTime - timeEnterNote;
                }
            }

            if (osuNextObj == null)
            {
                timeInNote += osuCurrObj.HitWindowMeh / 2;
            }
            else
            {
                double xPosition(double t) => positionVectorAt(osuNextObj, t)[0];
                double yPosition(double t) => positionVectorAt(osuNextObj, t)[1];
                double xVelocity(double t) => velocityVectorAt(osuNextObj, t)[0];
                double yVelocity(double t) => velocityVectorAt(osuNextObj, t)[1];

                double speed(double t) => Math.Sqrt(Math.Pow(xVelocity(t), 2) + Math.Pow(yVelocity(t), 2));

                double realSquaredDistance = Math.Pow(osuNextObj.NormalizedX - osuCurrObj.NormalizedX, 2) + Math.Pow(osuNextObj.NormalizedY - osuCurrObj.NormalizedY, 2);

                if (realSquaredDistance <= 1)
                {
                    aimDifficulty += Integrate.DoubleExponential(speed, 0, osuNextObj.StrainTime, numerical_algorithm_accuracy);
                    timeInNote += osuNextObj.StrainTime;
                }
                else
                {
                    double root(double t) => Math.Pow(xPosition(t) - osuCurrObj.NormalizedX, 2) + Math.Pow(yPosition(t) - osuCurrObj.NormalizedY, 2) - 1;
                    double timeExitNote = Brent.FindRoot(root, 0, osuNextObj.StrainTime, numerical_algorithm_accuracy);

                    aimDifficulty += Integrate.DoubleExponential(speed, 0, timeExitNote, numerical_algorithm_accuracy);
                    timeInNote += timeExitNote;
                }
            }

            return aimDifficulty / timeInNote;
        }

        /// <summary>
        /// Returns the cursor's position at some time <paramref name="t"/>, where t can range from 0 to this <paramref name="hitObject"/>'s DeltaTime,
        /// based on the path generated by <see cref="positionFunction"/>.
        /// At <paramref name="t"/> = 0, the function returns the coordinates of the previous object.
        /// At <paramref name="t"/> = DeltaTime, the function returns the coordinates of the current object.
        /// </summary>
        private static double[] positionVectorAt(DifficultyHitObject hitObject, double t)
        {
            var osuPrevObj = (OsuDifficultyHitObject)hitObject.Previous(0);
            var osuCurrObj = (OsuDifficultyHitObject)hitObject;

            double[] previousVelocityVector = generateVelocityVectorAt(osuPrevObj);
            double[] currentVelocityVector = generateVelocityVectorAt(osuCurrObj);

            double deltaTime = osuCurrObj.StrainTime;

            double ix = previousVelocityVector[0]; // Velocity along the x direction of the previous object
            double iy = previousVelocityVector[1]; // Velocity along the y direction of the previous object
            double fx = currentVelocityVector[0]; // Velocity along the x direction of the current object
            double fy = currentVelocityVector[1]; // Velocity along the y direction of the current object

            double xComponent = positionFunction(osuPrevObj.NormalizedX, osuCurrObj.NormalizedX, ix, fx, deltaTime, t);
            double yComponent = positionFunction(osuPrevObj.NormalizedY, osuCurrObj.NormalizedY, iy, fy, deltaTime, t);

            double[] positionVector = { xComponent, yComponent };
            return positionVector;
        }

        /// <summary>
        /// Returns the cursor's velocity at some time <paramref name="t"/>, where <paramref name="t"/> can range from 0 to the <paramref name="hitObject"/>'s DeltaTIme.
        /// </summary>
        private static double[] velocityVectorAt(DifficultyHitObject hitObject, double t)
        {
            var osuPrevObj = (OsuDifficultyHitObject)hitObject.Previous(0);
            var osuCurrObj = (OsuDifficultyHitObject)hitObject;

            double[] previousVelocityVector = generateVelocityVectorAt(osuPrevObj);
            double[] currentVelocityVector = generateVelocityVectorAt(osuCurrObj);

            double deltaTime = osuCurrObj.StrainTime;

            double ix = previousVelocityVector[0]; // Velocity along the x direction of the previous object
            double iy = previousVelocityVector[1]; // Velocity along the y direction of the previous object
            double fx = currentVelocityVector[0]; // Velocity along the x direction of the current object
            double fy = currentVelocityVector[1]; // Velocity along the y direction of the current object

            double xComponent = velocityFunction(osuPrevObj.NormalizedX, osuCurrObj.NormalizedX, ix, fx, deltaTime, t);
            double yComponent = velocityFunction(osuPrevObj.NormalizedY, osuCurrObj.NormalizedY, iy, fy, deltaTime, t);

            double[] velocityVector = { xComponent, yComponent };
            return velocityVector;
        }

        /// <summary>
        /// Defines the velocity vector of the <paramref name="hitObject"/> at the time the <paramref name="hitObject"/> is located at.
        /// This vector will determine how the player is moving when they reach the <paramref name="hitObject"/>.
        /// A vector close to (0, 0) means the player almost fully stops at the note (snap aim),
        /// whereas a vector far from (0, 0) indicates the player is moving rapidly through the note (flow aim).
        /// </summary>
        private static double[] generateVelocityVectorAt(DifficultyHitObject hitObject)
        {
            var osuPrevObj = (OsuDifficultyHitObject)hitObject.Previous(0);
            var osuNextObj = (OsuDifficultyHitObject)hitObject.Next(0);

            double[] velocityVector;

            if (osuPrevObj == null || osuNextObj == null)
            {
                velocityVector = new[] { 0.0, 0.0 };
                return velocityVector;
            }

            double previousNextStrainTime = osuNextObj.StartTime - osuPrevObj.StartTime;
            double xComponent = (osuNextObj.NormalizedX - osuPrevObj.NormalizedX) / previousNextStrainTime;
            double yComponent = (osuNextObj.NormalizedY - osuPrevObj.NormalizedY) / previousNextStrainTime;
            velocityVector = new[] { xComponent, yComponent };

            return velocityVector;
        }

        /// <summary>
        /// Generates a path along one axis from the previous note to the current one, and returns the cursor's location at a time <paramref name="t"/>,
        /// ranging from 0 to the current object's delta time.
        /// </summary>
        /// <param name="x0">
        /// Location of the previous note.
        /// </param>
        /// <param name="x1">
        /// Location of the current note.
        /// </param>
        /// <param name="v0">
        /// Velocity at the previous note.
        /// </param>
        /// <param name="v1">
        /// Velocity at the current note.
        /// </param>
        /// <param name="dt">
        /// Time between the current and previous notes.
        /// </param>
        /// <param name="t">
        /// Arbitrary time t.
        /// </param>
        private static double positionFunction(double x0, double x1, double v0, double v1, double dt, double t)
        {
            double tMinusDt = t - dt;
            double t2 = t * t;
            double t3 = t2 * t;

            double dt2 = dt * dt;
            double dt5 = dt2 * dt * dt * dt;

            double a = -x0 * tMinusDt * tMinusDt * tMinusDt * (6 * t2 + 3 * t * dt + dt2);
            double b = x1 * t3 * (6 * t2 - 15 * t * dt + 10 * dt2);
            double c = -t * tMinusDt * dt * (v1 * t2 * (3 * t - 4 * dt) + v0 * tMinusDt * tMinusDt * (3 * t + dt));

            return (a + b + c) / dt5;
        }

        /// <summary>
        /// The derivative of the path generated by <see cref="positionFunction"/>. Returns the velocity at a time <paramref name="t"/>,
        /// ranging from 0 to the current object's delta time.
        /// </summary>
        /// <param name="x0">
        /// Location of the previous note.
        /// </param>
        /// <param name="x1">
        /// Location of the current note.
        /// </param>
        /// <param name="v0">
        /// Velocity at the previous note.
        /// </param>
        /// <param name="v1">
        /// Velocity at the current note.
        /// </param>
        /// <param name="dt">
        /// Time between the current and previous notes.
        /// </param>
        /// <param name="t">
        /// Arbitrary time t.
        /// </param>
        private static double velocityFunction(double x0, double x1, double v0, double v1, double dt, double t)
        {
            double t2 = t * t;
            double t3 = t2 * t;
            double t4 = t3 * t;

            double dt2 = dt * dt;
            double dt3 = dt2 * dt;
            double dt5 = dt3 * dt * dt;

            double a = 30 * t2 * (t - dt) * (t - dt) * (x1 - x0);
            double b = -15 * t4 * dt * (v0 + v1);
            double c = 4 * t3 * dt2 * (8 * v0 + 7 * v1);
            double d = -6 * t2 * dt3 * (3 * v0 + 2 * v1);
            double e = v0 * dt5;

            return (a + b + c + d + e) / dt5;
        }
    }
}
