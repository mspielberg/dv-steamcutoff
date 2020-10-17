using UnityEngine;

namespace DvMod.SteamCutoff
{
    public static class BoilerSimulation
    {
        // See: Fig 19 of PRR L1s testing report
        // 12 lb H2: / lb coal @ 20 lb/ft^2/hr
        private const float MaxEvapEfficiency = 12;
        private const float MaxFiringRate = 180f /* lb/ft^2/h */ * 0.4535f /* kg/lb */ * 70f /* ft^2 */ / 3600f /* s/h */;

        /// <summary>Returns evaporation efficiency at a given firing rate.</summary>
        /// <param name="firingRate">Firing rate in kg/s.</param>
        /// <returns>Efficiency in kg H2O per kg coal.</param>
        public static float EvaporationEfficiency(float firingRate)
        {
            return MaxEvapEfficiency * Mathf.Lerp(1f, 0.5f, firingRate / MaxFiringRate);
        }

        /// <summary>Returns steady state evaporation rate at a given firing rate.</summary>
        /// <returns>Evaporation rate in kg/s H2O</param>
        public static float EvaporationRate(float firingRate) => firingRate * EvaporationEfficiency(firingRate);
    }
}