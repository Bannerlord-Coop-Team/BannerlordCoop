using Autofac;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.Issues.Quests.LesserNobleRevolt;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.Localization;
using Xunit;
using Xunit.Abstractions;
using Issue = TaleWorlds.CampaignSystem.Issues.LesserNobleRevoltIssueBehavior.LesserNobleRevoltIssue;

namespace E2E.Tests.Services.Issues;

public sealed class LesserNobleRevoltTests : IDisposable
{
    private readonly E2ETestEnvironment testEnvironment;
    private readonly string giverId;

    public LesserNobleRevoltTests(ITestOutputHelper output)
    {
        testEnvironment = new E2ETestEnvironment(output);
        giverId = testEnvironment.CreateRegisteredObject<Hero>();
    }

    public void Dispose() => testEnvironment.Dispose();

    [Fact]
    public void FrameworkRegistersTheQuestAndReplicatesItsCreationCapture()
    {
        var server = testEnvironment.Server;
        var client = testEnvironment.Clients.First();

        server.Call(() =>
        {
            Assert.True(server.Container.Resolve<IQuestTypeRegistry>().TryGetByName(typeof(Issue).Name, out var descriptor));
            Assert.IsType<LesserNobleRevoltCreationCaptureStrategy>(descriptor.CreationCaptureStrategy);
            Assert.IsType<LesserNobleRevoltQuestSolutionAcceptStrategy>(descriptor.QuestSolutionAcceptStrategy);
            Assert.NotNull(descriptor.FinalizationProofStrategy);
        });

        byte[] captured = Array.Empty<byte>();
        server.Call(() =>
        {
            Assert.True(server.ObjectManager.TryGetObject<Hero>(giverId, out var giver));
            var issue = new Issue(giver);
            issue._lesserNobleName = new TextObject("Noble A");
            issue.IssueCreationTime = CampaignTime.Days(6f);
            issue.IssueDueTime = CampaignTime.Days(26f);
            captured = server.Container.Resolve<IQuestTypeRegistry>().TryGetByName(typeof(Issue).Name, out var descriptor)
                ? descriptor.CreationCaptureStrategy.Capture(issue)
                : null;
        });

        client.Call(() =>
        {
            Assert.True(client.Container.Resolve<IQuestTypeRegistry>().TryGetByName(typeof(Issue).Name, out var descriptor));
            Assert.NotEmpty(captured);
            Assert.NotEmpty(captured);
        });
    }
}
