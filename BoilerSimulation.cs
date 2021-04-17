using System.Collections.Generic;
using UnityEngine;

namespace DvMod.SteamCutoff
{
    public static class BoilerSimulation
    {
        // See: Fig 19 of PRR L1s testing report
        // 12 lb H2: / lb coal @ 20 lb/ft^2/hr
        private const float MaxEvapEfficiency = 12;
        private const float MaxFiringRate = 180f /* lb/ft^2/h */ * 0.4535f /* kg/lb */ * 70f /* ft^2 */ / 3600f /* s/h */;

        private const float SteamGasConstant = 8.31446261815324f / 18E-3f; // J/(kg*K)
        
        // Used as a struct
        private class WaterState
        {
            public float waterTemp, smoothedEvaporationRate, smoothedEvaporationRateChange;
        }

        private static readonly Dictionary<SteamLocoSimulation, WaterState> boilerStates = new Dictionary<SteamLocoSimulation, WaterState>();

        /// <summary>Returns evaporation efficiency at a given firing rate.</summary>
        /// <param name="firingRate">Firing rate in kg/s.</param>
        /// <returns>Efficiency in kg H2O per kg coal.</param>
        public static float EvaporationEfficiency(float firingRate)
        {
            return MaxEvapEfficiency * Mathf.Lerp(1f, 0.5f, firingRate / MaxFiringRate);
        }

        /// <summary>Returns steady state evaporation rate at a given firing rate.</summary>
        /// <returns>Evaporation rate in kg/s H2O at constant pressure</param>
        public static float EvaporationRate(float firingRate) => firingRate * EvaporationEfficiency(firingRate);
        
        public static float BoilerSteamVolume(float boilerWater) => (SteamLocoSimulation.BOILER_WATER_CAPACITY_L * 1.05f) - boilerWater;

        public static void Run(SteamLocoSimulation __instance, float deltaTime, float heatEnergyFromCoal, float waterAdded, ref float boilerPressure, 
            ref float boilerWaterLevel, out float steamMass, out float evaporationRate)
        {
            float boilingTemp = SteamTables.BoilingPoint(boilerPressure);
            bool waterTempStored = boilerStates.TryGetValue(__instance, out WaterState currentWaterState);
            float currentWaterTemp = waterTempStored ? currentWaterState.waterTemp : boilingTemp;
            float waterDensity = SteamTables.WaterDensityByTemp(currentWaterTemp);
            float currentWaterMass = boilerWaterLevel * waterDensity;
            steamMass = ((boilerPressure + 1.01325f) * BoilerSteamVolume(boilerWaterLevel)) / (0.01f * SteamGasConstant * (currentWaterTemp + 273.15f));

            float newWaterMass = currentWaterMass + waterAdded;
            currentWaterTemp = (currentWaterMass * currentWaterTemp + waterAdded * Constants.FeedwaterTemperature) / newWaterMass;
            currentWaterMass = newWaterMass;

            float waterHeatCapacity = SteamTables.WaterSpecificHeatCapacity(currentWaterTemp) * currentWaterMass;
            float boilOffEnergy = SteamTables.SpecificEnthalpyOfVaporization(boilerPressure);
            float excessEnergy = (currentWaterTemp - boilingTemp) * waterHeatCapacity + heatEnergyFromCoal;
            float evaporatedMassLimit = excessEnergy / boilOffEnergy;
            float newWaterLevel;

            if (boilerPressure < 0.05f)
            {
                float evaporatedMass = heatEnergyFromCoal / boilOffEnergy;  // Prevent pressure from jumping up when draining steam
                currentWaterMass -= evaporatedMass;
                evaporationRate = evaporatedMass / deltaTime;
                newWaterLevel = currentWaterMass / SteamTables.WaterDensityByTemp(currentWaterTemp);

                steamMass += evaporatedMass;
                boilerPressure = 0.01f * SteamGasConstant * ((steamMass * (currentWaterTemp + 273.15f)) / BoilerSteamVolume(newWaterLevel)) - 1.01325f;

                if (waterTempStored)
                    boilerStates.Remove(__instance);
            }
            else
            {
                float minEvaporatedMass, maxEvaporatedMass;
                if (evaporatedMassLimit >= 0f)
                {
                    minEvaporatedMass = 0f;
                    maxEvaporatedMass = evaporatedMassLimit;
                }
                else
                {
                    minEvaporatedMass = evaporatedMassLimit;
                    maxEvaporatedMass = 0f;
                }
                float testEvaporatedMass = 0.5f * evaporatedMassLimit, evaporatedMass;
                float tempIncreaseFromCoal = currentWaterTemp + heatEnergyFromCoal / waterHeatCapacity;
                float boilOffTempDropPerKG = boilOffEnergy / waterHeatCapacity;
                int iterations = 0;
                while (true)
                {
                    float testWaterMass = currentWaterMass - testEvaporatedMass;
                    float testWaterTemp = tempIncreaseFromCoal - testEvaporatedMass * boilOffTempDropPerKG;
                    float testWaterLevel = testWaterMass / waterDensity;

                    float testSteamMass = steamMass + testEvaporatedMass;
                    float testSteamPressure = 0.01f * SteamGasConstant * ((testSteamMass * (testWaterTemp + 273.15f)) / BoilerSteamVolume(testWaterLevel)) - 1.01325f;

                    if (++iterations >= 10 || maxEvaporatedMass - minEvaporatedMass <= 0.01f * Mathf.Abs(testEvaporatedMass))
                    {
                        evaporatedMass = testEvaporatedMass;
                        currentWaterTemp = testWaterTemp;
                        boilerWaterLevel = testWaterMass / SteamTables.WaterDensityByTemp(currentWaterTemp);
                        steamMass = testSteamMass;
                        boilerPressure = testSteamPressure;
                        break;
                    }

                    if (testWaterTemp < SteamTables.BoilingPoint(testSteamPressure))
                        maxEvaporatedMass = testEvaporatedMass;
                    else
                        minEvaporatedMass = testEvaporatedMass;
                    testEvaporatedMass = 0.5f * (minEvaporatedMass + maxEvaporatedMass);
                }

                if (!waterTempStored)
                    boilerStates[__instance] = currentWaterState = new WaterState();
                currentWaterState.waterTemp = currentWaterTemp;
                currentWaterState.smoothedEvaporationRate = evaporationRate = Mathf.SmoothDamp(currentWaterState.smoothedEvaporationRate,
                    evaporatedMass / deltaTime, ref currentWaterState.smoothedEvaporationRateChange, 0.5f, Mathf.Infinity, deltaTime);
            }
        }
    }
}