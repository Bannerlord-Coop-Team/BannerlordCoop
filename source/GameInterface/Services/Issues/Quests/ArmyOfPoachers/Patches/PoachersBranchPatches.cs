using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using Quest = TaleWorlds.CampaignSystem.Issues.MerchantArmyOfPoachersIssueBehavior.MerchantArmyOfPoachersIssueQuest;

namespace GameInterface.Services.Issues.Quests.ArmyOfPoachers.Patches;

[HarmonyPatch]
internal class PoachersBranchPatches
{
    [HarmonyPatch(typeof(Quest), nameof(Quest.QuestSuccessPlayerComesToAnAgreementWithPoachers))]
    [HarmonyPrefix]
    private static bool AgreementPrefix(Quest __instance) => BeforeBranch(__instance, ArmyOfPoachersBranch.Agreement);

    [HarmonyPatch(typeof(Quest), nameof(Quest.QuestFailedAfterTalkingWithPoachers))]
    [HarmonyPrefix]
    private static bool LeavePoachersPrefix(Quest __instance) => BeforeBranch(__instance, ArmyOfPoachersBranch.LeavePoachersAlone);

    private static bool BeforeBranch(Quest quest, ArmyOfPoachersBranch branch)
    {
        return !ContainerProvider.TryResolve<IOutcomeDispatcher>(out var dispatcher) || dispatcher.BeforeQuestBranch(quest, (byte)branch);
    }
}
