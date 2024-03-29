namespace DvMod.SteamCutoff
{
    public static class Constants
    {
        public const float CoalboxCapacity = 400f;

        public const float HeatYieldTransitionTime = 10f;
        public const float TemperatureGaugeMaxPower = 20e3f;
        public const float TemperatureGaugeGamma = 0.4f;

        public const float FeedwaterTemp = 15f; // deg C

        public const float CutoffGamma = 1.0f;
        public const float MinCutoff = 0.06f;
        public const float MaxCutoff = 0.9f;

        public const float InjectorGamma = 4f;
    }
}