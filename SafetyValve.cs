using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;

namespace DvMod.SteamCutoff
{
    [HarmonyPatch(typeof(SteamLocoSimulation), "SimulateSteam")]
    static class SafetyValveThresholdPatch
    {
        static float GetOpenThreshold()
        {
            return Main.settings.safetyValveThreshold;
        }

        static float GetCloseThreshold()
        {
            return GetOpenThreshold() - 0.5f;
        }

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.Select(inst => {
                if (inst.LoadsConstant(20f))
                    return new CodeInstruction(
                        OpCodes.Call,
                        AccessTools.Method(typeof(SafetyValveThresholdPatch), "GetOpenThreshold"));
                else if (inst.LoadsConstant(19.5f))
                    return new CodeInstruction(
                        OpCodes.Call,
                        AccessTools.Method(typeof(SafetyValveThresholdPatch), "GetCloseThreshold"));
                else
                    return inst;
            });
        }
    }
}