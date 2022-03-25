using UnityModManagerNet;
using UnityEngine;

namespace DvMod.SteamCutoff
{
        public class Settings : UnityModManager.ModSettings, IDrawable
        {
            [Draw("Enable sight glass float")]
            public bool enableBallFloat = true;
            [Draw("Initial boiler pressure", Min = 0f, Max = 20f)]
            public float initialBoilerPressure = 10f;
            [Draw("Max boiler pressure", Min = 0f, Max = 20f)]
            public float safetyValveThreshold = 14f;
            [Draw("Safety valve vent rate")]
            public float safetyValveVentRate = 5f;
            // [Draw("Safety valve feathering")]
            public float safetyValveFeathering = 0.15f;
            // [Draw("Safety valve smoothing")]
            public float safetyValveSmoothing = 2f;

            // [Draw("Safety valve particle gravity")]
            public float safetyValveParticleGravity = 0.1f;
            // [Draw("Safety valve particle lifetime")]
            public float safetyValveParticleLifetime = 7f;
            // [Draw("Safety valve particle rate")]
            public float safetyValveParticleRate = 500f;
            // [Draw("Safety valve particle speed")]
            public float safetyValveParticleSpeed = 5f;
            // [Draw("Safety valve particle min size")]
            // public float safetyValveParticleMinSize = 1f;
            // [Draw("Safety valve particle max size")]
            // public float safetyValveParticleMaxSize = 1f;
            // [Draw("Safety valve particle min size speed")]
            // public float safetyValveParticleMinSizeSpeed = 1f;
            // [Draw("Safety valve particle min size speed")]
            // public float safetyValveParticleMaxSizeSpeed = 1f;

            [Draw("Front-end (exhaust) efficiency")]
            public float frontendEfficiency = 1.9f;
            [Draw("Coal combustion rate")]
            public float combustionRate = 3.0f;
            [Draw("Boiler thermal efficiency")]
            public float boilerThermalEfficiency = 0.55f;
            [Draw("Steam consumption multiplier")]
            public float steamConsumptionMultiplier = 1.0f;
            [Draw("Torque multiplier")]
            public float torqueMultiplier = 1.0f;

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
            public float cleanSmokeOpacity = 0.02f;

            // [Draw("Enable detailed low-speed simulation")]
            public bool enableLowSpeedSimulation = true;

            [Draw("Enable logging")]
            public bool enableLogging = false;

            override public void Save(UnityModManager.ModEntry entry) {
                Save(this, entry);
            }

            public void OnChange() {}
        }
}