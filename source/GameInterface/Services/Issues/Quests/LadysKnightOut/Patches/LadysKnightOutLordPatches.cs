using Common;
using Common.Messaging;
using GameInterface.Policies;
using HarmonyLib;
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
        if (!(__instance is Issue) || ModInformation.IsServer || CallOriginalPolicy.IsOriginalAllowed()) return true;
        MessageBroker.Instance.Publish(__instance, new LadysKnightOutLordTriggered(__instance, true));
        return false;
    }
}
