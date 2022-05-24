using System.Runtime.CompilerServices;

namespace DvMod.SteamCutoff
{
    public class ExtraState
    {
        private static readonly ConditionalWeakTable<TrainCar, ExtraState> states = new ConditionalWeakTable<TrainCar, ExtraState>();

        public static ExtraState? Instance(TrainCar car)
        {
            if (!states.TryGetValue(car, out var state))
            {
                var baseSim = car.GetComponent<SteamLocoSimulation>();
                if (baseSim == null)
                    return null;
                state = new ExtraState(new BaseSimAdapter(baseSim), car);
                states.Add(car, state);
            }
            return state;
        }

        private ExtraState(ISimAdapter sim, TrainCar car)
        {
            this.boilerState = new BoilerSimulation(sim, car);
            this.controlState = new ControlState();
            this.fireState = new FireState(sim);
        }

        public const int NumCylinders = 2;

        public readonly BoilerSimulation boilerState;
        public readonly ControlState controlState;
        public readonly FireState fireState;
        public float powerVel;
        // <summary>Whether a chamber (front/back of cylinder) has been pressurized.</summary>
        public bool[] cylinderFrontHasPressure = new bool[NumCylinders];
        public bool[] cylinderRearHasPressure = new bool[NumCylinders];
        public ref bool IsCylinderPressurized(int cylinder, bool isFront)
        {
            return ref (isFront ? cylinderFrontHasPressure : cylinderRearHasPressure)[cylinder];
        }
    }
}
