using DV.CabControls;
using DV.CabControls.NonVR;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace SteamCutoff
{
    [HarmonyPatch(typeof(WheelNonVR), "IMouseWheelHoverScrollable.OnHoverScrollReleased")]
    static class OnHoverScrollReleasedPatch {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var insts = new List<CodeInstruction>(instructions);
            var springStrengthField = typeof(WheelBase).GetField("springStrength", BindingFlags.NonPublic | BindingFlags.Instance);
            var index = insts.FindIndex(inst => inst.LoadsField(springStrengthField));

            var specField = typeof(WheelBase).GetField("spec", BindingFlags.NonPublic | BindingFlags.Instance);
            var jointSpringField = typeof(DV.CabControls.Spec.Wheel).GetField("jointSpring");
            insts[index].operand = jointSpringField;
            insts.Insert(index, new CodeInstruction(OpCodes.Ldfld, specField));

            return insts;
        }
    }

/*
        static void ResetWheelPhysics()
        {
            var mass = 0.1f;
            var damper = 2f;
            var springStrength = 20f;

            WheelBase[] wheels = MonoBehaviour.FindObjectsOfType<WheelBase>();
            mod.Logger.Log($"Found {wheels.Length} WheelBase behaviours");
            var springStrengthField = typeof(WheelBase).GetField("springStrength", BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var wheel in wheels)
            {
                var spec = wheel.GetComponent<DV.CabControls.Spec.Wheel>();
                mod.Logger.Log($"spec = {spec}; mass = {spec.mass}, angularDrag = {spec.angularDrag}, springDamper = {spec.springDamper}");

                var rb = wheel.GetComponent<Rigidbody>();
                rb.mass = mass;

                springStrengthField.SetValue(wheel, springStrength);

                var hj = wheel.GetComponent<HingeJoint>();
                var spring = hj.spring;
                spring.damper = damper;
                spring.spring = springStrength;
                hj.spring = spring;
                hj.useSpring = true;
                mod.Logger.Log($"Set spring to {spring.spring}, damper = {spring.damper}");
            }
        }
        */
}