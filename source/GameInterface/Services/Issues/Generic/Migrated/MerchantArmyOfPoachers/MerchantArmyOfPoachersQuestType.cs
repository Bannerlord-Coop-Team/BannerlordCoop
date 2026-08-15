using System.Runtime.CompilerServices;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using GameInterface.Services.Issues.Patches;

namespace GameInterface.Services.Issues.Generic.Migrated.MerchantArmyOfPoachers;

using Issue = MerchantArmyOfPoachersIssueBehavior.MerchantArmyOfPoachersIssue;
using Quest = MerchantArmyOfPoachersIssueBehavior.MerchantArmyOfPoachersIssueQuest;

[QuestTypeModule]
internal static class MerchantArmyOfPoachersQuestType
{
    internal const byte ProofDefeatedPoachers = 1;
    internal const byte ProofPersuadedPoachers = 2;

    private static readonly ConditionalWeakTable<Quest, object> ObservedSuccessProof = new();

    static MerchantArmyOfPoachersQuestType()
    {
        var descriptor = QuestDescriptorBuilder.For<Issue, Quest>("MerchantArmyOfPoachers")
            .WithQuestSolutionAccept()
            .WithAlternativeAccept()
            .WithQuestSuccessValidation(ValidateQuestSuccess)
            .WithQuestSuccessProofCapture(CaptureQuestSuccessProof)
            .Build();

        QuestTypeRegistry.Register(descriptor);
        DisableAllIssueBehaviorsExceptAllowlist.Allowlist.Add(typeof(MerchantArmyOfPoachersIssueBehavior));
    }

    private static bool ValidateQuestSuccess(Issue issue, MobileParty party) => QuestSuccessProofContext.Current != 0;

    private static byte CaptureQuestSuccessProof(Issue issue)
        => issue.IssueQuest is Quest quest && ObservedSuccessProof.TryGetValue(quest, out var proof) ? (byte)proof : (byte)0;

    internal static void Observe(Quest quest, byte proof)
    {
        ObservedSuccessProof.Remove(quest);
        ObservedSuccessProof.Add(quest, proof);
    }
}

[HarmonyPatch(typeof(MerchantArmyOfPoachersIssueBehavior.MerchantArmyOfPoachersIssueQuest))]
internal class MerchantArmyOfPoachersQuestSuccessObserverPatches
{
    [HarmonyPatch(nameof(MerchantArmyOfPoachersIssueBehavior.MerchantArmyOfPoachersIssueQuest.QuestSuccessWithPlayerDefeatedPoachers))]
    [HarmonyPostfix]
    private static void QuestSuccessWithPlayerDefeatedPoachersPostfix(MerchantArmyOfPoachersIssueBehavior.MerchantArmyOfPoachersIssueQuest __instance)
        => MerchantArmyOfPoachersQuestType.Observe(__instance, MerchantArmyOfPoachersQuestType.ProofDefeatedPoachers);

    [HarmonyPatch(nameof(MerchantArmyOfPoachersIssueBehavior.MerchantArmyOfPoachersIssueQuest.QuestSuccessPlayerComesToAnAgreementWithPoachers))]
    [HarmonyPostfix]
    private static void QuestSuccessPlayerComesToAnAgreementWithPoachersPostfix(MerchantArmyOfPoachersIssueBehavior.MerchantArmyOfPoachersIssueQuest __instance)
        => MerchantArmyOfPoachersQuestType.Observe(__instance, MerchantArmyOfPoachersQuestType.ProofPersuadedPoachers);
}
