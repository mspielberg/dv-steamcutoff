using System;
using UnityEngine;
using UnityModManagerNet;
using Formatter = System.Func<float, string>;
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

        private readonly Pusher? steamGenerationPusher;
        private readonly Pusher? steamConsumptionPusher;
        private readonly Pusher? cutoffSettingPusher;

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
            void RegisterPush(out Pusher pusher, string label, Formatter formatter, IComparable? order = null)
            {
                hudMod.Invoke(
                    "DvMod.HeadsUpDisplay.Registry.RegisterPush",
                    out var temp,
                    new object?[] { label, formatter, order },
                    RegisterPushArgumentTypes);
                pusher = (Pusher)temp;
            }

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