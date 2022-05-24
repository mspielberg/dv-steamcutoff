using DV.CabControls;
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DvMod.SteamCutoff
{
    public static class Stoker
    {
        private const float MaxFiringRate = 2.0f; // in kg/s, ~= 225 lb/h/ft^2 grate area on 70 ft^2 grate (PRR L1s)
        public static void Simulate(ISimAdapter sim, TrainCar loco, ExtraState extraState, float delta)
        {
            var state = extraState.controlState;
            var rate = sim.BoilerPressure.value / Main.settings.safetyValveThreshold * state.stokerSetting; // ratio 0-1
            var firingRate = MaxFiringRate * Mathf.Pow(rate, 2); // in kg/s
            HeadsUpDisplayBridge.instance?.UpdateStokerFeedRate(loco, firingRate);
            sim.TenderCoal.PassValueTo(sim.Coalbox, firingRate * delta);
            if (sim.FireOn.value == 0f && sim.Temperature.value > 400f)
            {
                sim.FireOn.SetValue(1f);
            }
        }
    }
}
