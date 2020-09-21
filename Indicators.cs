using HarmonyLib;
using UnityEngine;

namespace DvMod.SteamCutoff
{
    [HarmonyPatch(typeof(IndicatorsSteam), nameof(IndicatorsSteam.Update))]
    public static class IndicatorsSteamPatch
    {
        public const float BallSize = 0.015f;
        public const float MinLevel = 0.2f;
        public const float MaxLevel = 0.85f;
        public const float PositionMultiplier = 0.25f;
        public const float PositionOffset = -0.01f;
        public const string OriginName = "DvMod.SteamCutoff.waterOrigin";
        public const string BallFloatName = "DvMod.SteamCutoff.waterFloat";

        public static void Postfix(IndicatorsSteam __instance)
        {
            var waterLevel = __instance.boilerWater;
            var ballTransform = waterLevel.transform.parent.Find($"{OriginName}/{BallFloatName}");

            if (ballTransform == null)
            {
                var origin = new GameObject(OriginName);
                origin.transform.parent = waterLevel.transform.parent;
                origin.transform.localPosition = waterLevel.transform.localPosition;
                origin.transform.localRotation = waterLevel.transform.localRotation;

                var ballFloat = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                ballFloat.name = BallFloatName;
                Object.Destroy(ballFloat.GetComponent<SphereCollider>());

                ballTransform = ballFloat.transform;
                ballTransform.SetParent(origin.transform, worldPositionStays: false);
                ballTransform.localScale = Vector3.one * BallSize;
            }

            var height = Mathf.Clamp(waterLevel.GetNormalizedValue(), MinLevel, MaxLevel);
            ballTransform.localPosition = new Vector3(0f, 0f, (height * PositionMultiplier) + PositionOffset);
        }
    }
}