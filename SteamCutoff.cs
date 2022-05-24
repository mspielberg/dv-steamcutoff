using DV.CabControls;
using DVCustomCarLoader.LocoComponents.Steam;
using HarmonyLib;
using System;
using System.Reflection;
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

        [HarmonyPatch(typeof(SteamLocoSimulation), nameof(SteamLocoSimulation.SimulateTick))]
        public static class SimulateTickPatch
        {
            public static bool Prefix(SteamLocoSimulation __instance, float delta)
            {
                if (!enabled)
                    return true;
                if (delta <= 0)
                    return false;

                var sim = new BaseSimAdapter(__instance);
                var d = delta / __instance.timeMult;
                var loco = TrainCar.Resolve(__instance.gameObject);
                var chuffController = new BaseChuffAdapter(__instance.GetComponent<ChuffController>());
                var damageController = __instance.GetComponent<DamageController>();
                var extraState = ExtraState.Instance(loco)!;

                __instance.InitNextValues();
                SimulateFire(sim, loco, extraState, d);
                SimulateWater(sim, d);
                SimulateSteam(sim, extraState, damageController, d);
                SimulateCylinder(sim, loco, chuffController, extraState, d);
                Stoker.Simulate(sim, loco, extraState, d);
                __instance.SimulateSand(delta);
                __instance.SetValuesToNextValues();
                return false;
            }
        }

        [HarmonyPatch]
        public static class CCLSimulateTickPatch
        {
            public static bool Prepare()
            {
                return UnityModManager.FindMod("DVCustomCarLoader")?.Loaded ?? false;
            }

            public static MethodBase TargetMethod()
            {
                return UnityModManager.FindMod("DVCustomCarLoader").Assembly
                    .GetType("DVCustomCarLoader.LocoComponents.Steam.CustomLocoSimSteam")
                    .GetMethod("SimulateTick", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            }

            public static bool Prefix(object __instance, float delta)
            {
                return Inner.Execute(__instance, delta);
            }

            private static class Inner
            {
                public static bool Execute(object instance, float delta)
                {
                    if (!enabled)
                        return true;
                    if (delta <= 0)
                        return false;

                    var __instance = (CustomLocoSimSteam)instance;
                    var sim = new CustomSimAdapter((CustomLocoSimSteam)__instance);
                    var d = delta / __instance.timeMult;
                    var loco = TrainCar.Resolve(__instance.gameObject);
                    var chuffController = new CustomChuffAdapter(__instance.GetComponent<CustomChuffController>());
                    var damageController = __instance.GetComponent<DamageController>();
                    var extraState = ExtraState.Instance(loco)!;
                    __instance.InitNextValues();
                    SimulateFire(sim, loco, extraState, d);
                    SimulateWater(sim, d);
                    SimulateSteam(sim, extraState, damageController, d);
                    SimulateCylinder(sim, loco, chuffController, extraState, d);
                    // Stoker.Simulate(sim, loco, extraState, d);
                    __instance.SimulateSand(delta);
                    __instance.SetValuesToNextValues();
                    return false;
                }
            }
        }

        public const float BlowerMaxRate = 20f;
        public static void SimulateFire(ISimAdapter sim, TrainCar loco, ExtraState extraState, float deltaTime)
        {
            float cylinderMassFlow = CylinderSimulation.CylinderSteamMassFlow(sim);
            float blowerMassFlow = sim.GetBlowerBonusNormalized() * BlowerMaxRate;
            sim.BoilerPressure.AddNextValue(
                -blowerMassFlow * deltaTime /
                SteamTables.SteamDensity(sim.BoilerPressure.value) /
                BoilerSteamVolume(sim.BoilerWater.value));

            var exhaustFlow = cylinderMassFlow + blowerMassFlow;
            HeadsUpDisplayBridge.instance?.UpdateExhaustFlow(loco, exhaustFlow);
            var oxygenSupplyFlow = extraState.fireState.SetOxygenSupply(exhaustFlow, Mathf.Lerp(0.05f, 1f, sim.Draft.value));
            HeadsUpDisplayBridge.instance?.UpdateOxygenSupply(loco, oxygenSupplyFlow);

            if (loco.loadedInterior == null)
                extraState.controlState.fireOutSetting = 1f;
            else if (extraState.controlState.initialized)
                sim.Coalbox.AddNextValue(-10f * Mathf.InverseLerp(0.5f, 0f, extraState.controlState.fireOutSetting) * deltaTime);

            if (sim.FireOn.value == 1f && sim.Coalbox.value > 0f)
            {
                sim.CoalConsumptionRate = extraState.fireState.CoalConsumptionRate();
                float num = sim.CoalConsumptionRate * deltaTime;
                sim.TotalCoalConsumed += num;
                sim.Coalbox.AddNextValue(-num);
            }
            else
            {
                sim.FireOn.SetNextValue(0.0f);
                sim.CoalConsumptionRate = 0.0f;
            }
        }

        private static void SimulateWater(ISimAdapter sim, float deltaTime)
        {
            var injector = Mathf.Pow(sim.Injector.value, Constants.InjectorGamma);

            var waterVolumeRequested = 30000f * injector * deltaTime;
            var waterVolumeToExtract = Mathf.Min(
                waterVolumeRequested * Main.settings.waterConsumptionMultiplier,
                sim.TenderWater.value);
            sim.TenderWater.AddNextValue(-waterVolumeToExtract);
            sim.BoilerWater.AddNextValue(waterVolumeToExtract / Main.settings.waterConsumptionMultiplier);
            sim.BoilerWater.AddNextValue(-40000f * sim.WaterDump.value * deltaTime);
        }

        private const float PASSIVE_LEAK_ADJUST = 0.1f;
        private const float SafetyValveBlowoff = 0.35f; // 5 psi
        private const float SafetyValveOffset = 0.15f;

        private static void SimulateSteam(
            ISimAdapter sim,
            ExtraState extraState,
            DamageController damageController,
            float deltaTime)
        {

            static void SimulateSafetyValve(ISimAdapter sim, BoilerSimulation boilerSim, float deltaTime)
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
                        -normalizedRate * settings.safetyValveVentRate * deltaTime /
                        SteamTables.SteamDensity(sim.BoilerPressure.value) /
                        BoilerSteamVolume(sim.BoilerWater.value));
                }
            }

            float boilerPressure = sim.BoilerPressure.value, boilerWaterAmount = sim.BoilerWater.value;

            // heat from firebox
            var heatPower = extraState.fireState.SmoothedHeatYieldRate(sim.FireOn.value > 0f); // in kW
            sim.Temperature.SetNextValue(Mathf.Lerp(
                SteamTables.BoilingPoint(boilerPressure),
                1200f,
                Mathf.Pow(
                    Mathf.InverseLerp(0, Constants.TemperatureGaugeMaxPower, heatPower),
                    Constants.TemperatureGaugeGamma)));
            float heatEnergyFromCoal = heatPower * deltaTime; // in kJ

            // evaporation
            float waterAdded = Mathf.Max(0f, sim.BoilerWater.nextValue - boilerWaterAmount); // L
            extraState.boilerState.Update(waterAdded, heatEnergyFromCoal, deltaTime);

            // steam release
            if (sim.SteamReleaser.value > 0.0f && sim.BoilerPressure.value > 0.0f)
                sim.BoilerPressure.AddNextValue(-sim.SteamReleaser.value * 30.0f * deltaTime);

            SimulateSafetyValve(sim, extraState.boilerState, deltaTime);

            // passive leakage
            sim.PressureLeakMultiplier = Mathf.Lerp(
                1f, 100f / PASSIVE_LEAK_ADJUST,
                Mathf.InverseLerp(0.7f, 1f, damageController.bodyDamage.DamagePercentage));
            float leakage = PASSIVE_LEAK_ADJUST * SteamLocoSimulation.PRESSURE_LEAK_L * sim.PressureLeakMultiplier * deltaTime;
            sim.BoilerPressure.AddNextValue(-leakage);
        }

        private static void SimulateCylinder(ISimAdapter sim, TrainCar loco, IChuffAdapter chuff, ExtraState extraState, float deltaTime)
        {
            float cutoff = CylinderSimulation.Cutoff(sim);
            float boilerPressureRatio =
                sim.BoilerPressure.value / Main.settings.safetyValveThreshold;
            float regulator = sim.Regulator.value;
            float steamChestPressureRatio = boilerPressureRatio * regulator;

            float cylinderSteamTemp = Mathf.Max(sim.Temperature.value, SteamTables.BoilingPoint(sim));
            float powerRatio = CylinderSimulation.PowerRatio(
                regulator,
                cutoff,
                chuff.CurrentRevolution,
                chuff.RotationSpeed * deltaTime,
                cylinderSteamTemp,
                extraState);
            var powerTarget =
                steamChestPressureRatio
                * powerRatio
                * sim.Power.max;
            sim.Power.SetNextValue(
                Main.settings.torqueSmoothing <= 0
                ? powerTarget
                : Mathf.SmoothDamp(
                    sim.Power.value,
                    powerTarget,
                    ref extraState.powerVel,
                    smoothTime: Main.settings.torqueSmoothing));
            var residualPressureRatio = Mathf.Lerp(
                0.05f,
                1f,
                Mathf.InverseLerp(0f, 0.8f, CylinderSimulation.ResidualPressureRatio(cutoff)));
            chuff.ChuffPower = residualPressureRatio * steamChestPressureRatio;

            float boilerSteamVolume = BoilerSteamVolume(sim.BoilerWater.value);
            float boilerSteamMass = boilerSteamVolume * SteamTables.SteamDensity(sim);
            float steamMassConsumed =
                Main.settings.steamConsumptionMultiplier
                * 0.7f * CylinderSimulation.CylinderSteamMassFlow(sim)
                * deltaTime;
            float pressureConsumed = sim.BoilerPressure.value * steamMassConsumed / boilerSteamMass;
            sim.BoilerPressure.AddNextValue(-pressureConsumed);
            HeadsUpDisplayBridge.instance?.UpdateSteamUsage(loco, steamMassConsumed / deltaTime);
        }

        [HarmonyPatch(typeof(LocoControllerSteam), nameof(LocoControllerSteam.GetTotalPowerForcePercentage))]
        public static class GetTotalPowerForcePercentagePatch
        {
            // Re-normalizes running gear motion to average at 1.0 instead of peaking at 1.0 (approximately 1.111)
            // private static readonly float ScalingFactor = Mathf.PI * Mathf.Sqrt(2) / 4f;
            private const float MaxSpeed = 120f;
            public static bool Prefix(LocoControllerSteam __instance, ref float __result)
            {
                if (!enabled)
                    return true;
                __result = __instance.sim.power.value / __instance.sim.power.max * 0.4f * Mathf.InverseLerp(MaxSpeed, 12f, __instance.GetSpeedKmH());
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
