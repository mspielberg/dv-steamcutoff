using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Assertions;

namespace DvMod.SteamCutoff
{
    public class FireState
    {
        private const float CarbonAtomicWeight = 12.011f;
        private const float OxygenAtomicWeight = 15.999f;
        private const float ExcessOxygenFactor = 1.75f; // needed beyond stoichiometric to ensure full combustion
        private const float OxygenMassFactor = 2f * OxygenAtomicWeight / CarbonAtomicWeight * ExcessOxygenFactor;
        private const float CoalDensity = 1.4f; // kg/m^3

        /// <summary>Coal chunk radius in m.</summary>
        private const float CoalPieceRadius = 0.02f; // coal passed over 1.25 in = 3.175 cm screen, ~4cm diameter
        private const float CoalPieceVolume = (4f / 3f) * Mathf.PI * CoalPieceRadius * CoalPieceRadius;
        private const float CoalPieceMass = CoalDensity * CoalPieceVolume;
        /// <summary>Time for coal chunk to burn away in seconds.</summary>
        private const float CoalMaxLifetime = 120f;
        /// <summary>Per-second rate of decrase in radius for each chunk with unlimited oxygen</summary>
        private const float MaximumRadiusChange = CoalPieceRadius / CoalMaxLifetime;

        private const float CoalChunkMass = 2f; // kg
        private const float PiecesPerChunk = CoalChunkMass / CoalPieceMass;

        /// <summary>Average radius of pieces in each chunk.</summary>
        public readonly List<float> coalPieceRadii = new List<float>();

        public void AddCoalChunk() => coalPieceRadii.Insert(0, CoalPieceRadius);

        /// <summary>Coal surface area in m^3.</summary>
        public float TotalSurfaceArea() => PiecesPerChunk * 4 * Mathf.PI * coalPieceRadii.Sum(r => Mathf.Pow(r, 2f));
        /// <summary>Coal consumption rate in kg/s with unlimited oxygen.</summary>
        public float MaxCoalConsumptionRate() => TotalSurfaceArea();
        /// <summary>Maximum oxygen consumption in kg/s.</summary>
        public float MaxOxygenConsumptionRate() => MaxCoalConsumptionRate() * OxygenMassFactor;

        public float CombustionRate(float oxygenAvailability) => Mathf.Min(oxygenAvailability * 2f, 1f);

        public float CoalConsumptionRate(float oxygenAvailability) => CombustionRate(oxygenAvailability) * TotalSurfaceArea();

        public float HeatYieldRate(float oxygenAvailability)
        {
            Assert.AreEqual(Mathf.Clamp01(oxygenAvailability), oxygenAvailability, "oxygenAvailability must be between 0 and 1");
            float co = oxygenAvailability >= 0.5f ? 0.25f : oxygenAvailability / 2f;
            float co2 = oxygenAvailability >= 0.5f ? (1.5f * oxygenAvailability) - 0.75f : 0f;
            return TotalSurfaceArea() * (co + co2);
        }

        public void ConsumeCoal(float deltaTime, float oxygenAvailability)
        {
            Assert.AreEqual(Mathf.Clamp01(oxygenAvailability), oxygenAvailability, "oxygenAvailability must be between 0 and 1");
            var radiusChange = deltaTime * CombustionRate(oxygenAvailability) * MaximumRadiusChange;
            for (int i = coalPieceRadii.Count; i >= 0; i--)
            {
                if (coalPieceRadii[i] <= radiusChange)
                    coalPieceRadii.RemoveAt(i);
                else
                    coalPieceRadii[i] -= radiusChange;
            }
        }
    }
}