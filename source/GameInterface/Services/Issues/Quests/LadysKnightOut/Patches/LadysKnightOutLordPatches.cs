using Common;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.Issues.Framework.Finalization;
using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using Issue = TaleWorlds.CampaignSystem.Issues.LadysKnightOutIssueBehavior.LadysKnightOutIssue;

namespace GameInterface.Services.Issues.Quests.LadysKnightOut.Patches;

[HarmonyPatch]
internal class LadysKnightOutLordPatches
{
    [HarmonyPatch(typeof(IssueBase), nameof(IssueBase.StartIssueWithLordSolution))]
    [HarmonyPrefix]
    private static bool StartPrefix(IssueBase __instance)
    {
        if (!(__instance is Issue) || ModInformation.IsServer || CallOriginalPolicy.IsOriginalAllowed()) return true;
        MessageBroker.Instance.Publish(__instance, new LadysKnightOutLordTriggered(__instance, false));
        return false;
    }

    [HarmonyPatch(typeof(IssueBase), nameof(IssueBase.CompleteIssueWithLordSolutionWithRefuseCounterOffer))]
    [HarmonyPrefix]
    private static bool CompletePrefix(IssueBase __instance)
    {
        if (!(__instance is Issue) || ModInformation.IsServer) return true;
        // Settlement replay can open the menu, but the server must still run its consequence.
        MessageBroker.Instance.Publish(__instance, new LadysKnightOutLordTriggered(__instance, true));
        return false;
    }

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
