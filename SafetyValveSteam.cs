using HarmonyLib;

namespace DvMod.SteamCutoff
{
    public static class SafetyValveSteam
    {
        [HarmonyPatch(typeof(SteamLocoSteamParticles), nameof(SteamLocoSteamParticles.SteamReleaseAndSafetyDump))]
        public static class OnEnablePatch
        {
            public static void Postfix(SteamLocoSteamParticles __instance)
            {
                var safetyRelease = __instance.safetyRelease;
                var main = safetyRelease.main;
                var emission = safetyRelease.emission;
                var rate = __instance.controller.GetPressureSafetyValve();
                if (rate < 0.01f)
                {
                    if (safetyRelease.isPlaying)
                        safetyRelease.Stop();
                    return;
                }

                main.gravityModifier = Main.settings.safetyValveParticleGravity;
                main.startLifetime = Main.settings.safetyValveParticleLifetime;
                main.startSpeed = Main.settings.safetyValveParticleSpeed;
                main.maxParticles = (int)(Main.settings.safetyValveParticleLifetime * Main.settings.safetyValveParticleRate);
                emission.rateOverTime = rate * Main.settings.safetyValveParticleRate;
                if (!safetyRelease.isPlaying)
                    safetyRelease.Play();
            }
        }
    }
}