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
            Register("steamcutoff.dumpCoupler", _ =>
            {
                if (PlayerManager.Car?.carType != TrainCarType.Tender)
                    return;
                var frontCoupler = PlayerManager.Car.frontCoupler;
                Terminal.Log($"coupler = {frontCoupler}");
                Terminal.Log($"coupledTo = {frontCoupler.coupledTo}");
                Terminal.Log($"coupledTo.train = {frontCoupler.coupledTo.train}");
                Terminal.Log($"coupledTo.rigid = {frontCoupler.coupledTo.rigidCJ}");
                Terminal.Log($"coupledTo.springy = {frontCoupler.coupledTo.springyCJ}");
                var joint = frontCoupler.coupledTo.springyCJ;
                Terminal.Log(joint.breakForce.ToString());
                Terminal.Log($"bounciness={joint.linearLimit.bounciness},distance={joint.linearLimit.contactDistance},limit={joint.linearLimit.limit}");
                Terminal.Log($"spring={joint.linearLimitSpring.spring},damper={joint.linearLimitSpring.damper}");
            });
        }
    }
}