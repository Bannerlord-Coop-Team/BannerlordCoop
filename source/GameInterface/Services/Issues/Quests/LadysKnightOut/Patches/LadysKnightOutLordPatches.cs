using Common;
using GameInterface.Services.Issues.Framework.Finalization;
using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using Issue = TaleWorlds.CampaignSystem.Issues.LadysKnightOutIssueBehavior.LadysKnightOutIssue;

namespace GameInterface.Services.Issues.Quests.LadysKnightOut.Patches;

[HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
[HarmonyPatch]
internal class LadysKnightOutLordPatches
{
    // Vanilla IssueManager cancels a lord solution whose giver dies, and no dispatch patch tells the clients.
    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnHeroKilled))]
    [HarmonyPrefix]
    private static void DeathPrefix(Hero victim, out IssueBase __state) =>
        __state = ModInformation.IsServer && victim.Issue is Issue && victim.Issue.IsSolvingWithLordSolution ? victim.Issue : null;

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnHeroKilled))]
    [HarmonyPostfix]
    private static void DeathPostfix(IssueBase __state)
    {
        if (__state != null && ContainerProvider.TryResolve<IOutcomeDispatcher>(out var dispatcher)) dispatcher.After(__state, IssueOutcome.QuestCancel);
    }
}
