using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Generic.Migrated.GangLeaderNeedsToOffloadStolenGoods;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Patches;

[HarmonyPatch(typeof(GangLeaderNeedsToOffloadStolenGoodsIssueBehavior))]
internal class GangLeaderNeedsToOffloadStolenGoodsAlternativeSolutionCompletionPatches
{
    private static readonly ModuleRescanCompletion<GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssue> RescanSpec =
        new(GangLeaderNeedsToOffloadStolenGoodsQuestType.TryTriggerOwnedAlternativeSolutionCompletion);

    [HarmonyPatch(nameof(GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.RegisterEvents))]
    [HarmonyPostfix]
    private static void RegisterEventsPostfix(GangLeaderNeedsToOffloadStolenGoodsIssueBehavior __instance)
    {
        CampaignEvents.HourlyTickEvent.AddNonSerializedListener(__instance, OnHourlyTick);
    }

    private static void OnHourlyTick()
    {
        if (ContainerProvider.TryResolve<IModuleRescanCompletionRunner>(out var runner))
        {
            runner.Run(RescanSpec);
        }
    }
}
