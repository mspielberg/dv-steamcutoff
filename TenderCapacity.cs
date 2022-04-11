using HarmonyLib;
using UnityEngine;

namespace DvMod.SteamCutoff
{
    // https://www.steamlocomotive.com/locobase.php?country=USA&wheel=2-8-2&railroad=prr#32
    public static class TenderCapacity
    {
        [HarmonyPatch(typeof(TenderSimulation), nameof(TenderSimulation.Awake))]
        public static class TenderSimulationPatch
        {
            public static void Prefix(TenderSimulation __instance)
            {
                __instance.tenderCoal.max = __instance.tenderCoal.value = 10000f;
                __instance.tenderWater.max = __instance.tenderWater.value = 25000f;
            }
        }

        [HarmonyPatch(typeof(LocoStateSaveTender), nameof(LocoStateSaveTender.SetLocoStateSaveData))]
        public static class SetLocoStateSaveDataPatch
        {
            public static void Postfix(LocoStateSaveTender __instance)
            {
                if (__instance.sim.tenderCoal.value == SteamLocoSimulation.TENDER_COAL_CAPACITY_KG
                    && __instance.sim.tenderWater.value == SteamLocoSimulation.TENDER_WATER_CAPACITY_L)
                {
                    __instance.sim.tenderCoal.value = __instance.sim.tenderCoal.max;
                    __instance.sim.tenderWater.value = __instance.sim.tenderWater.max;
                }
            }
        }
    }
}