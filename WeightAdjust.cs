using HarmonyLib;
using UnityEngine;

namespace DvMod.SteamCutoff
{
    // https://www.steamlocomotive.com/locobase.php?country=USA&wheel=2-8-2&railroad=prr#32
    public static class WeightAdjust
    {
        [HarmonyPatch(typeof(CarTypes), nameof(CarTypes.GetCarPrefab))]
        public static class GetCarPrefabPatch
        {
            public static void Postfix(TrainCarType carType, GameObject __result)
            {
                switch (carType)
                {
                    case TrainCarType.Tender:
                        __result.GetComponent<TrainCar>().totalMass = 70000f;
                        break;
                    case TrainCarType.LocoSteamHeavy:
                        var car = __result.GetComponent<TrainCar>();
                        car.totalMass = 140000f;
                        car.bogieMassRatio = 108000f / car.totalMass / car.Bogies.Length;
                        break;
                }
            }
        }
    }
}