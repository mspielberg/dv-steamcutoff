using QuantitiesNet;
using static QuantitiesNet.Dimensions;
using static QuantitiesNet.Units;
using System;
using UnityModManagerNet;

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
                if (hudMod == null)
                    return;
                if (!hudMod.Loaded)
                    return;
                if (hudMod.Version.Major < 1)
                    return;
                instance = new HeadsUpDisplayBridge();
            }
            catch (System.IO.FileNotFoundException)
            {
            }
        }

        // fire
        private readonly Action<TrainCar, Quantity<MassFlow>> exhaustFlowPusher;
        private readonly Action<TrainCar, Quantity<MassFlow>> oxygenSupplyPusher;
        private readonly Action<TrainCar, Quantity<MassFlow>> stokerFeedRatePusher;

        // boiler
        private readonly Action<TrainCar, Quantity<Mass>> boilerSteamMassPusher;

        // cylinder
        private readonly Action<TrainCar, Quantity<MassFlow>> steamConsumptionPusher;

        private HeadsUpDisplayBridge()
        {
            void RegisterFloatPull(
                string label,
                Func<TrainCar, float?> provider,
                Func<float, string> formatter,
                IComparable? order = null,
                bool hidden = false)
            {
                DvMod.HeadsUpDisplay.Registry.RegisterPull(label, provider, formatter, order ?? label, hidden);
            }
            
            void RegisterPull<D>(
                string label,
                Func<TrainCar, Quantity<D>?> provider,
                IComparable? order = null,
                bool hidden = false)
            where D : IDimension, new()
            {
                DvMod.HeadsUpDisplay.Registry.RegisterPull(label, provider, order ?? label, hidden);
            }

            void RegisterPush<D>(out Action<TrainCar, Quantity<D>> pusher, string label, IComparable? order = null, bool hidden = false)
            where D : IDimension, new()
            {
                pusher = DvMod.HeadsUpDisplay.Registry.RegisterPush<D>(label, order ?? label, hidden);
            }

            RegisterFloatPull(
                "Cutoff",
                car => {
                    var sim = car.GetComponent<SteamLocoSimulation>();
                    if (sim != null)
                        return CylinderSimulation.Cutoff(sim);
                    return null;
                },
                v => $"{v:P0}");

            RegisterPull<QuantitiesNet.Dimensions.Volume>(
                "Boiler water level",
                car =>
                {
                    var sim = car.GetComponent<SteamLocoSimulation>();
                    if (sim == null)
                        return null;
                    return new Quantities.Volume(sim.boilerWater.value, Liter);
                });

            RegisterPush(
                out exhaustFlowPusher,
                "Exhaust flow");

            RegisterPush(out oxygenSupplyPusher, "Oxygen supply");

            RegisterFloatPull(
                "Oxygen availability",
                car => FireState.Instance(car)?.oxygenAvailability,
                v => $"{v:P0}");

            RegisterPull(
                "Firebox",
                car =>
                {
                    var sim = car.GetComponent<SteamLocoSimulation>();
                    if (sim == null)
                        return null;
                    return new Quantities.Mass(sim.coalbox.value, Kilogram);
                });

            RegisterPush(out stokerFeedRatePusher, "Stoker feed rate");

            RegisterPull(
                "Coal use",
                car =>
                {
                    var sim = car.GetComponent<SteamLocoSimulation>();
                    if (sim == null)
                        return null;
                    return new Quantities.MassFlow(sim.coalConsumptionRate, Kilogram / Second);
                });

            RegisterPull(
                "Heat yield",
                car =>
                {
                    var state = FireState.Instance(car);
                    if (state == null)
                        return null;
                    return new Quantities.Power(state.smoothedHeatYieldRate, Kilowatt);
                });

            RegisterPush(out boilerSteamMassPusher, "Boiler steam mass");

            RegisterPull(
                "Water temperature",
                car =>
                {
                    var sim = BoilerSimulation.Instance(car);
                    if (sim == null)
                        return null;
                    return new Quantities.Temperature(sim.WaterTemp, Celsius);
                });

            RegisterPull(
                "Evaporation",
                car =>
                {
                    var sim = BoilerSimulation.Instance(car);
                    if (sim == null)
                        return null;
                    return new Quantities.MassFlow(sim.SmoothedEvapRate);
                });

            RegisterPush(out steamConsumptionPusher, "Cylinder steam use");
        }

        public void UpdateExhaustFlow(TrainCar car, float exhaustFlow) =>
            exhaustFlowPusher(car, new Quantities.MassFlow(exhaustFlow, Kilogram / Second));
        public void UpdateOxygenSupply(TrainCar car, float oxygenSupply) =>
            oxygenSupplyPusher(car, new Quantities.MassFlow(oxygenSupply, Kilogram / Second));
        public void UpdateStokerFeedRate(TrainCar car, float stokerFeedRate) =>
            stokerFeedRatePusher(car, new Quantities.MassFlow(stokerFeedRate, Kilogram / Second));
        public void UpdateBoilerSteamMass(TrainCar car, float steamMass) =>
            boilerSteamMassPusher(car, new Quantities.Mass(steamMass, Kilogram));
        public void UpdateSteamUsage(TrainCar car, float steamKgPerS) =>
            steamConsumptionPusher(car, new Quantities.MassFlow(steamKgPerS, Kilogram / Second));
    }
}
