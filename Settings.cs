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
            [Draw("Enable sight glass float")]
            public bool enableBallFloat = true;
            [Draw("Enable logging")]
            public bool enableLogging = false;

            override public void Save(UnityModManager.ModEntry entry) {
                Save(this, entry);
            }

            public void OnChange() {}
        }
}