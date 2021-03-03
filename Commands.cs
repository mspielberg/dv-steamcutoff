using CommandTerminal;
using HarmonyLib;
using System;
using System.Linq;
using UnityEngine;

namespace DvMod.SteamCutoff
{
    public static class Commands
    {
        [HarmonyPatch(typeof(Terminal), nameof(Terminal.Start))]
        public static class RegisterCommandsPatch
        {
            public static void Postfix()
            {
                Register();
            }
        }

        private static void Register(string name, Action<CommandArg[]> proc)
        {
            if (Terminal.Shell == null)
                return;
            if (Terminal.Shell.Commands.Remove(name.ToUpper()))
                Main.DebugLog($"replacing existing command {name}");
            else
                Terminal.Autocomplete.Register(name);
            Terminal.Shell.AddCommand(name, proc);
        }

        public static void Register()
        {
            Register("steamcutoff.dumpChimney", _ =>
            {
                if (PlayerManager.Car?.carType != TrainCarType.LocoSteamHeavy)
                    return;
                var smoke = PlayerManager.Car.GetComponent<SteamLocoChuffSmokeParticles>();
                var chimney = smoke.chimneyParticles;
                Terminal.Log($"color={chimney.main.startColor.color}, rate={chimney.emission.rateOverTime.constant},lifetime={chimney.main.startLifetime.constant}");
            });
            Register("steamcutoff.dumpFireState", _ =>
            {
                if (PlayerManager.Car?.carType != TrainCarType.LocoSteamHeavy)
                    return;
                var sim = PlayerManager.Car.GetComponent<SteamLocoSimulation>();
                var state = FireState.Instance(sim);
                Terminal.Log($"fireOn={sim.fireOn.value},temperature={sim.temperature.value}, coalChunks={string.Join(",", state.coalChunkMasses)}");
            });
        }
    }
}