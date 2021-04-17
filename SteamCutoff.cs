using System.Collections.Generic;

using HarmonyLib;
using System;
using UnityEngine;
using UnityModManagerNet;

namespace DvMod.SteamCutoff
{
    [EnableReloading]
    public static class Main
    {
        public static bool enabled;
        public static Settings settings = new Settings();
        public static UnityModManager.ModEntry? mod;

        public static bool Load(UnityModManager.ModEntry modEntry)
        {
            mod = modEntry;

            try { settings = Settings.Load<Settings>(modEntry); } catch {}
            var harmony = new Harmony(modEntry.Info.Id);
            harmony.PatchAll();

            modEntry.OnGUI = OnGui;
            modEntry.OnSaveGUI = OnSaveGui;
            modEntry.OnToggle = OnToggle;
            modEntry.OnUnload = OnUnload;

            Commands.Register();

            return true;
        }

        private static void OnGui(UnityModManager.ModEntry modEntry)
        {
            settings.Draw(modEntry);
        }

        private static void OnSaveGui(UnityModManager.ModEntry modEntry)
        {
            settings.Save(modEntry);
        }

        private static bool OnToggle(UnityModManager.ModEntry modEntry, bool value)
        {
            if (value != enabled)
            {
                enabled = value;
            }
            return true;
        }

        private static bool OnUnload(UnityModManager.ModEntry modEntry)
        {
            var harmony = new Harmony(modEntry.Info.Id);
            harmony.UnpatchAll(modEntry.Info.Id);
            return true;
        }

        private static float BoilerSteamVolume(float boilerWater)
        {
            return (SteamLocoSimulation.BOILER_WATER_CAPACITY_L * 1.05f) - boilerWater;
        }

        public static void DebugLog(string message)
        {
            if (settings.enableLogging)
                mod?.Logger.Log(message);
        }

        public static void DebugLog(TrainCar car, Func<string> message)
        {
            if (settings.enableLogging && PlayerManager.Car == car)
                mod?.Logger.Log(message());
        }

        [HarmonyPatch(typeof(SteamLocoSimulation), nameof(SteamLocoSimulation.Awake))]
        public static class AwakePatch
        {
            public static void Postfix(SteamLocoSimulation __instance)
            {
                __instance.coalbox.max = Constants.CoalboxCapacity;
            }
        }

        [HarmonyPatch(typeof(SteamLocoSimulation), "SimulateBlowerDraftFireCoalTemp")]
        public static class SimulateFirePatch
        {
            public const float BlowerMaxRate = 10f;
            public static bool Prefix(SteamLocoSimulation __instance, float deltaTime)
            {
                if (!enabled)
                    return true;
                if (deltaTime <= 0)
                    return false;

                TrainCar loco = TrainCar.Resolve(__instance.gameObject);
                FireState state = FireState.Instance(__instance);
                float cylinderMassFlow = CylinderSimulation.CylinderSteamMassFlow(__instance);
                float blowerMassFlow = __instance.GetBlowerBonusNormalized() * BlowerMaxRate;
                __instance.boilerPressure.AddNextValue(
                    -blowerMassFlow * (deltaTime / __instance.timeMult) /
                    SteamTables.SteamDensity(__instance.boilerPressure.value) /
                    BoilerSteamVolume(__instance.boilerWater.value));

                var exhaustFlow = cylinderMassFlow + blowerMassFlow;
                HeadsUpDisplayBridge.instance?.UpdateExhaustFlow(loco, exhaustFlow);
                var oxygenSupplyFlow = state.SetOxygenSupply(exhaustFlow, Mathf.Lerp(0.05f, 1f, __instance.draft.value));
                HeadsUpDisplayBridge.instance?.UpdateOxygenSupply(loco, oxygenSupplyFlow);

                if (__instance.fireOn.value == 1f && __instance.coalbox.value > 0f)
                {
                    __instance.coalConsumptionRate = state.CoalConsumptionRate();
                    float num = __instance.coalConsumptionRate * (deltaTime / __instance.timeMult);
                    __instance.TotalCoalConsumed += num;
                    __instance.coalbox.AddNextValue(-num);
                }
                else
                {
                    __instance.fireOn.SetNextValue(0.0f);
                    __instance.coalConsumptionRate = 0.0f;
                }
                return false;
            }
        }

