using System.Collections.Generic;
using UnityEngine;

namespace DvMod.SteamCutoff
{
    public class FireState
    {
        private static readonly Dictionary<SteamLocoSimulation, FireState> states = new Dictionary<SteamLocoSimulation, FireState>();
        public static FireState Instance(SteamLocoSimulation sim)
        {
            if (states.TryGetValue(sim, out var state))
                return state;
            return states[sim] = new FireState(sim);
        }

        public static FireState? Instance(TrainCar car)
        {
            var sim = car.GetComponent<SteamLocoSimulation>();
            if (sim != null)
                return Instance(sim);
            return null;
        }

        public FireState(SteamLocoSimulation sim)
        {
            this.sim = sim;
        }

        private readonly SteamLocoSimulation sim;

        private const float CarbonAtomicWeight = 12.011f;
        private const float OxygenAtomicWeight = 15.999f;
        private const float ExcessOxygenFactor = 1.2f; // needed beyond stoichiometric to ensure full combustion
        private const float OxygenMassFactor = 2f * OxygenAtomicWeight / CarbonAtomicWeight * ExcessOxygenFactor;
        private const float CoalDensity = 1346f; // kg/m^3

        /// <summary>Coal chunk radius in m.</summary>
        private const float CoalPieceRadius = 0.02f; // coal passed over 1.25 in = 3.175 cm screen, ~4cm diameter
        private const float CoalPieceVolume = (4f / 3f) * Mathf.PI * CoalPieceRadius * CoalPieceRadius * CoalPieceRadius;
        private const float CoalPieceMass = CoalDensity * CoalPieceVolume;

        /// <summary>Coal consumption rate (kg/s) per unit surface area (m^2)</summary>
        private const float MaxConsumptionRate = 0.036f;
        private float MaxConsumptionRatePerArea() => MaxConsumptionRate;

        public const float CoalChunkMass = 7f; // kg
        private const float PiecesPerChunk = CoalChunkMass / CoalPieceMass;

        /// <summary>Current oxygen supply as kg/s.</summary>
        public float oxygenSupply;
        /// <summary>Current oxygen supply as a fraction of oxygen demand.</summary>
        public float oxygenAvailability;
        public float smoothedOxygenAvailability;
        public float smoothedOxygenAvailabilityVel;
        public float smoothedHeatYieldRate;
        private float smoothedHeatYieldRateVel;

        private static readonly float ChunkRadius = Mathf.Pow(CoalChunkMass / PiecesPerChunk / CoalDensity / (4f / 3f) / Mathf.PI, 1f / 3f);
        private static readonly float ChunkTotalSurfaceArea = PiecesPerChunk * Mathf.Pow(ChunkRadius, 2) * 4 * Mathf.PI;

        /// <summary>Coal surface area in m^2.</summary>
        public float TotalSurfaceArea() => sim.coalbox.value / CoalChunkMass * ChunkTotalSurfaceArea;
        /// <summary>Coal consumption rate in kg/s with unlimited oxygen.</summary>
        public float MaxCoalConsumptionRate() => MaxConsumptionRatePerArea() * TotalSurfaceArea();
        /// <summary>Maximum oxygen consumption in kg/s.</summary>
        public float MaxOxygenConsumptionRate() => MaxCoalConsumptionRate() * OxygenMassFactor * CoalCompositionCarbon;

        /// <summary>Airflow through stack due to natural convection in kg/s.</summary>
        /// Assuming 2m stack height, 0.5m stack radius, 3m overall height delta, 100 C in smokebox, 20 C at stack outlet.
        /// https://www.engineeringtoolbox.com/natural-draught-ventilation-d_122.html
        private const float PassiveStackFlow = 0.6f;
        /// <summary>Mass ratio of oxygen in atmospheric air.</summary>
        public const float OxygenRatio = 0.23f;

        /// <summary>Set factors affecting oxygen supply for the fire.</summary>
        /// <param name="exhaustFlow">Amount of steam being exhausted from cylinders and blower in kg/s.</summary>
        /// <returns>Oxygen supply in kg/s.</returns>
        public float SetOxygenSupply(float exhaustFlow, float damper)
        {
            oxygenSupply = (PassiveStackFlow + (exhaustFlow * Main.settings.frontendEfficiency)) * OxygenRatio * damper;
            oxygenAvailability = Mathf.Clamp01(oxygenSupply / MaxOxygenConsumptionRate());
            smoothedOxygenAvailability = Mathf.SmoothDamp(
                smoothedOxygenAvailability,
                oxygenAvailability,
                ref smoothedOxygenAvailabilityVel,
                smoothTime: Main.settings.smoke.colorChangeSmoothing);
            return oxygenSupply;
        }

        /// <summary>Multiplier on combustion rate based on oxygen availability.</summary>
        public float CombustionMultiplier() => Mathf.Min(oxygenAvailability * 2f, 1f);
        /// <summary>Current coal consumption rate in kg/s.</summary>
        public float CoalConsumptionRate() => CombustionMultiplier() * MaxCoalConsumptionRate();

        private const float CoalCompositionCarbon = 0.6f;
        private const float CoalCompositionVolatile = 0.3f;
        private const float CarbonSpecificEnthalpy = 32.81e3f; // kJ/kg
        private const float VolatileSpecificEnthalpy = 42.93f; // kJ/kg

        private const float CoalSpecificEnergy =
            (CoalCompositionCarbon * CarbonSpecificEnthalpy)
            + (CoalCompositionVolatile * VolatileSpecificEnthalpy);

        /// <summary>Energy yield from coal combustion in kW.</summary>
        public float SmoothedHeatYieldRate(bool fireOn)
        {
            return smoothedHeatYieldRate = Mathf.SmoothDamp(
                smoothedHeatYieldRate,
                fireOn ? InstantaneousHeatYieldRate() : 0f,
                ref smoothedHeatYieldRateVel,
                smoothTime: Constants.HeatYieldTransitionTime);
        }

        public float CombustionEfficiency()
        {
            float co = oxygenAvailability >= 0.5f ? 0.25f : oxygenAvailability / 2f;
            float co2 = oxygenAvailability >= 0.5f ? (1.5f * oxygenAvailability) - 0.75f : 0f;
            return Main.settings.boilerThermalEfficiency * (co + co2) * Mathf.Lerp(.80f, .45f, Mathf.InverseLerp(0.1f, 1.5f, CoalConsumptionRate()));
        }

        private float InstantaneousHeatYieldRate()
        {
            return CoalConsumptionRate() * CoalSpecificEnergy * CombustionEfficiency();
        }
    }
}
