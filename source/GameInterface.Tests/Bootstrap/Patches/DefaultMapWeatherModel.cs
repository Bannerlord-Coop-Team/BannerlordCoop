using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.GameComponents;
using static TaleWorlds.CampaignSystem.ComponentInterfaces.MapWeatherModel;

namespace GameInterface.Tests.Bootstrap.Patches;

[HarmonyPatch(typeof(DefaultMapWeatherModel))]
internal class DefaultMapWeatherModelPatches
{

    [HarmonyPatch(nameof(DefaultMapWeatherModel.GetWeatherEventInPosition))]
    [HarmonyPrefix]
    static bool GetWeatherEventInPositionPrefix(ref WeatherEvent __result)
    {
        Array values = Enum.GetValues(typeof(WeatherEvent));
        Random random = new Random();
        __result = (WeatherEvent)values.GetValue(random.Next(values.Length))!;

        return false;
    }
}