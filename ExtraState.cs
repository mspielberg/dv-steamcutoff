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

        public float powerVel;
    }
}