using System.Collections.Generic;
using UnityEngine;

namespace DvMod.SteamCutoff
{
    public sealed class BoilerSimulation
    {
        private static readonly Dictionary<SteamLocoSimulation, BoilerSimulation> states = new Dictionary<SteamLocoSimulation, BoilerSimulation>();

        public static BoilerSimulation Instance(SteamLocoSimulation sim)
        {
            if (!states.TryGetValue(sim, out var state))
                states[sim] = state = new BoilerSimulation(sim);
            return state;
        }

        private readonly SteamLocoSimulation sim;
        private readonly TrainCar loco;
        private float waterTemp; // deg C
        private float smoothedEvapRate;
        private float smoothedEvapRateVel;

        private BoilerSimulation(SteamLocoSimulation sim)
        {
            this.sim = sim;
            loco = TrainCar.Resolve(sim.gameObject);
            waterTemp = SteamTables.BoilingPoint(sim.boilerPressure.value);
        }

        private float BoilerSteamVolume(float waterVolume) => (sim.boilerWater.max * 1.05f) - waterVolume;

        public void Update(float waterAdded, float heatEnergyFromCoal, float deltaTime)
        {
            float boilerPressure = sim.boilerPressure.value;
            float boilerWaterAmount = sim.boilerWater.value;
            float boilingTemp = SteamTables.BoilingPoint(boilerPressure);
            float currentWaterMass = boilerWaterAmount * SteamTables.WaterDensityByTemp(waterTemp);
            float currentSteamMass = IdealGasSteam.Mass(boilerPressure, waterTemp, BoilerSteamVolume(boilerWaterAmount));

            float newWaterMass = currentWaterMass + waterAdded;
            waterTemp = ((currentWaterMass * waterTemp) + (waterAdded * Constants.FeedwaterTemp)) / newWaterMass;
            currentWaterMass = newWaterMass;

            float heatCapacity = currentWaterMass * SteamTables.WaterSpecificHeatCapacity(waterTemp); // kJ/K
            float boilOffEnergy = SteamTables.SpecificEnthalpyOfVaporization(boilerPressure);
            float excessEnergy = ((waterTemp - boilingTemp) * heatCapacity) + heatEnergyFromCoal;
            float evaporatedMassLimit = excessEnergy / boilOffEnergy;
            float initialWaterDensity = SteamTables.WaterDensityByTemp(waterTemp);

            // output variables
            float newWaterLevel;
            float newSteamPressure = boilerPressure;  // satisfy the compiler; overwritten below

            if (boilerPressure < 0.05f)
            {
                float evaporatedMass = heatEnergyFromCoal / boilOffEnergy;
                currentWaterMass -= evaporatedMass;
                newWaterLevel = currentWaterMass / initialWaterDensity;

                currentSteamMass += evaporatedMass;
                newSteamPressure = IdealGasSteam.Pressure(currentSteamMass, waterTemp, BoilerSteamVolume(newWaterLevel));

                waterTemp = boilingTemp;
                smoothedEvapRate = evaporatedMass / (deltaTime / sim.timeMult);
                smoothedEvapRateVel = 0f;
            }
            else
            {
                float initialSteamMass = currentSteamMass;
                float initialWaterMass = currentWaterMass;
                float initialWaterTemp = waterTemp;

                // binary search on actual evaporated mass to remain on the saturation curve
                float minEvaporatedMass = Mathf.Min(0f, evaporatedMassLimit);
                float maxEvaporatedMass = Mathf.Max(0f, evaporatedMassLimit);
                float evaporatedMass = 0.5f * evaporatedMassLimit;
                for (int i = 0; i < 10; i++)
                {
                    currentWaterMass = initialWaterMass - evaporatedMass;
                    currentSteamMass = initialSteamMass + evaporatedMass;

                    float evaporationEnergy = evaporatedMass * boilOffEnergy;
                    waterTemp = initialWaterTemp + ((heatEnergyFromCoal - evaporationEnergy) / heatCapacity);
                    newWaterLevel = currentWaterMass / initialWaterDensity; // approximation, compensate after loop

                    newSteamPressure = IdealGasSteam.Pressure(currentSteamMass, waterTemp, BoilerSteamVolume(newWaterLevel));

                    if (maxEvaporatedMass - minEvaporatedMass <= 0.01f * Mathf.Abs(evaporatedMass))
                        break;

                    if (waterTemp < SteamTables.BoilingPoint(newSteamPressure))
                        maxEvaporatedMass = evaporatedMass;
                    else
                        minEvaporatedMass = evaporatedMass;
                    evaporatedMass = (minEvaporatedMass + maxEvaporatedMass) / 2f;
                }

                // update water level to reflect density change with temperature
                newWaterLevel = currentWaterMass / SteamTables.WaterDensityByTemp(waterTemp);

                smoothedEvapRate = Mathf.SmoothDamp(
                    smoothedEvapRate,
                    evaporatedMass / (deltaTime / sim.timeMult),
                    ref smoothedEvapRateVel,
                    0.5f,
                    Mathf.Infinity,
                    deltaTime / sim.timeMult);
            }

            sim.boilerWater.AddNextValue(newWaterLevel - boilerWaterAmount);
            sim.boilerPressure.AddNextValue(newSteamPressure - boilerPressure);

            HeadsUpDisplayBridge.instance?.UpdateWaterEvap(loco, smoothedEvapRate);
            HeadsUpDisplayBridge.instance?.UpdateBoilerSteamMass(loco, currentSteamMass);
        }
    }
}