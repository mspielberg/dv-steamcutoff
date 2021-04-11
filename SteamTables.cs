using UnitsNet;
using UnitsNet.CustomCode.Units;
using UnitsNet.CustomCode.Wrappers;
using UnityEngine;

namespace DvMod.SteamCutoff
{
    public static class SteamTables
    {
        public static readonly Pressure Atmospheric = Pressure.FromBars(0);
        public static readonly Pressure MaxPressure = Pressure.FromBars(20f);

        private static float PressureRatio(Pressure p) => (float)(p / MaxPressure);

        public static Temperature BoilingPoint(Pressure p) =>
            Temperature.FromDegreesCelsius(Mathf.Lerp(99.632f, 212.417f, PressureRatio(p)));
        public static float BoilingPoint(float p) => (float)BoilingPoint(Pressure.FromBars(p)).DegreesCelsius;

        public static Density WaterDensity(Pressure p) =>
            Density.FromKilogramsPerCubicMeter(Mathf.Lerp(958.64f, 849.80f, PressureRatio(p)));
        public static float WaterDensity(float p) => (float)WaterDensity(Pressure.FromBars(p)).KilogramsPerLiter;
            
        public static Density WaterDensity(Temperature t) =>
            Density.FromKilogramsPerCubicMeter(Mathf.Lerp(958.64f, 849.80f, Mathf.InverseLerp(99.632f, 212.417f, (float)t.DegreesCelsius)));

        public static Density SteamDensity(Pressure p) =>
            Density.FromKilogramsPerCubicMeter(Mathf.Lerp(0.5903f, 10.042f, PressureRatio(p)));
        public static float SteamDensity(float p) => (float)SteamDensity(Pressure.FromBars(p)).KilogramsPerLiter;

        public static SpecificEnergy SpecificEnthalpyOfVaporization(Pressure p) =>
            SpecificEnergy.FromKilojoulesPerKilogram(Mathf.Lerp(2257.6f, 1890.0f, PressureRatio(p)));
        public static SpecificEntropy WaterSpecificHeat(Pressure p) =>
            SpecificEntropy.FromKilojoulesPerKilogramDegreeCelsius(Mathf.Lerp(3.7697f, 3.2713f, PressureRatio(p)));
        public static SpecificEntropy SteamSpecificHeat(Pressure p) =>
            SpecificEntropy.FromKilojoulesPerKilogramDegreeCelsius(Mathf.Lerp(1.5527f, 2.1586f, PressureRatio(p)));
    }
}
