using QuantitiesNet;
using static QuantitiesNet.Dimensions;
using static QuantitiesNet.Units;
using System;
using UnityModManagerNet;

namespace DvMod.SteamCutoff
{
    internal sealed class HeadsUpDisplayBridge
    {
        public static IHeadsUpDisplayBridge? instance;

        static HeadsUpDisplayBridge()
        {
            try
            {
                var hudMod = UnityModManager.FindMod("HeadsUpDisplay");
                if (hudMod == null)
                    return;
                if (!hudMod.Active)
                    return;
                if (hudMod.Version.Major < 1)
                    return;
                instance = Activator.CreateInstance<Impl>();
            }
            catch (System.IO.FileNotFoundException)
            {
            }
        }

        public interface IHeadsUpDisplayBridge
        {
            public void UpdateExhaustFlow(TrainCar car, float exhaustFlow);
            public void UpdateOxygenSupply(TrainCar car, float oxygenSupply);
            public void UpdateStokerFeedRate(TrainCar car, float stokerFeedRate);
            public void UpdateBoilerSteamMass(TrainCar car, float steamMass);
            public void UpdateSteamUsage(TrainCar car, float steamKgPerS);
        }

        private class Impl : HeadsUpDisplayBridge.IHeadsUpDisplayBridge
        {
            // fire
            private readonly Action<TrainCar, Quantity<MassFlow>> exhaustFlowPusher;
            private readonly Action<TrainCar, Quantity<MassFlow>> oxygenSupplyPusher;
            private readonly Action<TrainCar, Quantity<MassFlow>> stokerFeedRatePusher;

            // boiler
            private readonly Action<TrainCar, Quantity<Mass>> boilerSteamMassPusher;

            // cylinder
            private readonly Action<TrainCar, Quantity<MassFlow>> steamConsumptionPusher;

            private static readonly Unit<MassFlow> KilogramsPerSecond = (Kilogram / Second).Assert<MassFlow>();

            public Impl()
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

                void RegisterPush<D>(
                    out Action<TrainCar,
                    Quantity<D>> pusher,
                    string label,
                    IComparable? order = null,
                    bool hidden = false)
                where D : IDimension, new()
                {
                    pusher = DvMod.HeadsUpDisplay.Registry.RegisterPush<D>(label, order ?? label, hidden);
                }

                Func<TrainCar, T?> FromComponent<T, C>(Func<TrainCar, C?> extractor, Func<C, T> f)
                    where T : class
                    where C : class
                {
                    return car =>
                    {
                        var c = extractor(car);
                        if (c != null)
                            return f(c);
                        return default;
                    };
                }

                RegisterFloatPull(
                    "Cutoff",
                    car =>
                    {
                        var sim = car.GetComponent<SteamLocoSimulation>();
                        if (sim != null)
                            return CylinderSimulation.Cutoff(sim);
                        return null;
                    },
                    v => $"{v:P0}");

                Func<TrainCar, T?> FromSim<T>(Func<SteamLocoSimulation, T> f)
                    where T : class
                    => FromComponent(car => car.GetComponent<SteamLocoSimulation>(), f);

                RegisterPull("Boiler water level", FromSim(sim => sim.boilerWater.value * Liter));

                RegisterPush(out exhaustFlowPusher, "Exhaust flow");

                RegisterPush(out oxygenSupplyPusher, "Oxygen supply");

                RegisterFloatPull(
                    "Oxygen availability",
                    car => FireState.Instance(car)?.oxygenAvailability,
                    v => $"{v:P0}");

                RegisterPull("Firebox", FromSim(sim => sim.coalbox.value * Kilogram));

                RegisterPush(out stokerFeedRatePusher, "Stoker feed rate");

                RegisterPull("Coal use", FromSim(sim => sim.coalConsumptionRate * KilogramsPerSecond));

                RegisterFloatPull(
                    "Combustion efficiency",
                    car => FireState.Instance(car)?.CombustionEfficiency(),
                    v => $"{v:P0}");

                RegisterPull(
                    "Heat yield",
                    FromComponent(FireState.Instance, state => state.smoothedHeatYieldRate * Kilowatt));

                RegisterPush(out boilerSteamMassPusher, "Boiler steam mass");

                Func<TrainCar, T?> FromBoilerSim<T>(Func<BoilerSimulation, T> f)
                    where T : class
                    => FromComponent(BoilerSimulation.Instance, f);

                RegisterPull(
                    "Water temperature",
                    FromBoilerSim(sim => sim.WaterTemp * Celsius));

                RegisterPull(
                    "Evaporation",
                    FromBoilerSim(sim => sim.SmoothedEvapRate * KilogramsPerSecond));

                RegisterPush(out steamConsumptionPusher, "Cylinder steam use");
            }

            public void UpdateExhaustFlow(TrainCar car, float exhaustFlow) =>
                exhaustFlowPusher(car, exhaustFlow * KilogramsPerSecond);
            public void UpdateOxygenSupply(TrainCar car, float oxygenSupply) =>
                oxygenSupplyPusher(car, oxygenSupply * KilogramsPerSecond);
            public void UpdateStokerFeedRate(TrainCar car, float stokerFeedRate) =>
                stokerFeedRatePusher(car, stokerFeedRate * KilogramsPerSecond);
            public void UpdateBoilerSteamMass(TrainCar car, float steamMass) =>
                boilerSteamMassPusher(car, steamMass * Kilogram);
            public void UpdateSteamUsage(TrainCar car, float steamKgPerS) =>
                steamConsumptionPusher(car, steamKgPerS * KilogramsPerSecond);
        }
    }
}
