using HarmonyLib;
using System.Collections;
using UnityEngine;

namespace DvMod.SteamCutoff
{
    public static class ChimneySmoke
    {
        [HarmonyPatch(typeof(SteamLocoChuffSmokeParticles), nameof(SteamLocoChuffSmokeParticles.OnEnable))]
        public static class OnEnablePatch
        {
            public static bool Prefix(SteamLocoChuffSmokeParticles __instance)
            {
                __instance.loco.chuffController.OnChuff += __instance.Chuff;
                __instance.updateCoroutine = __instance.StartCoroutine(UpdateSmokeParticles(__instance));
                return false;
            }

            public static IEnumerator UpdateSmokeParticles(SteamLocoChuffSmokeParticles __instance)
            {
                WaitForSeconds waitTimeout = WaitFor.Seconds(0.2f);
                var loco = __instance.loco;
                var sim = loco.sim;
                var state = FireState.Instance(sim);
                while (true)
                {
                    yield return waitTimeout;

                    float volume = sim.fireOn.value > 0 ? state.oxygenSupply : 0;
                    float color = Mathf.Clamp01(2 - (2 * state.oxygenAvailability));

                    if (volume == 0f)
                    {
                        __instance.chimneyParticles.Stop();
                    }
                    else if (!__instance.chimneyParticles.isPlaying)
                    {
                        __instance.chimneyParticles.Play();
                    }
                    if (__instance.chimneyParticles.isPlaying)
                    {
                        var main = __instance.chimneyParticles.main;
                        main.startColor = Color.Lerp(__instance.startSmokeColorMin, __instance.startSmokeColorMax, color);
                        main.startLifetime = Mathf.Lerp(1f, 4f, volume);
                        var emission = __instance.chimneyParticles.emission;
                        emission.rateOverTime = Mathf.Lerp(5f, 100f, volume);
                    }
                }
            }
        }
    }
}