        [HarmonyPatch(typeof(SteamLocoSimulation), "SimulateWater")]
        private static class SimulateWaterPatch
        {
            public static bool Prefix(SteamLocoSimulation __instance, float deltaTime)
            {
                if (!enabled)
                    return true;

                var injector = Mathf.Pow(__instance.injector.value, Constants.InjectorGamma);

                var waterVolumeToInject = 3000f * injector * deltaTime;
                __instance.tenderWater.PassValueToNext(__instance.boilerWater, waterVolumeToInject);
                __instance.boilerWater.AddNextValue(-4000f * __instance.waterDump.value * deltaTime);

                return false;
            }
        }

        public class BoilerState
        {
            public float waterTemp;
            public float smoothedEvapRate;
            public float smoothedEvapRateVel;

            private static readonly Dictionary<SteamLocoSimulation, BoilerState> states = new Dictionary<SteamLocoSimulation, BoilerState>();

            public static BoilerState Instance(SteamLocoSimulation sim)
            {
                if (!states.TryGetValue(sim, out var state))
                    states[sim] = state = new BoilerState(sim);
                return state;
            }

            private BoilerState(SteamLocoSimulation sim)
            {
                this.waterTemp = SteamTables.BoilingPoint(sim.boilerPressure.value);
            }
        }

        [HarmonyPatch(typeof(SteamLocoSimulation), "SimulateSteam")]
        private static class SimulateSteamPatch
        {
            private const float PASSIVE_LEAK_ADJUST = 0.1f;

