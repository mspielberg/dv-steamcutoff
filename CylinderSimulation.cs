using System.Collections.Generic;
using UnityEngine;

namespace DvMod.SteamCutoff
{
    public static class CylinderSimulation
    {
        private const float SinusoidAverage = 2f / Mathf.PI;

        private const float SteamAdaiabaticIndex = 1.33f;
        private const float MinSteamTemperature_K = 380.0f;

        private const float LowSpeedTransitionStart = 10f;
        private const float LowSpeedTransitionWidth = 5;

        public const float CylinderVolume = 282f; // PRR L1s: 27x30"
        public static float SteamChestPressure(SteamLocoSimulation sim) => sim.boilerPressure.value * sim.regulator.value;
        public static float Cutoff(SteamLocoSimulation sim) => 
            Mathf.Max(Constants.MinCutoff, Mathf.Pow(sim.cutoff.value, Constants.CutoffGamma) * Constants.MaxCutoff);
        // 4 strokes / revolution
        // 4.4m driver circumference (see ChuffController)
        // ~909 strokes / km
        // (~0.25 strokes / s) / (km/h)
        public static float CylinderSteamVolumetricFlow(SteamLocoSimulation sim) =>
            sim.speed.value * 0.25f * CylinderVolume * Cutoff(sim);
        public static float CylinderSteamMassFlow(SteamLocoSimulation sim) =>
            sim.regulator.value == 0 ? 0 : CylinderSteamVolumetricFlow(sim) * SteamTables.SteamDensity(SteamChestPressure(sim));

        private static readonly HashSet<SteamLocoSimulation> leftCylinderHasSteam = new HashSet<SteamLocoSimulation>();
        private static readonly HashSet<SteamLocoSimulation> rightCylinderHasSteam = new HashSet<SteamLocoSimulation>();

        private static float InstantaneousCylinderPowerRatio(float cutoff, float pistonPosition, float maxExpansionRatio,
            SteamLocoSimulation instance, HashSet<SteamLocoSimulation> cylinderHasSteam)
        {
            float pressureRatio;
            if (pistonPosition <= cutoff)
            {
                pressureRatio = 1f;
                cylinderHasSteam.Add(instance);
            }
            else
            {
                float cylinderExpansionRatio = pistonPosition / cutoff;
                pressureRatio = cylinderExpansionRatio > maxExpansionRatio || !cylinderHasSteam.Contains(instance) ? 0f :
                    Mathf.Pow(1f / cylinderExpansionRatio, SteamAdaiabaticIndex);
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
        private static float InstantaneousPowerRatio(float regulator, float cutoff, float rotation, float maxExpansionRatio,
            SteamLocoSimulation instance)
        {
            if (regulator < 0.01f)
            {
                leftCylinderHasSteam.Remove(instance);
                rightCylinderHasSteam.Remove(instance);
            }

            float pistonPosition1 = rotation % 0.5f * 2f;
            float pistonPosition2 = (rotation + 0.25f) % 0.5f * 2f;
            return InstantaneousCylinderPowerRatio(cutoff, pistonPosition1, maxExpansionRatio, instance, leftCylinderHasSteam) +
                InstantaneousCylinderPowerRatio(cutoff, pistonPosition2, maxExpansionRatio, instance, rightCylinderHasSteam);
        }

        private static float AveragePowerRatio(float cutoff, float maxExpansionRatio)
        {
            float expansionRatio = Mathf.Min(1f / cutoff, maxExpansionRatio);
            if (expansionRatio <= 1f)
                return cutoff;
            float meanExpansionPower = (Mathf.Pow(expansionRatio, 1f - SteamAdaiabaticIndex) - 1f) /
                    ((1f - SteamAdaiabaticIndex) * (expansionRatio - 1f));
            return cutoff * ((expansionRatio - 1) * meanExpansionPower + 1f);
        }

        public static float PowerRatio(bool enableLowSpeedSimulation, float regulator, float cutoff, float speed, float revolution, 
            float cylinderSteamTemp, SteamLocoSimulation instance)
        {
            float condensationExpansionRatio = Mathf.Pow((cylinderSteamTemp + 273.15f) / MinSteamTemperature_K,
                    1f / (SteamAdaiabaticIndex - 1f));

            if (!enableLowSpeedSimulation)
                return AveragePowerRatio(cutoff, condensationExpansionRatio);

            return Mathf.Lerp(
                InstantaneousPowerRatio(regulator, cutoff, revolution, condensationExpansionRatio, instance),
                AveragePowerRatio(cutoff, condensationExpansionRatio),
                (speed - LowSpeedTransitionStart) / LowSpeedTransitionWidth);
        }
    }
}