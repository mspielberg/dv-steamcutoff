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

        public Overlay()
        {
            this.settings = Main.settings;
            instance = this;
        }

        public void OnGUI()
        {
            if (!settings.showInfoOverlay)
                return;
            GUILayout.BeginHorizontal("box");
            GUILayout.BeginVertical();
            GUILayout.Label("Cutoff:");
            GUILayout.Label("Tractive effort:");
            GUILayout.Label("Steam generation:");
            GUILayout.Label("Steam usage:");
            GUILayout.EndVertical();
            GUILayout.BeginVertical();
            GUILayout.Label($"{Mathf.RoundToInt(cutoffSetting * 100)}%");
            GUILayout.Label($"{Mathf.RoundToInt(tractionForce / 1000)} kN");
            GUILayout.Label($"{Mathf.RoundToInt(steamGeneration * 1000)} mbar/s");
            GUILayout.Label($"{Mathf.RoundToInt(steamUsage * 1000)} mbar/s");
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }

        private const float alpha = 0.1f;

        public void UpdateSteamGeneration(float pressureRise)
        {
            steamGeneration = steamGeneration * alpha + pressureRise * (1f -alpha);
        }
        public void UpdateSteamUsage(float pressureDrop)
        {
            steamUsage = steamUsage * alpha + pressureDrop * (1f - alpha);
        }
    }
}