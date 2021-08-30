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
        private class ExtraState
        {
            public float valveOpening;

            private static readonly Dictionary<SteamLocoSimulation, ExtraState> states = new Dictionary<SteamLocoSimulation, ExtraState>();
            public static ExtraState Instance(SteamLocoSimulation sim)
            {
                if (!states.TryGetValue(sim, out var state))
                    states[sim] = state = new ExtraState();
                return state;
            }
        }

        [HarmonyPatch(typeof(CabInputSteamExtra), nameof(CabInputSteamExtra.OnEnable))]
        public static class CabInputSteamExtraOnEnablePatch
        {
            private static IEnumerator AddCallbackCoro(CabInputSteamExtra __instance)
            {
                while (!__instance.ctrl || !__instance.ctrl.sim)
                    yield return WaitFor.SecondsRealtime(1f);
                var state = ExtraState.Instance(__instance.ctrl.sim);
                var stokerCtrl = __instance.transform.Find("C valve controller/C valve 3").GetComponent<ControlImplBase>();
                stokerCtrl.SetValue(state.valveOpening);
                stokerCtrl.ValueChanged += e => state.valveOpening = e.newValue;
            }

            public static void Postfix(CabInputSteamExtra __instance)
            {
                __instance.StartCoroutine(AddCallbackCoro(__instance));
            }
        }

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
                var state = ExtraState.Instance(__instance);
                var rate = __instance.boilerPressure.value / Main.settings.safetyValveThreshold * state.valveOpening; // ratio 0-1
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
