using HarmonyLib;
using System.Collections;

namespace DvMod.SteamCutoff
{
    public static class WarningSounds
    {
        [HarmonyPatch(typeof(SteamLocoWarningSounds), nameof(SteamLocoWarningSounds.OnEnable))]
        public static class OnEnablePatch
        {
            public static bool Prefix(SteamLocoWarningSounds __instance)
            {
                __instance.StartCoroutine(CheckGaugeWarnings(__instance));
                return false;
            }

            private static IEnumerator CheckGaugeWarnings(SteamLocoWarningSounds __instance)
            {
                var indicators = __instance.indicators;
                while (true)
                {
                    yield return WaitFor.SecondsRealtime(2f);
                    __instance.CheckIndicatorForWarningValue(indicators.boilerWater, 0.75f, ref __instance.waterGaugeCouldSoundWarning);
                    __instance.CheckIndicatorForWarningValue(indicators.sand, 0.2f, ref __instance.sandGaugeCouldSoundWarning);
                    __instance.CheckIndicatorForWarningValue(indicators.pressure, 0.4f, ref __instance.boilerPressureGaugeCouldSoundWarning);
                    __instance.CheckIndicatorForWarningValue(indicators.brakeAux, 0.1f, ref __instance.brakeAuxGaugeCouldSoundWarning);
                }
            }
        }
    }
}