using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Messages;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encyclopedia;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Settlements;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Issues;

using Issue = TheConquestOfSettlementIssueBehavior.TheConquestOfSettlementIssue;

public class TheConquestOfSettlementIssueTests : IDisposable
{
    private readonly E2ETestEnvironment environment;
    private EnvironmentInstance Server => environment.Server;

    public TheConquestOfSettlementIssueTests(ITestOutputHelper output)
    {
        environment = new E2ETestEnvironment(output);
    }

    public void Dispose() => environment.Dispose();

    private NetworkConquestIssueCreated CreateIssue()
    {
        var giverId = environment.CreateRegisteredObject<Hero>();
        var targetId = environment.CreateRegisteredObject<Settlement>();
        foreach (var instance in new[] { Server }.Concat(environment.Clients))
        {
            instance.Call(() =>
            {
                using (new AllowedThread())
                {
                    Campaign.Current.EncyclopediaManager ??= new EncyclopediaManager();
                    Campaign.Current.EncyclopediaManager.CreateEncyclopediaPages();
                    if (instance != Server) Campaign.Current.IssueManager._nextIssueUniqueIndex = 200;
                }
            });
        }
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(giverId, out var giver));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(targetId, out var target));
            var potential = new PotentialIssueData((in PotentialIssueData _, Hero owner) => new Issue(owner, target),
                typeof(Issue), IssueBase.IssueFrequency.VeryCommon);
            Assert.True(Campaign.Current.IssueManager.CreateNewIssue(in potential, giver));
        });
        return Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkConquestIssueCreated>());
    }

    [Fact]
    public void CreationKeepsTheServerTargetDeadlineAndIdentityAcrossDifferentClientCounters()
    {
        var created = CreateIssue();
        Assert.StartsWith("coop_conquest_issue_", created.IssueId);
        foreach (var instance in new[] { Server }.Concat(environment.Clients))
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(created.GiverId, out var giver));
                Assert.True(instance.ObjectManager.TryGetObject<Settlement>(created.TargetId, out var target));
                var issue = Assert.IsType<Issue>(giver.Issue);
                Assert.Same(target, issue._targetSettlement);
                Assert.Equal(created.IssueId, issue.StringId);
                Assert.Equal(created.DueTime, issue.IssueDueTime);
                Assert.True(instance.Resolve<IIssueGenerationRegistry>().TryGetGeneration(giver, out var generation));
                Assert.Equal(created.Generation, generation);
            });
        }
    }

    [Fact]
    public void DelayedCreationCannotReplaceTheCurrentIssue()
    {
        var created = CreateIssue();
        var differentTargetId = environment.CreateRegisteredObject<Settlement>();
        foreach (var client in environment.Clients)
        {
            client.SimulateMessage(this, new NetworkConquestIssueCreated(created.GiverId, differentTargetId,
                created.Generation - 1, CampaignTime.Never, "old-issue"));
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<Hero>(created.GiverId, out var giver));
                Assert.True(client.ObjectManager.TryGetObject<Settlement>(created.TargetId, out var target));
                var issue = Assert.IsType<Issue>(giver.Issue);
                Assert.Same(target, issue._targetSettlement);
                Assert.Equal(created.IssueId, issue.StringId);
                Assert.Equal(created.DueTime, issue.IssueDueTime);
            });
        }
    }
}
