using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.Settlements;
using GameInterface.Utils;
using GameInterface.Services.Towns.Messages.Collections;
using TaleWorlds.CampaignSystem.Settlements.Buildings;
using Helpers;
using TaleWorlds.Library;
using GameInterface.AutoSync.Fields;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Settlements.Workshops;

namespace GameInterface.Services.Heroes.Patches;

[HarmonyPatch]
internal class TownCollectionPatches : GenericPatches<TownCollectionPatches, Town>
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (var method in AccessTools.GetDeclaredMethods(typeof(Town)))
        {
            yield return method;
        }
        yield return AccessTools.Method(typeof(BuildingHelper), nameof(BuildingHelper.ChangeCurrentBuildingQueue));
        yield return AccessTools.Method(typeof(BuildingHelper), nameof(BuildingHelper.ChangeCurrentBuilding));
        yield return AccessTools.Method(typeof(WorkshopsCampaignBehavior), nameof(WorkshopsCampaignBehavior.BuildArtisanWorkshop));
        yield return AccessTools.Method(typeof(WorkshopsCampaignBehavior), nameof(WorkshopsCampaignBehavior.BuildWorkshopForHeroAtGameStart));
        yield return AccessTools.Method(typeof(WorkshopsCampaignBehavior), nameof(WorkshopsCampaignBehavior.BuildWorkshopsAtGameStart));
        // Breaks on patching
        // yield return AccessTools.Method(typeof(BuildingsCampaignBehavior), nameof(BuildingsCampaignBehavior.BuildDevelopmentsAtGameStart));
        // yield return AccessTools.Method(typeof(BuildingsCampaignBehavior), nameof(BuildingsCampaignBehavior.DecideProject));
    }

    // Only set in ctor
    [HarmonyTranspiler]
    static IEnumerable<CodeInstruction> BuildingsTranspiler(IEnumerable<CodeInstruction> instructions) 
        => MBListFieldTranspiler<Building, BuildingsListAdded, BuildingsListRemoved>(instructions, nameof(Town.Buildings));

    [HarmonyTranspiler]
    static IEnumerable<CodeInstruction> BuildingsInProgressCollectionTranspiler(IEnumerable<CodeInstruction> instructions)
        => QueueFieldTranspiler<Building, BuildingsInProgressQueueEnqueue, BuildingsInProgressQueueDequeue>(instructions, nameof(Town.BuildingsInProgress));

    // Reference getting replaced by BuildingHelper
    [HarmonyTranspiler]
    static IEnumerable<CodeInstruction> BuildingsInProgressFieldTranspiler(IEnumerable<CodeInstruction> instructions)
        => FieldTranspiler<Queue<Building>, BuildingsInProgressSet>(instructions, nameof(Town.BuildingsInProgress));

    static IEnumerable<CodeInstruction> TradeBoundVillagesCacheTranspiler(IEnumerable<CodeInstruction> instructions)
        => MBListFieldTranspiler<Village, TradeBoundVillagesCacheListAdded, TradeBoundVillagesCacheListRemoved>(instructions, nameof(Town._tradeBoundVillagesCache));

    [HarmonyTranspiler]
    static IEnumerable<CodeInstruction> WorkshopsArrayTranspiler(IEnumerable<CodeInstruction> instructions)
        => ArrayTranspiler<Workshop, WorkshopsChanged>(instructions, nameof(Town.Workshops));

    [HarmonyTranspiler]
    static IEnumerable<CodeInstruction> WorkshopsTranspiler(IEnumerable<CodeInstruction> instructions)
        => PropertyTranspiler<Workshop[], WorkshopsSet>(instructions, nameof(Town.Workshops));
}