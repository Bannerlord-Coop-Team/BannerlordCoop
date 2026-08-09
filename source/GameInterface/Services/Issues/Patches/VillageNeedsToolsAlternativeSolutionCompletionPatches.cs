using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Generic.Migrated.VillageNeedsTools;
using GameInterface.Services.Issues.Interfaces;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Patches;

[HarmonyPatch(typeof(IssueBase), nameof(IssueBase.CompleteIssueWithAlternativeSolution))]
internal class VillageNeedsToolsAlternativeSolutionOwnershipGatePatch
{
    [HarmonyPrefix]
    private static bool Prefix(IssueBase __instance)
    {
        if (__instance is not VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssue) return true;

        return IssueOwnershipRegistry.IsLocalPeerOwner(__instance.IssueOwner);
    }
}

[HarmonyPatch(typeof(VillageNeedsToolsIssueBehavior))]
internal class VillageNeedsToolsAlternativeSolutionCompletionPatches
{
    private static readonly ModuleRescanCompletion<VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssue> RescanSpec =
        new(VillageNeedsToolsQuestType.TryTriggerOwnedAlternativeSolutionCompletion);

    [HarmonyPatch(nameof(VillageNeedsToolsIssueBehavior.RegisterEvents))]
    [HarmonyPostfix]
    private static void RegisterEventsPostfix(VillageNeedsToolsIssueBehavior __instance)
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
