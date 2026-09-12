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
        var boundSettlementId = TestEnvironment.CreateRegisteredObject<Settlement>();
        var itemId = TestEnvironment.CreateRegisteredObject<ItemObject>();
        var companionHeroId = TestEnvironment.CreateRegisteredObject<Hero>();

        foreach (var instance in new[] { Server, Client })
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(heroId, out var hero));
                Assert.True(instance.ObjectManager.TryGetObject<Village>(villageId, out var village));
                Assert.True(instance.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));
                Assert.True(instance.ObjectManager.TryGetObject<Settlement>(boundSettlementId, out var boundSettlement));
                Assert.True(instance.ObjectManager.TryGetObject<ItemObject>(itemId, out var item));
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(companionHeroId, out var companion));

                using (new AllowedThread())
                {
                    Campaign.Current.EncyclopediaManager ??= new EncyclopediaManager();
                    Campaign.Current.EncyclopediaManager.CreateEncyclopediaPages();

                    settlement.SetSettlementComponent(village);
                    village.Bound = boundSettlement;
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

    private record OwnedIssue(string ControllerId, int Generation);

    private OwnedIssue SetupOwnedIssue(VillageFixture fixture)
    {
        var controllerId = "player-A-" + Guid.NewGuid();
        var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        var generation = 0;

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
            generation = Server.Resolve<IIssueGenerationRegistry>().Bump(owner);
        });

        TestEnvironment.ConnectRegisteredPlayer(Client, controllerId);
        Client.Resolve<GameInterface.Services.Entity.IControllerIdProvider>().SetControllerId(controllerId);

        Client.Call(() =>
        {
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Client.Resolve<IIssueGenerationRegistry>().SetGeneration(owner, generation);
        });

        return new OwnedIssue(controllerId, generation);
    }

    [Fact]
    public void RequestIssueRemoved_ClaimingAlternativeSolutionSuccess_RejectedAsServerOnlyReason()
    {
        var fixture = SetupVillageOwner();
        var owned = SetupOwnedIssue(fixture);

        Client.Call(() =>
        {
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Client.ObjectManager.TryGetId(owner, out var ownerId));

            var network = Client.Resolve<Common.Network.INetwork>();
            network.SendAll(new RequestIssueRemoved(ownerId, IssueFinalizeReason.AlternativeSolutionSuccess, owned.Generation));
        });

        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkIssueRemoved>());
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.NotNull(owner.Issue);
        });
    }

    [Fact]
    public void RequestIssueRemoved_ClaimingIssueOnlyWhileSolvingWithAlternative_Rejected()
    {
        var fixture = SetupVillageOwner();
        var owned = SetupOwnedIssue(fixture);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.CompanionHeroId, out var companion));
            using (new AlternativeSolutionStartAuthorityGuard())
            using (new AllowedThread())
            {
                owner.Issue.AlternativeSolutionSentTroops.AddToCounts(companion.CharacterObject, 1);
                owner.Issue.StartIssueWithAlternativeSolution();
            }
            Assert.True(owner.Issue.IsSolvingWithAlternative);
        });

        Client.Call(() =>
        {
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Client.ObjectManager.TryGetId(owner, out var ownerId));

            var network = Client.Resolve<Common.Network.INetwork>();
            network.SendAll(new RequestIssueRemoved(ownerId, IssueFinalizeReason.IssueOnly, owned.Generation));
        });

        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkIssueRemoved>());
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.NotNull(owner.Issue);
            Assert.True(owner.Issue.IsSolvingWithAlternative);
            Assert.Equal(1, owner.Issue.AlternativeSolutionSentTroops.TotalManCount);
        });
    }

    [Fact]
    public void RequestIssueRemoved_ClaimingUndefinedReasonValueWhileSolvingWithAlternative_Rejected()
    {
        var fixture = SetupVillageOwner();
        var owned = SetupOwnedIssue(fixture);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.CompanionHeroId, out var companion));
            using (new AlternativeSolutionStartAuthorityGuard())
            using (new AllowedThread())
            {
                owner.Issue.AlternativeSolutionSentTroops.AddToCounts(companion.CharacterObject, 1);
                owner.Issue.StartIssueWithAlternativeSolution();
            }
            Assert.True(owner.Issue.IsSolvingWithAlternative);
        });

        Client.Call(() =>
        {
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Client.ObjectManager.TryGetId(owner, out var ownerId));

            var network = Client.Resolve<Common.Network.INetwork>();
            network.SendAll(new RequestIssueRemoved(ownerId, (IssueFinalizeReason)200, owned.Generation));
        });

        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkIssueRemoved>());
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.NotNull(owner.Issue);
            Assert.True(owner.Issue.IsSolvingWithAlternative);
            Assert.Equal(1, owner.Issue.AlternativeSolutionSentTroops.TotalManCount);
        });
    }

    [Fact]
    public void RequestIssueRemoved_ClaimingQuestFailWithNoOngoingQuest_Rejected()
    {
        var fixture = SetupVillageOwner();
        var owned = SetupOwnedIssue(fixture);

        Client.Call(() =>
        {
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Client.ObjectManager.TryGetId(owner, out var ownerId));

            var network = Client.Resolve<Common.Network.INetwork>();
            network.SendAll(new RequestIssueRemoved(ownerId, IssueFinalizeReason.QuestFail, owned.Generation));
        });

        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkIssueRemoved>());
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(owner.Issue.IsOngoingWithoutQuest);
        });
    }

    [Fact]
    public void RequestIssueRemoved_ClaimingQuestCancelForAQuestTypeWithNoRegisteredValidator_Rejected()
    {
        var fixture = SetupVillageOwner();
        var owned = SetupOwnedIssue(fixture);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            using (new QuestSolutionStartAuthorityGuard())
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
            network.SendAll(new RequestIssueRemoved(ownerId, IssueFinalizeReason.QuestCancel, owned.Generation));
        });

        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkIssueRemoved>());
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.NotNull(owner.Issue);
            Assert.True(owner.Issue.IssueQuest.IsOngoing);
        });
    }

    [Fact]
    public void RequestIssueRemoved_ClaimingQuestBetrayalForAQuestTypeWithNoRegisteredValidator_Rejected()
    {
        var fixture = SetupVillageOwner();
        var owned = SetupOwnedIssue(fixture);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            using (new QuestSolutionStartAuthorityGuard())
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
            network.SendAll(new RequestIssueRemoved(ownerId, IssueFinalizeReason.QuestBetrayal, owned.Generation));
        });

        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkIssueRemoved>());
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.NotNull(owner.Issue);
            Assert.True(owner.Issue.IssueQuest.IsOngoing);
        });
    }

    [Fact]
    public void RequestIssueRemoved_ClaimingQuestTimeoutBeforeDueTimeHasPassed_Rejected()
    {
        var fixture = SetupVillageOwner();
        var owned = SetupOwnedIssue(fixture);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            using (new QuestSolutionStartAuthorityGuard())
            using (new AllowedThread())
            {
                owner.Issue.StartIssueWithQuest();
            }
            Assert.True(owner.Issue.IssueQuest.IsOngoing);
            Assert.False(owner.Issue.IssueQuest.QuestDueTime.IsPast);
        });

        Client.Call(() =>
        {
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Client.ObjectManager.TryGetId(owner, out var ownerId));

            var network = Client.Resolve<Common.Network.INetwork>();
            network.SendAll(new RequestIssueRemoved(ownerId, IssueFinalizeReason.QuestTimeout, owned.Generation));
        });

        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkIssueRemoved>());
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.NotNull(owner.Issue);
            Assert.True(owner.Issue.IssueQuest.IsOngoing);
        });
    }

    [Fact]
    public void RequestIssueRemoved_ClaimingQuestSuccessForAQuestTypeWithNoRegisteredValidator_Rejected()
    {
        var fixture = SetupVillageOwner();
        var owned = SetupOwnedIssue(fixture);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            using (new QuestSolutionStartAuthorityGuard())
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
            network.SendAll(new RequestIssueRemoved(ownerId, IssueFinalizeReason.QuestSuccess, owned.Generation));
        });

        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkIssueRemoved>());
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.NotNull(owner.Issue);
            Assert.True(owner.Issue.IssueQuest.IsOngoing);
        });
    }

    [Fact]
    public void RequestIssueRemoved_StaleGeneration_Rejected()
    {
        var fixture = SetupVillageOwner();
        var owned = SetupOwnedIssue(fixture);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            using (new QuestSolutionStartAuthorityGuard())
            using (new AllowedThread())
            {
                owner.Issue.StartIssueWithQuest();
            }
        });

        Client.Call(() =>
        {
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Client.ObjectManager.TryGetId(owner, out var ownerId));

            var network = Client.Resolve<Common.Network.INetwork>();
            network.SendAll(new RequestIssueRemoved(ownerId, IssueFinalizeReason.QuestCancel, owned.Generation - 1));
        });

        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkIssueRemoved>());
    }

    [Fact]
    public void ForgedNetworkIssueRemoved_SentDirectlyToServer_IgnoredInsteadOfFinalizingTheRealIssue()
    {
        var fixture = SetupVillageOwner();
        var owned = SetupOwnedIssue(fixture);

        Client.Call(() =>
        {
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Client.ObjectManager.TryGetId(owner, out var ownerId));

            var network = Client.Resolve<Common.Network.INetwork>();
            network.SendAll(new NetworkIssueRemoved(ownerId, IssueFinalizeReason.IssueOnly));
        });

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.NotNull(owner.Issue);
        });
    }
}
