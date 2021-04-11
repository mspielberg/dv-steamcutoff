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
            yield return null;
            var stress = __instance.GetComponent<TrainStress>();
            stress.DisableStressCheckForTwoSeconds();
            coupler.DestroyRigidJoint();
            var cj = coupler.springyCJ;
            cj.breakForce = float.PositiveInfinity;
            cj.linearLimit = new SoftJointLimit
            {
                limit = 2.0f
            };
            cj.linearLimitSpring = new SoftJointLimitSpring {
                spring = 0f,
                damper = 0f,
            };
            cj.anchor += Vector3.forward * -0.8f;
            while (cj.linearLimit.limit > 0f)
            {
                yield return WaitFor.FixedUpdate;
                cj.linearLimit = new SoftJointLimit { limit = Mathf.Max(0f, cj.linearLimit.limit - 0.001f) };
            }
            __instance.enstrongCoro = null;
        }

        private static Vector3 JointDelta(Joint joint)
        {
            var delta = joint.transform.InverseTransformPoint(joint.connectedBody.transform.TransformPoint(joint.connectedAnchor)) - joint.anchor;
            return delta;
        }
    }
}