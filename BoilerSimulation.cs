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

        public static BoilerSimulation? Instance(TrainCar car)
        {
            var sim = car.GetComponent<SteamLocoSimulation>();
            return sim == null ? null : Instance(sim);
        }

        private readonly SteamLocoSimulation sim;
        private readonly TrainCar loco;
        public float WaterTemp { get; private set; } // deg C
        public float SmoothedEvapRate { get; private set; } // kg/s
        private float smoothedEvapRateVel;

        private BoilerSimulation(SteamLocoSimulation sim)
        {
            this.sim = sim;
            loco = TrainCar.Resolve(sim.gameObject);
            WaterTemp = SteamTables.BoilingPoint(sim.boilerPressure.value);
        }

        private float BoilerSteamVolume(float waterVolume) => (sim.boilerWater.max * 1.05f) - waterVolume;

        public void Update(float waterAdded, float heatEnergyFromCoal, float deltaTime)
        {
            float boilerPressure = sim.boilerPressure.value;
            float boilerWaterAmount = sim.boilerWater.value;
            float boilingTemp = SteamTables.BoilingPoint(boilerPressure);
            float currentWaterMass = boilerWaterAmount * SteamTables.WaterDensityByTemp(WaterTemp);
            float currentSteamMass = IdealGasSteam.Mass(boilerPressure, WaterTemp, BoilerSteamVolume(boilerWaterAmount));

            float newWaterMass = currentWaterMass + waterAdded;
            WaterTemp = ((currentWaterMass * WaterTemp) + (waterAdded * Constants.FeedwaterTemp)) / newWaterMass;
            currentWaterMass = newWaterMass;

            float heatCapacity = currentWaterMass * SteamTables.WaterSpecificHeatCapacity(WaterTemp); // kJ/K
            float boilOffEnergy = SteamTables.SpecificEnthalpyOfVaporization(boilerPressure);
            float excessEnergy = ((WaterTemp - boilingTemp) * heatCapacity) + heatEnergyFromCoal;
            float evaporatedMassLimit = excessEnergy / boilOffEnergy;

            // output variables
            float evaporatedMass;
            float newWaterLevel;
            float newSteamPressure = boilerPressure;  // satisfy the compiler; overwritten below

            if (boilerPressure < 0.05f)
            {
                WaterTemp = boilingTemp;
                evaporatedMass = heatEnergyFromCoal / boilOffEnergy;
                currentWaterMass -= evaporatedMass;
                newWaterLevel = currentWaterMass / SteamTables.WaterDensityByTemp(WaterTemp);

                currentSteamMass += evaporatedMass;
                newSteamPressure = IdealGasSteam.Pressure(currentSteamMass, WaterTemp, BoilerSteamVolume(newWaterLevel));
            }
            else
            {
                float initialSteamMass = currentSteamMass;
                float initialWaterMass = currentWaterMass;
                float initialWaterTemp = WaterTemp;
                float initialWaterDensity = SteamTables.WaterDensityByTemp(WaterTemp);

                // binary search on actual evaporated mass to remain on the saturation curve
                float minEvaporatedMass = Mathf.Min(0f, evaporatedMassLimit);
                float maxEvaporatedMass = Mathf.Max(0f, evaporatedMassLimit);
                evaporatedMass = 0.5f * evaporatedMassLimit;
                for (int i = 0; i < 10; i++)
                {
                    currentWaterMass = initialWaterMass - evaporatedMass;
                    currentSteamMass = initialSteamMass + evaporatedMass;

                    float evaporationEnergy = evaporatedMass * boilOffEnergy;
                    WaterTemp = initialWaterTemp + ((heatEnergyFromCoal - evaporationEnergy) / heatCapacity);
                    newWaterLevel = currentWaterMass / initialWaterDensity; // approximation, compensate after loop

                    newSteamPressure = IdealGasSteam.Pressure(currentSteamMass, WaterTemp, BoilerSteamVolume(newWaterLevel));

                    if (maxEvaporatedMass - minEvaporatedMass <= 0.01f * Mathf.Abs(evaporatedMass))
                        break;

                    if (WaterTemp < SteamTables.BoilingPoint(newSteamPressure))
                        maxEvaporatedMass = evaporatedMass;
                    else
                        minEvaporatedMass = evaporatedMass;
                    evaporatedMass = (minEvaporatedMass + maxEvaporatedMass) / 2f;
                }

                // reflect density change with temperature
                newWaterLevel = currentWaterMass / SteamTables.WaterDensityByTemp(WaterTemp);
            }

            SmoothedEvapRate = Mathf.SmoothDamp(
                SmoothedEvapRate,
                evaporatedMass / (deltaTime / sim.timeMult),
                ref smoothedEvapRateVel,
                0.5f,
                Mathf.Infinity,
                deltaTime / sim.timeMult);

            sim.boilerWater.AddNextValue(newWaterLevel - boilerWaterAmount);
            sim.boilerPressure.AddNextValue(newSteamPressure - boilerPressure);

            HeadsUpDisplayBridge.instance?.UpdateBoilerSteamMass(loco, currentSteamMass);
        }
    }
}