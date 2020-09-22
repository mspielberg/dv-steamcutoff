using System;
using UnityEngine;
using UnityModManagerNet;

namespace DvMod.SteamCutoff
{
    using Formatter = Func<float, string>;
    using Pusher = Action<TrainCar, float>;

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
            typeof(object),
            typeof(string),
            typeof(Formatter),
            typeof(IComparable)
        };

        private static readonly Type[] GetPusherArgumentTypes = new Type[]
        {
            typeof(object),
            typeof(string)
        };

        private HeadsUpDisplayBridge(UnityModManager.ModEntry hudMod)
        {
            if (hudMod.Invoke(
                "DvMod.HeadsUpDisplay.Registry.RegisterPush",
                out var steamGenerationPusher,
                new object?[] { TrainCarType.LocoSteamHeavy, "Steam gen", (Formatter)(v => $"{Mathf.RoundToInt(v * 1000)} mbar/s"), null },
                RegisterPushArgumentTypes))
            {
                this.steamGenerationPusher = (Pusher)steamGenerationPusher;
            }

            if (hudMod.Invoke(
                "DvMod.HeadsUpDisplay.Registry.RegisterPush",
                out var steamConsumptionPusher,
                new object?[] { TrainCarType.LocoSteamHeavy, "Steam use", (Formatter)(v => $"{Mathf.RoundToInt(v * 1000)} mbar/s"), null },
                RegisterPushArgumentTypes))
            {
                this.steamConsumptionPusher = (Pusher)steamConsumptionPusher;
            }

            if (hudMod.Invoke(
                "DvMod.HeadsUpDisplay.Registry.GetPusher",
                out var cutoffSettingPusher,
                new object?[] { TrainCarType.LocoSteamHeavy , "Cutoff"},
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