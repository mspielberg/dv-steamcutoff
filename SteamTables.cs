using UnityEngine;

namespace DvMod.SteamCutoff
{
    public static class SteamTables
    {
        /// <summary>Boiling point in degrees centigrade.</summary>
        public static float BoilingPoint(float pressure) => Mathf.Lerp(100f, 212f, Mathf.InverseLerp(1f, 20f, pressure));
        /// <summary>Boiling point in degrees centigrade.</summary>
        public static float BoilingPoint(SteamLocoSimulation sim) => BoilingPoint(sim.boilerPressure.value);

        /// <summary>Density of water in kg/L.</summary>
        public static float WaterDensity(float pressure) => Mathf.Lerp(0.95866f, 0.84985f, Mathf.InverseLerp(1f, 20f, pressure));
        /// <summary>Density of water in kg/L.</summary>
        public static float WaterDensity(SteamLocoSimulation sim) => WaterDensity(sim.boilerPressure.value);

        /// <summary>Density of saturated steam in kg/L.</summary>
        public static float SteamDensity(float pressure) => pressure / 20f * 0.010041f;
        /// <summary>Density of saturated steam in kg/L.</summary>
        public static float SteamDensity(SteamLocoSimulation sim) => SteamDensity(sim.boilerPressure.value);

        /// <summary>Energy to boil water in kJ/kg.</summary>
        public static float SpecificEnthalpyOfVaporization(float pressure) => Mathf.Lerp(2257.6f, 1890.0f, Mathf.InverseLerp(1f, 20f, pressure));
        /// <summary>Energy to boil water in kJ/kg.</summary>
        public static float SpecificEnthalpyOfVaporization(SteamLocoSimulation sim) => SpecificEnthalpyOfVaporization(sim.boilerPressure.value);
    }
}
