using DV.CabControls;
using DV.CabControls.Spec;
using HarmonyLib;

namespace DvMod.SteamCutoff
{
    public static class AdjustCabControls
    {
        [HarmonyPatch(typeof(ControlsInstantiator), nameof(ControlsInstantiator.Spawn))]
        public static class SpawnPatch
        {
            public static void Prefix(ControlSpec spec)
            {
                if (spec.name == "C throttle regulator" && spec is Lever leverSpec)
                {
                    leverSpec.invertDirection ^= true;
                }
                else if (spec.name == "C draft" && spec is Puller pullerSpec)
                {
                    pullerSpec.invertDirection ^= true;
                }
            }
        }
    }
}