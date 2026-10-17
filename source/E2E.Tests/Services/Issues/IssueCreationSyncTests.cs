using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using GameInterface.Services.Issues.Framework.CreationCapture;
using HarmonyLib;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Settlements;
using Xunit.Abstractions;
using AllowedIssue = TaleWorlds.CampaignSystem.Issues.GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssue;

namespace E2E.Tests.Services.Issues;

public class IssueCreationSyncTests : IDisposable
{
    private const int StolenTradeGood = 3;

    private static readonly FieldInfo RandomForStolenTradeGoodField = AccessTools.Field(typeof(AllowedIssue), nameof(AllowedIssue._randomForStolenTradeGood));

    private readonly string notableId;
    private readonly string ownerSettlementId;
    private readonly string hideoutSettlementId;
    private readonly string counterOfferHeroId;

    private E2ETestEnvironment TestEnvironment { get; }
    private EnvironmentInstance Server => TestEnvironment.Server;
    private IEnumerable<EnvironmentInstance> AllInstances => TestEnvironment.Clients.Prepend(Server);

    public IssueCreationSyncTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);

        notableId = TestEnvironment.CreateRegisteredObject<Hero>();
        ownerSettlementId = TestEnvironment.CreateRegisteredObject<Settlement>();
        hideoutSettlementId = TestEnvironment.CreateRegisteredObject<Settlement>();
        counterOfferHeroId = TestEnvironment.CreateRegisteredObject<Hero>();

        foreach (var instance in AllInstances)
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(notableId, out var notable));
                Assert.True(instance.ObjectManager.TryGetObject<Settlement>(ownerSettlementId, out var ownerSettlement));
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(counterOfferHeroId, out var counterOfferHero));

                using (new AllowedThread())
                {
                    notable.StayingInSettlement = ownerSettlement;
                    notable.Occupation = Occupation.GangLeader;
                    counterOfferHero.Occupation = Occupation.Merchant;

                    if (instance == Server)
                    {
                        ownerSettlement.AddHeroWithoutParty(counterOfferHero);
                        counterOfferHero.StayingInSettlement = ownerSettlement;
                        counterOfferHero.ChangeState(Hero.CharacterStates.Active);
                    }
                }
            });
        }
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
    }

    private void CreateIssueOnServer()
    {
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(notableId, out var notable));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(hideoutSettlementId, out var hideout));

            var potentialIssue = new PotentialIssueData(
                (in PotentialIssueData _, Hero hero) =>
                {
                    var issue = new AllowedIssue(hero, hideout);
                    RandomForStolenTradeGoodField.SetValue(issue, StolenTradeGood);
                    return issue;
                },
                typeof(AllowedIssue),
                IssueBase.IssueFrequency.Common);

            Assert.True(Campaign.Current.IssueManager.CreateNewIssue(in potentialIssue, notable));
        });
    }

    [Fact]
    public void AnIssueTheServerCreatesIsBuiltOnEveryClientWithTheServersValues()
    {
        CreateIssueOnServer();

        var created = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueCreated>());
        Assert.Equal(notableId, created.IssueOwnerId);
        Assert.Equal(typeof(AllowedIssue).Name, created.IssueTypeName);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(notableId, out var notable));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(counterOfferHeroId, out var counterOfferHero));
            var issue = Assert.IsType<AllowedIssue>(notable.Issue);

            Assert.Equal(created.IssueId, issue.StringId);
            Assert.Same(counterOfferHero, issue.CounterOfferHero);
        });

        foreach (var client in TestEnvironment.Clients)
        {
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<Hero>(notableId, out var notable));
                Assert.True(client.ObjectManager.TryGetObject<Settlement>(hideoutSettlementId, out var hideout));
                Assert.True(client.ObjectManager.TryGetObject<Hero>(counterOfferHeroId, out var counterOfferHero));
                var issue = Assert.IsType<AllowedIssue>(notable.Issue);

                Assert.Equal(created.IssueId, issue.StringId);
                Assert.Equal(StolenTradeGood, issue._randomForStolenTradeGood);
                Assert.Same(hideout, issue._issueHideout);
                Assert.Same(counterOfferHero, issue.CounterOfferHero);
            });
        }
    }

    [Fact]
    public void AClientCannotCreateAnIssueOnItsOwn()
    {
        var client = TestEnvironment.Clients.First();

        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<Hero>(notableId, out var notable));
            Assert.True(client.ObjectManager.TryGetObject<Settlement>(hideoutSettlementId, out var hideout));

            var potentialIssue = new PotentialIssueData(
                (in PotentialIssueData _, Hero hero) => new AllowedIssue(hero, hideout),
                typeof(AllowedIssue),
                IssueBase.IssueFrequency.Common);

            Assert.False(Campaign.Current.IssueManager.CreateNewIssue(in potentialIssue, notable));
            Assert.Null(notable.Issue);
        });

        Assert.Empty(client.NetworkSentMessages.GetMessages<NetworkIssueCreated>());
    }

    [Fact]
    public void ACreationForAnOwnerThatAlreadyHasAnIssueIsIgnored()
    {
        CreateIssueOnServer();
        var created = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueCreated>());
        var client = TestEnvironment.Clients.First();
        string firstIssueId = null!;

        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<Hero>(notableId, out var notable));
            firstIssueId = notable.Issue.StringId;
        });

        client.SimulateMessage(this, new NetworkIssueCreated(notableId, created.IssueTypeName, "issue_second", created.Captured));

        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<Hero>(notableId, out var notable));
            Assert.Equal(firstIssueId, notable.Issue.StringId);
        });
    }
}
