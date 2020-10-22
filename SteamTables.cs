using UnityEngine;

namespace DvMod.SteamCutoff
{
    public static class SteamTables
    {
        private const float MaxPressure = 20f;

        /// <summary>Boiling point in degrees centigrade.</summary>
        public static float BoilingPoint(float gaugePressure) => Mathf.Lerp(100f, 212f, gaugePressure / MaxPressure);
        public static float BoilingPoint(SteamLocoSimulation sim) => BoilingPoint(sim.boilerPressure.value);

        /// <summary>Density of water in kg/L.</summary>
        public static float WaterDensity(float gaugePressure) => Mathf.Lerp(0.95866f, 0.84985f, gaugePressure / MaxPressure);
        public static float WaterDensity(SteamLocoSimulation sim) => WaterDensity(sim.boilerPressure.value);

        /// <summary>Density of saturated steam in kg/L.</summary>
        public static float SteamDensity(float gaugePressure) => Mathf.Lerp(0.0005902f, 0.010041f, gaugePressure / MaxPressure);
        public static float SteamDensity(SteamLocoSimulation sim) => SteamDensity(sim.boilerPressure.value);

        /// <summary>Energy to boil water in kJ/kg.</summary>
        public static float SpecificEnthalpyOfVaporization(float gaugePressure) => Mathf.Lerp(2257.6f, 1890.0f, gaugePressure / MaxPressure);
        public static float SpecificEnthalpyOfVaporization(SteamLocoSimulation sim) => SpecificEnthalpyOfVaporization(sim.boilerPressure.value);
    }
}
