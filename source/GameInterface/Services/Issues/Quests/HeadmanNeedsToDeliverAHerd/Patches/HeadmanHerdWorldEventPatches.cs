using Common;
using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Issues;
using Quest = TaleWorlds.CampaignSystem.Issues.HeadmanNeedsToDeliverAHerdIssueBehavior.HeadmanNeedsToDeliverAHerdIssueQuest;
using Issue = TaleWorlds.CampaignSystem.Issues.HeadmanNeedsToDeliverAHerdIssueBehavior.HeadmanNeedsToDeliverAHerdIssue;

namespace GameInterface.Services.Issues.Quests.HeadmanNeedsToDeliverAHerd.Patches;

[HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
[HarmonyPatch]
internal class HeadmanHerdWorldEventPatches
{
    [HarmonyPatch(typeof(IssueBase), nameof(IssueBase.StartIssueWithAlternativeSolution))]
    [HarmonyPrefix]
    private static void AlternativeSolutionStartedPrefix(IssueBase __instance)
    {
        if (ModInformation.IsClient || !(__instance is Issue) ||
            !ContainerProvider.TryResolve<IIssueOwnerResolver>(out var owners) ||
            !owners.TryResolveOwnerHero(__instance, out var owner) || owner.PartyBelongedTo == null ||
            !ContainerProvider.TryResolve<IHeadmanHerdCleanup>(out var cleanup))
            return;

        cleanup.Remember(__instance, owner.PartyBelongedTo);
    }

    [HarmonyPatch(typeof(IssueManager), nameof(IssueManager.TryToMakeTroopsReturn))]
    [HarmonyPrefix]
    private static bool ReturnAlternativeTroopsPrefix(IssueBase issue)
    {
        if (ModInformation.IsClient || !(issue is Issue) ||
            !ContainerProvider.TryResolve<IHeadmanHerdCleanup>(out var cleanup))
            return true;

        return !cleanup.ReturnAlternativeTroops(issue);
    }

    [HarmonyPatch(typeof(Quest), nameof(Quest.OnHeroKilled))]
    [HarmonyPrefix]
    private static bool HeroKilledPrefix() => ModInformation.IsServer;

    [HarmonyPatch(typeof(Quest), nameof(Quest.OnMapEventStarted))]
    [HarmonyPrefix]
    private static bool MapEventStartedPrefix() => ModInformation.IsServer;
}
