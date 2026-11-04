using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using Xunit.Abstractions;
using AllowedIssue = TaleWorlds.CampaignSystem.Issues.GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssue;

namespace E2E.Tests.Services.Issues;

public class ClientStayAliveCancelGateTests : IDisposable
{
    private const string IssueId = "issue_stay_alive";

    private readonly StubIssuePreconditions preconditions = new(typeof(AllowedIssue));
    private readonly string notableId;

    private E2ETestEnvironment TestEnvironment { get; }
    private EnvironmentInstance Server => TestEnvironment.Server;
    private EnvironmentInstance Client => TestEnvironment.Clients.First();

    public ClientStayAliveCancelGateTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);

        notableId = TestEnvironment.CreateRegisteredObject<Hero>();

        foreach (var instance in new[] { Server, Client })
        {
            AddIssue(instance);
        }
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
        preconditions.Dispose();
    }

    [Fact]
    public void AClientCheckingAnIssueThatFailsToStayAlive_IsRefusedWithoutEndingIt()
    {
        preconditions.FailStayAlive(Client);

        Client.Call(() =>
        {
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(notableId, out var notable));
            Assert.False(notable.Issue.CheckPreconditions(notable, out _));
            Assert.NotNull(notable.Issue);
            Assert.True(notable.Issue.IsOngoingWithoutQuest);
            Assert.Same(notable.Issue, Campaign.Current.IssueManager.Issues[notable]);
        });
    }

    [Fact]
    public void AClientRefusedAnIssueThatFailsToStayAlive_IsGivenARefusalText()
    {
        preconditions.FailStayAlive(Client);

        Client.Call(() =>
        {
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(notableId, out var notable));
            Assert.False(notable.Issue.CheckPreconditions(notable, out var explanation));
            Assert.Equal("Never mind, I no longer need help with that.", explanation.ToString());
        });
    }

    [Fact]
    public void TheServerCheckingAnIssueThatFailsToStayAlive_StillEndsIt()
    {
        preconditions.FailStayAlive(Server);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(notableId, out var notable));
            Assert.False(notable.Issue.CheckPreconditions(notable, out _));
            Assert.Null(notable.Issue);
        });
    }

    private void AddIssue(EnvironmentInstance instance)
    {
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<Hero>(notableId, out var notable));

            using (new AllowedThread())
            {
                var issue = new AllowedIssue(notable, null);
                issue.StringId = IssueId;
                issue._issueState = IssueBase.IssueState.Ongoing;

                Campaign.Current.IssueManager._issues.Add(notable, issue);
                notable.OnIssueCreatedForHero(issue);
            }
        });
    }
}
