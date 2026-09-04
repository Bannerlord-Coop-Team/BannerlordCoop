using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using GameInterface.Services.Issues.Messages;
using HarmonyLib;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encyclopedia;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Issues;

public class IssueExpiryServerAuthorityTests : IDisposable
{
    private E2ETestEnvironment TestEnvironment { get; }
    private EnvironmentInstance Server => TestEnvironment.Server;

    public IssueExpiryServerAuthorityTests(ITestOutputHelper output)
    {
        TestQuestTypeFixture.EnsureVillageNeedsToolsRegistered();
        TestEnvironment = new E2ETestEnvironment(output);
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
    }

    private string CreateOwnedIssueOnServer()
    {
        var heroId = TestEnvironment.CreateRegisteredObject<Hero>();
        var villageId = TestEnvironment.CreateRegisteredObject<Village>();
        var settlementId = TestEnvironment.CreateRegisteredObject<Settlement>();
        var itemId = TestEnvironment.CreateRegisteredObject<ItemObject>();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(heroId, out var owner));
            Assert.True(Server.ObjectManager.TryGetObject<Village>(villageId, out var village));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));
            Assert.True(Server.ObjectManager.TryGetObject<ItemObject>(itemId, out var requestedItem));

            using (new AllowedThread())
            {
                Campaign.Current.EncyclopediaManager ??= new EncyclopediaManager();
                Campaign.Current.EncyclopediaManager.CreateEncyclopediaPages();

                settlement.SetSettlementComponent(village);
                village.Bound = settlement;
                village.Hearth = 650f;
                owner.StayingInSettlement = settlement;
                owner.Occupation = Occupation.RuralNotable;
                AccessTools.Property(typeof(ItemObject), nameof(ItemObject.Value)).SetValue(requestedItem, 40);
            }

            var pid = new PotentialIssueData(
                (in PotentialIssueData _, Hero h) => new VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssue(h, requestedItem),
                typeof(VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssue),
                IssueBase.IssueFrequency.VeryCommon);

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
