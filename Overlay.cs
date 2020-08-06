using UnityEngine;

namespace SteamCutoff
{
    class Overlay : MonoBehaviour
    {
        public static Overlay instance;
        public Main.Settings settings;

        public float cutoffSetting;
        public float tractionForce;
        public float steamGeneration;
        public float steamUsage;

        public float inclination;

        public Overlay()
        {
            this.settings = Main.settings;
            instance = this;
        }

        public void OnGUI()
        {
            if (!settings.showInfoOverlay)
                return;
            if (PlayerManager.Car?.GetComponent<SteamLocoSimulation>() == null)
                return;
            GUILayout.BeginHorizontal("box");
            GUILayout.BeginVertical();
            GUILayout.Label("Cutoff:");
            GUILayout.Label("Tractive effort:");
            GUILayout.Label("Steam generation:");
            GUILayout.Label("Steam usage:");
            GUILayout.Label("Inclination:");
            GUILayout.Label("Grade:");
            GUILayout.EndVertical();
            GUILayout.BeginVertical();
            GUILayout.Label($"{Mathf.RoundToInt(cutoffSetting * 100)}%");
            GUILayout.Label($"{Mathf.RoundToInt(tractionForce / 1000)} kN");
            GUILayout.Label($"{Mathf.RoundToInt(steamGeneration * 1000)} mbar/s");
            GUILayout.Label($"{Mathf.RoundToInt(steamUsage * 1000)} mbar/s");
            GUILayout.Label($"{inclination.ToString("F1")}\xB0");
            GUILayout.Label($"{(Mathf.Tan(inclination * Mathf.PI / 180) * 100).ToString("F1")}%");
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }

        private const float alpha = 0.1f;

        public void UpdateSteamGeneration(float pressureRise)
        {
            steamGeneration = steamGeneration * alpha + pressureRise * (1f - alpha);
        }
        public void UpdateSteamUsage(float pressureDrop)
        {
            steamUsage = steamUsage * alpha + pressureDrop * (1f - alpha);
        }

        public void UpdateInclination(float inclinationDegrees)
        {
            var alpha = 0.999f;
            inclination = inclination * alpha + inclinationDegrees * (1f - alpha);
        }
    }
}