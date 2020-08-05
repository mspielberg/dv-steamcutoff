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
            modEntry.OnUpdate = OnUpdate;

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

        static class Monitor
        {
            public static float steamUsage;
            public static float tractionForce;
            public static float cutoffSetting;

            public static float lastLogTime;
        }

        static void OnUpdate(UnityModManager.ModEntry modEntry, float deltaTime)
        {
            if (!settings.enableLogging)
                return;

            if (Time.time > Monitor.lastLogTime + 1f)
            {
                Monitor.lastLogTime = Time.time;
                modEntry.Logger.Log($"cutoff = {(int)(Monitor.cutoffSetting * 100)}%; steamUsage = {Monitor.steamUsage} mbar/s; tractionForce = {(int)(Monitor.tractionForce / 1000)} kN");
                Monitor.steamUsage = 0;
            }
        }

        public class Settings : UnityModManager.ModSettings, IDrawable
        {
            [Draw("Boiler steam generation rate")] public float steamGenerationRate = 0.5f;
            [Draw("Enable logging")] public bool enableLogging = false;
            [Draw("Cutoff wheel gamma")] public float cutoffGamma = 1.9f;

            override public void Save(UnityModManager.ModEntry entry) {
                Save<Settings>(this, entry);
            }

            public void OnChange() {
                cutoffGamma = Mathf.Max(cutoffGamma, 0.1f);
            }
        }

        [HarmonyPatch(typeof(LocoControllerSteam), "GetTractionForce")]
        static class GetTractionForcePatch
        {
            static void Postfix(LocoControllerSteam __instance, float __result)
            {
                var car = __instance.GetComponent<TrainCar>();
                if (car == PlayerManager.LastLoco)
                    Monitor.tractionForce = __result;
            }
        }

        [HarmonyPatch(typeof(SteamLocoSimulation), "SimulateSteam")]
        static class SimulateSteamPatch
        {
            static void Postfix(SteamLocoSimulation __instance)
            {
                var id = __instance.GetComponent<TrainCar>().ID;
                float before = __instance.boilerPressure.value;
                float after = __instance.boilerPressure.nextValue;
                if (after > before)
                {
                    float pressureGain = after - before;
                    float adjustedGain = pressureGain * settings.steamGenerationRate;
                    float newPressure = before + adjustedGain;
                    __instance.boilerPressure.SetNextValue(newPressure);
                }
            }
        }

        [HarmonyPatch(typeof(SteamLocoSimulation), "SimulateCylinder")]
        static class SimulateCylinderPatch
        {
            static bool Prefix(SteamLocoSimulation __instance, float deltaTime)
            {
                var loco = __instance.GetComponent<TrainCar>();
                if (Main.enabled)
                {
                    try
                    {
                        float cutoff = Mathf.Pow(__instance.cutoff.value, settings.cutoffGamma) * 0.85f;
                        if (loco == PlayerManager.LastLoco)
                            Monitor.cutoffSetting = cutoff;
                        if (cutoff > 0)
                        {
                            float steamChestPressure = __instance.boilerPressure.value * __instance.regulator.value;
                            float pressureRatio = steamChestPressure / SteamLocoSimulation.BOILER_PRESSURE_MAX_KG_PER_SQR_CM * SteamLocoSimulation.POWER_CONST_HP;
                            float injectionPower = pressureRatio * cutoff;
                            float expansionPower = (float)(pressureRatio * cutoff * -Math.Log(cutoff));
                            __instance.power.SetNextValue(injectionPower + expansionPower);

                            // USRA Light Mikado
                            // cylinder displacement = 262L
                            // 4 strokes / revolution
                            // 4.4m driver circumference (see ChuffController)
                            // ~909 strokes / km
                            // (~0.25 strokes / s) / (km/h)
                            float cylinderSteamVolumeConsumed = __instance.speed.value * 0.25f * 262f * cutoff * deltaTime;
                            float boilerSteamVolumeConsumed = cylinderSteamVolumeConsumed * __instance.regulator.value;
                            float boilerSteamVolume = SteamLocoSimulation.BOILER_WATER_CAPACITY_L * 1.05f - __instance.boilerWater.value;
                            float pressureConsumed = __instance.boilerPressure.value * boilerSteamVolumeConsumed / boilerSteamVolume;
                            __instance.boilerPressure.AddNextValue(-pressureConsumed);
                            if (loco == PlayerManager.LastLoco)
                                Monitor.steamUsage += pressureConsumed * 1000;
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
