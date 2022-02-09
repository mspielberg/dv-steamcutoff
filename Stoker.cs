using DV.CabControls;
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DvMod.SteamCutoff
{
    public static class Stoker
    {
        private const float MaxFiringRate = 2.0f; // in kg/s, ~= 225 lb/h/ft^2 grate area on 70 ft^2 grate (PRR L1s)
        [HarmonyPatch(typeof(SteamLocoSimulation), nameof(SteamLocoSimulation.SimulateTick))]
        public static class SimulateTickPatch
        {
            private static AudioClip[]? chuffClips;
            private static void PlayStokerChuff(Transform transform, float volume)
            {
                (chuffClips ??= Component.FindObjectOfType<LocoAudioSteam>().cylClipsSlow).Play(
                    position: transform.position,
                    volume: volume,
                    maxDistance: 5f,
                    mixerGroup: AudioManager.e.cabGroup,
                    parent: transform);
            }

            public static void Postfix(SteamLocoSimulation __instance, float delta)
            {
                var car = TrainCar.Resolve(__instance.gameObject);
                var state = ExtraControlState.Instance(__instance);
                var rate = __instance.boilerPressure.value / Main.settings.safetyValveThreshold * state.stokerSetting; // ratio 0-1
                var firingRate = MaxFiringRate * Mathf.Pow(rate, 2); // in kg/s
                HeadsUpDisplayBridge.instance?.UpdateStokerFeedRate(car, firingRate);
                __instance.tenderCoal.PassValueTo(__instance.coalbox, firingRate * delta / __instance.timeMult);
                if (__instance.fireOn.value == 0f && __instance.temperature.value > 400f)
                {
                    __instance.fireOn.SetValue(1f);
                }
            }
        }
    }
}
