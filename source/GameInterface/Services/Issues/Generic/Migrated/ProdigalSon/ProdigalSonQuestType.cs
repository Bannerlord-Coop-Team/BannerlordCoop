using GameInterface.Services.Issues.Patches;
using System.Runtime.CompilerServices;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Issues.Generic.Migrated.ProdigalSon;

using Issue = SandBox.Issues.ProdigalSonIssueBehavior.ProdigalSonIssue;
using Quest = SandBox.Issues.ProdigalSonIssueBehavior.ProdigalSonIssueQuest;

[QuestTypeModule]
internal static class ProdigalSonQuestType
{
    internal const byte ProofDefeatedThugs = 1;
    internal const byte ProofConvincedGangLeader = 3;
    internal const byte ProofPaidTheDebt = 4;

    private static readonly ConditionalWeakTable<Quest, object> ObservedSuccessProof = new();

    static ProdigalSonQuestType()
    {
        var descriptor = QuestDescriptorBuilder.For<Issue, Quest>("ProdigalSon")
            .WithQuestSolutionAccept()
            .WithAlternativeAccept()
            .WithQuestSuccessValidation(ValidateQuestSuccess)
            .WithQuestSuccessProofCapture(CaptureQuestSuccessProof)
            .Build();

        QuestTypeRegistry.Register(descriptor);
        DisableAllIssueBehaviorsExceptAllowlist.Allowlist.Add(typeof(SandBox.Issues.ProdigalSonIssueBehavior));
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

[HarmonyPatch(typeof(SandBox.Issues.ProdigalSonIssueBehavior.ProdigalSonIssueQuest))]
internal class ProdigalSonQuestSuccessObserverPatches
{
    [HarmonyPatch("FinishQuestSuccess1")]
    [HarmonyPostfix]
    private static void FinishQuestSuccess1Postfix(SandBox.Issues.ProdigalSonIssueBehavior.ProdigalSonIssueQuest __instance)
        => ProdigalSonQuestType.Observe(__instance, ProdigalSonQuestType.ProofDefeatedThugs);

    [HarmonyPatch("FinishQuestSuccess3")]
    [HarmonyPostfix]
    private static void FinishQuestSuccess3Postfix(SandBox.Issues.ProdigalSonIssueBehavior.ProdigalSonIssueQuest __instance)
        => ProdigalSonQuestType.Observe(__instance, ProdigalSonQuestType.ProofConvincedGangLeader);

    [HarmonyPatch("FinishQuestSuccess4")]
    [HarmonyPostfix]
    private static void FinishQuestSuccess4Postfix(SandBox.Issues.ProdigalSonIssueBehavior.ProdigalSonIssueQuest __instance)
        => ProdigalSonQuestType.Observe(__instance, ProdigalSonQuestType.ProofPaidTheDebt);
}
