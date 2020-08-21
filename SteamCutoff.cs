using HarmonyLib;
using System;
using UnityEngine;
using UnityModManagerNet;

namespace SteamCutoff
{
    [EnableReloading]
    public static class Main
    {
        public static bool enabled;
        public static Settings settings;
        public static UnityModManager.ModEntry mod;

        static bool Load(UnityModManager.ModEntry modEntry)
        {
            mod = modEntry;

            try { settings = Settings.Load<Settings>(modEntry); } catch {}
            var harmony = new Harmony(modEntry.Info.Id);
            harmony.PatchAll();

            modEntry.OnGUI = OnGui;
            modEntry.OnSaveGUI = OnSaveGui;
            modEntry.OnToggle = OnToggle;
            modEntry.OnUnload = OnUnload;

            return true;
        }

        static void OnGui(UnityModManager.ModEntry modEntry)
        {
            settings.Draw(modEntry);
        }

        static void OnSaveGui(UnityModManager.ModEntry modEntry)
        {
            settings.Save(modEntry);
        }

        static bool OnToggle(UnityModManager.ModEntry modEntry, bool value)
        {
            if (value != enabled)
            {
                enabled = value;
            }
            return true;
        }

        static bool OnUnload(UnityModManager.ModEntry modEntry)
        {
            var harmony = new Harmony(modEntry.Info.Id);
            harmony.UnpatchAll(modEntry.Info.Id);
            return true;
        }

        static float BoilerSteamVolume(float boilerWater)
        {
            return SteamLocoSimulation.BOILER_WATER_CAPACITY_L * 1.05f - boilerWater;
        }

        public static void DebugLog(string message)
        {
            if (settings.enableLogging)
                mod.Logger.Log(message);
        }

        public class Settings : UnityModManager.ModSettings, IDrawable
        {
            [Draw("Boiler steam generation rate")] public float steamGenerationRate = 0.5f;
            [Draw("Cutoff wheel gamma")] public float cutoffGamma = 1.9f;
            [Draw("Max boiler pressure")] public float safetyValveThreshold = 16f;

            [Draw("Enable detailed low-speed simulation")] public bool enableLowSpeedSimulation = true;
            [Draw("Low-speed simulation transition start", VisibleOn = "enableLowSpeedSimulation|true")]
            public float lowSpeedTransitionStart = 10f;
            [Draw("Low-speed simulation transition width", VisibleOn = "enableLowSpeedSimulation|true")]
            public float lowSpeedTransitionWidth = 5f;

            [Draw("Enable logging")] public bool enableLogging = false;

            override public void Save(UnityModManager.ModEntry entry) {
                Save<Settings>(this, entry);
            }

            public void OnChange() {
                cutoffGamma = Mathf.Max(cutoffGamma, 0.1f);
                safetyValveThreshold = Mathf.Clamp(safetyValveThreshold, 0f, 20f);
            }
        }

        [HarmonyPatch(typeof(SteamLocoSimulation), "SimulateWater")]
        static class SimulateWaterPatch
        {
            static void Postfix(SteamLocoSimulation __instance)
            {
                if (!Main.enabled)
                    return;
                float steamVolumeBefore = BoilerSteamVolume(__instance.boilerWater.value);
                float steamVolumeAfter = BoilerSteamVolume(__instance.boilerWater.nextValue);
                __instance.boilerPressure.SetValue(__instance.boilerPressure.value * steamVolumeBefore / steamVolumeAfter);
            }
        }

        [HarmonyPatch(typeof(SteamLocoSimulation), "SimulateSteam")]
        static class SimulateSteamPatch
        {
            static void Postfix(SteamLocoSimulation __instance, float deltaTime)
            {
                var loco = __instance.GetComponent<TrainCar>();
                float before = __instance.boilerPressure.value;
                float after = __instance.boilerPressure.nextValue;
                if (after > before)
                {
                    float pressureGain = after - before;
                    float adjustedGain = pressureGain * settings.steamGenerationRate;
                    float newPressure = before + adjustedGain;
                    __instance.boilerPressure.SetNextValue(newPressure);
                    if (deltaTime > 0 && loco == PlayerManager.LastLoco)
                        HeadsUpDisplayBridge.instance?.UpdateSteamGeneration(loco, adjustedGain / deltaTime * __instance.timeMult);
                }
            }
        }

