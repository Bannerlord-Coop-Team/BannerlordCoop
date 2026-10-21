using Common;
using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Conversation;
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

        PlayerEncounter.LeaveEncounter = true;
        return false;
    }

    [HarmonyPatch(typeof(Quest), nameof(Quest.persuasion_start_with_smugglers_on_consequence))]
    [HarmonyPostfix]
    private static void PersuasionStarted(Quest __instance)
    {
        if (ModInformation.IsClient && ContainerProvider.TryResolve<IPersuasionSessionService>(out var sessions))
        {
            sessions.Start(__instance, __instance._smugglerParty);
        }
    }

    [HarmonyPatch(typeof(Quest), nameof(Quest.SucceedQuest))]
    [HarmonyPrefix]
    private static bool SuccessByPersuasionPrefix(Quest __instance) =>
        BeforeBranch(__instance, SmugglersBranch.SuccessByPersuasion);

    [HarmonyPatch(typeof(Quest), nameof(Quest.persuasion_complete_with_smugglers_on_consequence))]
    [HarmonyPrefix]
    private static bool CompletePersuasion(Quest __instance)
    {
        if (ModInformation.IsServer)
        {
            return true;
        }

        // Send success before conversation cleanup invalidates the session.
        __instance.SucceedQuest(null);
        PlayerEncounter.LeaveEncounter = true;
        ConversationManager.EndPersuasion();
        return false;
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
