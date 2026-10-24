using Common;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.Issues.Quests.HeadmanNeedsGrain;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Quests.HeadmanNeedsGrain.Patches;

[HarmonyPatch(typeof(HeadmanNeedsGrainIssueBehavior))]
internal class HeadmanNeedsGrainPriceCachePatches
{
    [HarmonyPatch(nameof(HeadmanNeedsGrainIssueBehavior.CacheGrainPrice))]
    [HarmonyPrefix]
    internal static bool Prefix() => CallOriginalPolicy.IsOriginalAllowed() || ModInformation.IsServer;

    [HarmonyPatch(nameof(HeadmanNeedsGrainIssueBehavior.CacheGrainPrice))]
    [HarmonyPostfix]
    private static void Postfix(HeadmanNeedsGrainIssueBehavior __instance)
    {
        if (CallOriginalPolicy.IsOriginalAllowed() || ModInformation.IsClient) return;

        MessageBroker.Instance.Publish(__instance, new HeadmanGrainPriceCached(__instance._averageGrainPriceInCalradia));
    }
}
