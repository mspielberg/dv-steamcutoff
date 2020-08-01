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
        public static UnityModManager.ModEntry mod;

        static bool Load(UnityModManager.ModEntry modEntry)
        {
            var harmony = new Harmony(modEntry.Info.Id);
            harmony.PatchAll();

            mod = modEntry;
            modEntry.OnToggle = OnToggle;
            modEntry.OnUnload = OnUnload;

            return true;
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

        [HarmonyPatch(typeof(SteamLocoSimulation), "SimulateCylinder")]
        static class SimulateCylinderPatch
        {
            static UInt64 frame = 0;

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
                            if (++frame % 60 == 0)
                                mod.Logger.Log($"cutoff = {cutoff}; injectionPower = {injectionPower}; expansionPower = {expansionPower}; total = {__instance.power.nextValue}");

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
                            if (frame % 60 == 0)
                                mod.Logger.Log($"boilerSteamVolumeConsumed = {boilerSteamVolumeConsumed}; boilerSteamVolume = {boilerSteamVolume}; pressureConsumed = {pressureConsumed}");
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
                    if (frame++ % 60 == 0)
                        mod.Logger.Log($"cutoff = {__instance.cutoff.value}; injectionPower = {__instance.power.value}");
                }
                return true;
            }
        }
    }
}