        [HarmonyPatch(typeof(SteamLocoSimulation), "SimulateCylinder")]
        static class SimulateCylinderPatch
        {
            const float SINUSOID_AVERAGE = 2f / Mathf.PI;
            static float InstantaneousCylinderPowerRatio(float cutoff, float pistonPosition)
            {
                float pressureRatio = pistonPosition <= cutoff ? 1f : cutoff / pistonPosition;
                float angleRatio = Mathf.Sin(Mathf.PI * pistonPosition) / SINUSOID_AVERAGE;
                return pressureRatio * angleRatio;
            }

            // Assume: cyl2 is leading cyl1 by 90 degrees (0.25 rotation)
            // Piston position moves through 1 stroke every 0.5 rotation
            // 0 <= rotation < 0.25
            //    cyl1 acting forward, position = rotation * 2
            //    cyl2 acting forward, position = (rotation + 0.25) * 2
            // 0.25 <= rotation < 0.5
            //    cyl1 acting forward, position = rotation * 2
            //    cyl2 acting backward, position = (rotation + 0.25) % 0.5 * 2
            // 0.5 <= rotation < 0.75
            //    cyl1 acting backward, position = (rotation - 0.5) * 2
            //    cyl2 acting backward, position = (rotation - 0.25) * 2
            // 0.75 <= rotation < 1
            //    cyl1 acting backward, position = (rotation - 0.5) * 2
            //    cyl2 acting forward, position = (rotation - 0.75) * 2
            static float InstantaneousPowerRatio(float cutoff, float rotation)
            {
                float pistonPosition1 = rotation % 0.5f * 2f;
                float pistonPosition2 = (rotation + 0.25f) % 0.5f * 2f;
                return InstantaneousCylinderPowerRatio(cutoff, pistonPosition1) +
                    InstantaneousCylinderPowerRatio(cutoff, pistonPosition2);
            }

            static float AveragePowerRatio(float cutoff)
            {
                float injectionPower = cutoff;
                float expansionPower = cutoff * -Mathf.Log(cutoff);
                return injectionPower + expansionPower;
            }

            static float PowerRatio(float cutoff, float speed, float revolution)
            {
                if (!settings.enableLowSpeedSimulation)
                    return AveragePowerRatio(cutoff);

                return Mathf.Lerp(
                        InstantaneousPowerRatio(cutoff, revolution),
                        AveragePowerRatio(cutoff),
                        (speed - settings.lowSpeedTransitionStart) /
                            settings.lowSpeedTransitionWidth);
            }

            static bool Prefix(SteamLocoSimulation __instance, float deltaTime)
            {
                var loco = __instance.GetComponent<TrainCar>();
                if (Main.enabled)
                {
                    try
                    {
                        float cutoff = Mathf.Pow(__instance.cutoff.value, settings.cutoffGamma) * 0.85f;
                        HeadsUpDisplayBridge.instance?.UpdateCutoffSetting(loco, cutoff);
                        if (cutoff > 0)
                        {
                            float boilerPressureRatio =
                                __instance.boilerPressure.value / SteamLocoSimulation.BOILER_PRESSURE_MAX_KG_PER_SQR_CM;
                            float steamChestPressureRatio = boilerPressureRatio * __instance.regulator.value;

                            var chuff = __instance.GetComponent<ChuffController>();
                            float powerRatio = PowerRatio(cutoff, __instance.speed.value, chuff.dbgCurrentRevolution);
                            __instance.power.SetNextValue(steamChestPressureRatio * powerRatio * SteamLocoSimulation.POWER_CONST_HP);

                            // USRA Light Mikado
                            // cylinder displacement = 262L
                            // 4 strokes / revolution
                            // 4.4m driver circumference (see ChuffController)
                            // ~909 strokes / km
                            // (~0.25 strokes / s) / (km/h)
                            float cylinderSteamVolumeConsumed = __instance.speed.value * 0.25f * 262f * cutoff * deltaTime;
                            float boilerSteamVolumeConsumed = cylinderSteamVolumeConsumed * __instance.regulator.value;
                            float boilerSteamVolume = BoilerSteamVolume(__instance.boilerWater.value);
                            float pressureConsumed = __instance.boilerPressure.value * boilerSteamVolumeConsumed / boilerSteamVolume;
                            __instance.boilerPressure.AddNextValue(-pressureConsumed);
                            if (deltaTime > 0)
                                HeadsUpDisplayBridge.instance?.UpdateSteamUsage(loco, pressureConsumed / deltaTime * __instance.timeMult);
                        }
                        return false;
                    }
                    catch (Exception e)
                    {
                        mod.Logger.Error(e.ToString());
                    }
                }
                return true;
            }
        }
    }
}