            public static bool Prefix(SteamLocoSimulation __instance, float deltaTime)
            {
                if (!enabled)
                    return true;
                if (deltaTime <= 0)
                    return false;

                TrainCar loco = __instance.GetComponent<TrainCar>();
                FireState state = FireState.Instance(__instance);
                float boilerPressure = __instance.boilerPressure.value, boilerWaterAmount = __instance.boilerWater.value;

                // heat from boiler
                var heatPower = state.SmoothedHeatYieldRate(__instance.fireOn.value > 0f); // in kW
                __instance.temperature.SetNextValue(Mathf.Lerp(
                    SteamTables.BoilingPoint(boilerPressure),
                    1200f,
                    Mathf.Pow(
                        Mathf.InverseLerp(0, Constants.TemperatureGaugeMaxPower, heatPower),
                        Constants.TemperatureGaugeGamma)));
                float heatEnergyFromCoal = heatPower * (deltaTime / __instance.timeMult); // in kJ

                // evaporation
                var boilerState = BoilerState.Instance(__instance);
                float currentWaterTemp = boilerState.waterTemp;
                float waterAdded = Mathf.Max(0f, __instance.boilerWater.nextValue - boilerWaterAmount); // L
                float boilingTemp = SteamTables.BoilingPoint(boilerPressure);
                float currentWaterMass = boilerWaterAmount * SteamTables.WaterDensityByTemp(currentWaterTemp);
                float currentSteamMass = IdealGasSteam.Mass(boilerPressure, currentWaterTemp, BoilerSteamVolume(boilerWaterAmount));

                float newWaterMass = currentWaterMass + waterAdded;
                currentWaterTemp = ((currentWaterMass * currentWaterTemp) + (waterAdded * Constants.FeedwaterTemp)) / newWaterMass;
                currentWaterMass = newWaterMass;

                float waterHeatCapacity = SteamTables.WaterSpecificHeatCapacity(currentWaterTemp);
                float boilOffEnergy = SteamTables.SpecificEnthalpyOfVaporization(boilerPressure);
                float excessEnergy = ((currentWaterTemp - boilingTemp) * currentWaterMass * waterHeatCapacity) + heatEnergyFromCoal;
                float evaporatedMassLimit = excessEnergy / boilOffEnergy;
                float newWaterLevel, newSteamPressure;
                if (boilerPressure < 0.05f)
                {
                    currentWaterMass -= evaporatedMassLimit;
                    newWaterLevel = currentWaterMass / SteamTables.WaterDensityByTemp(currentWaterTemp);

                    currentSteamMass += evaporatedMassLimit;
                    newSteamPressure = IdealGasSteam.Pressure(currentSteamMass, currentWaterTemp, BoilerSteamVolume(newWaterLevel));

                    boilerState.waterTemp = boilingTemp;
                    boilerState.smoothedEvapRate = evaporatedMassLimit / (deltaTime / __instance.timeMult);
                    boilerState.smoothedEvapRateVel = 0f;
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
                    int iterations = 0;
                    while (true)
                    {
                        float testWaterMass = currentWaterMass - testEvaporatedMass;
                        float testWaterTemp = currentWaterTemp + (heatEnergyFromCoal - (testEvaporatedMass * boilOffEnergy)) / (currentWaterMass * waterHeatCapacity);
                        float testWaterLevel = testWaterMass / SteamTables.WaterDensityByTemp(testWaterTemp);

                        float testSteamMass = currentSteamMass + testEvaporatedMass;
                        float testSteamPressure = IdealGasSteam.Pressure(testSteamMass, testWaterTemp, BoilerSteamVolume(testWaterLevel));

                        if (++iterations >= 10 || maxEvaporatedMass - minEvaporatedMass <= 0.01f * Mathf.Abs(testEvaporatedMass))
                        {
                            evaporatedMass = testEvaporatedMass;
                            currentWaterTemp = testWaterTemp;
                            newWaterLevel = testWaterLevel;
                            currentSteamMass = testSteamMass;
                            newSteamPressure = testSteamPressure;
                            break;
                        }

                        if (testWaterTemp < SteamTables.BoilingPoint(testSteamPressure))
                            maxEvaporatedMass = testEvaporatedMass;
                        else
                            minEvaporatedMass = testEvaporatedMass;
                        testEvaporatedMass = (minEvaporatedMass + maxEvaporatedMass) / 2f;
                    }

                    boilerState.waterTemp = currentWaterTemp;
                    boilerState.smoothedEvapRate = Mathf.SmoothDamp(
                        boilerState.smoothedEvapRate,
                        evaporatedMass / (deltaTime / __instance.timeMult),
                        ref boilerState.smoothedEvapRateVel,
                        0.5f,
                        Mathf.Infinity,
                        deltaTime / __instance.timeMult);
                }

                __instance.boilerWater.AddNextValue(newWaterLevel - boilerWaterAmount);
                __instance.boilerPressure.AddNextValue(newSteamPressure - boilerPressure);

                HeadsUpDisplayBridge.instance?.UpdateWaterEvap(loco, boilerState.smoothedEvapRate);
                HeadsUpDisplayBridge.instance?.UpdateBoilerSteamMass(loco, currentSteamMass);

                // steam release
                if (__instance.steamReleaser.value > 0.0f && __instance.boilerPressure.value > 0.0f)
                    __instance.boilerPressure.AddNextValue(-__instance.steamReleaser.value * 30.0f * deltaTime);

                // safety valve
                const float SAFETY_VALVE_BLOWOFF = 0.2f; // 3 psi
                var safetyValveCloseThreshold = settings.safetyValveThreshold - SAFETY_VALVE_BLOWOFF;
                if (__instance.boilerPressure.value >= settings.safetyValveThreshold && __instance.safetyPressureValve.value == 0f)
                    __instance.safetyPressureValve.SetNextValue(1f);
                else if (__instance.boilerPressure.value <= safetyValveCloseThreshold && __instance.safetyPressureValve.value == 1f)
                    __instance.safetyPressureValve.SetNextValue(0f);

                if ( __instance.safetyPressureValve.value == 1f)
                {
                    __instance.boilerPressure.AddNextValue(-2f * __instance.boilerPressure.value * deltaTime);
                    if (__instance.boilerPressure.nextValue < safetyValveCloseThreshold)
                        __instance.boilerPressure.SetNextValue(safetyValveCloseThreshold);
                }

                // passive leakage
                __instance.pressureLeakMultiplier = Mathf.Lerp(
                    1f, 100f / PASSIVE_LEAK_ADJUST,
                    Mathf.InverseLerp(0.7f, 1f, __instance.GetComponent<DamageController>().bodyDamage.DamagePercentage));
                float leakage = PASSIVE_LEAK_ADJUST * SteamLocoSimulation.PRESSURE_LEAK_L * __instance.pressureLeakMultiplier * deltaTime;
                __instance.boilerPressure.AddNextValue(-leakage);

                return false;
            }
        }

