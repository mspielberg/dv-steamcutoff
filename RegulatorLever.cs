using DV.CabControls;
using DV.CabControls.Spec;
using HarmonyLib;

namespace DvMod.SteamCutoff
{
    [HarmonyPatch(typeof(ControlsInstantiator), nameof(ControlsInstantiator.Spawn))]
    public static class RegulatorLeverPatch
    {
        public static void Prefix(ControlSpec spec)
        {
            if (spec.name == "C throttle regulator" && spec is Lever leverSpec)
            {
                Main.DebugLog($"before: regulator invertDirection={leverSpec.invertDirection}");
                leverSpec.invertDirection ^= true;
                Main.DebugLog($"after: regulator invertDirection={leverSpec.invertDirection}");
            }
        }
    }
}