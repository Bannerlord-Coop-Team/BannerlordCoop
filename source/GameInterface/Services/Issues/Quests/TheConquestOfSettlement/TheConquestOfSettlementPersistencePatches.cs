using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Quests.TheConquestOfSettlement;

[HarmonyPatch(typeof(TheConquestOfSettlementIssueBehavior))]
internal static class TheConquestOfSettlementPersistencePatches
{
    [HarmonyPatch(nameof(TheConquestOfSettlementIssueBehavior.RegisterEvents))]
    [HarmonyPostfix]
    private static void RegisterEventsPostfix()
    {
        TheConquestOfSettlementService.ResetQuestOwners();
    }

    [HarmonyPatch(nameof(TheConquestOfSettlementIssueBehavior.SyncData))]
    [HarmonyPostfix]
    private static void SyncDataPostfix(IDataStore dataStore)
    {
        TheConquestOfSettlementService.SyncQuestOwners(dataStore);
    }
}
