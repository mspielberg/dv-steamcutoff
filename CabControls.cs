using System.Collections;
using System.Collections.Generic;
using DV.CabControls;
using DV.CabControls.Spec;
using HarmonyLib;
using UnityEngine;

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
                    leverSpec.scrollWheelHoverScroll *= -1;
                }
                else if (spec.name == "C draft" && spec is Puller pullerSpec)
                {
                    pullerSpec.invertDirection ^= true;
                }
            }
        }

        [HarmonyPatch(typeof(HJAFDrivenAnimation), nameof(HJAFDrivenAnimation.Update))]
        public static class HJAFDrivenAnimationPatch
        {
            public static bool Prefix(HJAFDrivenAnimation __instance, HingeJointAngleFix ___hjaf)
            {
                if (__instance.name != "Regulator Stem Animated")
                    return true;
                if (!__instance.initialized)
                    return false;
                __instance.animator.SetFloat(__instance.floatParameterName, Mathf.Clamp(__instance.debugOverride ? __instance.debugValue : 1f - ___hjaf.Percentage, 0.0f, 0.999f));
                return false;
            }
        }
    }

    public class ControlState
    {
        public bool initialized;
        public float stokerSetting;
        public float fireOutSetting;
    }

    [HarmonyPatch(typeof(CabInputSteamExtra), nameof(CabInputSteamExtra.OnEnable))]
    public static class CabInputSteamExtraOnEnablePatch
    {
        private static IEnumerator AddCallbackCoro(CabInputSteamExtra __instance)
        {
            while (!__instance.ctrl || !__instance.ctrl.sim)
                yield return WaitFor.SecondsRealtime(1f);
            var state = ExtraState.Instance(TrainCar.Resolve(__instance.gameObject))!.controlState;
            var stokerCtrl = __instance.transform.Find("C valve controller/C valve 3").GetComponent<ControlImplBase>();
            stokerCtrl.SetValue(state.stokerSetting);
            stokerCtrl.ValueChanged += e => state.stokerSetting = e.newValue;
            var fireOutCtrl = __instance.fireOutValveObj.GetComponent<ControlImplBase>();
            fireOutCtrl.ValueChanged += e => state.fireOutSetting = e.newValue;
            state.initialized = true;
        }

        public static void Postfix(CabInputSteamExtra __instance)
        {
            __instance.StartCoroutine(AddCallbackCoro(__instance));
        }
    }
}
