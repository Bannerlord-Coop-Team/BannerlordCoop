using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Encounters;
using Quest = TaleWorlds.CampaignSystem.Issues.SmugglersIssueBehavior.SmugglersIssueQuest;

namespace GameInterface.Services.Issues.Quests.Smugglers.Patches;

[HarmonyPatch]
internal class SmugglersBranchPatches
{
    [HarmonyPatch(typeof(Quest), nameof(Quest.SucceedQuestWithBribe))]
    [HarmonyPrefix]
    private static bool SuccessByBribePrefix(Quest __instance)
    {
        if (BeforeBranch(__instance, SmugglersBranch.SuccessByBribe))
        {
            return true;
        }

        // The server pays the bribe as the owner, leaving the encounter is this client's part
        PlayerEncounter.LeaveEncounter = true;
        return false;
    }

    // On a client only the persuasion reaches SucceedQuest, the fight is decided on the server and the bribe is patched above
    [HarmonyPatch(typeof(Quest), nameof(Quest.SucceedQuest))]
    [HarmonyPrefix]
    private static bool SuccessByPersuasionPrefix(Quest __instance)
    {
        return BeforeBranch(__instance, SmugglersBranch.SuccessByPersuasion);
    }

    private static bool BeforeBranch(Quest quest, SmugglersBranch branch)
    {
        if (!ContainerProvider.TryResolve<IOutcomeDispatcher>(out var dispatcher))
        {
            return true;
        }

        return dispatcher.BeforeQuestBranch(quest, (byte)branch);
    }
}
