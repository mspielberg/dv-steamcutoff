using HarmonyLib;
using System.Collections.Generic;
using System.Reflection.Emit;

namespace DvMod.SteamCutoff
{
    public static class WhistleFix
    {
        [HarmonyPatch(typeof(SteamLocoSteamParticles), nameof(SteamLocoSteamParticles.UseBoilerPressureForWhistle))]
        public static class UseBoilerPressureForWhistlePatch
        {
            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                var insts = new List<CodeInstruction>(instructions);
                var index = insts.FindIndex(inst => inst.opcode == OpCodes.Ldloc_0);
                if (index >= 0)
                {
                    insts[index + 1].operand = 0.1f;
                }
                return insts;
            }
        }
    }
}
