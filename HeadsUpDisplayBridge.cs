using DvMod.HeadsUpDisplay;
using UnityEngine;
using UnityModManagerNet;

namespace DvMod.SteamCutoff
{
    internal class HeadsUpDisplayBridge
    {
        public static HeadsUpDisplayBridge? instance;

        static HeadsUpDisplayBridge()
        {
            try
            {
                if (UnityModManager.FindMod("HeadsUpDisplay") == null)
                    return;
                instance = new HeadsUpDisplayBridge();
                instance.Register();
            }
            catch (System.IO.FileNotFoundException)
            {
            }
        }

        private void Register()
        {
            PushProvider steamGenerationProvider = new PushProvider(
                "Steam generation", () => true, v => $"{Mathf.RoundToInt(v * 1000)} mbar/s");
            PushProvider steamConsumptionProvider = new PushProvider(
                "Steam consumption", () => true, v => $"{Mathf.RoundToInt(v * 1000)} mbar/s");
            Registry.Register(TrainCarType.LocoSteamHeavy, steamGenerationProvider);
            Registry.Register(TrainCarType.LocoSteamHeavy, steamConsumptionProvider);
        }

        public void UpdateSteamGeneration(TrainCar car, float pressureRise)
        {
            if (Registry.GetProvider(car.carType, "Steam generation") is PushProvider pp)
                pp.MixSmoothedValue(car, pressureRise);
        }

        public void UpdateSteamUsage(TrainCar car, float pressureDrop)
        {
            if (Registry.GetProvider(car.carType, "Steam consumption") is PushProvider pp)
                pp.MixSmoothedValue(car, pressureDrop);
        }

        public void UpdateCutoffSetting(TrainCar car, float cutoff)
        {
            if (Registry.GetProvider(car.carType, "Cutoff") is PushProvider pp)
                pp.SetValue(car, cutoff);
        }
    }
}