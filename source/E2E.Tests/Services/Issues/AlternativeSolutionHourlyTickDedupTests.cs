using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Issues.Messages;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using HarmonyLib;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Encyclopedia;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Issues;

public class AlternativeSolutionHourlyTickDedupTests : IDisposable
{
    private E2ETestEnvironment TestEnvironment { get; }
    private EnvironmentInstance Server => TestEnvironment.Server;
    private EnvironmentInstance Client => TestEnvironment.Clients.First();

    public AlternativeSolutionHourlyTickDedupTests(ITestOutputHelper output)
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

    private void CreateIssueOnBothPeers(VillageFixture fixture)
    {
        var generation = 0;
        foreach (var instance in new[] { Server, Client })
        {
            var isServer = instance == Server;
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
                Assert.True(instance.ObjectManager.TryGetObject<ItemObject>(fixture.ItemId, out var requestedItem));

                if (owner.Issue == null)
                {
                    var pid = new PotentialIssueData(
                        (in PotentialIssueData _, Hero h) => new VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssue(h, requestedItem),
                        typeof(VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssue),
                        IssueBase.IssueFrequency.VeryCommon);

                    using (new AllowedThread())
                    {
                        Assert.True(Campaign.Current.IssueManager.CreateNewIssue(in pid, owner));
                    }
                }

                var generationRegistry = instance.Resolve<IIssueGenerationRegistry>();
                if (isServer) generation = generationRegistry.Bump(owner);
                else generationRegistry.SetGeneration(owner, generation);
            });
        }
    }

    private string ConnectPlayer(VillageFixture fixture)
    {
        var controllerId = "player-A-" + Guid.NewGuid();
        var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.CompanionHeroId, out var companion));
            using (new AllowedThread())
            {
                party.MemberRoster.AddToCounts(companion.CharacterObject, 1);
            }

            var playerManager = Server.Resolve<IPlayerManager>();
            Assert.True(playerManager.AddPlayer(new Player(controllerId, fixture.HeroId, partyId, "", "")));
        });
        TestEnvironment.ConnectRegisteredPlayer(Client, controllerId);
        Client.Resolve<IControllerIdProvider>().SetControllerId(controllerId);
        return controllerId;
    }

    [Fact]
    public void OnHourlyTick_OneDueOwnedIssue_SendsExactlyOneCompletionRequest()
    {
        var fixture = SetupVillageOwner();
        CreateIssueOnBothPeers(fixture);
        var controllerId = ConnectPlayer(fixture);

        Client.Call(() =>
        {
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Client.Resolve<IIssueOwnershipRegistry>().SetOwner(owner, controllerId);

            using (new AllowedThread())
            {
                owner.Issue._issueState = IssueBase.IssueState.SolvingWithAlternativeSolution;
                owner.Issue.IsTriedToSolveBefore = true;
                owner.Issue.AlternativeSolutionReturnTimeForTroops = CampaignTime.Now - CampaignTime.Days(1f);
            }

            new IssuesCampaignBehavior().RegisterEvents();
            CampaignEvents.Instance.HourlyTick();
        });

        var requests = Client.NetworkSentMessages.GetMessages<RequestAlternativeSolutionCompletion>();
        Assert.Single(requests);
    }
}
