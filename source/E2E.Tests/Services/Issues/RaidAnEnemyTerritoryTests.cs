using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using Xunit;
using Xunit.Abstractions;
using Issue = TaleWorlds.CampaignSystem.Issues.RaidAnEnemyTerritoryIssueBehavior.RaidAnEnemyTerritoryIssue;
using Quest = TaleWorlds.CampaignSystem.Issues.RaidAnEnemyTerritoryIssueBehavior.RaidAnEnemyTerritoryQuest;

namespace E2E.Tests.Services.Issues;

public class RaidAnEnemyTerritoryTests : IDisposable
{
    private readonly E2ETestEnvironment testEnvironment;
    private readonly string giverId;
    private readonly string ownerHeroId;
    private readonly string ownerPartyId;
    private readonly string giverClanId;
    private readonly string giverKingdomId;
    private readonly string enemyKingdomId;
    private readonly string settlementId;
    private readonly string villageId;
    private readonly string boundTownSettlementId;
    private readonly string boundTownId;

    public RaidAnEnemyTerritoryTests(ITestOutputHelper output)
    {
        testEnvironment = new E2ETestEnvironment(output);
        giverId = testEnvironment.CreateRegisteredObject<Hero>();
        ownerHeroId = testEnvironment.CreateRegisteredObject<Hero>();
        ownerPartyId = testEnvironment.CreateRegisteredObject<MobileParty>();
        giverClanId = testEnvironment.CreateRegisteredObject<Clan>();
        giverKingdomId = testEnvironment.CreateRegisteredObject<Kingdom>();
        enemyKingdomId = testEnvironment.CreateRegisteredObject<Kingdom>();
        settlementId = testEnvironment.CreateRegisteredObject<Settlement>();
        villageId = testEnvironment.CreateRegisteredObject<Village>();
        boundTownSettlementId = testEnvironment.CreateRegisteredObject<Settlement>();
        boundTownId = testEnvironment.CreateRegisteredObject<Town>();

        RegisterOwner();
        foreach (var instance in new[] { testEnvironment.Server, testEnvironment.Clients.First() })
        {
            GiveGiverTheIssue(instance);
        }
    }

    public void Dispose() => testEnvironment.Dispose();

    [Fact]
    public void RaidQuest_IsRegisteredOnTheNewFrameworkForEveryPeer()
    {
        foreach (var instance in new[] { testEnvironment.Server }.Concat(testEnvironment.Clients))
        {
            instance.Call(() =>
            {
                Assert.True(instance.Resolve<IQuestTypeRegistry>().TryGetByName(
                    "RaidAnEnemyTerritoryIssue", out var descriptor));
                Assert.NotNull(descriptor.CreationCaptureStrategy);
                Assert.NotNull(descriptor.QuestSolutionAcceptStrategy);
                Assert.NotNull(descriptor.FinalizationProofStrategy);
            });
        }
    }

    [Fact]
    public void RaidCompletedThroughCampaignDispatcher_UpdatesTheOwnersQuestAndLeavesItActive()
    {
        var server = testEnvironment.Server;
        var owner = testEnvironment.Clients.First();

        server.Call(() =>
        {
            Assert.True(server.ObjectManager.TryGetObject<Hero>(giverId, out var giver));
            Assert.True(giver.Issue.StartIssueWithQuest());
        });

        server.Call(() =>
        {
            Assert.True(server.ObjectManager.TryGetObject<Hero>(giverId, out var giver));
            var quest = Assert.IsType<Quest>(giver.Issue.IssueQuest);
            Assert.True(server.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));
            Assert.True(server.ObjectManager.TryGetObject<MobileParty>(ownerPartyId, out var ownerParty));
            settlement.SettlementHitPoints = 0f;

            var mapEvent = GameObjectCreator.CreateInitializedObject<MapEvent>();
            var raid = new RaidEventComponent(mapEvent);
            mapEvent.Initialize(ownerParty.Party, settlement.Party, raid, MapEvent.BattleTypes.Raid);
            mapEvent.MapEventSettlement = settlement;
            mapEvent.State = MapEventState.Wait;
            Campaign.Current.MapEventManager.OnMapEventCreated(mapEvent);

            CampaignEventDispatcher.Instance.RaidCompleted(BattleSideEnum.Attacker, raid);

            Assert.True(quest.IsOngoing);
            Assert.Single(quest._raidedVillages);
            Assert.Contains(settlement, quest._raidedVillages);
        });

        owner.Call(() =>
        {
            Assert.True(owner.ObjectManager.TryGetObject<Hero>(giverId, out var giver));
            var quest = Assert.IsType<Quest>(giver.Issue.IssueQuest);
            Assert.True(quest.IsOngoing);
            Assert.Single(quest._raidedVillages);
        });
    }

    private void RegisterOwner()
    {
        var server = testEnvironment.Server;
        var owner = testEnvironment.Clients.First();
        server.Call(() => Assert.True(server.Resolve<IPlayerManager>().AddPlayer(
            new Player("raid-owner", ownerHeroId, ownerPartyId, "", ""))));
        testEnvironment.ConnectRegisteredPlayer(owner, "raid-owner");
        owner.Resolve<IControllerIdProvider>().SetControllerId("raid-owner");
    }

    private void GiveGiverTheIssue(EnvironmentInstance instance)
    {
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<Hero>(giverId, out var giver));
            Assert.True(instance.ObjectManager.TryGetObject<Hero>(ownerHeroId, out var ownerHero));
            Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(ownerPartyId, out var ownerParty));
            Assert.True(instance.ObjectManager.TryGetObject<Clan>(giverClanId, out var giverClan));
            Assert.True(instance.ObjectManager.TryGetObject<Kingdom>(giverKingdomId, out var giverKingdom));
            Assert.True(instance.ObjectManager.TryGetObject<Kingdom>(enemyKingdomId, out var enemyKingdom));
            Assert.True(instance.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));
            Assert.True(instance.ObjectManager.TryGetObject<Village>(villageId, out var village));
            Assert.True(instance.ObjectManager.TryGetObject<Settlement>(boundTownSettlementId, out var boundTownSettlement));
            Assert.True(instance.ObjectManager.TryGetObject<Town>(boundTownId, out var boundTown));

            using (new AllowedThread())
            {
                giverClan._kingdom = giverKingdom;
                giver.Clan = giverClan;
                giver.Occupation = Occupation.Lord;
                ownerHero.Clan = giverClan;
                ownerHero.PartyBelongedTo = ownerParty;
                boundTownSettlement.SetSettlementComponent(boundTown);
                boundTown._ownerClan = giverClan;
                settlement.SetSettlementComponent(village);
                village.Bound = boundTownSettlement;
                village.VillageState = Village.VillageStates.BeingRaided;

                DeclareWarAction.ApplyInternal(giverKingdom, enemyKingdom, DeclareWarAction.DeclareWarDetail.CausedByKingdomDecision);

                var issue = new Issue(giver)
                {
                    StringId = "issue_raid_enemy_territory_e2e",
                    _issueState = IssueBase.IssueState.Ongoing,
                };
                Campaign.Current.IssueManager._issues.Add(giver, issue);
                giver.OnIssueCreatedForHero(issue);
            }
        });
    }
}
