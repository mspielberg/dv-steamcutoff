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
        public static bool loggingEnabled;
        public static UnityModManager.ModEntry mod;

        static bool Load(UnityModManager.ModEntry modEntry)
        {
            var harmony = new Harmony(modEntry.Info.Id);
            harmony.PatchAll();

            mod = modEntry;
            modEntry.OnGUI = OnGui;
            modEntry.OnToggle = OnToggle;
            modEntry.OnUnload = OnUnload;

            return true;
        }

        static void OnGui(UnityModManager.ModEntry modEntry)
        {
            loggingEnabled = GUILayout.Toggle(loggingEnabled, "enable logging");
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

        static float frameStart = 0;

        static void MaybeLog(String message)
        {
            float currentFrameStart = Time.time;
            if (currentFrameStart == frameStart || currentFrameStart > frameStart + 1)
            {
                mod.Logger.Log(message);
                frameStart = currentFrameStart;
            }
        }

        [HarmonyPatch(typeof(SteamLocoSimulation), "SimulateSteam")]
        static class SimulateSteamPatch
        {
            static void Postfix(SteamLocoSimulation __instance)
            {
                float before = __instance.boilerPressure.value;
                float after = __instance.boilerPressure.nextValue;
                if (after > before)
                {
                    float newPressure = Mathf.Lerp(before, after, 0.5f);
                    MaybeLog($"Loco {__instance.GetInstanceID()}: Adjusting boiler pressure from {after} to {newPressure}");
                    __instance.boilerPressure.SetNextValue(newPressure);
                }
            }
        }

        [HarmonyPatch(typeof(SteamLocoSimulation), "SimulateCylinder")]
        static class SimulateCylinderPatch
        {
            static bool Prefix(SteamLocoSimulation __instance, float deltaTime)
            {
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
                            MaybeLog($"Loco {__instance.GetInstanceID()}: cutoff = {cutoff}; injectionPower = {injectionPower}; expansionPower = {expansionPower}; total = {__instance.power.nextValue}");

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
                            MaybeLog($"Loco {__instance.GetInstanceID()}: boilerSteamVolumeConsumed = {boilerSteamVolumeConsumed}; boilerSteamVolume = {boilerSteamVolume}; pressureConsumed = {pressureConsumed}");
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
                    MaybeLog($"Loco {__instance.GetInstanceID()}: cutoff = {__instance.cutoff.value}; injectionPower = {__instance.power.value}");
                }
                return true;
            }
        }
    }
}
