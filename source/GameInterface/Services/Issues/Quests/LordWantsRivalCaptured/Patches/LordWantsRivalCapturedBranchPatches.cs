using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using Quest = TaleWorlds.CampaignSystem.Issues.LordWantsRivalCapturedIssueBehavior.LordWantsRivalCapturedIssueQuest;

namespace GameInterface.Services.Issues.Quests.LordWantsRivalCaptured.Patches;

[HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
[HarmonyPatch(typeof(Quest))]
internal class LordWantsRivalCapturedBranchPatches
{
    [HarmonyPatch(nameof(Quest.FirstCounterOfferFinished))]
    [HarmonyPrefix]
    private static bool FirstCounterOfferPrefix(Quest __instance)
    {
        if (BeforeBranch(__instance, LordWantsRivalCapturedBranch.CapturedAtCounterOffer))
        {
            return true;
        }

        // The owner's own dialogs read the flag, the server takes the prisoner
        __instance._firstCounterOfferMade = true;
        return false;
    }

    [HarmonyPatch(nameof(Quest.QuestFailCounterOfferAccepted))]
    [HarmonyPrefix]
    private static bool CounterOfferAcceptedPrefix(Quest __instance)
    {
        return BeforeBranch(__instance, LordWantsRivalCapturedBranch.CounterOfferAccepted);
    }

    [HarmonyPatch(nameof(Quest.PlayerDeliveredPrisonerQuestSuccess))]
    [HarmonyPrefix]
    private static bool DeliveredPrefix(Quest __instance)
    {
        var branch = Hero.OneToOneConversationHero == __instance.QuestGiver
            ? LordWantsRivalCapturedBranch.DeliveredToGiver
            : LordWantsRivalCapturedBranch.DeliveredToGuard;

        return BeforeBranch(__instance, branch);
    }

    private static bool BeforeBranch(Quest quest, LordWantsRivalCapturedBranch branch)
    {
        if (!ContainerProvider.TryResolve<IOutcomeDispatcher>(out var dispatcher))
        {
            return true;
        }

        // The server runs the branch as the owning player, this client waits for the result
        return dispatcher.BeforeQuestBranch(quest, (byte)branch);
    }
}
