using Common.Messaging;
using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Issues.Messages;
using GameInterface.Services.Issues.Patches;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using HarmonyLib;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Encyclopedia;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Issues;

public class VillageNeedsToolsIssueTests : IDisposable
{
    private static readonly PropertyInfo ItemValueProperty =
        AccessTools.Property(typeof(ItemObject), nameof(ItemObject.Value));
    private static readonly FieldInfo GameModelsField = AccessTools.Field(typeof(Campaign), "_gameModels");
    private static readonly PropertyInfo CharacterDevelopmentModelProperty =
        AccessTools.Property(typeof(GameModels), nameof(GameModels.CharacterDevelopmentModel));
    private static readonly PropertyInfo PlayerTraitDeveloperProperty =
        AccessTools.Property(typeof(Campaign), nameof(Campaign.PlayerTraitDeveloper));

    private static void InstallCharacterDevelopmentModel()
    {
        var models = (GameModels)GameModelsField.GetValue(Campaign.Current);
        if (models == null)
        {
            models = ObjectHelper.SkipConstructor<GameModels>();
            GameModelsField.SetValue(Campaign.Current, models);
        }

        CharacterDevelopmentModelProperty.SetValue(models, new DefaultCharacterDevelopmentModel());
        PlayerTraitDeveloperProperty.SetValue(Campaign.Current, Campaign.Current.PlayerTraitDeveloper ?? new PropertyOwner<PropertyObject>());
    }

    private E2ETestEnvironment TestEnvironment { get; }
    private EnvironmentInstance Server => TestEnvironment.Server;
    private EnvironmentInstance Client => TestEnvironment.Clients.First();
    private EnvironmentInstance OtherClient => TestEnvironment.Clients.Last();
    private IEnumerable<EnvironmentInstance> AllInstances => new[] { Server }.Concat(TestEnvironment.Clients);

    public VillageNeedsToolsIssueTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
    }

    private void OpenConversation(EnvironmentInstance instance, string ownerId, string controllerId)
    {
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<Hero>(ownerId, out var owner));
            MessageBroker.Instance.Publish(owner, new IssueConversationOpenedLocally(owner, controllerId));
        });
    }

    private record VillageFixture(string HeroId, string VillageId, string SettlementId, string ItemId);

    private VillageFixture SetupVillageOwner(int itemValue = 40)
    {
        var heroId = TestEnvironment.CreateRegisteredObject<Hero>();
        var villageId = TestEnvironment.CreateRegisteredObject<Village>();
        var settlementId = TestEnvironment.CreateRegisteredObject<Settlement>();
        var boundSettlementId = TestEnvironment.CreateRegisteredObject<Settlement>();
        var itemId = TestEnvironment.CreateRegisteredObject<ItemObject>();

        foreach (var instance in AllInstances)
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(heroId, out var hero));
                Assert.True(instance.ObjectManager.TryGetObject<Village>(villageId, out var village));
                Assert.True(instance.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));
                Assert.True(instance.ObjectManager.TryGetObject<Settlement>(boundSettlementId, out var boundSettlement));
                Assert.True(instance.ObjectManager.TryGetObject<ItemObject>(itemId, out var item));

                using (new AllowedThread())
                {
                    Campaign.Current.EncyclopediaManager ??= new EncyclopediaManager();
                    Campaign.Current.EncyclopediaManager.CreateEncyclopediaPages();
                    InstallCharacterDevelopmentModel();

                    settlement.SetSettlementComponent(village);
                    village.Bound = boundSettlement;
                    village.Hearth = 650f;
                    hero.StayingInSettlement = settlement;
                    ItemValueProperty.SetValue(item, itemValue);
                }
            });
        }

        return new VillageFixture(heroId, villageId, settlementId, itemId);
    }

    private void CreateIssueOnServer(VillageFixture fixture)
    {
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Server.ObjectManager.TryGetObject<ItemObject>(fixture.ItemId, out var requestedItem));

            var pid = new PotentialIssueData(
                (in PotentialIssueData _, Hero h) => new VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssue(h, requestedItem),
                typeof(VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssue),
                IssueBase.IssueFrequency.VeryCommon);

            Assert.True(Campaign.Current.IssueManager.CreateNewIssue(in pid, owner));
        });
    }

    [Fact]
    public void GenuineServerCreation_CapturesRolledFieldsAndReplicatesAByteIdenticalIssueToEveryClient()
    {
        var fixture = SetupVillageOwner(itemValue: 40);

        CreateIssueOnServer(fixture);

        var created = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkVillageIssueCreated>());
        Assert.Equal(fixture.HeroId, created.OwnerId);
        Assert.Equal(fixture.ItemId, created.RequestedItemId);
        Assert.Null(created.ExchangeItemId);
        Assert.Equal(0, created.NumberOfExchangeItem);
        Assert.True(created.Payment > 0, "Expected a resolved gold payment for a Hearth >= 300 village");
        Assert.True(created.NumberOfRequestedItem > 0);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            var serverIssue = Assert.IsType<VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssue>(owner.Issue);
            Assert.Equal(created.NumberOfRequestedItem, serverIssue._numberOfRequestedItem);
            Assert.Equal(created.Payment, serverIssue._payment);
        });

        foreach (var client in TestEnvironment.Clients)
        {
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
                var mirrored = Assert.IsType<VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssue>(owner.Issue);

                Assert.True(client.ObjectManager.TryGetObject<ItemObject>(fixture.ItemId, out var requestedItem));
                Assert.Same(requestedItem, mirrored._requestedItem);
                Assert.Null(mirrored._exchangeItem);
                Assert.Equal(created.NumberOfRequestedItem, mirrored._numberOfRequestedItem);
                Assert.Equal(created.NumberOfExchangeItem, mirrored._numberOfExchangeItem);
                Assert.Equal(created.Payment, mirrored._payment);
            });
        }
    }

    [Fact]
    public void ClientOriginatedCreation_IsBlocked_IssueManagerNeverCreatesIt()
    {
        var fixture = SetupVillageOwner();

        Client.Call(() =>
        {
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Client.ObjectManager.TryGetObject<ItemObject>(fixture.ItemId, out var requestedItem));

            var pid = new PotentialIssueData(
                (in PotentialIssueData _, Hero h) => new VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssue(h, requestedItem),
                typeof(VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssue),
                IssueBase.IssueFrequency.VeryCommon);

            Assert.False(Campaign.Current.IssueManager.CreateNewIssue(in pid, owner));
            Assert.Null(owner.Issue);
        });

        Assert.Empty(Client.NetworkSentMessages.GetMessages<NetworkVillageIssueCreated>());
    }

    [Fact]
    public void QuestOwnershipGate_BlocksTurnInForAnyoneOtherThanTheRecordedOwner_EvenWithTheToolsInHand()
    {
        var fixture = SetupVillageOwner();
        CreateIssueOnServer(fixture);
        var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();

        Server.Resolve<IControllerIdProvider>().SetControllerId("host-controller");

        VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssueQuest quest = null;
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Assert.True(Server.ObjectManager.TryGetObject<ItemObject>(fixture.ItemId, out var requestedItem));

            Campaign.Current.MainParty = party;
            using (new QuestSolutionStartAuthorityGuard())
            {
                Assert.True(Campaign.Current.IssueManager.StartIssueQuest(owner));
            }
            quest = Assert.IsType<VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssueQuest>(owner.Issue.IssueQuest);
            Server.Resolve<IIssueOwnershipRegistry>().SetOwner(owner, "host-controller");

            party.ItemRoster.AddToCounts(requestedItem, quest._numberOfRequestedGood);
        });

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Server.Resolve<IIssueOwnershipRegistry>().TryGetOwnerControllerId(owner, out var ownerControllerId));
            Assert.Equal("host-controller", ownerControllerId);
        });

        Server.Call(() => Assert.True(quest.PlayerHasTools()));

        Server.Resolve<IControllerIdProvider>().SetControllerId("someone-else");
        Server.Call(() => Assert.False(quest.PlayerHasTools()));

        Server.Resolve<IControllerIdProvider>().SetControllerId("host-controller");
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Assert.True(Server.ObjectManager.TryGetObject<ItemObject>(fixture.ItemId, out var requestedItem));
            party.ItemRoster.AddToCounts(requestedItem, -quest._numberOfRequestedGood);
        });
        Server.Call(() => Assert.False(quest.PlayerHasTools()));
    }

    [Fact]
    public void SaveTransferJoinScenario_JoiningNonOwnerPeersOwnRealQuest_StillBlockedByTheOwnershipGate()
    {
        var fixture = SetupVillageOwner();
        CreateIssueOnServer(fixture);

        Server.Resolve<IControllerIdProvider>().SetControllerId("host-controller");
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            using (new QuestSolutionStartAuthorityGuard())
            {
                Assert.True(Campaign.Current.IssueManager.StartIssueQuest(owner));
            }
        });

        var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();

        VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssueQuest joiningPeerQuest = null;
        OtherClient.Call(() =>
        {
            Assert.True(OtherClient.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            using (new QuestSolutionStartAuthorityGuard())
            {
                Assert.True(owner.Issue.StartIssueWithQuest());
            }
            joiningPeerQuest = Assert.IsType<VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssueQuest>(owner.Issue.IssueQuest);

            Assert.False(OtherClient.Resolve<IIssueOwnershipRegistry>().TryGetOwnerControllerId(owner, out var recordedOwner) &&
                OtherClient.Resolve<IControllerIdProvider>().ControllerId == recordedOwner);

            Assert.True(OtherClient.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Campaign.Current.MainParty = party;
            Assert.True(OtherClient.ObjectManager.TryGetObject<ItemObject>(fixture.ItemId, out var requestedItem));
            party.ItemRoster.AddToCounts(requestedItem, joiningPeerQuest._numberOfRequestedGood);
        });

        OtherClient.Call(() => Assert.False(joiningPeerQuest.PlayerHasTools()));
    }

    [Fact]
    public void RequestQuestTypeAcceptQuest_FirstRequestWins_SecondIsRejectedAndOwnershipConvergesOnEveryPeer()
    {
        var fixture = SetupVillageOwner();
        CreateIssueOnServer(fixture);

        var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(fixture.SettlementId, out var settlement));
            using (new AllowedThread())
            {
                party.CurrentSettlement = settlement;
            }

            var playerManager = Server.Resolve<IPlayerManager>();
            Assert.True(playerManager.AddPlayer(new Player("player-A", fixture.HeroId, partyId, "", "")));
            Assert.True(playerManager.AddPlayer(new Player("player-B", "", "", "", "")));
        });
        TestEnvironment.ConnectRegisteredPlayer(Client, "player-A");
        TestEnvironment.ConnectRegisteredPlayer(OtherClient, "player-B");
        OpenConversation(Client, fixture.HeroId, "player-A");

        var generation = 0;
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Server.Resolve<IIssueGenerationRegistry>().TryGetGeneration(owner, out generation));
            Server.Resolve<IMessageBroker>().Publish(Client.NetPeer, new RequestQuestTypeAcceptQuest(fixture.HeroId, generation));
        });

        var accepted = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkQuestTypeQuestAccepted>());
        Assert.Equal(fixture.HeroId, accepted.OwnerId);
        Assert.Equal("player-A", accepted.OwnerControllerId);
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkQuestTypeAcceptRejected>());

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.False(owner.Issue.IsOngoingWithoutQuest);
            Assert.IsType<VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssueQuest>(owner.Issue.IssueQuest);
            Assert.True(Server.Resolve<IIssueOwnershipRegistry>().TryGetOwnerControllerId(owner, out var ownerControllerId));
            Assert.Equal("player-A", ownerControllerId);
        });

        OpenConversation(OtherClient, fixture.HeroId, "player-B");
        Server.Call(() =>
        {
            Server.Resolve<IMessageBroker>().Publish(OtherClient.NetPeer, new RequestQuestTypeAcceptQuest(fixture.HeroId, generation));
        });

        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkQuestTypeQuestAccepted>());
        var rejected = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkQuestTypeAcceptRejected>());
        Assert.Equal(fixture.HeroId, rejected.OwnerId);

        Assert.Single(OtherClient.InternalMessages.GetMessages<NetworkQuestTypeAcceptRejected>());
        Assert.Empty(Client.InternalMessages.GetMessages<NetworkQuestTypeAcceptRejected>());

        foreach (var client in TestEnvironment.Clients)
        {
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
                Assert.IsType<VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssueQuest>(owner.Issue.IssueQuest);
                Assert.True(owner.Issue.IsSolvingWithQuest);
                Assert.True(client.Resolve<IIssueOwnershipRegistry>().TryGetOwnerControllerId(owner, out var ownerControllerId));
                Assert.Equal("player-A", ownerControllerId);
            });
        }
    }

    [Fact]
    public void RequestQuestTypeAcceptQuest_FromUnregisteredRequester_IsRejectedWithoutMutatingTheIssue()
    {
        var fixture = SetupVillageOwner();
        CreateIssueOnServer(fixture);

        Server.Call(() =>
        {
            Server.Resolve<IMessageBroker>().Publish(Client.NetPeer, new RequestQuestTypeAcceptQuest(fixture.HeroId, 0));
        });

        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkQuestTypeQuestAccepted>());
        var rejected = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkQuestTypeAcceptRejected>());
        Assert.Equal(fixture.HeroId, rejected.OwnerId);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(owner.Issue.IsOngoingWithoutQuest);
            Assert.False(Server.Resolve<IIssueOwnershipRegistry>().TryGetOwnerControllerId(owner, out _));
        });
    }

    [Fact]
    public void RequestVillageIssueRemoved_FinalizesTheRealQuestAndBroadcastsRemovalToEveryPeer()
    {
        var fixture = SetupVillageOwner();
        CreateIssueOnServer(fixture);

        var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        Server.Call(() =>
        {
            var playerManager = Server.Resolve<IPlayerManager>();
            Assert.True(playerManager.AddPlayer(new Player("player-A", "", partyId, "", "")));
        });
        TestEnvironment.ConnectRegisteredPlayer(Client, "player-A");

        VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssueQuest quest = null;
        int rewardGold = 0;
        int goldBefore = 0;
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            using (new QuestSolutionStartAuthorityGuard())
            {
                Assert.True(Campaign.Current.IssueManager.StartIssueQuest(owner));
            }
            Server.Resolve<IIssueOwnershipRegistry>().SetOwner(owner, "player-A");
            quest = Assert.IsType<VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssueQuest>(owner.Issue.IssueQuest);
            rewardGold = quest.RewardGold;
            Assert.True(rewardGold > 0);
            goldBefore = owner.Gold;
        });

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            using (new AllowedThread())
            {
                party.ItemRoster.AddToCounts(quest._requestedTradeGood, quest._numberOfRequestedGood);
            }
        });

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.NotNull(owner.Issue);
            Assert.IsType<VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssueQuest>(owner.Issue.IssueQuest);
        });
        foreach (var client in TestEnvironment.Clients)
        {
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
                Assert.NotNull(owner.Issue);
                using (new AllowedThread())
                {
                    owner.Issue._issueState = IssueBase.IssueState.SolvingWithQuestSolution;
                }
                Assert.Null(owner.Issue.IssueQuest);
                Assert.True(owner.Issue.IsSolvingWithQuest);
            });
        }

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Server.Resolve<IIssueGenerationRegistry>().TryGetGeneration(owner, out var generation));
            Server.Resolve<IMessageBroker>().Publish(Client.NetPeer,
                new RequestIssueRemoved(fixture.HeroId, IssueFinalizeReason.QuestSuccess, generation));
        });

        var removed = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueRemoved>());
        Assert.Equal(fixture.HeroId, removed.OwnerId);
        Assert.Equal(IssueFinalizeReason.QuestSuccess, removed.Reason);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Assert.Equal(goldBefore + rewardGold, owner.Gold);
            Assert.Equal(0, party.ItemRoster.GetItemNumber(quest._requestedTradeGood));
        });

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
    public void RequestIssueRemoved_QuestCancel_ClaimingTheVillageWasRaided_IsAcceptedWhenGenuinelyRaided()
    {
        var fixture = SetupVillageOwner();
        CreateIssueOnServer(fixture);

        var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        Server.Call(() =>
        {
            var playerManager = Server.Resolve<IPlayerManager>();
            Assert.True(playerManager.AddPlayer(new Player("player-A", fixture.HeroId, partyId, "", "")));
        });
        TestEnvironment.ConnectRegisteredPlayer(Client, "player-A");

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            using (new QuestSolutionStartAuthorityGuard())
            {
                Assert.True(Campaign.Current.IssueManager.StartIssueQuest(owner));
            }
            Server.Resolve<IIssueOwnershipRegistry>().SetOwner(owner, "player-A");
        });

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(fixture.SettlementId, out var settlement));
            using (new AllowedThread())
            {
                settlement.Village.VillageState = Village.VillageStates.Looted;
            }
            Assert.True(settlement.IsRaided);
        });

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Server.Resolve<IIssueGenerationRegistry>().TryGetGeneration(owner, out var generation));
            Server.Resolve<IMessageBroker>().Publish(Client.NetPeer,
                new RequestIssueRemoved(fixture.HeroId, IssueFinalizeReason.QuestCancel, generation));
        });

        var removed = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueRemoved>());
        Assert.Equal(fixture.HeroId, removed.OwnerId);
        Assert.Equal(IssueFinalizeReason.QuestCancel, removed.Reason);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.Null(owner.Issue);
        });
    }

    [Fact]
    public void RequestIssueRemoved_QuestCancel_ClaimingTheVillageWasRaided_IsRejectedWhenNotActuallyRaided()
    {
        var fixture = SetupVillageOwner();
        CreateIssueOnServer(fixture);

        var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        Server.Call(() =>
        {
            var playerManager = Server.Resolve<IPlayerManager>();
            Assert.True(playerManager.AddPlayer(new Player("player-A", fixture.HeroId, partyId, "", "")));
        });
        TestEnvironment.ConnectRegisteredPlayer(Client, "player-A");

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            using (new QuestSolutionStartAuthorityGuard())
            {
                Assert.True(Campaign.Current.IssueManager.StartIssueQuest(owner));
            }
            Server.Resolve<IIssueOwnershipRegistry>().SetOwner(owner, "player-A");
        });

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Server.Resolve<IIssueGenerationRegistry>().TryGetGeneration(owner, out var generation));
            Server.Resolve<IMessageBroker>().Publish(Client.NetPeer,
                new RequestIssueRemoved(fixture.HeroId, IssueFinalizeReason.QuestCancel, generation));
        });

        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkIssueRemoved>());

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.NotNull(owner.Issue);
        });
    }

    [Fact]
    public void RequestIssueRemoved_QuestFail_ClaimingThePlayerDeclaredWar_IsAccepted()
    {
        var fixture = SetupVillageOwner();
        CreateIssueOnServer(fixture);

        var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        Server.Call(() =>
        {
            var playerManager = Server.Resolve<IPlayerManager>();
            Assert.True(playerManager.AddPlayer(new Player("player-A", fixture.HeroId, partyId, "", "")));
        });
        TestEnvironment.ConnectRegisteredPlayer(Client, "player-A");

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            using (new QuestSolutionStartAuthorityGuard())
            {
                Assert.True(Campaign.Current.IssueManager.StartIssueQuest(owner));
            }
            Server.Resolve<IIssueOwnershipRegistry>().SetOwner(owner, "player-A");
        });

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Server.Resolve<IIssueGenerationRegistry>().TryGetGeneration(owner, out var generation));
            Server.Resolve<IMessageBroker>().Publish(Client.NetPeer,
                new RequestIssueRemoved(fixture.HeroId, IssueFinalizeReason.QuestFail, generation));
        });

        var removed = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueRemoved>());
        Assert.Equal(fixture.HeroId, removed.OwnerId);
        Assert.Equal(IssueFinalizeReason.QuestFail, removed.Reason);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.Null(owner.Issue);
        });
    }

    [Fact]
    public void RequestIssueRemoved_ClaimingQuestSuccessWithoutTheRealItems_IsRejected()
    {
        var fixture = SetupVillageOwner();
        CreateIssueOnServer(fixture);

        var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        Server.Call(() =>
        {
            var playerManager = Server.Resolve<IPlayerManager>();
            Assert.True(playerManager.AddPlayer(new Player("player-A", "", partyId, "", "")));
        });
        TestEnvironment.ConnectRegisteredPlayer(Client, "player-A");

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            using (new QuestSolutionStartAuthorityGuard())
            {
                Assert.True(Campaign.Current.IssueManager.StartIssueQuest(owner));
            }
            Server.Resolve<IIssueOwnershipRegistry>().SetOwner(owner, "player-A");
        });

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Server.Resolve<IIssueGenerationRegistry>().TryGetGeneration(owner, out var generation));
            Server.Resolve<IMessageBroker>().Publish(Client.NetPeer,
                new RequestIssueRemoved(fixture.HeroId, IssueFinalizeReason.QuestSuccess, generation));
        });

        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkIssueRemoved>());

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.NotNull(owner.Issue);
            Assert.IsType<VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssueQuest>(owner.Issue.IssueQuest);
        });
    }

    [Fact]
    public void RequestVillageIssueRemoved_WithNoQuestYet_FallsBackToBareIssueFinalizedWithoutOrphaning()
    {
        var fixture = SetupVillageOwner();
        CreateIssueOnServer(fixture);

        Server.Call(() =>
        {
            var playerManager = Server.Resolve<IPlayerManager>();
            Assert.True(playerManager.AddPlayer(new Player("player-A", "", "", "", "")));
        });
        TestEnvironment.ConnectRegisteredPlayer(Client, "player-A");

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Server.Resolve<IIssueOwnershipRegistry>().SetOwner(owner, "player-A");
        });

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Server.Resolve<IIssueGenerationRegistry>().TryGetGeneration(owner, out var generation));
            Server.Resolve<IMessageBroker>().Publish(Client.NetPeer,
                new RequestIssueRemoved(fixture.HeroId, IssueFinalizeReason.IssueOnly, generation));
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

    private record VillageFixtureWithCompanion(string HeroId, string VillageId, string SettlementId, string ItemId, string CompanionHeroId);

    private VillageFixtureWithCompanion SetupVillageOwnerWithCompanion()
    {
        var heroId = TestEnvironment.CreateRegisteredObject<Hero>();
        var villageId = TestEnvironment.CreateRegisteredObject<Village>();
        var settlementId = TestEnvironment.CreateRegisteredObject<Settlement>();
        var itemId = TestEnvironment.CreateRegisteredObject<ItemObject>();
        var companionHeroId = TestEnvironment.CreateRegisteredObject<Hero>();

        foreach (var instance in AllInstances)
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
                    ItemValueProperty.SetValue(item, 40);
                    companion.ChangeState(Hero.CharacterStates.Disabled);
                }
            });
        }

        return new VillageFixtureWithCompanion(heroId, villageId, settlementId, itemId, companionHeroId);
    }

    private void CreateIssueOnServer(VillageFixtureWithCompanion fixture)
    {
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Server.ObjectManager.TryGetObject<ItemObject>(fixture.ItemId, out var requestedItem));

            var pid = new PotentialIssueData(
                (in PotentialIssueData _, Hero h) => new VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssue(h, requestedItem),
                typeof(VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssue),
                IssueBase.IssueFrequency.VeryCommon);

            Assert.True(Campaign.Current.IssueManager.CreateNewIssue(in pid, owner));
        });
    }

    private static readonly MethodInfo OnHourlyTickMethod =
        AccessTools.Method(typeof(VillageNeedsToolsAlternativeSolutionCompletionPatches), "OnHourlyTick");

    [Fact]
    public void OwnedAlternativeSolutionPastDue_HourlyTickReachesGenuineCompletionTriggerForTheRecordedOwner()
    {
        var fixture = SetupVillageOwnerWithCompanion();
        CreateIssueOnServer(fixture);

        var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        var escortTroopId = TestEnvironment.CreateRegisteredObject<CharacterObject>();
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.CompanionHeroId, out var companion));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(fixture.SettlementId, out var settlement));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Server.ObjectManager.TryGetObject<CharacterObject>(escortTroopId, out var escortTroop));
            using (new AllowedThread())
            {
                party.MemberRoster.AddToCounts(companion.CharacterObject, 1);
                escortTroop.Level = 15;
                party.MemberRoster.AddToCounts(escortTroop, 20);
                party.CurrentSettlement = settlement;
                owner.Gold = 1000000;
            }

            var playerManager = Server.Resolve<IPlayerManager>();
            Assert.True(playerManager.AddPlayer(new Player("player-A", fixture.HeroId, partyId, "", "")));
        });
        TestEnvironment.ConnectRegisteredPlayer(Client, "player-A");
        Client.Resolve<IControllerIdProvider>().SetControllerId("player-A");
        OpenConversation(Client, fixture.HeroId, "player-A");

        Client.Call(() =>
        {
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(fixture.CompanionHeroId, out var companion));
            Assert.True(Client.ObjectManager.TryGetObject<CharacterObject>(escortTroopId, out var escortTroop));
            using (new AllowedThread())
            {
                owner.Issue.AlternativeSolutionSentTroops.AddToCounts(companion.CharacterObject, 1);
                owner.Issue.AlternativeSolutionSentTroops.AddToCounts(escortTroop, 20);
            }
            owner.Issue.StartIssueWithAlternativeSolution();
        });

        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkQuestTypeAcceptRejected>());

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Server.Resolve<IIssueOwnershipRegistry>().TryGetOwnerControllerId(owner, out var ownerControllerId));
            Assert.Equal("player-A", ownerControllerId);
        });

        Client.Call(() =>
        {
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(owner.Issue.IsSolvingWithAlternative);

            owner.Issue.AlternativeSolutionReturnTimeForTroops = default;
            Assert.True(owner.Issue.AlternativeSolutionReturnTimeForTroops.IsPast);

            OnHourlyTickMethod.Invoke(null, null);
        });

        var request = Assert.Single(Client.NetworkSentMessages.GetMessages<RequestAlternativeSolutionCompletion>());
        Assert.Equal(fixture.HeroId, request.OwnerId);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            owner.Issue.AlternativeSolutionReturnTimeForTroops = default;
        });

        Server.Call(() =>
        {
            Server.Resolve<IMessageBroker>().Publish(Client.NetPeer, request);
        });

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.Null(owner.Issue);
        });
    }

    [Fact]
    public void RequestGenericIssueAcceptAlternative_ClampsClaimedTroopsToTheRequesterSRealPartyRoster()
    {
        var fixture = SetupVillageOwnerWithCompanion();
        CreateIssueOnServer(fixture);

        var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        var escortTroopId = TestEnvironment.CreateRegisteredObject<CharacterObject>();
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.CompanionHeroId, out var companion));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(fixture.SettlementId, out var settlement));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Server.ObjectManager.TryGetObject<CharacterObject>(escortTroopId, out var escortTroop));
            using (new AllowedThread())
            {
                party.MemberRoster.AddToCounts(companion.CharacterObject, 1);
                escortTroop.Level = 15;
                party.MemberRoster.AddToCounts(escortTroop, 20);
                party.CurrentSettlement = settlement;
                owner.Gold = 1000000;
            }

            var playerManager = Server.Resolve<IPlayerManager>();
            Assert.True(playerManager.AddPlayer(new Player("player-A", fixture.HeroId, partyId, "", "")));
        });
        TestEnvironment.ConnectRegisteredPlayer(Client, "player-A");
        Client.Resolve<IControllerIdProvider>().SetControllerId("player-A");
        OpenConversation(Client, fixture.HeroId, "player-A");

        Client.Call(() =>
        {
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(fixture.CompanionHeroId, out var companion));
            Assert.True(Client.ObjectManager.TryGetObject<CharacterObject>(escortTroopId, out var escortTroop));
            using (new AllowedThread())
            {
                owner.Issue.AlternativeSolutionSentTroops.AddToCounts(companion.CharacterObject, 5);
                owner.Issue.AlternativeSolutionSentTroops.AddToCounts(escortTroop, 20);
            }
            owner.Issue.StartIssueWithAlternativeSolution();
        });

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Server.Resolve<IIssueOwnershipRegistry>().TryGetOwnerControllerId(owner, out var ownerControllerId));
            Assert.Equal("player-A", ownerControllerId);
            Assert.Equal(1, owner.Issue.AlternativeSolutionSentTroops.TotalHeroes);
            Assert.Equal(21, owner.Issue.AlternativeSolutionSentTroops.TotalManCount);
        });
    }

    [Fact]
    public void NonOwnerPastDueAlternativeSolution_HourlyTickNeverTriggersCompletion()
    {
        var fixture = SetupVillageOwnerWithCompanion();
        CreateIssueOnServer(fixture);

        var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        var escortTroopId = TestEnvironment.CreateRegisteredObject<CharacterObject>();
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(fixture.SettlementId, out var settlement));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.CompanionHeroId, out var companion));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Server.ObjectManager.TryGetObject<CharacterObject>(escortTroopId, out var escortTroop));
            using (new AllowedThread())
            {
                party.MemberRoster.AddToCounts(companion.CharacterObject, 1);
                escortTroop.Level = 15;
                party.MemberRoster.AddToCounts(escortTroop, 20);
                party.CurrentSettlement = settlement;
                owner.Gold = 1000000;
            }

            var playerManager = Server.Resolve<IPlayerManager>();
            Assert.True(playerManager.AddPlayer(new Player("player-A", fixture.HeroId, partyId, "", "")));
        });
        TestEnvironment.ConnectRegisteredPlayer(Client, "player-A");
        Client.Resolve<IControllerIdProvider>().SetControllerId("player-A");
        OpenConversation(Client, fixture.HeroId, "player-A");

        Client.Call(() =>
        {
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(fixture.CompanionHeroId, out var companion));
            Assert.True(Client.ObjectManager.TryGetObject<CharacterObject>(escortTroopId, out var escortTroop));
            Assert.NotNull(owner.Issue);
            using (new AllowedThread())
            {
                owner.Issue.AlternativeSolutionSentTroops.AddToCounts(companion.CharacterObject, 1);
                owner.Issue.AlternativeSolutionSentTroops.AddToCounts(escortTroop, 20);
            }
            owner.Issue.StartIssueWithAlternativeSolution();
        });

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(owner.Issue.IsSolvingWithAlternative);
            Assert.False(Server.Resolve<IIssueOwnershipRegistry>().IsLocalPeerOwner(owner));

            OnHourlyTickMethod.Invoke(null, null);

            Assert.NotNull(owner.Issue);
            Assert.True(owner.Issue.IsSolvingWithAlternative);
        });

        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkIssueRemoved>());
    }
}
