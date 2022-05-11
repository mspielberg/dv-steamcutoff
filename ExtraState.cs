using System.Runtime.CompilerServices;

namespace DvMod.SteamCutoff
{
    public class ExtraState
    {
        private static readonly ConditionalWeakTable<SteamLocoSimulation, ExtraState> states = new ConditionalWeakTable<SteamLocoSimulation, ExtraState>();

        public static ExtraState Instance(SteamLocoSimulation sim)
        {
            if (!states.TryGetValue(sim, out var state))
            {
                state = new ExtraState();
                states.Add(sim, state);
            }
            return state;
        }

        public const int NumCylinders = 2;

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