        [HarmonyPatch(typeof(SteamLocoSimulation), "SimulateCylinder")]
        private static class SimulateCylinderPatch
        {
            public static bool Prefix(SteamLocoSimulation __instance, float deltaTime)
            {
                if (!enabled)
                    return true;
                if (deltaTime <= 0)
                    return false;

                var loco = __instance.GetComponent<TrainCar>();
                float cutoff = CylinderSimulation.Cutoff(__instance);
                if (cutoff > 0)
                {
                    float boilerPressureRatio =
                        __instance.boilerPressure.value / SteamLocoSimulation.BOILER_PRESSURE_MAX_KG_PER_SQR_CM;
                    float regulator = __instance.regulator.value;
                    float steamChestPressureRatio = boilerPressureRatio * regulator;

                    var chuff = __instance.GetComponent<ChuffController>();
                    float powerRatio = CylinderSimulation.PowerRatio(settings.enableLowSpeedSimulation, regulator, cutoff, __instance.speed.value, 
                        chuff.dbgCurrentRevolution, Mathf.Max(__instance.temperature.value, SteamTables.BoilingPoint(__instance)), __instance);
                    __instance.power.SetNextValue(steamChestPressureRatio * powerRatio * SteamLocoSimulation.POWER_CONST_HP);

                    float boilerSteamVolume = BoilerSteamVolume(__instance.boilerWater.value);
                    float boilerSteamMass = boilerSteamVolume * SteamTables.SteamDensity(__instance);
                    float steamMassConsumed = CylinderSimulation.CylinderSteamMassFlow(__instance) * (deltaTime / __instance.timeMult);
                    float pressureConsumed =  __instance.boilerPressure.value * steamMassConsumed / boilerSteamMass;
                    __instance.boilerPressure.AddNextValue(-pressureConsumed);
                    HeadsUpDisplayBridge.instance?.UpdateSteamUsage(loco, steamMassConsumed / (deltaTime / __instance.timeMult));
                }
                return false;
            }
        }

        [HarmonyPatch(typeof(ChuffController), nameof(ChuffController.Update))]
        public static class ChuffControllerPatch
        {
            public static bool Prefix(ChuffController __instance)
            {
                __instance.chuffsPerRevolution = 4;
                return true;
            }
        }

        [HarmonyPatch(typeof(SteamLocoSimulation), nameof(SteamLocoSimulation.AddCoalChunk))]
        public static class ShovelPatch
        {
            public static bool Prefix(SteamLocoSimulation __instance)
            {
                if (!enabled)
                    return true;
                if (__instance.tenderCoal.value < FireState.CoalChunkMass ||
                    __instance.coalbox.max - __instance.coalbox.value < FireState.CoalChunkMass)
                {
                    return false;
                }
                __instance.tenderCoal.PassValueTo(__instance.coalbox, FireState.CoalChunkMass);
                if (__instance.fireOn.value == 0f && __instance.temperature.value > 400f)
                {
                    __instance.fireOn.SetValue(1f);
                }
                return false;
            }
        }
    }
}
