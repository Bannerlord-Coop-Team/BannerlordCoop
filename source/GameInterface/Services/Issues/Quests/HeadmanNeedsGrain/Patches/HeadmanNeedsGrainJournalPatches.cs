using Common;
using Common.Network;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using Issue = TaleWorlds.CampaignSystem.Issues.HeadmanNeedsGrainIssueBehavior.HeadmanNeedsGrainIssue;
using Quest = TaleWorlds.CampaignSystem.Issues.HeadmanNeedsGrainIssueBehavior.HeadmanNeedsGrainIssueQuest;

namespace GameInterface.Services.Issues.Quests.HeadmanNeedsGrain.Patches;

[HarmonyPatch]
[HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
internal class HeadmanNeedsGrainJournalPatches
{
    // A client whose own daily tick misses the clearing day still clears the village's effects
    [HarmonyPatch(typeof(IssueManager), nameof(IssueManager.DailyTick))]
    [HarmonyPostfix]
    private static void DailyTickPostfix()
    {
        if (ModInformation.IsClient || !ContainerProvider.TryResolve<IObjectManager>(out var objects) ||
            !ContainerProvider.TryResolve<INetwork>(out var network)) return;
        foreach (var issue in Campaign.Current.IssueManager.Issues.Values.OfType<Issue>())
        {
            if (issue.IsSolvingWithAlternative && issue._areIssueEffectsResolved &&
                objects.TryGetIdWithLogging(issue.IssueOwner, out var giverId))
                network.SendAll(new NetworkHeadmanNeedsGrainEffectsResolved(giverId, issue.StringId));
        }
    }

    [HarmonyPatch(typeof(Quest), nameof(Quest.OnTimedOut))]
    [HarmonyPrefix]
    private static bool TimeoutPrefix() => ModInformation.IsServer;
}
