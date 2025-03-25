using GameInterface.Services.MapEvents.Messages;
using GameInterface.Utils;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.MapEvents;

namespace GameInterface.Services.MapEvents.Patches;

[HarmonyPatch]
internal class MapEventCollectionPatches : GenericPatches<MapEventCollectionPatches, MapEvent>
{
    static IEnumerable<MethodBase> TargetMethods() => AccessTools.GetDeclaredMethods(typeof(MapEvent));

    [HarmonyTranspiler]
    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        => ArrayFieldChangeTranspiler<MapEventSide, MapEventSidesArrayUpdated>(instructions, nameof(MapEvent._sides));
}
