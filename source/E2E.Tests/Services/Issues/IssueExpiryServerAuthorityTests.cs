using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Messages;
using GameInterface.Services.Issues.Patches;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using HarmonyLib;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encyclopedia;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
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

    private record OwnedIssue(string HeroId, string SettlementId);

    private OwnedIssue CreateOwnedIssueOnServer()
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

        return new OwnedIssue(heroId, settlementId);
    }

    [Fact]
    public void DailyTickOnServer_StayAliveConditionsFailed_GenuinelyRemovesTheIssue_NotJustTellsClientsItWasRemoved()
    {
        var issue = CreateOwnedIssueOnServer();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(issue.HeroId, out var owner));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(issue.SettlementId, out var settlement));
            Assert.True(owner.Issue.IsOngoingWithoutQuest);
            Assert.True(owner.Issue.IssueStayAliveConditions());

            using (new AllowedThread())
            {
                settlement.Village.VillageState = Village.VillageStates.Looted;
            }
            Assert.False(owner.Issue.IssueStayAliveConditions());

            Campaign.Current.IssueManager.DailyTick();

            Assert.Null(owner.Issue);
            Assert.False(Campaign.Current.IssueManager.Issues.ContainsKey(owner));
        });

        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueRemoved>());
    }

    [Fact]
    public void DailyTickOnServer_PastDueUnacceptedIssue_GenuinelyRemovesTheIssue_NotJustTellsClientsItWasRemoved()
    {
        var issue = CreateOwnedIssueOnServer();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(issue.HeroId, out var owner));
            Assert.True(owner.Issue.IsOngoingWithoutQuest);

            using (new AllowedThread())
            {
                owner.Issue.IssueDueTime = default;
            }
            Assert.True(owner.Issue.IssueDueTime.IsPast);

            for (var day = 0; day < 200 && owner.Issue != null; day++)
            {
                Campaign.Current.IssueManager.DailyTick();
            }

            Assert.Null(owner.Issue);
            Assert.False(Campaign.Current.IssueManager.Issues.ContainsKey(owner));
        });

        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueRemoved>());
    }

    [Fact]
    public void HourlyTickOnServer_AcceptedQuestTimedOut_AppliesTheOwnersRelationPenaltyToTheRealOwnerOnly()
    {
        var issue = CreateOwnedIssueOnServer();

        var ownerHeroId = TestEnvironment.CreateRegisteredObject<Hero>();
        var ownerPartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();

        int hostRelationBefore = 0;
        int ownerRelationBefore = 0;
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(issue.HeroId, out var giver));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(ownerHeroId, out var ownerHero));

            using (new QuestSolutionStartAuthorityGuard())
            {
                Assert.True(Campaign.Current.IssueManager.StartIssueQuest(giver));
            }
            using (new AllowedThread())
            {
                giver.Issue.IssueQuest.StartQuest();
            }
            Server.Resolve<IIssueOwnershipRegistry>().SetOwner(giver, "owner-controller");
            Assert.True(Server.Resolve<IPlayerManager>().AddPlayer(new Player("owner-controller", ownerHeroId, ownerPartyId, "", "")));

            hostRelationBefore = giver.GetRelation(Hero.MainHero);
            ownerRelationBefore = giver.GetRelation(ownerHero);

            var quest = giver.Issue.IssueQuest;
            Assert.NotNull(quest);
            quest.ChangeQuestDueTime(CampaignTime.Now - CampaignTime.Days(1f));
            Assert.True(quest.QuestDueTime.IsPast);

            Campaign.Current.QuestManager.HourlyTick();

            Assert.Null(giver.Issue);
            Assert.False(Campaign.Current.IssueManager.Issues.ContainsKey(giver));
            Assert.Equal(hostRelationBefore, giver.GetRelation(Hero.MainHero));
            Assert.Equal(ownerRelationBefore - 5, giver.GetRelation(ownerHero));
        });

        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueRemoved>());
    }
}
