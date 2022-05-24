using System.Collections.Generic;
using UnityEngine;

namespace DvMod.SteamCutoff
{
    public static class CylinderSimulation
    {
        private const float SinusoidAverage = 2f / Mathf.PI;

        private const float SteamAdiabaticIndex = 1.33f;
        private const float MinSteamTemperature_K = 380.0f;

        public const float CylinderVolume = 282f; // PRR L1s: 27x30"
        public const float MaxSpeed = 26.8224f; // USRA Light Mikado: 60 mph
        public const float DriverCircumference = 4.4f; // see ChuffController
        public const float MaxRevSpeed = MaxSpeed / DriverCircumference * 1.25f;
        public const float FullPowerSpeed = 3f; // PRR L1s: ~7 mph before dropoff
        public const float FullPowerRevSpeed = FullPowerSpeed / DriverCircumference;
        public static float SteamChestPressure(ISimAdapter sim) => sim.BoilerPressure.value * sim.Regulator.value;
        public static float Cutoff(ISimAdapter sim) =>
            Mathf.Max(Constants.MinCutoff, Mathf.Pow(sim.Cutoff.value, Constants.CutoffGamma) * Constants.MaxCutoff);
        // 4 strokes / revolution
        // 4.4m driver circumference (see ChuffController)
        // ~909 strokes / km
        // (~0.25 strokes / s) / (km/h)
        public static float CylinderSteamVolumetricFlow(ISimAdapter sim) =>
            sim.Speed.value * 0.25f * CylinderVolume * Cutoff(sim);
        public static float CylinderSteamMassFlow(ISimAdapter sim) =>
            sim.Regulator.value == 0 ? 0 : CylinderSteamVolumetricFlow(sim) * SteamTables.SteamDensity(SteamChestPressure(sim));

        // <summary>Returns the position of the piston within a cylinder at a certain crank position. Assumes equal crank angles.</summary>
        // <param name="cylinder">Zero-based index of the cylinder of interest.</param>
        // <param name="totalCylinder">Total number of cylinders on the locomotive.</param>
        // <param name="rotation">Current crank position in the range 0-1, with 0 being where cylinder 0 is at rear dead center, progressing forwards to front dead center at rotation 0.5.</param>
        // <returns>A pair with the linear position of the active (intake) side of the piston in the range 0-1, with 0 being at the start of the stroke and 1 at the end, and true if the front side of the piston is the active (intake) side, and false if the rear side is active.</returns>
        private static (float linearPosition, bool IsFrontActive) PistonLinearPosition(int cylinder, int totalCylinders, float rotation)
        {
            var lead = (float)cylinder / (float)totalCylinders / 2f;
            var pistonRotation = rotation + lead;
            var linearPosition = 0.5f * (1f - Mathf.Cos(2f * Mathf.PI * (pistonRotation % 0.5f)));
            var isFront = pistonRotation >= 0.5;
            return (linearPosition, isFront);
        }

        private static float InstantaneousCylinderPowerRatio(
            float cutoff,
            float maxExpansionRatio,
            int cylinder,
            float rotation,
            ExtraState state)
        {
            var (pistonPosition, isFrontActive) = PistonLinearPosition(cylinder, totalCylinders: 2, rotation);
            ref bool intakeHasSteam = ref state.IsCylinderPressurized(cylinder, isFrontActive);
            ref bool exhaustHasSteam = ref state.IsCylinderPressurized(cylinder, !isFrontActive);

            float pressureRatio;
            if (pistonPosition <= cutoff)
            {
                pressureRatio = 1f;
                intakeHasSteam = true;
                exhaustHasSteam = false;
            }
            else
            {
                float cylinderExpansionRatio = pistonPosition / cutoff;
                if (cylinderExpansionRatio > maxExpansionRatio || !intakeHasSteam)
                    return 0f;
                pressureRatio = InstantaneousPressureRatio(cylinderExpansionRatio);
            }

            float angleRatio = Mathf.Sin(Mathf.PI * pistonPosition) / SinusoidAverage;
            return pressureRatio * angleRatio;
        }

        // Assume: cyl2 is leading cyl1 by 90 degrees (0.25 rotation)
        // Piston position moves through 1 stroke every 0.5 rotation
        // 0 <= rotation < 0.25
        //    cyl1 acting forward, position = rotation * 2
        //    cyl2 acting forward, position = (rotation + 0.25) * 2
        // 0.25 <= rotation < 0.5
        //    cyl1 acting forward, position = rotation * 2
        //    cyl2 acting backward, position = (rotation + 0.25) % 0.5 * 2
        // 0.5 <= rotation < 0.75
        //    cyl1 acting backward, position = (rotation - 0.5) * 2
        //    cyl2 acting backward, position = (rotation - 0.25) * 2
        // 0.75 <= rotation < 1
        //    cyl1 acting backward, position = (rotation - 0.5) * 2
        //    cyl2 acting forward, position = (rotation - 0.75) * 2
        private static float InstantaneousPowerRatio(
            float cutoff,
            float rotation,
            float maxExpansionRatio,
            ExtraState extraState)
        {
            float totalPower = 0f;
            for (int cylinder = 0; cylinder < ExtraState.NumCylinders; cylinder++)
                totalPower += InstantaneousCylinderPowerRatio(cutoff, maxExpansionRatio, cylinder, rotation, extraState);
            return totalPower;
        }

        public static float PowerRatio(float regulator, float cutoff, float revolution,
            float revDistance, float revSpeed, float cylinderSteamTemp, ExtraState extraState)
        {
            float condensationExpansionRatio = CondensationExpansionRatio(cylinderSteamTemp);
            float powerAtPosition(float revolution)
            {
                return InstantaneousPowerRatio(
                    cutoff,
                    revolution,
                    condensationExpansionRatio,
                    extraState);
            }

            var startRevolution = revDistance > revolution ? revolution - revDistance + 1 : revolution - revDistance;
            float powerAtStart = powerAtPosition(startRevolution);
            float powerAtEnd = powerAtPosition(revolution);
            float speedMultiplier = Mathf.InverseLerp(MaxRevSpeed, FullPowerRevSpeed, revSpeed);
            //Debug.Log($"apparent revspeed={revSpeed*DriverCircumference*3.6f}");
            return 0.5f * (powerAtStart + powerAtEnd) * speedMultiplier;
        }

        public static float ResidualPressureRatio(float cutoff)
        {
            return InstantaneousPressureRatio(1f / cutoff);
        }

        private static float CondensationExpansionRatio(float cylinderSteamTemp)
        {
            return Mathf.Pow(
                (cylinderSteamTemp + 273.15f) / MinSteamTemperature_K,
                1f / (SteamAdiabaticIndex - 1f));
        }

        private static float InstantaneousPressureRatio(float expansionRatio)
        {
            return Mathf.Pow(expansionRatio, -SteamAdiabaticIndex);
        }
    }
}
