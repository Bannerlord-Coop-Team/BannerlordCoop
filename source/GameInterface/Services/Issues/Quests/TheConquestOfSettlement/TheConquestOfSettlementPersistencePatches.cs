using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Quests.TheConquestOfSettlement;

[HarmonyPatch]
internal static class TheConquestOfSettlementPersistencePatches
{
    [HarmonyPatch(typeof(Campaign), "OnInitialize")]
    [HarmonyPrefix]
    internal static void CampaignOnInitializePrefix()
    {
        TheConquestOfSettlementService.ResetQuestOwners();
    }

    [HarmonyPatch(typeof(TheConquestOfSettlementIssueBehavior), nameof(TheConquestOfSettlementIssueBehavior.RegisterEvents))]
    [HarmonyPostfix]
    internal static void RegisterEventsPostfix()
    {
        TheConquestOfSettlementService.ResetQuestOwnersIfNotRestored();
    }

    [HarmonyPatch(typeof(TheConquestOfSettlementIssueBehavior), nameof(TheConquestOfSettlementIssueBehavior.SyncData))]
    [HarmonyPostfix]
    internal static void SyncDataPostfix(IDataStore dataStore)
    {
        TheConquestOfSettlementService.SyncQuestOwners(dataStore);
    }
}
