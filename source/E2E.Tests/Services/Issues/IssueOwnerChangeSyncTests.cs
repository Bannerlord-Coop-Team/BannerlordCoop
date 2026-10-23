using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using GameInterface.Services.Issues.Framework.CreationCapture;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.Issues.Framework.OwnerChange;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Settlements;
using Xunit.Abstractions;
using AllowedIssue = TaleWorlds.CampaignSystem.Issues.GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssue;

namespace E2E.Tests.Services.Issues;

public class IssueOwnerChangeSyncTests : IDisposable
{
    private const string OwnerControllerId = "player-A";

    private readonly string deadNotableId;
    private readonly string newNotableId;
    private readonly string ownerSettlementId;
    private readonly string hideoutSettlementId;
    private readonly string issueId;

    private E2ETestEnvironment TestEnvironment { get; }
    private EnvironmentInstance Server => TestEnvironment.Server;
    private IEnumerable<EnvironmentInstance> AllInstances => TestEnvironment.Clients.Prepend(Server);

    public IssueOwnerChangeSyncTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);

        deadNotableId = TestEnvironment.CreateRegisteredObject<Hero>();
        newNotableId = TestEnvironment.CreateRegisteredObject<Hero>();
        ownerSettlementId = TestEnvironment.CreateRegisteredObject<Settlement>();
        hideoutSettlementId = TestEnvironment.CreateRegisteredObject<Settlement>();

        foreach (var instance in AllInstances)
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(deadNotableId, out var deadNotable));
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(newNotableId, out var newNotable));
                Assert.True(instance.ObjectManager.TryGetObject<Settlement>(ownerSettlementId, out var ownerSettlement));

                using (new AllowedThread())
                {
                    deadNotable.StayingInSettlement = ownerSettlement;
                    deadNotable.Occupation = Occupation.GangLeader;
                    newNotable.StayingInSettlement = ownerSettlement;
                    newNotable.Occupation = Occupation.GangLeader;
                }
            });
        }

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(deadNotableId, out var deadNotable));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(hideoutSettlementId, out var hideout));

            var potentialIssue = new PotentialIssueData(
                (in PotentialIssueData _, Hero hero) => new AllowedIssue(hero, hideout),
                typeof(AllowedIssue),
                IssueBase.IssueFrequency.Common);

            Assert.True(Campaign.Current.IssueManager.CreateNewIssue(in potentialIssue, deadNotable));
        });

        issueId = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueCreated>()).IssueId;
    }

    private void AcceptOnEveryMachine()
    {
        foreach (var instance in AllInstances)
        {
            instance.Call(() => Assert.True(instance.Resolve<IIssueOwnershipRegistry>().TrySetOwner(deadNotableId, issueId, OwnerControllerId)));
        }
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
    }

    private void ChangeOwnerOnServer()
    {
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(deadNotableId, out var deadNotable));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(newNotableId, out var newNotable));

            Campaign.Current.IssueManager.ChangeIssueOwner(deadNotable.Issue, newNotable);
        });
    }

    [Fact]
    public void TheServerAnnouncesTheNewOwnerOnce()
    {
        ChangeOwnerOnServer();

        var changed = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueOwnerChanged>());
        Assert.Equal(deadNotableId, changed.OldOwnerId);
        Assert.Equal(newNotableId, changed.NewOwnerId);
        Assert.Equal(issueId, changed.IssueId);
    }

    [Fact]
    public void EveryClientMovesTheIssueToTheNewOwner()
    {
        ChangeOwnerOnServer();

        foreach (var client in TestEnvironment.Clients)
        {
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<Hero>(deadNotableId, out var deadNotable));
                Assert.True(client.ObjectManager.TryGetObject<Hero>(newNotableId, out var newNotable));

                Assert.Null(deadNotable.Issue);
                var issue = Assert.IsType<AllowedIssue>(newNotable.Issue);
                Assert.Equal(issueId, issue.StringId);
                Assert.Same(newNotable, issue.IssueOwner);
            });
        }
    }

    [Fact]
    public void AnIssueNobodyAcceptedMovesToTheNewOwnerWithoutAnOwnershipEntry()
    {
        ChangeOwnerOnServer();

        foreach (var instance in AllInstances)
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(newNotableId, out var newNotable));

                Assert.Equal(issueId, newNotable.Issue?.StringId);
                Assert.Empty(instance.Resolve<IIssueOwnershipRegistry>().GetAll());
            });
        }
    }

    [Fact]
    public void EveryMachineMovesTheOwnershipEntryToTheNewOwner()
    {
        AcceptOnEveryMachine();
        ChangeOwnerOnServer();

        foreach (var instance in AllInstances)
        {
            instance.Call(() =>
            {
                var ownership = instance.Resolve<IIssueOwnershipRegistry>();

                Assert.False(ownership.TryGetOwner(deadNotableId, issueId, out _));
                Assert.True(ownership.TryGetOwner(newNotableId, issueId, out var controllerId));
                Assert.Equal(OwnerControllerId, controllerId);
            });
        }
    }

    [Fact]
    public void AClientDoesNotMoveAnIssueOnItsOwn()
    {
        var client = TestEnvironment.Clients.First();

        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<Hero>(deadNotableId, out var deadNotable));
            Assert.True(client.ObjectManager.TryGetObject<Hero>(newNotableId, out var newNotable));

            Campaign.Current.IssueManager.ChangeIssueOwner(deadNotable.Issue, newNotable);

            Assert.NotNull(deadNotable.Issue);
            Assert.Null(newNotable.Issue);
        });

        Assert.Empty(client.NetworkSentMessages.GetMessages<NetworkIssueOwnerChanged>());
    }
}
