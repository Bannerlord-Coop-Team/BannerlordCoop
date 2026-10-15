using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using GameInterface.Services.Issues.Framework.CreationCapture;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Settlements;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Issues;

using Issue = TheConquestOfSettlementIssueBehavior.TheConquestOfSettlementIssue;

public sealed class TheConquestOfSettlementIssueTests : IDisposable
{
    private readonly E2ETestEnvironment environment;
    private readonly string issueOwnerId;
    private readonly string targetSettlementId;

    private EnvironmentInstance Server => environment.Server;

    public TheConquestOfSettlementIssueTests(ITestOutputHelper output)
    {
        environment = new E2ETestEnvironment(output);
        issueOwnerId = environment.CreateRegisteredObject<Hero>();
        targetSettlementId = environment.CreateRegisteredObject<Settlement>();
    }

    public void Dispose() => environment.Dispose();

    [Fact]
    public void ServerCreationUsesCapturedSettlementOnEveryClient()
    {
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(issueOwnerId, out var issueOwner));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(targetSettlementId, out var target));

            var potential = new PotentialIssueData(
                (in PotentialIssueData _, Hero owner) => new Issue(owner, target),
                typeof(Issue),
                IssueBase.IssueFrequency.Common);

            Assert.True(Campaign.Current.IssueManager.CreateNewIssue(in potential, issueOwner));
        });

        var created = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueCreated>());
        Assert.Equal(issueOwnerId, created.IssueOwnerId);
        Assert.Equal(typeof(Issue).Name, created.IssueTypeName);

        foreach (var instance in new[] { Server }.Concat(environment.Clients))
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(issueOwnerId, out var issueOwner));
                Assert.True(instance.ObjectManager.TryGetObject<Settlement>(targetSettlementId, out var target));
                var issue = Assert.IsType<Issue>(issueOwner.Issue);

                Assert.Equal(created.IssueId, issue.StringId);
                Assert.Same(target, issue._targetSettlement);
            });
        }
    }

    [Fact]
    public void ClientCannotCreateAConquestIssue()
    {
        var client = environment.Clients.First();

        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<Hero>(issueOwnerId, out var issueOwner));
            Assert.True(client.ObjectManager.TryGetObject<Settlement>(targetSettlementId, out var target));

            var potential = new PotentialIssueData(
                (in PotentialIssueData _, Hero owner) => new Issue(owner, target),
                typeof(Issue),
                IssueBase.IssueFrequency.Common);

            Assert.False(Campaign.Current.IssueManager.CreateNewIssue(in potential, issueOwner));
            Assert.Null(issueOwner.Issue);
        });

        Assert.Empty(client.NetworkSentMessages.GetMessages<NetworkIssueCreated>());
    }
}