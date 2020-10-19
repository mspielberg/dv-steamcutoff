using UnityEngine;

namespace DvMod.SteamCutoff
{
    public static class CylinderSimulation
    {
        public const float CylinderVolume = 282f; // PRR L1s: 27x30"
        public static float SteamChestPressure(SteamLocoSimulation sim) => sim.boilerPressure.value * sim.regulator.value;
        public static float Cutoff(SteamLocoSimulation sim) => Mathf.Pow(sim.cutoff.value, Main.settings.cutoffGamma) * 0.85f;

        // 4 strokes / revolution
        // 4.4m driver circumference (see ChuffController)
        // ~909 strokes / km
        // (~0.25 strokes / s) / (km/h)
        public static float CylinderSteamVolumetricFlow(SteamLocoSimulation sim) =>
            sim.speed.value * 0.25f * CylinderVolume * Cutoff(sim);
        public static float CylinderSteamMassFlow(SteamLocoSimulation sim) =>
            CylinderSteamVolumetricFlow(sim) * SteamTables.SteamDensity(SteamChestPressure(sim));
    }
}