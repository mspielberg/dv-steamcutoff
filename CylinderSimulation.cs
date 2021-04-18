using UnityEngine;

namespace DvMod.SteamCutoff
{
    public static class CylinderSimulation
    {
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
    }
}