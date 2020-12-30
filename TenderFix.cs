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
            coupler.springyCJ.breakForce = float.PositiveInfinity;
            coupler.springyCJ.linearLimit = new SoftJointLimit
            {
                limit = 0.8f
            };
            while (coupler.IsJointAdaptationActive)
            {
                yield return WaitFor.Seconds(0.5f);
            }
            coupler.springyCJ.linearLimit = new SoftJointLimit
            {
                limit = 0.1f
            };
            coupler.springyCJ.linearLimitSpring = new SoftJointLimitSpring
            {
                spring = Coupler.SPRINGY_JOINT_LIMIT_SPRING_TIGHT,
                damper = Coupler.SPRINGY_JOINT_LIMIT_DAMPER_TIGHT
            };
            __instance.enstrongCoro = null;
        }
    }
}