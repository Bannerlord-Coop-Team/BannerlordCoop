using Common;
using GameInterface.Policies;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Issues.Quests.HeadmanNeedsGrain.Patches;

[HarmonyPatch(typeof(HeadmanNeedsGrainIssueBehavior.HeadmanNeedsGrainIssue), "NearbySuitableSettlement", MethodType.Getter)]
internal sealed class HeadmanNeedsGrainMarketPatches
{
    [HarmonyPrefix]
    internal static bool Prefix(HeadmanNeedsGrainIssueBehavior.HeadmanNeedsGrainIssue __instance, ref Settlement __result)
    {
        if (CallOriginalPolicy.IsOriginalAllowed() || ModInformation.IsServer) return true;

        // Player trade perks can select a different market from the server's cached offer.
        __result = __instance._nearbySuitableSettlementCache;
        return false;
    }
}
