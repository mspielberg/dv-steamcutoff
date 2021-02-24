using UnityModManagerNet;
using UnityEngine;

namespace DvMod.SteamCutoff
{
        public class Settings : UnityModManager.ModSettings, IDrawable
        {
            [Draw("Max boiler pressure", Min = 0f, Max = 20f)]
            public float safetyValveThreshold = 14f;
            [Draw("Coal combustion rate")]
            public float combustionRate = 1.5f;
            [Draw("Boiler thermal efficiency")]
            public float boilerThermalEfficiency = 0.8f;

            [Draw("Enable detailed low-speed simulation")]
            public bool enableLowSpeedSimulation = true;
            [Draw("Low-speed simulation transition start", VisibleOn = "enableLowSpeedSimulation|true")]
            public float lowSpeedTransitionStart = 10f;
            [Draw("Low-speed simulation transition width", VisibleOn = "enableLowSpeedSimulation|true")]
            public float lowSpeedTransitionWidth = 5f;

            [Draw("Enable logging")] public bool enableLogging = false;

            override public void Save(UnityModManager.ModEntry entry) {
                Save(this, entry);
            }

            public void OnChange() {}
        }
}