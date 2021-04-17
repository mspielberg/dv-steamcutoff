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
        public static float WaterDensityByTemp(float temp) => Mathf.Lerp(0.99821f, 0.84022f, Mathf.InverseLerp(20f, 220f, temp));
        public static float WaterDensity(float gaugePressure) => Mathf.Lerp(0.95866f, 0.84985f, gaugePressure / MaxPressure);
        public static float WaterDensity(SteamLocoSimulation sim) => WaterDensity(sim.boilerPressure.value);

        /// <summary>Density of saturated steam in kg/L.</summary>
        public static float SteamDensity(float gaugePressure) => Mathf.Lerp(0.0005902f, 0.010041f, gaugePressure / MaxPressure);
        public static float SteamDensity(SteamLocoSimulation sim) => SteamDensity(sim.boilerPressure.value);

        /// <summary>Energy to boil water in kJ/kg.</summary>
        public static float SpecificEnthalpyOfVaporization(float gaugePressure) => Mathf.Lerp(2257.6f, 1890.0f, gaugePressure / MaxPressure);
        public static float SpecificEnthalpyOfVaporization(SteamLocoSimulation sim) => SpecificEnthalpyOfVaporization(sim.boilerPressure.value);

        /// <summary>Energy to heat water in kJ/(kg*K)</summary>
        public static float WaterSpecificHeatCapacity(float temp) => Mathf.Lerp(4.157f, 3.248f, Mathf.InverseLerp(20f, 220f, temp));
    }

    public static class IdealGasSteam
    {
        private const float IdealGasConstant = 8.31446261815324f; // J/(mol*K)
        private const float WaterMolarMass = 18.015257f; // g/mol
        private const float SteamSpecificGasConstant = IdealGasConstant / WaterMolarMass * 1000f; // J/(kg*K) = Pa * m^3 / kg / K

        private const float CUBIC_METERS_PER_LITER = 1e-3f;
        private const float BAR_PER_PASCAL = 1e-5f;
        private const float ATMOSPHERIC_PRESSURE = 1.01325f; // bar
        private const float KELVIN_OFFSET = 273.15f;

        /// <summary>Pressure of steam as ideal gas.</summary>
        /// <param name="mass">Mass in kg.</param>
        /// <param name="temp">Temperature in degrees Celsius.</param>
        /// <param name="volume">Volume in L.</param>
        /// <returns>Gauge pressure in bar.</returns>
        public static float Pressure(float mass, float temp, float volume) =>
            (BAR_PER_PASCAL *
            SteamSpecificGasConstant * mass * (temp + KELVIN_OFFSET) /
            (volume * CUBIC_METERS_PER_LITER)) - ATMOSPHERIC_PRESSURE;

        public static float Mass(float pressure, float temp, float volume) =>
            (pressure + ATMOSPHERIC_PRESSURE) / BAR_PER_PASCAL *
            volume * CUBIC_METERS_PER_LITER /
            SteamSpecificGasConstant /
            (temp + KELVIN_OFFSET);
    }
}
