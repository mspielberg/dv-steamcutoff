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

        // fire
        private readonly Pusher? exhaustFlowPusher;
        private readonly Pusher? oxygenSupplyPusher;

        // boiler
        private readonly Pusher? waterEvapPusher;
        private readonly Pusher? boilerSteamMassPusher;

        // cylinder
        private readonly Pusher? steamConsumptionPusher;

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
                "Cutoff",
                car => {
                    var sim = car.GetComponent<SteamLocoSimulation>();
                    if (sim != null)
                        return CylinderSimulation.Cutoff(sim);
                    return null;
                },
                v => $"{v:P0}");

            RegisterPull(
                "Boiler water level",
                car => car.GetComponent<SteamLocoSimulation>()?.boilerWater?.value,
                v => $"{v:F0} L");

            RegisterPush(
                out exhaustFlowPusher,
                "Exhaust flow",
                v => $"{v * 3600:F0} kg/h");

            RegisterPush(
                out oxygenSupplyPusher,
                "Oxygen supply",
                v => $"{v * 3600:F0} kg/h");

            RegisterPull(
                "Oxygen availability",
                car => FireState.Instance(car)?.oxygenAvailability,
                v => $"{v:P0}");

            RegisterPull(
                "Coalbox",
                car => car.GetComponent<SteamLocoSimulation>()?.coalbox?.value,
                v => $"{v:F1} kg");

            RegisterPull(
                "Coal use",
                car => car.GetComponent<SteamLocoSimulation>()?.coalConsumptionRate,
                v => $"{v * 3600:F0} kg/h");

            RegisterPull(
                "Heat yield",
                car => FireState.Instance(car)?.smoothedHeatYieldRate,
                v => $"{v:F0} kW");

            RegisterPush(
                out boilerSteamMassPusher,
                "Boiler steam mass",
                v => $"{v:F1} kg");

            RegisterPush(
                out waterEvapPusher,
                "Evaporation",
                v => $"{v * 3600:F0} kg/h");

            RegisterPush(
                out steamConsumptionPusher,
                "Cylinder steam use",
                 v => $"{v * 3600:F0} kg/h");
        }

        public void UpdateExhaustFlow(TrainCar car, float exhaustFlow) => exhaustFlowPusher?.Invoke(car, exhaustFlow);
        public void UpdateOxygenSupply(TrainCar car, float oxygenSupply) => oxygenSupplyPusher?.Invoke(car, oxygenSupply);

        public void UpdateWaterEvap(TrainCar car, float evapKgPerS) => waterEvapPusher?.Invoke(car, evapKgPerS);
        public void UpdateBoilerSteamMass(TrainCar car, float steamMass) => boilerSteamMassPusher?.Invoke(car, steamMass);

        public void UpdateSteamUsage(TrainCar car, float steamKgPerS) => steamConsumptionPusher?.Invoke(car, steamKgPerS);
    }
}