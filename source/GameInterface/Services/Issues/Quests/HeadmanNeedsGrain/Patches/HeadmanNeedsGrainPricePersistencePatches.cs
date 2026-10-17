using HarmonyLib;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Quests.HeadmanNeedsGrain.Patches;

[HarmonyPatch(typeof(HeadmanNeedsGrainIssueBehavior))]
internal class HeadmanNeedsGrainPricePersistencePatches
{
    private const string SaveKey = "_coop_headman_needs_grain_average_price";
    private const string MarketSaveKey = "_coop_headman_needs_grain_nearby_markets";

    [HarmonyPatch(nameof(HeadmanNeedsGrainIssueBehavior.SyncData))]
    [HarmonyPostfix]
    private static void SyncDataPostfix(HeadmanNeedsGrainIssueBehavior __instance, IDataStore dataStore)
    {
        if (ContainerProvider.TryResolve<IHeadmanNeedsGrainTraitRewards>(out var traits)) traits.SyncData(dataStore);

        int price = __instance._averageGrainPriceInCalradia;
        dataStore.SyncData(SaveKey, ref price);

        if (dataStore.IsLoading)
        {
            __instance._averageGrainPriceInCalradia = price;
        }

        Dictionary<HeadmanNeedsGrainIssueBehavior.HeadmanNeedsGrainIssue, Settlement> markets = null;
        if (dataStore.IsSaving)
        {
            markets = new Dictionary<HeadmanNeedsGrainIssueBehavior.HeadmanNeedsGrainIssue, Settlement>();
            foreach (var entry in Campaign.Current.IssueManager.Issues)
            {
                if (entry.Value is HeadmanNeedsGrainIssueBehavior.HeadmanNeedsGrainIssue issue)
                    markets[issue] = issue._nearbySuitableSettlementCache;
            }
        }
        dataStore.SyncData(MarketSaveKey, ref markets);
        if (dataStore.IsLoading && markets != null)
        {
            foreach (var entry in markets)
            {
                entry.Key._nearbySuitableSettlementCache = entry.Value;
            }
        }
    }
}
