using UnityModManagerNet;
using UnityEngine;

namespace DvMod.SteamCutoff
{
        public class Settings : UnityModManager.ModSettings, IDrawable
        {
            [Draw("Enable sight glass float")]
            public bool enableBallFloat = true;
            [Draw("Max boiler pressure", Min = 0f, Max = 20f)]
            public float safetyValveThreshold = 14f;
            [Draw("Coal combustion rate")]
            public float combustionRate = 1.5f;

            [Draw("Airflow for min smoke")]
            public float minSmokeOxygenSupply = 0f;
            [Draw("Airflow for max smoke")]
            public float maxSmokeOxygenSupply = 1f;
            [Draw("Min smoke rate")]
            public float minSmokeRate = 5f;
            [Draw("Max smoke rate")]
            public float maxSmokeRate = 300f;
            [Draw("Min smoke lifetime")]
            public float minSmokeLifetime = 0.5f;
            [Draw("Max smoke lifetime")]
            public float maxSmokeLifetime = 10f;
            [Draw("Clean smoke opacity", Min = 0f, Max = 1f)]
            public float cleanSmokeOpacity = 0.1f;

            [Draw("Boiler thermal efficiency")]
            public float boilerThermalEfficiency = 0.8f;

            [Draw("Enable detailed low-speed simulation")]
            public bool enableLowSpeedSimulation = true;

            [Draw("Enable logging")]
            public bool enableLogging = false;

            override public void Save(UnityModManager.ModEntry entry) {
                Save(this, entry);
            }

            public void OnChange() {}
        }
}