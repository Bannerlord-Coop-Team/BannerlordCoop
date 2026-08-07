using Common.Messaging;
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
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encyclopedia;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Issues;

public class VillageNeedsCraftingMaterialsIssueTests : IDisposable
{
    private static readonly PropertyInfo PlayerProgressProperty =
        AccessTools.Property(typeof(Campaign), nameof(Campaign.PlayerProgress));

    private E2ETestEnvironment TestEnvironment { get; }
    private EnvironmentInstance Server => TestEnvironment.Server;
    private EnvironmentInstance Client => TestEnvironment.Clients.First();
    private EnvironmentInstance OtherClient => TestEnvironment.Clients.Last();
    private IEnumerable<EnvironmentInstance> AllInstances => new[] { Server }.Concat(TestEnvironment.Clients);

    public VillageNeedsCraftingMaterialsIssueTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
    }

    private record CraftingFixture(string HeroId, string SettlementId);

    private void RegisterDefaultCraftingMaterialItemsOnClients()
    {
        foreach (var client in TestEnvironment.Clients)
        {
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.AddExisting(DefaultItems.IronIngot1.StringId, DefaultItems.IronIngot1));
                Assert.True(client.ObjectManager.AddExisting(DefaultItems.IronIngot2.StringId, DefaultItems.IronIngot2));
            });
        }
    }

    private CraftingFixture SetupIssueOwner()
    {
        RegisterDefaultCraftingMaterialItemsOnClients();

        var heroId = TestEnvironment.CreateRegisteredObject<Hero>();
        var settlementId = TestEnvironment.CreateRegisteredObject<Settlement>();

        foreach (var instance in AllInstances)
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(heroId, out var hero));
                Assert.True(instance.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));

                using (new AllowedThread())
                {
                    Campaign.Current.EncyclopediaManager ??= new EncyclopediaManager();
                    Campaign.Current.EncyclopediaManager.CreateEncyclopediaPages();

                    hero.StayingInSettlement = settlement;
                }
            });
        }

        return new CraftingFixture(heroId, settlementId);
    }

    private void ForcePromisedPayment(EnvironmentInstance instance, string ownerId, int payment)
    {
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<Hero>(ownerId, out var owner));
            var issue = Assert.IsType<VillageNeedsCraftingMaterialsIssueBehavior.VillageNeedsCraftingMaterialsIssue>(owner.Issue);

            using (new AllowedThread())
            {
                issue._promisedPayment = payment;
            }
        });
    }

    private void ForcePromisedPaymentEverywhere(string ownerId, int payment = 500)
    {
        foreach (var instance in AllInstances)
        {
            ForcePromisedPayment(instance, ownerId, payment);
        }
    }

    private void CreateIssueOnServer(string ownerId)
    {
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(ownerId, out var owner));

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

            Assert.True(Campaign.Current.IssueManager.CreateNewIssue(in pid, owner));
        });
    }

    [Fact]
    public void GenuineServerCreation_CapturesTheRolledRequestedItemAndReplicatesAByteIdenticalIssueToEveryClient()
    {
        var fixture = SetupIssueOwner();

        CreateIssueOnServer(fixture.HeroId);

        var created = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkVillageCraftingIssueCreated>());
        Assert.Equal(fixture.HeroId, created.OwnerId);
        Assert.True(created.RequestedItemId is "ironIngot1" or "ironIngot2",
            $"Expected one of the two real SelectCraftingMaterial() variants, got {created.RequestedItemId}");

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            var serverIssue = Assert.IsType<VillageNeedsCraftingMaterialsIssueBehavior.VillageNeedsCraftingMaterialsIssue>(owner.Issue);
            Assert.True(Server.ObjectManager.TryGetId(serverIssue._requestedItem, out var serverItemId));
            Assert.Equal(created.RequestedItemId, serverItemId);
        });

        foreach (var client in TestEnvironment.Clients)
        {
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
                var mirrored = Assert.IsType<VillageNeedsCraftingMaterialsIssueBehavior.VillageNeedsCraftingMaterialsIssue>(owner.Issue);

                Assert.True(client.ObjectManager.TryGetObject<ItemObject>(created.RequestedItemId, out var requestedItem));
                Assert.Same(requestedItem, mirrored._requestedItem);
            });
        }
    }

    [Fact]
    public void ClientOriginatedCreation_IsBlocked_IssueManagerNeverCreatesIt()
    {
        var fixture = SetupIssueOwner();

        Client.Call(() =>
        {
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));

            var pid = new PotentialIssueData(
                (in PotentialIssueData _, Hero h) => new VillageNeedsCraftingMaterialsIssueBehavior.VillageNeedsCraftingMaterialsIssue(h),
                typeof(VillageNeedsCraftingMaterialsIssueBehavior.VillageNeedsCraftingMaterialsIssue),
                IssueBase.IssueFrequency.Rare);

            Assert.False(Campaign.Current.IssueManager.CreateNewIssue(in pid, owner));
            Assert.Null(owner.Issue);
        });

        Assert.Empty(Client.NetworkSentMessages.GetMessages<NetworkVillageCraftingIssueCreated>());
    }

    [Fact]
    public void QuestOwnershipGate_BlocksTurnInForAnyoneOtherThanTheRecordedOwner_EvenWithTheMaterialsInHand()
    {
        var fixture = SetupIssueOwner();
        CreateIssueOnServer(fixture.HeroId);
        ForcePromisedPaymentEverywhere(fixture.HeroId);
        var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();

        Server.Resolve<IControllerIdProvider>().SetControllerId("host-controller");

        VillageNeedsCraftingMaterialsIssueBehavior.VillageNeedsCraftingMaterialsIssueQuest quest = null;
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));

            Assert.True(Campaign.Current.IssueManager.StartIssueQuest(owner));
            quest = Assert.IsType<VillageNeedsCraftingMaterialsIssueBehavior.VillageNeedsCraftingMaterialsIssueQuest>(owner.Issue.IssueQuest);
        });

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Campaign.Current.MainParty = party;

            party.ItemRoster.AddToCounts(quest._requestedItem, quest._requestedItemAmount);

            quest.QuestAcceptedConsequences();
        });

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(IssueOwnershipRegistry.TryGetOwnerControllerId(owner, out var ownerControllerId));
            Assert.Equal("host-controller", ownerControllerId);
        });

        Server.Call(() =>
        {
            Assert.True(quest.CompleteQuestClickableConditions(out var explanation));
            Assert.Null(explanation);
        });

        Server.Resolve<IControllerIdProvider>().SetControllerId("someone-else");
        Server.Call(() =>
        {
            Assert.False(quest.CompleteQuestClickableConditions(out var explanation));
            Assert.NotNull(explanation);
        });

        Server.Resolve<IControllerIdProvider>().SetControllerId("host-controller");
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            party.ItemRoster.AddToCounts(quest._requestedItem, -quest._requestedItemAmount);
            quest.UpdateQuestLog();
        });
        Server.Call(() =>
        {
            Assert.False(quest.CompleteQuestClickableConditions(out var explanation));
            Assert.NotNull(explanation);
        });
    }

    [Fact]
    public void RemoteClientAccept_ForceCorrectsQuantityAndRewardOnEveryPeer_IncludingTheAccepterItself_WhenIssueDifficultyMultiplierDiverges()
    {
        var fixture = SetupIssueOwner();
        CreateIssueOnServer(fixture.HeroId);

        Server.Call(() =>
        {
            var playerManager = Server.Resolve<IPlayerManager>();
            Assert.True(playerManager.AddPlayer(new Player("player-A", "", "", "", "")));
        });
        TestEnvironment.ConnectRegisteredPlayer(Client, "player-A");

        Server.Call(() => PlayerProgressProperty.SetValue(Campaign.Current, 1.0f));
        Client.Call(() => PlayerProgressProperty.SetValue(Campaign.Current, 0.1f));
        OtherClient.Call(() => PlayerProgressProperty.SetValue(Campaign.Current, 0.55f));

        ForcePromisedPayment(Server, fixture.HeroId, 2000);
        ForcePromisedPayment(Client, fixture.HeroId, 500);
        ForcePromisedPayment(OtherClient, fixture.HeroId, 800);

        Client.Call(() =>
        {
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Campaign.Current.IssueManager.StartIssueQuest(owner));
        });

        var clientTriggered = Assert.Single(Client.InternalMessages.GetMessages<VillageCraftingIssueQuestAcceptTriggered>());
        Assert.True(clientTriggered.RequestedItemAmount > 0);

        var accepted = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkVillageCraftingIssueQuestAccepted>());
        Assert.Equal(fixture.HeroId, accepted.OwnerId);
        Assert.Equal("player-A", accepted.OwnerControllerId);
        Assert.NotEqual(clientTriggered.RequestedItemAmount, accepted.RequestedItemAmount);
        Assert.NotEqual(clientTriggered.RewardGold, accepted.RewardGold);

        foreach (var instance in AllInstances)
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
                var quest = Assert.IsType<VillageNeedsCraftingMaterialsIssueBehavior.VillageNeedsCraftingMaterialsIssueQuest>(owner.Issue.IssueQuest);
                Assert.Equal(accepted.RequestedItemAmount, quest._requestedItemAmount);
                Assert.Equal(accepted.RewardGold, quest.RewardGold);
            });
        }

        foreach (var instance in AllInstances)
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
                Assert.True(IssueOwnershipRegistry.TryGetOwnerControllerId(owner, out var ownerControllerId));
                Assert.Equal("player-A", ownerControllerId);
            });
        }
    }

    [Fact]
    public void RequestVillageCraftingIssueAcceptQuest_FirstRequestWins_SecondIsRejectedAndOwnershipConvergesOnEveryPeer()
    {
        var fixture = SetupIssueOwner();
        CreateIssueOnServer(fixture.HeroId);
        ForcePromisedPaymentEverywhere(fixture.HeroId);

        Server.Call(() =>
        {
            var playerManager = Server.Resolve<IPlayerManager>();
            Assert.True(playerManager.AddPlayer(new Player("player-A", "", "", "", "")));
            Assert.True(playerManager.AddPlayer(new Player("player-B", "", "", "", "")));
        });
        TestEnvironment.ConnectRegisteredPlayer(Client, "player-A");
        TestEnvironment.ConnectRegisteredPlayer(OtherClient, "player-B");

        var generation = 0;
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(IssueGenerationRegistry.TryGetGeneration(owner, out generation));
            Server.Resolve<IMessageBroker>().Publish(Client.NetPeer, new RequestVillageCraftingIssueAcceptQuest(fixture.HeroId, generation));
        });

        var accepted = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkVillageCraftingIssueQuestAccepted>());
        Assert.Equal(fixture.HeroId, accepted.OwnerId);
        Assert.Equal("player-A", accepted.OwnerControllerId);
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkVillageCraftingIssueAcceptRejected>());

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.False(owner.Issue.IsOngoingWithoutQuest);
            Assert.IsType<VillageNeedsCraftingMaterialsIssueBehavior.VillageNeedsCraftingMaterialsIssueQuest>(owner.Issue.IssueQuest);
            Assert.True(IssueOwnershipRegistry.TryGetOwnerControllerId(owner, out var ownerControllerId));
            Assert.Equal("player-A", ownerControllerId);
        });

        Server.Call(() =>
        {
            Server.Resolve<IMessageBroker>().Publish(OtherClient.NetPeer, new RequestVillageCraftingIssueAcceptQuest(fixture.HeroId, generation));
        });

        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkVillageCraftingIssueQuestAccepted>());
        var rejected = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkVillageCraftingIssueAcceptRejected>());
        Assert.Equal(fixture.HeroId, rejected.OwnerId);

        Assert.Single(OtherClient.InternalMessages.GetMessages<NetworkVillageCraftingIssueAcceptRejected>());
        Assert.Empty(Client.InternalMessages.GetMessages<NetworkVillageCraftingIssueAcceptRejected>());

        foreach (var client in TestEnvironment.Clients)
        {
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
                Assert.IsType<VillageNeedsCraftingMaterialsIssueBehavior.VillageNeedsCraftingMaterialsIssueQuest>(owner.Issue.IssueQuest);
                Assert.True(IssueOwnershipRegistry.TryGetOwnerControllerId(owner, out var ownerControllerId));
                Assert.Equal("player-A", ownerControllerId);
            });
        }
    }

    [Fact]
    public void RequestVillageCraftingIssueAcceptQuest_FromUnregisteredRequester_IsRejectedWithoutMutatingTheIssue()
    {
        var fixture = SetupIssueOwner();
        CreateIssueOnServer(fixture.HeroId);

        Server.Call(() =>
        {
            Server.Resolve<IMessageBroker>().Publish(Client.NetPeer, new RequestVillageCraftingIssueAcceptQuest(fixture.HeroId, 0));
        });

        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkVillageCraftingIssueQuestAccepted>());
        var rejected = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkVillageCraftingIssueAcceptRejected>());
        Assert.Equal(fixture.HeroId, rejected.OwnerId);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(owner.Issue.IsOngoingWithoutQuest);
            Assert.False(IssueOwnershipRegistry.TryGetOwnerControllerId(owner, out _));
        });
    }

    [Fact]
    public void RequestVillageIssueRemoved_FinalizesTheRealQuestAndBroadcastsRemovalToEveryPeer()
    {
        var fixture = SetupIssueOwner();
        CreateIssueOnServer(fixture.HeroId);
        ForcePromisedPaymentEverywhere(fixture.HeroId);

        Server.Call(() =>
        {
            var playerManager = Server.Resolve<IPlayerManager>();
            Assert.True(playerManager.AddPlayer(new Player("player-A", "", "", "", "")));
        });
        TestEnvironment.ConnectRegisteredPlayer(Client, "player-A");

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Campaign.Current.IssueManager.StartIssueQuest(owner));
            IssueOwnershipRegistry.SetOwner(owner, "player-A");
        });

        foreach (var instance in AllInstances)
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
                Assert.NotNull(owner.Issue);
                Assert.IsType<VillageNeedsCraftingMaterialsIssueBehavior.VillageNeedsCraftingMaterialsIssueQuest>(owner.Issue.IssueQuest);
            });
        }

        Server.Call(() =>
        {
            Server.Resolve<IMessageBroker>().Publish(Client.NetPeer,
                new RequestIssueRemoved(fixture.HeroId, IssueFinalizeReason.QuestSuccess));
        });

        var removed = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueRemoved>());
        Assert.Equal(fixture.HeroId, removed.OwnerId);
        Assert.Equal(IssueFinalizeReason.QuestSuccess, removed.Reason);

        foreach (var instance in AllInstances)
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
                Assert.Null(owner.Issue);
                Assert.False(Campaign.Current.IssueManager.Issues.ContainsKey(owner));
            });
        }
    }

    [Fact]
    public void RequestVillageIssueRemoved_WithNoQuestYet_FallsBackToBareIssueFinalizedWithoutOrphaning()
    {
        var fixture = SetupIssueOwner();
        CreateIssueOnServer(fixture.HeroId);

        Server.Call(() =>
        {
            var playerManager = Server.Resolve<IPlayerManager>();
            Assert.True(playerManager.AddPlayer(new Player("player-A", "", "", "", "")));
        });
        TestEnvironment.ConnectRegisteredPlayer(Client, "player-A");

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            IssueOwnershipRegistry.SetOwner(owner, "player-A");
        });

        Server.Call(() =>
        {
            Server.Resolve<IMessageBroker>().Publish(Client.NetPeer,
                new RequestIssueRemoved(fixture.HeroId, IssueFinalizeReason.IssueOnly));
        });

        var removed = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueRemoved>());
        Assert.Equal(IssueFinalizeReason.IssueOnly, removed.Reason);

        foreach (var instance in AllInstances)
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
                Assert.Null(owner.Issue);
            });
        }
    }
}
