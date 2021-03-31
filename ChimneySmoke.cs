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
        }

        private static readonly Color clearSmoke = new Color(1f, 1f, 1f, 0.1f);

        public static IEnumerator UpdateSmokeParticles(SteamLocoChuffSmokeParticles __instance)
        {
            WaitForSeconds waitTimeout = WaitFor.Seconds(0.2f);
            yield return waitTimeout;
            var sim = __instance.loco.sim;
            var state = FireState.Instance(sim);
            while (true)
            {
                yield return waitTimeout;

                if (sim.fireOn.value == 0f)
                {
                    __instance.chimneyParticles.Stop();
                    continue;
                }

                if (!__instance.chimneyParticles.isPlaying)
                    __instance.chimneyParticles.Play();

                float volume = Mathf.InverseLerp(Main.settings.minSmokeOxygenSupply, Main.settings.maxSmokeOxygenSupply, state.oxygenSupply);
                var color = Color.Lerp(Color.black, clearSmoke, state.oxygenAvailability);

                var main = __instance.chimneyParticles.main;
                main.startColor = color;
                main.startLifetime = Mathf.Lerp(Main.settings.minSmokeLifetime, Main.settings.maxSmokeLifetime, volume);
                var emission = __instance.chimneyParticles.emission;
                emission.rateOverTime = Mathf.Lerp(Main.settings.minSmokeRate, Main.settings.maxSmokeRate, volume);
            }
        }
    }
}