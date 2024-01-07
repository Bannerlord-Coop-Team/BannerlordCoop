using HarmonyLib;
using SandBox.View.Map;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.MobileParties.Patches;
/// <summary>
/// Disables following method, might not be needed
/// </summary>
[HarmonyPatch(typeof(DefaultTroopSupplierProbabilityModel), nameof(DefaultTroopSupplierProbabilityModel.EnqueueTroopSpawnProbabilitiesAccordingToUnitSpawnPrioritization))]
class ComputeIsWaitingPatch3
{
    [HarmonyPrefix]
    static bool Prefix(MapEventParty battleParty, FlattenedTroopRoster priorityTroops, bool includePlayer, int sizeOfSide, bool forcePriorityTroops, List<(FlattenedTroopRosterElement, MapEventParty, float)> priorityList)
    {
        return false;
    }
}