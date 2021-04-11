using System.Collections.Generic;
using UnitsNet;
using UVolume = UnitsNet.Volume;

namespace DvMod.SteamCutoff
{
    public static class BoilerSimulation
    {
        public class State
        {
            private static readonly Dictionary<SteamLocoSimulation, State> instances = new Dictionary<SteamLocoSimulation, State>();

            public static State Instance(SteamLocoSimulation sim)
            {
                if (!instances.TryGetValue(sim, out var state))
                    instances[sim] = state = new State(sim);
                return state;
            }

            // private static readonly Density TenderWaterDensity = SteamTables.WaterDensity(SteamTables.Atmospheric);
            // private static readonly MassFlow MaxInjectionRate = MassFlow.FromKilogramsPerSecond(3000);

            private readonly SteamLocoSimulation sim;

            private Pressure pressure;
            private Temperature temperature;
            private Mass waterMass;
            private UVolume WaterVolume() => waterMass / WaterDensity();
            private UVolume BoilerVolume() => UVolume.FromLiters(sim.boilerWater.max * 1.05f);
            private UVolume SteamVolume() => BoilerVolume() - WaterVolume();
            private Mass SteamMass() => SteamVolume() * SteamDensity();

            private Temperature BoilingPoint() => SteamTables.BoilingPoint(pressure);
            private Density SteamDensity() => SteamTables.SteamDensity(pressure);
            private Density WaterDensity() => SteamTables.WaterDensity(pressure);
            private SpecificEnergy HeatOfVaporization() => SteamTables.SpecificEnthalpyOfVaporization(pressure);
            private SpecificEntropy SteamSpecificHeat() => SteamTables.SteamSpecificHeat(pressure);
            private SpecificEntropy WaterSpecificHeat() => SteamTables.WaterSpecificHeat(pressure);

            private State(SteamLocoSimulation sim)
            {
                this.sim = sim;
                this.pressure = Pressure.FromBars(sim.boilerPressure.value);
                this.temperature = SteamTables.BoilingPoint(pressure);
                this.waterMass = UVolume.FromLiters(sim.boilerWater.value) * SteamTables.WaterDensity(pressure);
            }

            public void Update(Energy netHeat)
            {
                ImportFromSim();
                SimulateWaterHeating(netHeat);
                ExportToSim();
            }

            private void ImportFromSim()
            {
                pressure = Pressure.FromBars(sim.boilerPressure.value);
                waterMass = UVolume.FromLiters(sim.boilerWater.value) * SteamTables.WaterDensity(pressure);
            }

            public float PressureRatio() => (float)(pressure / Pressure.FromBars(Main.settings.safetyValveThreshold));

            // public MassFlow WaterInjectionRate() => sim.injector.value * PressureRatio() * MaxInjectionRate;

            // public void SimulateInjector(Duration deltaTime)
            // {
            //     var massToInject = UnitMath.Min(deltaTime * WaterInjectionRate(), Volume.FromLiters(sim.tenderWater.value) * TenderWaterDensity;
            //     var volumeToInject = massToInject / TenderWaterDensity;
            //     waterMass += massToInject;
            //     sim.tenderWater.AddNextValue(-volumeToInject.Liters);
            // }

            private static readonly Mass Threshold = Mass.FromKilograms(0.1);
            private static readonly MolarEntropy IdealGasConstant = MolarEntropy.FromJoulesPerMoleKelvin(8.31451);
            private static readonly MolarMass SteamMolarMass = MolarMass.FromGramsPerMole(18.015257);
            private static readonly SpecificEntropy SteamGasConstant = SpecificEntropy.FromKilojoulesPerKilogramKelvin(
                IdealGasConstant.KilojoulesPerMoleKelvin / SteamMolarMass.KilogramsPerMole);

            public void SimulateWaterHeating(Energy netHeat)
            {
                var excessTemp = temperature - BoilingPoint();
                var excessHeat = (excessTemp * WaterSpecificHeat() * waterMass) + netHeat;
                Mass maxPhaseChangeMass = (UnitExtras.EnergyWrapper)excessHeat / HeatOfVaporization();

                // perform binary search to determine the actual mass condensed/vaporized
                Mass lowerBound = maxPhaseChangeMass < Mass.Zero ? maxPhaseChangeMass : Mass.Zero;
                Mass upperBound = maxPhaseChangeMass > Mass.Zero ? maxPhaseChangeMass : Mass.Zero;
                var totalMass = waterMass + SteamMass();
                var originalTemp = temperature;
                Main.DebugLog($"initial: waterMass={waterMass},steamMass={SteamMass()},totalMass={totalMass},temp={originalTemp},excessTemp={excessTemp},excessHeat={excessHeat}");
                while (upperBound - lowerBound > Threshold)
                {
                    var guessEvaporationMass = (upperBound + lowerBound) / 2;
                    var newWaterMass = waterMass - guessEvaporationMass;
                    var vaporizationEnergy = guessEvaporationMass * HeatOfVaporization();
                    var heatingEnergy = excessHeat - vaporizationEnergy;
                    var overallSpecificHeat = (waterMass / totalMass * WaterSpecificHeat()) + (SteamMass() / totalMass * SteamSpecificHeat());
                    var tempRise = TemperatureDelta.FromDegreesCelsius(
                        heatingEnergy.Kilojoules / totalMass.Kilograms / overallSpecificHeat.KilojoulesPerKilogramDegreeCelsius);
                    temperature = originalTemp + tempRise;
                    var newWaterVolume = newWaterMass / SteamTables.WaterDensity(temperature);

                    var newSteamMass = SteamMass() + guessEvaporationMass;
                    var newSteamVolume = BoilerVolume() - newWaterVolume;
                    pressure = Pressure.FromPascals(
                        SteamGasConstant.KilojoulesPerKilogramKelvin * temperature.Kelvins
                        * newSteamMass.Kilograms / newSteamVolume.CubicMeters);

                    var saturationTemp = SteamTables.BoilingPoint(pressure);
                    Main.DebugLog($"l={lowerBound},u={upperBound},guess={guessEvaporationMass},newTemp={temperature},guessPressure={pressure},idealGasTemp={saturationTemp}");
                    if (temperature < saturationTemp)
                        upperBound = guessEvaporationMass;
                    else
                        lowerBound = guessEvaporationMass;
                }

                var actualEvaporationMass = (upperBound + lowerBound) / 2;
                waterMass -= actualEvaporationMass;
                Main.DebugLog($"final: waterMass={waterMass},steamMass={SteamMass()},totalMass={waterMass+SteamMass()},temp={temperature},pressure={pressure}");
            }

            private void ExportToSim()
            {
                sim.boilerWater.SetNextValue((float)(waterMass / SteamTables.WaterDensity(pressure)).Liters);
                sim.boilerPressure.SetNextValue((float)pressure.Bars);
            }
        }
    }
}