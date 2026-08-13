using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Messages;
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

public class IssueFinalizationSecurityTests : IDisposable
{
    private E2ETestEnvironment TestEnvironment { get; }
    private EnvironmentInstance Server => TestEnvironment.Server;
    private EnvironmentInstance Client => TestEnvironment.Clients.First();

    public IssueFinalizationSecurityTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
    }

    private record VillageFixture(string HeroId, string VillageId, string SettlementId, string ItemId, string CompanionHeroId);

    private VillageFixture SetupVillageOwner()
    {
        var heroId = TestEnvironment.CreateRegisteredObject<Hero>();
        var villageId = TestEnvironment.CreateRegisteredObject<Village>();
        var settlementId = TestEnvironment.CreateRegisteredObject<Settlement>();
        var itemId = TestEnvironment.CreateRegisteredObject<ItemObject>();
        var companionHeroId = TestEnvironment.CreateRegisteredObject<Hero>();

        foreach (var instance in new[] { Server, Client })
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(heroId, out var hero));
                Assert.True(instance.ObjectManager.TryGetObject<Village>(villageId, out var village));
                Assert.True(instance.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));
                Assert.True(instance.ObjectManager.TryGetObject<ItemObject>(itemId, out var item));
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(companionHeroId, out var companion));

                using (new AllowedThread())
                {
                    Campaign.Current.EncyclopediaManager ??= new EncyclopediaManager();
                    Campaign.Current.EncyclopediaManager.CreateEncyclopediaPages();

                    settlement.SetSettlementComponent(village);
                    village.Bound = settlement;
                    village.Hearth = 650f;
                    hero.StayingInSettlement = settlement;
                    hero.Occupation = Occupation.RuralNotable;
                    AccessTools.Property(typeof(ItemObject), nameof(ItemObject.Value)).SetValue(item, 40);
                    companion.ChangeState(Hero.CharacterStates.Disabled);
                }
            });
        }

        return new VillageFixture(heroId, villageId, settlementId, itemId, companionHeroId);
    }

    private string SetupOwnedIssue(VillageFixture fixture)
    {
        var controllerId = "player-A-" + Guid.NewGuid();
        var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Server.ObjectManager.TryGetObject<ItemObject>(fixture.ItemId, out var requestedItem));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));

            var pid = new PotentialIssueData(
                (in PotentialIssueData _, Hero h) => new VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssue(h, requestedItem),
                typeof(VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssue),
                IssueBase.IssueFrequency.VeryCommon);

            using (new AllowedThread())
            {
                Assert.True(Campaign.Current.IssueManager.CreateNewIssue(in pid, owner));
            }

            var playerManager = Server.Resolve<IPlayerManager>();
            Assert.True(playerManager.AddPlayer(new Player(controllerId, fixture.HeroId, partyId, "", "")));
            Server.Resolve<IIssueOwnershipRegistry>().SetOwner(owner, controllerId);
        });

        TestEnvironment.ConnectRegisteredPlayer(Client, controllerId);
        Client.Resolve<GameInterface.Services.Entity.IControllerIdProvider>().SetControllerId(controllerId);

        return controllerId;
    }

    [Fact]
    public void RequestIssueRemoved_ClaimingAlternativeSolutionSuccess_RejectedAsServerOnlyReason()
    {
        var fixture = SetupVillageOwner();
        var controllerId = SetupOwnedIssue(fixture);

        Client.Call(() =>
        {
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Client.ObjectManager.TryGetId(owner, out var ownerId));

            var network = Client.Resolve<Common.Network.INetwork>();
            network.SendAll(new RequestIssueRemoved(ownerId, IssueFinalizeReason.AlternativeSolutionSuccess));
        });

        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkIssueRemoved>());
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.NotNull(owner.Issue);
        });
    }

    [Fact]
    public void RequestIssueRemoved_ClaimingQuestFailWithNoOngoingQuest_Rejected()
    {
        var fixture = SetupVillageOwner();
        var controllerId = SetupOwnedIssue(fixture);

        Client.Call(() =>
        {
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Client.ObjectManager.TryGetId(owner, out var ownerId));

            var network = Client.Resolve<Common.Network.INetwork>();
            network.SendAll(new RequestIssueRemoved(ownerId, IssueFinalizeReason.QuestFail));
        });

        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkIssueRemoved>());
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(owner.Issue.IsOngoingWithoutQuest);
        });
    }

    [Fact]
    public void RequestIssueRemoved_ClaimingQuestCancelWithARealOngoingQuest_Finalizes()
    {
        var fixture = SetupVillageOwner();
        var controllerId = SetupOwnedIssue(fixture);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            using (new AllowedThread())
            {
                owner.Issue.StartIssueWithQuest();
            }
            Assert.True(owner.Issue.IssueQuest.IsOngoing);
        });

        Client.Call(() =>
        {
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Client.ObjectManager.TryGetId(owner, out var ownerId));

            var network = Client.Resolve<Common.Network.INetwork>();
            network.SendAll(new RequestIssueRemoved(ownerId, IssueFinalizeReason.QuestCancel));
        });

        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueRemoved>());
    }
}
