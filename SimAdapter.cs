using System.Collections.Generic;

using DVCustomCarLoader.LocoComponents.Steam;
using UnityModManagerNet;

namespace DvMod.SteamCutoff
{
    public interface ISimAdapter
    {
        SimComponent FireOn { get; }
        // SimComponent FireDoorOpen { get; }
        SimComponent Temperature { get; }
        SimComponent Coalbox { get; }
        SimComponent BoilerWater { get; }
        SimComponent BoilerPressure { get; }
        SimComponent SteamReleaser { get; }
        SimComponent SafetyPressureValve { get; }
        SimComponent Injector { get; }
        SimComponent WaterDump { get; }
        SimComponent Regulator { get; }
        SimComponent Cutoff { get; }
        SimComponent Draft { get; }
        // SimComponent Blower { get; }
        SimComponent Power { get; }
        SimComponent Speed { get; }
        // SimComponent Sand { get; }
        // SimComponent SandFlow { get; }
        // SimComponent SandValve { get; }
        SimComponent TenderWater { get; }
        SimComponent TenderCoal { get; }

        float CoalConsumptionRate { get; set; }
        float TotalCoalConsumed { get; set; }
        float GetBlowerBonusNormalized();
        float TimeMult { get; }
        float PressureLeakMultiplier { get; set; }
    }

    public static class SimAdapter
    {
        private static bool cclLoaded = UnityModManager.FindMod("DVCustomCarLoader")?.Loaded ?? false;
        public static ISimAdapter? From(TrainCar car)
        {
            if (cclLoaded)
                return Inner.From(car);
            if (car.GetComponent<SteamLocoSimulation>() is SteamLocoSimulation baseSim)
                return new BaseSimAdapter(baseSim);
            return null;
        }

        private static class Inner
        {
            public static ISimAdapter? From(TrainCar car)
            {
                if (car.GetComponent<SteamLocoSimulation>() is SteamLocoSimulation baseSim)
                    return new BaseSimAdapter(baseSim);
                else if (car.GetComponent<CustomLocoSimSteam>() is CustomLocoSimSteam customSim)
                    return new CustomSimAdapter(customSim);
                return null;
            }
        }
    }

    public class BaseSimAdapter : ISimAdapter
    {
        private readonly SteamLocoSimulation baseSim;

        public BaseSimAdapter(SteamLocoSimulation baseSim)
        {
            this.baseSim = baseSim;
        }

        public SimComponent FireOn => baseSim.fireOn;
        public SimComponent FireDoorOpen => baseSim.fireDoorOpen;
        public SimComponent Temperature => baseSim.temperature;
        public SimComponent Coalbox => baseSim.coalbox;
        public SimComponent BoilerWater => baseSim.boilerWater;
        public SimComponent BoilerPressure => baseSim.boilerPressure;
        public SimComponent SteamReleaser => baseSim.steamReleaser;
        public SimComponent SafetyPressureValve => baseSim.safetyPressureValve;
        public SimComponent Injector => baseSim.injector;
        public SimComponent WaterDump => baseSim.waterDump;
        public SimComponent Regulator => baseSim.regulator;
        public SimComponent Cutoff => baseSim.cutoff;
        public SimComponent Draft => baseSim.draft;
        public SimComponent Blower => baseSim.blower;
        public SimComponent Power => baseSim.power;
        public SimComponent Speed => baseSim.speed;
        public SimComponent Sand => baseSim.sand;
        public SimComponent SandFlow => baseSim.sandFlow;
        public SimComponent SandValve => baseSim.sandValve;
        public SimComponent TenderWater => baseSim.tenderWater;
        public SimComponent TenderCoal => baseSim.tenderCoal;

        public float CoalConsumptionRate
        {
            get => baseSim.coalConsumptionRate;
            set => baseSim.coalConsumptionRate = value;
        }

        public float TotalCoalConsumed
        {
            get => baseSim.TotalCoalConsumed;
            set => baseSim.TotalCoalConsumed = value;
        }

        public float GetBlowerBonusNormalized() => baseSim.GetBlowerBonusNormalized();
        public float TimeMult => baseSim.timeMult;

        public float PressureLeakMultiplier
        {
            get => baseSim.pressureLeakMultiplier;
            set => baseSim.pressureLeakMultiplier = value;
        }
    }

    public class CustomSimAdapter : ISimAdapter
    {
        private readonly CustomLocoSimSteam customSim;

        public CustomSimAdapter(CustomLocoSimSteam customSim)
        {
            this.customSim = customSim;
        }

        public SimComponent FireOn => customSim.fireOn;
        public SimComponent FireDoorOpen => customSim.fireDoorOpen;
        public SimComponent Temperature => customSim.temperature;
        public SimComponent Coalbox => customSim.fireboxFuel;
        public SimComponent BoilerWater => customSim.boilerWater;
        public SimComponent BoilerPressure => customSim.boilerPressure;
        public SimComponent SteamReleaser => customSim.steamReleaser;
        public SimComponent SafetyPressureValve => customSim.safetyPressureValve;
        public SimComponent Injector => customSim.injector;
        public SimComponent WaterDump => customSim.waterDump;
        public SimComponent Regulator => customSim.regulator;
        public SimComponent Cutoff => customSim.cutoff;
        public SimComponent Draft => customSim.damper;
        public SimComponent Blower => customSim.blower;
        public SimComponent Power => customSim.power;
        public SimComponent Speed => customSim.speed;
        public SimComponent Sand => customSim.sand;
        public SimComponent SandFlow => customSim.sandFlow;
        public SimComponent SandValve => customSim.sandValve;
        public SimComponent TenderWater => customSim.tenderWater;
        public SimComponent TenderCoal => customSim.tenderFuel;

        public float CoalConsumptionRate
        {
            get => customSim.fuelConsumptionRate;
            set => customSim.fuelConsumptionRate = value;
        }

        public float TotalCoalConsumed
        {
            get => customSim.TotalFuelConsumed;
            set => customSim.TotalFuelConsumed = value;
        }

        public float GetBlowerBonusNormalized() => customSim.GetBlowerFlowPercent();
        public float TimeMult => customSim.timeMult;

        public float PressureLeakMultiplier
        {
            get => customSim.pressureLeakMultiplier;
            set => customSim.pressureLeakMultiplier = value;
        }
    }
}
