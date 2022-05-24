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

                Func<TrainCar, T?> FromSim<T>(Func<ISimAdapter, T> f)
                    where T : class
                {
                    return car =>
                        Option<SteamLocoSimulation>.Of(car.GetComponent<SteamLocoSimulation>())
                        .Map(baseSim => new BaseSimAdapter(baseSim))
                        .Map(f)
                        .ToNullable();
                }

                Func<TrainCar, float?> SFromSim(Func<ISimAdapter, float> f)
                {
                    return car =>
                        Option<SteamLocoSimulation>.Of(car.GetComponent<SteamLocoSimulation>())
                        .Map(baseSim => new BaseSimAdapter(baseSim))
                        .MapS(f)
                        .ToNullable();
                }

                RegisterFloatPull(
                    "Cutoff",
                    SFromSim(sim => CylinderSimulation.Cutoff(sim)),
                    v => $"{v:P0}");

                RegisterPull("Boiler water level", FromSim(sim => sim.BoilerWater.value * Liter));

                RegisterPush(out exhaustFlowPusher, "Exhaust flow");

                RegisterPush(out oxygenSupplyPusher, "Oxygen supply");

                RegisterFloatPull(
                    "Oxygen availability",
                    car => ExtraState.Instance(car)?.fireState?.oxygenAvailability,
                    v => $"{v:P0}");

                RegisterPull("Firebox", FromSim(sim => sim.Coalbox.value * Kilogram));

                RegisterPush(out stokerFeedRatePusher, "Stoker feed rate");

                RegisterPull("Coal use", FromSim(sim => sim.CoalConsumptionRate * KilogramsPerSecond));

                Func<TrainCar, T?> FromFireState<T>(Func<FireState, T> f)
                    where T : class
                {
                    return car => Option<ExtraState>.Of(ExtraState.Instance(car))
                        .Map(state => state.fireState)
                        .Map(f).ToNullable();
                }

                Func<TrainCar, float?> FromFireStateS(Func<FireState, float> f)
                {
                    return car => Option<ExtraState>.Of(ExtraState.Instance(car))
                        .Map(state => state.fireState)
                        .MapS(f).ToNullable();
                }

                RegisterFloatPull(
                    "Combustion efficiency",
                    FromFireStateS(fireState => fireState.CombustionEfficiency()),
                    v => $"{v:P0}");

                RegisterPull(
                    "Heat yield",
                    FromFireState(fireState => fireState.smoothedHeatYieldRate * Kilowatt));

                RegisterPush(out boilerSteamMassPusher, "Boiler steam mass");

                Func<TrainCar, T?> FromBoilerSim<T>(Func<BoilerSimulation, T> f)
                    where T : class
                    => car => Option<ExtraState>.Of(ExtraState.Instance(car))
                        .Map(state => state.boilerState)
                        .Map(f).ToNullable();

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
