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

        public static IEnumerator UpdateSmokeParticles(SteamLocoChuffSmokeParticles __instance)
        {
            WaitForSeconds waitTimeout = WaitFor.Seconds(0.2f);
            yield return waitTimeout;
            var sim = __instance.loco.sim;
            var state = FireState.Instance(sim);
            var settings = Main.settings.smoke;
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

                float volume = Mathf.InverseLerp(settings.minSmokeOxygenSupply, settings.maxSmokeOxygenSupply, state.oxygenSupply);
                var rate = Mathf.Lerp(settings.minSmokeRate, settings.maxSmokeRate, volume);
                var lifetime = Mathf.Lerp(settings.minSmokeLifetime, settings.maxSmokeLifetime, volume);

                var cleanSmoke = new Color(1f, 1f, 1f, settings.cleanSmokeOpacity);
                var color = Color.Lerp(Color.black, cleanSmoke, state.smoothedOxygenAvailability);

                var main = __instance.chimneyParticles.main;
                main.startColor = color;
                main.startLifetime = lifetime;
                main.maxParticles = (int)(settings.maxSmokeLifetime * settings.maxSmokeRate) * 10;
                var emission = __instance.chimneyParticles.emission;
                emission.rateOverTime = rate;
                Main.DebugLog(TrainCar.Resolve(__instance.gameObject), () => $"volume={volume},color={color},rate={rate},lifetime={lifetime}");
            }
        }
    }
}
