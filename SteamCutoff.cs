using HarmonyLib;
using System;
using UnityEngine;
using UnityModManagerNet;

namespace SteamCutoff
{
    [EnableReloading]
    static class Main
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

        static void DebugLog(String message)
        {
            if (settings.enableLogging)
                mod.Logger.Log(message);
        }

        static float frameStart = 0;

        static void MaybeLog(String message)
        {
            float currentFrameStart = Time.time;
            if (currentFrameStart == frameStart || currentFrameStart > frameStart + 1)
            {
                DebugLog(message);
                frameStart = currentFrameStart;
            }
        }

        public class Settings : UnityModManager.ModSettings, IDrawable
        {
            [Draw("Boiler steam generation rate")] public float steamGenerationRate = 0.5f;
            [Draw("Enable logging")] public bool enableLogging = false;

            override public void Save(UnityModManager.ModEntry entry) {
                Save<Settings>(this, entry);
            }

            public void OnChange() {}
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
                    MaybeLog($"{id}: Adjusting steam gain from {pressureGain} to {adjustedGain}");
                    __instance.boilerPressure.SetNextValue(newPressure);
                }
            }
        }

        [HarmonyPatch(typeof(SteamLocoSimulation), "SimulateCylinder")]
        static class SimulateCylinderPatch
        {
            static bool Prefix(SteamLocoSimulation __instance, float deltaTime)
            {
                var id = __instance.GetComponent<TrainCar>().ID;
                if (Main.enabled)
                {
                    try
                    {
                        float cutoff = __instance.cutoff.value * 0.85f;
                        if (cutoff > 0)
                        {
                            float steamChestPressure = __instance.boilerPressure.value * __instance.regulator.value;
                            float pressureRatio = steamChestPressure / SteamLocoSimulation.BOILER_PRESSURE_MAX_KG_PER_SQR_CM * SteamLocoSimulation.POWER_CONST_HP;
                            float injectionPower = pressureRatio * cutoff;
                            float expansionPower = (float)(pressureRatio * cutoff * -Math.Log(cutoff));
                            __instance.power.SetNextValue(injectionPower + expansionPower);
                            MaybeLog($"{id}: cutoff = {cutoff}; injectionPower = {injectionPower}; expansionPower = {expansionPower}; total = {__instance.power.nextValue}");

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
                            MaybeLog($"{id}: boilerSteamVolumeConsumed = {boilerSteamVolumeConsumed}; boilerSteamVolume = {boilerSteamVolume}; pressureConsumed = {pressureConsumed}");
                        }
                        return false;
                    }
                    catch (Exception e)
                    {
                        mod.Logger.Error(e.ToString());
                    }
                }
                else
                {
                    MaybeLog($"{id}: cutoff = {__instance.cutoff.value}; injectionPower = {__instance.power.value}");
                }
                return true;
            }
        }
    }
}
