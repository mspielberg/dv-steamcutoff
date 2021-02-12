using HarmonyLib;
using System.Collections;
using UnityEngine;

namespace DvMod.SteamCutoff
{
    [HarmonyPatch(typeof(TenderCouplerJointEnstronger), nameof(TenderCouplerJointEnstronger.EnstrongJoints))]
    public static class TenderFix
    {
        public static bool Prefix(TenderCouplerJointEnstronger __instance, Coupler coupler, ref IEnumerator __result)
        {
            __result = EnstrongJoints(__instance, coupler);
            return false;
        }

        private static IEnumerator EnstrongJoints(TenderCouplerJointEnstronger __instance, Coupler coupler)
        {
            coupler.DestroyRigidJoint();
            var cj = coupler.springyCJ;
            cj.breakForce = float.PositiveInfinity;
            cj.linearLimit = new SoftJointLimit
            {
                limit = 0.8f
            };
            while (coupler.IsJointAdaptationActive)
            {
                yield return WaitFor.Seconds(0.5f);
            }
            cj.anchor += Vector3.forward * -0.8f;
            cj.linearLimit = new SoftJointLimit { limit = 0f };
            cj.linearLimitSpring = new SoftJointLimitSpring {
                spring = 0f,
                damper = 0f,
            };
            __instance.enstrongCoro = null;
        }
    }
}