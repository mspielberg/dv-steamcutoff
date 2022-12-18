using System.Collections.Generic;
using UnityEngine;

namespace DvMod.SteamCutoff
{
    public static class CylinderSimulation
    {
        private static readonly float RootTwo = Mathf.Sqrt(2f);

        private const float SteamAdiabaticIndex = 1.33f;
        private const float MinSteamTemperature_K = 380.0f;

        public static float SteamChestPressure(ISimAdapter sim) => sim.BoilerPressure.value * sim.Regulator.value;
        public static float Cutoff(ISimAdapter sim) =>
            Mathf.Max(Constants.MinCutoff, Mathf.Pow(sim.Cutoff.value, Constants.CutoffGamma) * Constants.MaxCutoff);
        public static float CylinderSteamVolumetricFlow(ISimAdapter sim) =>
            sim.Speed.value * sim.SteamConsumptionMultiplier * Cutoff(sim);
        public static float CylinderSteamMassFlow(ISimAdapter sim) =>
            sim.Regulator.value == 0 ? 0 : CylinderSteamVolumetricFlow(sim) * SteamTables.SteamDensity(SteamChestPressure(sim));

        // <summary>Returns the position of the piston within a cylinder at a certain crank position. Assumes equal crank angles.</summary>
        // <param name="cylinder">Zero-based index of the cylinder of interest.</param>
        // <param name="totalCylinder">Total number of cylinders on the locomotive.</param>
        // <param name="rotation">Current crank position in the range 0-1, with 0 being where cylinder 0 is at rear dead center, progressing forwards to front dead center at rotation 0.5.</param>
        // <returns>A pair with the linear position of the active (intake) side of the piston in the range 0-1, with 0 being at the start of the stroke and 1 at the end, and true if the front side of the piston is the active (intake) side, and false if the rear side is active.</returns>
        private static (float linearPosition, bool IsFrontActive, float crankOffset) PistonLinearPosition(int cylinder, int totalCylinders, float rotation)
        {
            var lead = (float)cylinder / (float)totalCylinders / 2f;
            var pistonRotation = rotation + lead;
            var angle = 2f * Mathf.PI * (pistonRotation % 0.5f);
            var linearPosition = 0.5f * (1f - Mathf.Cos(angle));
            var isFront = pistonRotation >= 0.5;
            var crankOffset = Mathf.Sin(angle);
            return (linearPosition, isFront, crankOffset);
        }

        private static float InstantaneousCylinderPowerRatio(
            float cutoff,
            float maxExpansionRatio,
            int cylinder,
            float rotation,
            ExtraState state)
        {
            var (pistonPosition, isFrontActive, crankOffset) = PistonLinearPosition(cylinder, state.NumCylinders, rotation);
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

            float angleRatio = crankOffset;
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
            for (int cylinder = 0; cylinder < extraState.NumCylinders; cylinder++)
                totalPower += InstantaneousCylinderPowerRatio(cutoff, maxExpansionRatio, cylinder, rotation, extraState);
            return totalPower / extraState.NumCylinders;
        }

        public static float PowerRatio(float regulator, float cutoff, float revolution,
            float revDistance, float cylinderSteamTemp, ExtraState extraState)
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
            // normalize to peak at 1.0
            return (powerAtStart + powerAtEnd) / RootTwo;
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
