namespace DvMod.SteamCutoff
{
    public interface ISimAdapter
    {
        SimComponent FireOn { get; }
        // SimComponent FireDoorOpen { get; }
        // SimComponent Temperature { get; }
        SimComponent Coalbox { get; }
        SimComponent BoilerWater { get; }
        SimComponent BoilerPressure { get; }
        // SimComponent SteamReleaser { get; }
        SimComponent SafetyPressureValve { get; }
        // SimComponent Injector { get; }
        // SimComponent WaterDump { get; }
        SimComponent Regulator { get; }
        SimComponent Cutoff { get; }
        SimComponent Draft { get; }
        // SimComponent Blower { get; }
        SimComponent Power { get; }
        SimComponent Speed { get; }
        // SimComponent Sand { get; }
        // SimComponent SandFlow { get; }
        // SimComponent SandValve { get; }
        // SimComponent TenderWater { get; }
        // SimComponent TenderCoal { get; }

        float CoalConsumptionRate { get; set; }
        float TotalCoalConsumed { get; set; }
        float GetBlowerBonusNormalized();
        float TimeMult { get; }
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
        public ref float PressureLeakMultiplier => ref baseSim.pressureLeakMultiplier;
    }
}
