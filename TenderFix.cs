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
            var stress = __instance.GetComponent<TrainStress>();
            stress.DisableStressCheckForTwoSeconds();
            coupler.DestroyRigidJoint();
            coupler.KillJointCoroutines();
            var cj = coupler.springyCJ;
            cj.breakForce = float.PositiveInfinity;
            cj.linearLimitSpring = new SoftJointLimitSpring {
                spring = 2e7f,
                damper = 0f,
            };
            if (CarTypes.IsSteamLocomotive(coupler.train.carType))
                cj.anchor += Vector3.back * 0.8f;
            else
                cj.connectedAnchor += Vector3.back * 0.8f;
            cj.linearLimit = new SoftJointLimit
            {
                limit = cj.CurrentDisplacement().magnitude,
            };
            while (cj.linearLimit.limit > 0f)
            {
                yield return WaitFor.FixedUpdate;
                cj.linearLimit = new SoftJointLimit { limit = Mathf.Max(0f, cj.linearLimit.limit - 0.001f) };
            }
            __instance.enstrongCoro = null;
        }

        private static void DebugLog(ConfigurableJoint cj)
        {
            Main.DebugLog($"anchor={cj.anchor},connectedAnchor={cj.connectedAnchor},limit={cj.linearLimit.limit},spring={cj.linearLimitSpring.spring},damper={cj.linearLimitSpring.damper},displacement={cj.CurrentDisplacement().z}");
        }
    }
}