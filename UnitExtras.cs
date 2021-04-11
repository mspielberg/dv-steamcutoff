using UnitsNet;

namespace DvMod.UnitExtras
{
    public sealed class EnergyWrapper
    {
        private readonly Energy energy;
        private EnergyWrapper(Energy energy) { this.energy = energy; }
        public static implicit operator EnergyWrapper(Energy energy) => new EnergyWrapper(energy);

        public static Mass operator /(EnergyWrapper wrapper, SpecificEnergy specificEnergy) =>
            Mass.FromKilograms(wrapper.energy.Joules / specificEnergy.JoulesPerKilogram);
    }
}