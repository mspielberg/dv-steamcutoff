using System;
using UnityModManagerNet;
using Formatter = System.Func<float, string>;
using Provider = System.Func<TrainCar, float?>;
using Pusher = System.Action<TrainCar, float>;

namespace DvMod.SteamCutoff
{
    internal sealed class HeadsUpDisplayBridge
    {
        public static HeadsUpDisplayBridge? instance;

        static HeadsUpDisplayBridge()
        {
            try
            {
                var hudMod = UnityModManager.FindMod("HeadsUpDisplay");
                if (hudMod?.Loaded != true)
                    return;
                instance = new HeadsUpDisplayBridge(hudMod);
            }
            catch (System.IO.FileNotFoundException)
            {
            }
        }

        private readonly Pusher? waterEvapPusher;
        private readonly Pusher? steamGenerationPusher;
        private readonly Pusher? steamConsumptionPusher;
        private readonly Pusher? cutoffSettingPusher;

        private static readonly Type[] RegisterPullArgumentTypes = new Type[]
        {
            typeof(string),
            typeof(Provider),
            typeof(Formatter),
            typeof(IComparable)
        };

        private static readonly Type[] RegisterPushArgumentTypes = new Type[]
        {
            typeof(string),
            typeof(Formatter),
            typeof(IComparable)
        };

        private static readonly Type[] GetPusherArgumentTypes = new Type[]
        {
            typeof(string)
        };

        private HeadsUpDisplayBridge(UnityModManager.ModEntry hudMod)
        {
            void RegisterPull(string label, Provider provider, Formatter formatter, IComparable? order = null)
            {
                hudMod.Invoke(
                    "DvMod.HeadsUpDisplay.Registry.RegisterPull",
                    out var _,
                    new object?[] { label, provider, formatter, order },
                    RegisterPullArgumentTypes);
            }

            void RegisterPush(out Pusher pusher, string label, Formatter formatter, IComparable? order = null)
            {
                hudMod.Invoke(
                    "DvMod.HeadsUpDisplay.Registry.RegisterPush",
                    out var temp,
                    new object?[] { label, formatter, order },
                    RegisterPushArgumentTypes);
                pusher = (Pusher)temp;
            }

            RegisterPull(
                "Coalbox",
                car => car.GetComponent<SteamLocoSimulation>()?.coalbox?.value,
                v => $"{v:F1} kg");

            RegisterPull(
                "Coal use",
                car => car.GetComponent<SteamLocoSimulation>()?.coalConsumptionRate,
                v => $"{v:F1} kg/s");

            RegisterPush(
                out waterEvapPusher,
                "Evaporation",
                v => $"{v:F1} kg/s");

            RegisterPush(
                out steamGenerationPusher,
                "Steam gen",
                v => $"{v * 1000:F0} mbar/s");

            RegisterPush(
                out steamConsumptionPusher,
                "Steam use",
                 v => $"{v * 1000:F0} mbar/s");

            if (hudMod.Invoke(
                "DvMod.HeadsUpDisplay.Registry.GetPusher",
                out var cutoffSettingPusher,
                new object?[] { "Cutoff" },
                GetPusherArgumentTypes))
            {
                this.cutoffSettingPusher = (Pusher)cutoffSettingPusher;
            }
        }

        public void UpdateWaterEvap(TrainCar car, float evapKg)
        {
            waterEvapPusher?.Invoke(car, evapKg);
        }

        public void UpdateSteamGeneration(TrainCar car, float pressureRise)
        {
            steamGenerationPusher?.Invoke(car, pressureRise);
        }

        public void UpdateSteamUsage(TrainCar car, float pressureDrop)
        {
            steamConsumptionPusher?.Invoke(car, pressureDrop);
        }

        public void UpdateCutoffSetting(TrainCar car, float cutoff)
        {
            cutoffSettingPusher?.Invoke(car, cutoff);
        }
    }
}