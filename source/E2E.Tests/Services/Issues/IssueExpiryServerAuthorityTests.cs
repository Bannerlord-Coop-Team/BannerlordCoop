using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using GameInterface.Services.Issues.Messages;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encyclopedia;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Settlements;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Issues;

public class IssueExpiryServerAuthorityTests : IDisposable
{
    private E2ETestEnvironment TestEnvironment { get; }
    private EnvironmentInstance Server => TestEnvironment.Server;

    public IssueExpiryServerAuthorityTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
    }

    private string CreateOwnedIssueOnServer()
    {
        var heroId = TestEnvironment.CreateRegisteredObject<Hero>();
        var settlementId = TestEnvironment.CreateRegisteredObject<Settlement>();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(heroId, out var owner));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));

            using (new AllowedThread())
            {
                Campaign.Current.EncyclopediaManager ??= new EncyclopediaManager();
                Campaign.Current.EncyclopediaManager.CreateEncyclopediaPages();
                owner.StayingInSettlement = settlement;
            }

            var pid = new PotentialIssueData(
                (in PotentialIssueData _, Hero h) =>
                {
                    var issue = new VillageNeedsCraftingMaterialsIssueBehavior.VillageNeedsCraftingMaterialsIssue(h);
                    if (!Server.ObjectManager.Contains(issue._requestedItem))
                    {
                        Assert.True(Server.ObjectManager.AddExisting(issue._requestedItem.StringId, issue._requestedItem));
                    }
                    return issue;
                },
                typeof(VillageNeedsCraftingMaterialsIssueBehavior.VillageNeedsCraftingMaterialsIssue),
                IssueBase.IssueFrequency.Rare);

            using (new AllowedThread())
            {
                Assert.True(Campaign.Current.IssueManager.CreateNewIssue(in pid, owner));
            }
        });

        return heroId;
    }

    [Fact]
    public void StayAliveConditionsFailedOnServer_GenuinelyRemovesTheIssue_NotJustTellsClientsItWasRemoved()
    {
        var heroId = CreateOwnedIssueOnServer();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(heroId, out var owner));
            Assert.True(owner.Issue.IsOngoingWithoutQuest);

            owner.Issue.CompleteIssueWithStayAliveConditionsFailed();

            Assert.Null(owner.Issue);
        });

        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueRemoved>());
    }

    [Fact]
    public void TimedOutOnServer_GenuinelyRemovesTheIssue_NotJustTellsClientsItWasRemoved()
    {
        var heroId = CreateOwnedIssueOnServer();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(heroId, out var owner));
            Assert.True(owner.Issue.IsOngoingWithoutQuest);

            owner.Issue.CompleteIssueWithTimedOut();

            Assert.Null(owner.Issue);
        });

        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueRemoved>());
    }
}
