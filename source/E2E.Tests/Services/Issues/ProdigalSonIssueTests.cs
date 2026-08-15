using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Generic.Migrated.ProdigalSon;
using HarmonyLib;
using SandBox.Issues;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Issues;

using Issue = ProdigalSonIssueBehavior.ProdigalSonIssue;
using Quest = ProdigalSonIssueBehavior.ProdigalSonIssueQuest;

public class ProdigalSonIssueTests : IDisposable
{
    private E2ETestEnvironment TestEnvironment { get; }
    private EnvironmentInstance Server => TestEnvironment.Server;

    public ProdigalSonIssueTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
    }

    [Theory]
    [InlineData("FinishQuestSuccess1")]
    [InlineData("FinishQuestSuccess3")]
    [InlineData("FinishQuestSuccess4")]
    public void SuccessMethod_HasAnObserverPostfixPatchApplied(string methodName)
    {
        Server.Call(() =>
        {
            var method = AccessTools.Method(typeof(Quest), methodName);
            Assert.NotNull(method);

            var patches = Harmony.GetPatchInfo(method);
            Assert.NotNull(patches);
            Assert.Contains(patches.Postfixes, p => p.PatchMethod.DeclaringType == typeof(ProdigalSonQuestSuccessObserverPatches));
        });
    }

    [Fact]
    public void CaptureQuestSuccessProof_ReflectsWhicheverSuccessMethodWasObserved()
    {
        Server.Call(() =>
        {
            var descriptor = QuestTypeRegistry.Get(typeof(Issue));
            Assert.NotNull(descriptor?.CaptureQuestSuccessProof);

            var issue = ObjectHelper.SkipConstructor<Issue>();
            var quest = (Quest)FormatterServices_GetUninitializedObject();
            SetIssueQuest(issue, quest);

            ProdigalSonQuestType.Observe(quest, ProdigalSonQuestType.ProofDefeatedThugs);
            Assert.Equal(ProdigalSonQuestType.ProofDefeatedThugs, descriptor.CaptureQuestSuccessProof(issue));

            ProdigalSonQuestType.Observe(quest, ProdigalSonQuestType.ProofConvincedGangLeader);
            Assert.Equal(ProdigalSonQuestType.ProofConvincedGangLeader, descriptor.CaptureQuestSuccessProof(issue));

            ProdigalSonQuestType.Observe(quest, ProdigalSonQuestType.ProofPaidTheDebt);
            Assert.Equal(ProdigalSonQuestType.ProofPaidTheDebt, descriptor.CaptureQuestSuccessProof(issue));
        });
    }

    [Fact]
    public void ValidateQuestSuccess_RequiresTheProofThreadedThroughTheAmbientContext()
    {
        var descriptor = QuestTypeRegistry.Get(typeof(Issue));
        var issue = ObjectHelper.SkipConstructor<Issue>();

        Server.Call(() =>
        {
            QuestSuccessProofContext.Set(0);
            Assert.False(descriptor.ValidateQuestSuccess(issue, null));

            QuestSuccessProofContext.Set(ProdigalSonQuestType.ProofPaidTheDebt);
            Assert.True(descriptor.ValidateQuestSuccess(issue, null));
            QuestSuccessProofContext.Set(0);
        });
    }

    private static object FormatterServices_GetUninitializedObject()
        => System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(Quest));

    private static void SetIssueQuest(IssueBase issue, QuestBase quest)
        => AccessTools.Property(typeof(IssueBase), nameof(IssueBase.IssueQuest)).SetValue(issue, quest);
}
