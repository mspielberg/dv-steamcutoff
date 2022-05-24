using DVCustomCarLoader.LocoComponents.Steam;
using UnityEngine;

namespace DvMod.SteamCutoff
{
    public interface IChuffAdapter
    {
        float DriverCircumference { get; }
        float CurrentRevolution { get; }
        float RotationSpeed { get; }
        float ChuffPower { set; }
    }

    public static class ChuffAdapter
    {
    }

    public class BaseChuffAdapter : IChuffAdapter
    {
        private readonly ChuffController chuff;
        public BaseChuffAdapter(ChuffController chuff)
        {
            this.chuff = chuff;
        }

        public float DriverCircumference => chuff.wheelCircumference;
        public float CurrentRevolution => chuff.dbgCurrentRevolution;
        public float RotationSpeed => chuff.drivingWheel.rotationSpeed;
        public float ChuffPower { set => chuff.chuffPower = value; }
    }

    public class CustomChuffAdapter : IChuffAdapter
    {
        private readonly CustomChuffController chuff;
        public CustomChuffAdapter(CustomChuffController chuff)
        {
            this.chuff = chuff;
        }

        public float DriverCircumference => chuff.wheelCircumference;
        public float CurrentRevolution => chuff.revolutionPos / chuff.wheelCircumference;
        public float RotationSpeed => chuff.driverAnimation.defaultRotationSpeed / 2f / Mathf.PI;
        public float ChuffPower { set => chuff.chuffPower = value; }
    }
}
