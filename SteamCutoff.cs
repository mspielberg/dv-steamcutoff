using DV.CabControls;
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

            try
            {
                var loaded = Settings.Load<Settings>(modEntry);
                if (loaded?.version == modEntry.Info.Version)
                {
                    settings = loaded;
                    modEntry.Logger.Log("Loading settings.");
                }
                else
                {
                    modEntry.Logger.Log("Reset settings to default after upgrade.");
                    settings.version = modEntry.Info.Version;
                }
            }
            catch
            {
                modEntry.Logger.Log("Could not read settings. Reset settings to default.");
                settings.version = modEntry.Info.Version;
            }

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
                __instance.boilerPressure.value = Main.settings.initialBoilerPressure;
            }
        }

        [HarmonyPatch(typeof(SteamLocoSimulation), "SimulateBlowerDraftFireCoalTemp")]
        public static class SimulateFirePatch
        {
            public const float BlowerMaxRate = 20f;
            public static bool Prefix(SteamLocoSimulation __instance, float deltaTime)
            {
                if (!enabled)
                    return true;
                if (deltaTime <= 0)
                    return false;

                ISimAdapter sim = new BaseSimAdapter(__instance);
                TrainCar loco = TrainCar.Resolve(__instance.gameObject);
                FireState state = FireState.Instance(__instance);
                float cylinderMassFlow = CylinderSimulation.CylinderSteamMassFlow(sim);
                float blowerMassFlow = sim.GetBlowerBonusNormalized() * BlowerMaxRate;
                sim.BoilerPressure.AddNextValue(
                    -blowerMassFlow * (deltaTime / sim.TimeMult) /
                    SteamTables.SteamDensity(sim.BoilerPressure.value) /
                    BoilerSteamVolume(sim.BoilerWater.value));

                var exhaustFlow = cylinderMassFlow + blowerMassFlow;
                HeadsUpDisplayBridge.instance?.UpdateExhaustFlow(loco, exhaustFlow);
                var oxygenSupplyFlow = state.SetOxygenSupply(exhaustFlow, Mathf.Lerp(0.05f, 1f, sim.Draft.value));
                HeadsUpDisplayBridge.instance?.UpdateOxygenSupply(loco, oxygenSupplyFlow);

                var extraControlState = ExtraControlState.Instance(__instance);
                if (loco.loadedInterior == null)
                    extraControlState.fireOutSetting = 1f;
                else
                    sim.Coalbox.AddNextValue(-10f * Mathf.InverseLerp(0.5f, 0f, extraControlState.fireOutSetting) * (deltaTime / sim.TimeMult));

                if (sim.FireOn.value == 1f && sim.Coalbox.value > 0f)
                {
                    sim.CoalConsumptionRate = state.CoalConsumptionRate();
                    float num = sim.CoalConsumptionRate * (deltaTime / sim.TimeMult);
                    sim.TotalCoalConsumed += num;
                    sim.Coalbox.AddNextValue(-num);
                }
                else
                {
                    sim.FireOn.SetNextValue(0.0f);
                    sim.CoalConsumptionRate = 0.0f;
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

                var sim = new BaseSimAdapter(__instance);

                var injector = Mathf.Pow(sim.Injector.value, Constants.InjectorGamma);

                var waterVolumeRequested = 3000f * injector * deltaTime;
                var waterVolumeToExtract = Mathf.Min(
                    waterVolumeRequested * Main.settings.waterConsumptionMultiplier,
                    sim.TenderWater.value);
                sim.TenderWater.AddNextValue(-waterVolumeToExtract);
                sim.BoilerWater.AddNextValue(waterVolumeToExtract / Main.settings.waterConsumptionMultiplier);
                sim.BoilerWater.AddNextValue(-4000f * sim.WaterDump.value * deltaTime);

                return false;
            }
        }

        [HarmonyPatch(typeof(SteamLocoSimulation), "SimulateSteam")]
        private static class SimulateSteamPatch
        {
            private const float PASSIVE_LEAK_ADJUST = 0.1f;
            private const float SafetyValveBlowoff = 0.35f; // 5 psi
            private const float SafetyValveOffset = 0.15f;

            private static void SimulateSafetyValve(ISimAdapter sim, BoilerSimulation boilerSim, float deltaTime)
            {
                var closeThreshold = settings.safetyValveThreshold - SafetyValveBlowoff;
                var secondaryThreshold = settings.safetyValveThreshold + SafetyValveOffset;
                var secondaryCloseThreshold = secondaryThreshold - SafetyValveBlowoff;

                var pressure = sim.BoilerPressure.value;
                boilerSim.numSafetyValvesOpen = boilerSim.numSafetyValvesOpen switch
                {
                    0 => pressure > settings.safetyValveThreshold ? 1 : boilerSim.numSafetyValvesOpen,
                    1 => pressure > secondaryThreshold ? 2 : pressure < closeThreshold ? 0 : boilerSim.numSafetyValvesOpen,
                    2 => pressure < secondaryCloseThreshold ? 1 : boilerSim.numSafetyValvesOpen,
                    _ => throw new NotImplementedException(),
                };

                var targetVentRate = boilerSim.numSafetyValvesOpen switch
                {
                    0 => Mathf.Lerp(0, settings.safetyValveFeatheringAmount,
                            Mathf.InverseLerp(
                                settings.safetyValveThreshold - settings.safetyValveFeatheringPressure,
                                settings.safetyValveThreshold,
                                pressure)),
                    1 => Mathf.Lerp(0.5f, 0.5f + settings.safetyValveFeatheringAmount,
                            Mathf.InverseLerp(
                                secondaryThreshold - settings.safetyValveFeatheringPressure,
                                secondaryThreshold,
                                pressure)),
                    2 => 1,
                    _ => throw new NotImplementedException(),
                };

                var normalizedRate =
                    Mathf.SmoothDamp(
                        sim.SafetyPressureValve.value,
                        targetVentRate,
                        ref boilerSim.safetyValveRateVel,
                        targetVentRate >= sim.SafetyPressureValve.value ? 0 : settings.safetyValveSmoothing);
                if (normalizedRate < 0.01f)
                    normalizedRate = 0f;
                sim.SafetyPressureValve.SetNextValue(normalizedRate);

                if (sim.SafetyPressureValve.value > 0)
                {
                    sim.BoilerPressure.AddNextValue(
                        -normalizedRate * settings.safetyValveVentRate * (deltaTime / sim.TimeMult) /
                        SteamTables.SteamDensity(sim.BoilerPressure.value) /
                        BoilerSteamVolume(sim.BoilerWater.value));
                }
            }

            public static bool Prefix(SteamLocoSimulation __instance, float deltaTime)
            {
                if (!enabled)
                    return true;
                if (deltaTime <= 0)
                    return false;

                var sim = new BaseSimAdapter(__instance);
                FireState state = FireState.Instance(__instance);
                float boilerPressure = sim.BoilerPressure.value, boilerWaterAmount = sim.BoilerWater.value;

                // heat from firebox
                var heatPower = state.SmoothedHeatYieldRate(sim.FireOn.value > 0f); // in kW
                sim.Temperature.SetNextValue(Mathf.Lerp(
                    SteamTables.BoilingPoint(boilerPressure),
                    1200f,
                    Mathf.Pow(
                        Mathf.InverseLerp(0, Constants.TemperatureGaugeMaxPower, heatPower),
                        Constants.TemperatureGaugeGamma)));
                float heatEnergyFromCoal = heatPower * (deltaTime / sim.TimeMult); // in kJ

                // evaporation
                var boilerSim = BoilerSimulation.Instance(__instance);
                float waterAdded = Mathf.Max(0f, sim.BoilerWater.nextValue - boilerWaterAmount); // L
                boilerSim.Update(waterAdded, heatEnergyFromCoal, deltaTime);

                // steam release
                if (sim.SteamReleaser.value > 0.0f && sim.BoilerPressure.value > 0.0f)
                    sim.BoilerPressure.AddNextValue(-sim.SteamReleaser.value * 30.0f * deltaTime);

                SimulateSafetyValve(sim, boilerSim, deltaTime);

                // passive leakage
                sim.PressureLeakMultiplier = Mathf.Lerp(
                    1f, 100f / PASSIVE_LEAK_ADJUST,
                    Mathf.InverseLerp(0.7f, 1f, __instance.GetComponent<DamageController>().bodyDamage.DamagePercentage));
                float leakage = PASSIVE_LEAK_ADJUST * SteamLocoSimulation.PRESSURE_LEAK_L * sim.PressureLeakMultiplier * deltaTime;
                sim.BoilerPressure.AddNextValue(-leakage);

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
                var sim = new BaseSimAdapter(__instance);
                float cutoff = CylinderSimulation.Cutoff(sim);
                float boilerPressureRatio =
                    sim.BoilerPressure.value / Main.settings.safetyValveThreshold;
                float regulator = sim.Regulator.value;
                float steamChestPressureRatio = boilerPressureRatio * regulator;

                var chuff = __instance.GetComponent<ChuffController>();
                float cylinderSteamTemp = Mathf.Max(sim.Temperature.value, SteamTables.BoilingPoint(__instance));
                float powerRatio = CylinderSimulation.PowerRatio(
                    regulator,
                    cutoff,
                    chuff.dbgCurrentRevolution,
                    chuff.drivingWheel.rotationSpeed * (deltaTime / sim.TimeMult),
                    chuff.drivingWheel.rotationSpeed,
                    cylinderSteamTemp,
                    __instance);
                var state = ExtraState.Instance(__instance);
                var powerTarget = Main.settings.torqueMultiplier
                    * steamChestPressureRatio
                    * powerRatio
                    * 0.28f * SteamLocoSimulation.POWER_CONST_HP;
                sim.Power.SetNextValue(
                    Main.settings.torqueSmoothing <= 0 ? powerTarget :
                     Mathf.SmoothDamp(
                    sim.Power.value,
                    powerTarget,
                    ref state.powerVel,
                    smoothTime: Main.settings.torqueSmoothing));
                var residualPressureRatio = Mathf.Lerp(
                    0.05f,
                    1f,
                    Mathf.InverseLerp(0f, 0.8f, CylinderSimulation.ResidualPressureRatio(cutoff)));
                chuff.chuffPower = residualPressureRatio * steamChestPressureRatio;
                // Main.DebugLog(loco, () => $"residualPressure={residualPressureRatio}, steamChestPressure={steamChestPressureRatio}, chuffPower={chuff.chuffPower}");

                float boilerSteamVolume = BoilerSteamVolume(sim.BoilerWater.value);
                float boilerSteamMass = boilerSteamVolume * SteamTables.SteamDensity(__instance);
                float steamMassConsumed =
                    Main.settings.steamConsumptionMultiplier
                    * 0.7f * CylinderSimulation.CylinderSteamMassFlow(sim)
                    * (deltaTime / sim.TimeMult);
                float pressureConsumed = sim.BoilerPressure.value * steamMassConsumed / boilerSteamMass;
                sim.BoilerPressure.AddNextValue(-pressureConsumed);
                HeadsUpDisplayBridge.instance?.UpdateSteamUsage(loco, steamMassConsumed / (deltaTime / sim.TimeMult));

                return false;
            }
        }

        [HarmonyPatch(typeof(ChuffController), nameof(ChuffController.Update))]
        public static class ChuffControllerUpdatePatch
        {
            private const int ChuffsPerRevolution = 4;
            public static bool Prefix(ChuffController __instance)
            {
                float num = (__instance.loco.drivingForce.wheelslip > 0f) ? (__instance.drivingWheel.rotationSpeed * __instance.wheelCircumference) : __instance.loco.GetForwardSpeed();
                __instance.wheelRevolution = (__instance.wheelRevolution + num * Time.deltaTime + __instance.wheelCircumference) % __instance.wheelCircumference;
                __instance.currentChuff = (int)(__instance.wheelRevolution / __instance.wheelCircumference * (float)ChuffsPerRevolution) % ChuffsPerRevolution;
                __instance.chuffKmh = Mathf.Abs(num * 3.6f);
                __instance.dbgVelocity = num;
                __instance.dbgCurrentRevolution = __instance.wheelRevolution / __instance.wheelCircumference;
                __instance.dbgCurrentChuff = __instance.currentChuff;
                __instance.dbgKmHVelocity = __instance.chuffKmh;
                if (__instance.currentChuff != __instance.lastChuff)
                {
                    __instance.lastChuff = __instance.currentChuff;
                    if (__instance.OnChuff != null)
                    {
                        __instance.OnChuff(__instance.chuffPower);
                    }
                }
                return false;
            }
        }

        [HarmonyPatch(typeof(SteamLocoChuffSmokeParticles), nameof(SteamLocoChuffSmokeParticles.Chuff))]
        public static class ChuffPatch
        {
            public static void Postfix(SteamLocoChuffSmokeParticles __instance)
            {
                var chuffController = __instance.GetComponent<ChuffController>();
                var rev = chuffController.wheelRevolution / chuffController.wheelCircumference % 0.5f;
                if (rev >= 0.125f && rev <= 0.375f)
                    __instance.chuffParticlesLeft.Stop();
                else
                    __instance.chuffParticlesRight.Stop();
            }
        }

        [HarmonyPatch(typeof(SteamLocoSimulation), nameof(SteamLocoSimulation.AddCoalChunk))]
        public static class ShovelPatch
        {
            public static bool Prefix(SteamLocoSimulation __instance)
            {
                if (!enabled)
                    return true;

                var massToExtract = Mathf.Min(
                    FireState.CoalChunkMass * Main.settings.coalConsumptionMultiplier,
                    __instance.tenderCoal.value);
                var massToInsert = massToExtract / Main.settings.coalConsumptionMultiplier;
                __instance.tenderCoal.AddValue(-massToExtract);
                __instance.coalbox.AddValue(massToInsert);

                if (__instance.fireOn.value == 0f && __instance.temperature.value > 400f)
                {
                    __instance.fireOn.SetValue(1f);
                }

                return false;
            }
        }

        [HarmonyPatch(typeof(ShovelNonPhysicalCoal), nameof(ShovelNonPhysicalCoal.UnloadCoal))]
        public static class UnloadCoalPatch
        {
            public static bool Prefix(ShovelNonPhysicalCoal __instance, GameObject target, ref bool __result)
            {
                if (!enabled)
                    return true;

                __result = false;
                var sim = TrainCar.Resolve(target)?.GetComponent<SteamLocoSimulation>();
                if (sim == null)
                    return false;
                var massRequested = __instance.shovelChunksCapacity * FireState.CoalChunkMass;
                var massToExtract = Mathf.Min(
                     massRequested * Main.settings.coalConsumptionMultiplier,
                     sim.tenderCoal.value);
                var massToInsert = massToExtract / Main.settings.coalConsumptionMultiplier;
                return sim.coalbox.value + massToInsert <= sim.coalbox.max;
            }
        }
    }
}
