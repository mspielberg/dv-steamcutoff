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
    }
}