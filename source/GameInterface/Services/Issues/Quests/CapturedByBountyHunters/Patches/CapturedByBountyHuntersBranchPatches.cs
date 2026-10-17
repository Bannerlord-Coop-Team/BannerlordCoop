using Common;
using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using Quest = TaleWorlds.CampaignSystem.Issues.CapturedByBountyHuntersIssueBehavior.CapturedByBountyHuntersIssueQuest;

namespace GameInterface.Services.Issues.Quests.CapturedByBountyHunters.Patches;

/// <summary>
/// The owner's client sees its hideout battle end, the server runs the matching consequences as the owner.
/// </summary>
[HarmonyPatch(typeof(Quest))]
internal class CapturedByBountyHuntersBranchPatches
{
    [HarmonyPatch(nameof(Quest.SuccessConsequences))]
    [HarmonyPrefix]
    private static bool SuccessConsequencesPrefix(Quest __instance)
    {
        return BeforeBranch(__instance, CapturedByBountyHuntersBranch.SuccessByClearingHideout);
    }

    [HarmonyPatch(nameof(Quest.FailConsequences))]
    [HarmonyPrefix]
    private static bool FailConsequencesPrefix(Quest __instance, bool isTimedOut)
    {
        // The server already ran the timeout consequences, a client only ends its copy of the quest
        if (isTimedOut)
        {
            return ModInformation.IsServer;
        }

        return BeforeBranch(__instance, CapturedByBountyHuntersBranch.FailByLosingHideoutBattle);
    }

    private static bool BeforeBranch(Quest quest, CapturedByBountyHuntersBranch branch)
    {
        if (!ContainerProvider.TryResolve<IOutcomeDispatcher>(out var dispatcher))
        {
            return true;
        }

        return dispatcher.BeforeQuestBranch(quest, (byte)branch);
    }
}
