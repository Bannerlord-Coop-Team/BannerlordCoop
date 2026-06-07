using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using TaleWorlds.CampaignSystem.MapEvents;
using GameInterface.Policies;

namespace GameInterface.Services.MapEventComponents.Patches;


/// <summary>
/// This is required as there is some strange optimization happening in the constructor where it is not calling the MapEvent setter
/// </summary>
[HarmonyPatch(typeof(MapEventComponent))]
internal class MapEventComponentPatches
{
    static MethodBase TargetMethod() => AccessTools.Constructor(typeof(MapEventComponent), new Type[] { typeof(MapEvent) });

    [HarmonyPrefix]
    private static bool ConstructorPrefix(MapEventComponent __instance, MapEvent mapEvent)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        __instance.MapEvent = mapEvent;
        return false;
    }
}
