using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Generic.AcceptMirror;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Issues.Messages;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using HarmonyLib;
using Helpers;
using Moq;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encyclopedia;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Issues;

public class GenericQuestTypeAcceptSecurityTests : IDisposable
{
    private E2ETestEnvironment TestEnvironment { get; }
    private EnvironmentInstance Server => TestEnvironment.Server;
    private EnvironmentInstance Client => TestEnvironment.Clients.First();

    private static readonly Type TestIssueType = typeof(VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssue);

    private string lastConnectedEligibleTroopId;
    private string lastConnectedPartyId;
    private QuestTypeDescriptor previousDescriptor;

    public GenericQuestTypeAcceptSecurityTests(ITestOutputHelper output)
    {
        TestQuestTypeFixture.EnsureVillageNeedsToolsRegistered();
        TestEnvironment = new E2ETestEnvironment(output);
    }

    public void Dispose()
    {
        if (previousDescriptor != null) QuestTypeRegistry.Register(previousDescriptor);
        TestEnvironment.Dispose();
    }

    private record VillageFixture(string HeroId, string VillageId, string SettlementId, string ItemId, string CompanionHeroId);

    private static PartyScreenLogic CreateQuestSelectionScreen(TroopRoster roster, MobileParty party)
    {
        var screen = new PartyScreenLogic();
        screen._partyScreenMode = PartyScreenHelper.PartyScreenMode.QuestTroopManage;
        screen.MemberRosters[(int)PartyScreenLogic.PartyRosterSide.Left] = roster;
        screen.MemberRosters[(int)PartyScreenLogic.PartyRosterSide.Right] = party.MemberRoster;
        screen.CurrentData.LeftMemberRoster = roster;
        screen.CurrentData.RightMemberRoster = party.MemberRoster;
        screen.CurrentData.LeftPrisonerRoster = TroopRoster.CreateDummyTroopRoster();
        screen.CurrentData.RightPrisonerRoster = TroopRoster.CreateDummyTroopRoster();
        screen._initialData.LeftMemberRoster = TroopRoster.CreateDummyTroopRoster();
        screen._initialData.RightMemberRoster = party.MemberRoster.CloneRosterData();
        screen._initialData.LeftPrisonerRoster = TroopRoster.CreateDummyTroopRoster();
        screen._initialData.RightPrisonerRoster = TroopRoster.CreateDummyTroopRoster();
        return screen;
    }

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
        var eligibleTroopId = TestEnvironment.CreateRegisteredObject<CharacterObject>();
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.CompanionHeroId, out var companion));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(fixture.SettlementId, out var settlement));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Server.ObjectManager.TryGetObject<CharacterObject>(eligibleTroopId, out var eligibleTroop));
            using (new AllowedThread())
            {
                eligibleTroop.Level = 20;
                party.MemberRoster.AddToCounts(companion.CharacterObject, 1);
                party.MemberRoster.AddToCounts(eligibleTroop, 6);
                party.CurrentSettlement = settlement;
                owner.Gold = 1000000;
            }

            var playerManager = Server.Resolve<IPlayerManager>();
            Assert.True(playerManager.AddPlayer(new Player(controllerId, fixture.HeroId, partyId, "", "")));
        });
        lastConnectedEligibleTroopId = eligibleTroopId;
        lastConnectedPartyId = partyId;
        TestEnvironment.ConnectRegisteredPlayer(Client, controllerId);
        Client.Resolve<IControllerIdProvider>().SetControllerId(controllerId);
        return controllerId;
    }

    private void OpenConversation(VillageFixture fixture, string controllerId)
    {
        Client.Call(() =>
        {
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Common.Messaging.MessageBroker.Instance.Publish(owner, new IssueConversationOpenedLocally(owner, controllerId));
        });
    }

    private string ConnectPlayerAwayFromIssueGiver(VillageFixture fixture)
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
    public void RequestQuestTypeAcceptQuest_PeerNeverOpenedConversation_RejectedDespiteFreshGeneration()
    {
        var fixture = SetupVillageOwner();
        CreateIssueOnBothPeers(fixture);
        var controllerId = ConnectPlayer(fixture);

        Client.Call(() =>
        {
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Client.ObjectManager.TryGetId(owner, out var ownerId));
            Assert.True(Client.Resolve<IIssueGenerationRegistry>().TryGetGeneration(owner, out var generation));

            var network = Client.Resolve<Common.Network.INetwork>();
            network.SendAll(new RequestQuestTypeAcceptQuest(ownerId, generation));
        });

        var rejection = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkQuestTypeAcceptRejected>());
        Assert.False(rejection.IsAlternative);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Server.ObjectManager.TryGetId(owner, out var ownerId));
            Assert.Equal(ownerId, rejection.OwnerId);
            Assert.False(Server.Resolve<IIssueOwnershipRegistry>().TryGetOwnerControllerId(owner, out _));
        });
    }

    [Fact]
    public void RequestIssueConversationOpened_PeerNotPresentAtIssueGiversSettlement_DeniedAndAcceptRejected()
    {
        var fixture = SetupVillageOwner();
        CreateIssueOnBothPeers(fixture);
        var controllerId = ConnectPlayerAwayFromIssueGiver(fixture);

        OpenConversation(fixture, controllerId);

        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueConversationDenied>());
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkIssueConversationAllowed>());

        Client.Call(() =>
        {
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Client.ObjectManager.TryGetId(owner, out var ownerId));
            Assert.True(Client.Resolve<IIssueGenerationRegistry>().TryGetGeneration(owner, out var generation));

            var network = Client.Resolve<Common.Network.INetwork>();
            network.SendAll(new RequestQuestTypeAcceptQuest(ownerId, generation));
        });

        var rejection = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkQuestTypeAcceptRejected>());
        Assert.False(rejection.IsAlternative);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.False(Server.Resolve<IIssueOwnershipRegistry>().TryGetOwnerControllerId(owner, out _));
        });
    }

    [Fact]
    public void RequestQuestTypeAcceptAlternative_PeerNeverOpenedConversation_RejectedDespiteFreshGeneration()
    {
        var fixture = SetupVillageOwner();
        CreateIssueOnBothPeers(fixture);
        var controllerId = ConnectPlayer(fixture);

        Client.Call(() =>
        {
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Client.ObjectManager.TryGetId(owner, out var ownerId));
            Assert.True(Client.Resolve<IIssueGenerationRegistry>().TryGetGeneration(owner, out var generation));

            var troopRosterInterface = Client.Resolve<GameInterface.Services.TroopRosters.Interfaces.ITroopRosterInterface>();
            var packedTroops = troopRosterInterface.PackTroopRosterData(owner.Issue.AlternativeSolutionSentTroops);

            var network = Client.Resolve<Common.Network.INetwork>();
            network.SendAll(new RequestQuestTypeAcceptAlternative(ownerId, generation, packedTroops));
        });

        var rejection = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkQuestTypeAcceptRejected>());
        Assert.True(rejection.IsAlternative);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Server.ObjectManager.TryGetId(owner, out var ownerId));
            Assert.Equal(ownerId, rejection.OwnerId);
            Assert.False(Server.Resolve<IIssueOwnershipRegistry>().TryGetOwnerControllerId(owner, out _));
        });
    }

    [Theory]
    [InlineData(false, false, false, false, false, false, false)]
    [InlineData(true, false, false, false, false, false, false)]
    [InlineData(false, true, false, false, false, false, false)]
    [InlineData(false, false, true, false, false, false, false)]
    [InlineData(false, false, false, true, false, false, false)]
    [InlineData(true, true, false, false, false, false, false)]
    [InlineData(true, false, true, false, false, false, false)]
    [InlineData(false, false, false, false, true, false, false)]
    [InlineData(false, false, false, false, false, true, false)]
    [InlineData(false, false, false, false, false, true, true)]
    public void RequestQuestTypeAcceptAlternative_Rejected_RestoresSentTroopsToTheClickingClientsMainParty(
        bool typeSpecificReject, bool anotherPlayerAcceptedAlternativeFirst, bool anotherPlayerAcceptedQuestFirst,
        bool sentTroopMissingRegistryHandle, bool repeatBeforeReply, bool genuineLocalStart, bool repeatOriginalBeforeReply)
    {
        var fixture = SetupVillageOwner();
        CreateIssueOnBothPeers(fixture);

        var controllerId = "player-A-" + Guid.NewGuid();
        var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        var eligibleTroopId = TestEnvironment.CreateRegisteredObject<CharacterObject>();
        var winningCompanionHeroId = anotherPlayerAcceptedAlternativeFirst ? TestEnvironment.CreateRegisteredObject<Hero>() : null;
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<CharacterObject>(eligibleTroopId, out var eligibleTroop));
            using (new AllowedThread())
            {
                eligibleTroop.Level = 20;
            }

            var playerManager = Server.Resolve<IPlayerManager>();
            Assert.True(playerManager.AddPlayer(new Player(controllerId, fixture.HeroId, partyId, "", "")));
        });
        TestEnvironment.ConnectRegisteredPlayer(Client, controllerId);
        Client.Resolve<IControllerIdProvider>().SetControllerId(controllerId);

        var memberCountBeforeSending = 0;
        Client.Call(() =>
        {
            Assert.True(Client.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(fixture.CompanionHeroId, out var companion));
            Assert.True(Client.ObjectManager.TryGetObject<CharacterObject>(eligibleTroopId, out var eligibleTroop));
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));

            using (new AllowedThread())
            {
                Campaign.Current.MainParty = party;
                party.MemberRoster.AddToCounts(companion.CharacterObject, 1);
                party.MemberRoster.AddToCounts(eligibleTroop, repeatBeforeReply ? 9 : 6);
                memberCountBeforeSending = party.MemberRoster.TotalManCount;
                party.MemberRoster.AddToCounts(companion.CharacterObject, -1);
                party.MemberRoster.AddToCounts(eligibleTroop, -6);
                owner.Issue.AlternativeSolutionSentTroops.AddToCounts(companion.CharacterObject, 1);
                owner.Issue.AlternativeSolutionSentTroops.AddToCounts(eligibleTroop, 6);
            }
            if (sentTroopMissingRegistryHandle) Assert.True(Client.ObjectManager.Remove(eligibleTroop));
        });

        Mock<IAlternativeAcceptMirrorStrategy<int>> typeSpecificHandler = null;
        if (typeSpecificReject)
        {
            previousDescriptor = QuestTypeRegistry.Get(TestIssueType);
            typeSpecificHandler = new Mock<IAlternativeAcceptMirrorStrategy<int>>();
            QuestTypeRegistry.Register(QuestDescriptorBuilder
                .For<VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssue, VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssueQuest>("VillageNeedsTools")
                .WithAlternativeAccept(typeSpecificHandler.Object)
                .Build());
        }

        var router = Server.Resolve<TestNetworkRouter>();
        if (repeatBeforeReply || repeatOriginalBeforeReply) router.AutoDrainReady = false;

        Client.Call(() =>
        {
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            if (genuineLocalStart)
            {
                owner.Issue.StartIssueWithAlternativeSolution();
            }
            else
            {
                Common.Messaging.MessageBroker.Instance.Publish(owner, new QuestTypeAlternativeAcceptTriggered(owner, controllerId));
            }
            if (repeatOriginalBeforeReply)
            {
                var countAfterFirstStart = Campaign.Current.MainParty.MemberRoster.TotalManCount;
                owner.Issue.StartIssueWithAlternativeSolution();
                Assert.Equal(countAfterFirstStart, Campaign.Current.MainParty.MemberRoster.TotalManCount);
            }
            if (repeatBeforeReply)
            {
                Assert.True(Client.ObjectManager.TryGetObject<CharacterObject>(eligibleTroopId, out var eligibleTroop));
                using (new AllowedThread())
                {
                    Assert.Equal(3, Campaign.Current.MainParty.MemberRoster.GetTroopCount(eligibleTroop));
                    Campaign.Current.MainParty.MemberRoster.AddToCounts(eligibleTroop, -1);
                    Campaign.Current.MainParty.MemberRoster.AddToCounts(eligibleTroop, -2);
                    owner.Issue.AlternativeSolutionSentTroops.Clear();
                    owner.Issue.AlternativeSolutionSentTroops.AddToCounts(eligibleTroop, 2);
                }
                var secondSelection = TroopRoster.CreateDummyTroopRoster();
                secondSelection.Add(owner.Issue.AlternativeSolutionSentTroops);
                Common.Messaging.MessageBroker.Instance.Publish(owner, new QuestTypeAlternativeAcceptTriggered(
                    owner, controllerId, secondSelection));
                Assert.Equal(2, Campaign.Current.MainParty.MemberRoster.GetTroopCount(eligibleTroop));
            }
            if (anotherPlayerAcceptedAlternativeFirst)
            {
                Assert.True(Client.ObjectManager.TryGetId(owner, out var ownerId));
                Assert.True(Client.ObjectManager.TryGetObject<Hero>(winningCompanionHeroId, out var winningCompanion));
                using (new AllowedThread()) { winningCompanion.ChangeState(Hero.CharacterStates.Disabled); }
                var winningRoster = TroopRoster.CreateDummyTroopRoster();
                winningRoster.AddToCounts(winningCompanion.CharacterObject, 1);
                var packedWinningTroops = Client.Resolve<GameInterface.Services.TroopRosters.Interfaces.ITroopRosterInterface>()
                    .PackTroopRosterData(winningRoster);
                var fieldsBytes = typeSpecificReject ? GenericAcceptFieldsSerializer.Serialize(0) : null;
                Common.Messaging.MessageBroker.Instance.Publish(owner,
                    new NetworkQuestTypeAlternativeAccepted(ownerId, "player-B", default, fieldsBytes, packedWinningTroops));
            }
            else if (anotherPlayerAcceptedQuestFirst)
            {
                Assert.True(Client.ObjectManager.TryGetId(owner, out var ownerId));
                Common.Messaging.MessageBroker.Instance.Publish(owner,
                    new NetworkQuestTypeQuestAccepted(ownerId, "player-B", null));
            }
            if (anotherPlayerAcceptedAlternativeFirst || anotherPlayerAcceptedQuestFirst)
            {
                Assert.True(Client.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
                Assert.Equal(memberCountBeforeSending, party.MemberRoster.TotalManCount);
                Assert.Equal(anotherPlayerAcceptedAlternativeFirst ? 1 : 0,
                    owner.Issue.AlternativeSolutionSentTroops.TotalManCount);
            }
        });

        if (repeatBeforeReply || repeatOriginalBeforeReply)
        {
            Assert.Single(Client.NetworkSentMessages.GetMessages<RequestQuestTypeAcceptAlternative>());
            router.DrainReady();
            router.AutoDrainReady = true;
            Server.PumpGameThread();
            Client.PumpGameThread();
        }

        var alternativeRejection = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkQuestTypeAcceptRejected>());
        Assert.True(alternativeRejection.IsAlternative);

        Client.Call(() =>
        {
            Assert.True(Client.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));

            Assert.Equal(memberCountBeforeSending - (repeatBeforeReply ? 1 : 0), party.MemberRoster.TotalManCount);
            Assert.Equal(anotherPlayerAcceptedAlternativeFirst ? 1 : 0, owner.Issue.AlternativeSolutionSentTroops.TotalManCount);
            if (genuineLocalStart)
            {
                Assert.True(owner.Issue.IsOngoingWithoutQuest);
                Assert.True(Client.ObjectManager.TryGetObject<Hero>(fixture.CompanionHeroId, out var companion));
                Assert.Equal(Hero.CharacterStates.Active, companion.HeroState);
            }
            if (anotherPlayerAcceptedAlternativeFirst || anotherPlayerAcceptedQuestFirst)
            {
                Assert.True(Client.Resolve<IIssueOwnershipRegistry>().TryGetOwnerControllerId(owner, out var acceptedControllerId));
                Assert.Equal("player-B", acceptedControllerId);
            }
        });
        typeSpecificHandler?.Verify(x => x.RejectAcceptance(It.IsAny<Hero>()),
            anotherPlayerAcceptedAlternativeFirst || anotherPlayerAcceptedQuestFirst ? Times.Never() : Times.Once());
    }

    [Fact]
    public void RepeatedAlternativeStart_WithAnIdenticalSecondSelectionAndPartyGain_ReturnsOnlyNewTroops()
    {
        var fixture = SetupVillageOwner();
        CreateIssueOnBothPeers(fixture);
        var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        var troopId = TestEnvironment.CreateRegisteredObject<CharacterObject>();
        var controllerId = "player-A-" + Guid.NewGuid();
        Server.Call(() =>
        {
            Assert.True(Server.Resolve<IPlayerManager>().AddPlayer(new Player(controllerId, fixture.HeroId, partyId, "", "")));
        });
        TestEnvironment.ConnectRegisteredPlayer(Client, controllerId);
        Client.Resolve<IControllerIdProvider>().SetControllerId(controllerId);

        Client.Call(() =>
        {
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Client.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Assert.True(Client.ObjectManager.TryGetObject<CharacterObject>(troopId, out var troop));
            using (new AllowedThread())
            {
                Campaign.Current.MainParty = party;
                party.MemberRoster.AddToCounts(troop, 12);
                party.MemberRoster.AddToCounts(troop, -6);
                owner.Issue.AlternativeSolutionSentTroops.AddToCounts(troop, 6);
            }
            owner.Issue.StartIssueWithAlternativeSolution();

            using (new AllowedThread())
            {
                party.MemberRoster.AddToCounts(troop, 2);
                party.MemberRoster.AddToCounts(troop, -6);
                owner.Issue.AlternativeSolutionSentTroops.AddToCounts(troop, 6);
            }
            owner.Issue.StartIssueWithAlternativeSolution();
            Assert.Equal(8, party.MemberRoster.GetTroopCount(troop));
        });

        Assert.Single(Client.NetworkSentMessages.GetMessages<RequestQuestTypeAcceptAlternative>());
        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkQuestTypeAcceptRejected>());
        Client.Call(() =>
        {
            Assert.True(Client.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Assert.True(Client.ObjectManager.TryGetObject<CharacterObject>(troopId, out var troop));
            Assert.Equal(14, party.MemberRoster.GetTroopCount(troop));
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void QuestScreenReset_BeforeAlternativeRejection_DoesNotRestoreSelectedTroopsTwice(bool resetBeforeTrigger)
    {
        var fixture = SetupVillageOwner();
        CreateIssueOnBothPeers(fixture);
        var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        var troopId = TestEnvironment.CreateRegisteredObject<CharacterObject>();
        var controllerId = "player-A-" + Guid.NewGuid();
        Server.Call(() =>
        {
            Assert.True(Server.Resolve<IPlayerManager>().AddPlayer(new Player(controllerId, fixture.HeroId, partyId, "", "")));
        });
        TestEnvironment.ConnectRegisteredPlayer(Client, controllerId);
        Client.Resolve<IControllerIdProvider>().SetControllerId(controllerId);
        var router = Server.Resolve<TestNetworkRouter>();
        router.AutoDrainReady = false;

        Client.Call(() =>
        {
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Client.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Assert.True(Client.ObjectManager.TryGetObject<CharacterObject>(troopId, out var troop));
            using (new AllowedThread())
            {
                Campaign.Current.MainParty = party;
                party.MemberRoster.AddToCounts(troop, 6);
                party.MemberRoster.AddToCounts(troop, -6);
                owner.Issue.AlternativeSolutionSentTroops.AddToCounts(troop, 6);
            }
            var screen = new PartyScreenLogic();
            var selected = TroopRoster.CreateDummyTroopRoster();
            selected.Add(owner.Issue.AlternativeSolutionSentTroops);
            if (resetBeforeTrigger)
            {
                Common.Messaging.MessageBroker.Instance.Publish(owner,
                    new QuestAlternativeTroopSelectionReset(owner.Issue.AlternativeSolutionSentTroops, screen));
                using (new AllowedThread()) party.MemberRoster.AddToCounts(troop, 6);
                Common.Messaging.MessageBroker.Instance.Publish(owner,
                    new QuestTypeAlternativeAcceptTriggered(owner, controllerId, selected, null));
                Common.Messaging.MessageBroker.Instance.Publish(owner,
                    new QuestAlternativeTroopSelectionClosed(owner.Issue.AlternativeSolutionSentTroops));
            }
            else
            {
                Common.Messaging.MessageBroker.Instance.Publish(owner,
                    new QuestTypeAlternativeAcceptTriggered(owner, controllerId, selected, screen));
                Common.Messaging.MessageBroker.Instance.Publish(owner,
                    new QuestAlternativeTroopSelectionReset(owner.Issue.AlternativeSolutionSentTroops, new PartyScreenLogic()));
                Assert.Equal(0, party.MemberRoster.GetTroopCount(troop));
                using (new AllowedThread()) party.MemberRoster.AddToCounts(troop, 6);
                Common.Messaging.MessageBroker.Instance.Publish(owner,
                    new QuestAlternativeTroopSelectionReset(owner.Issue.AlternativeSolutionSentTroops, screen));
            }
            Assert.Equal(6, party.MemberRoster.GetTroopCount(troop));
            Common.Messaging.MessageBroker.Instance.Publish(owner,
                new QuestAlternativeTroopSelectionReset(owner.Issue.AlternativeSolutionSentTroops, screen));
            Assert.Equal(6, party.MemberRoster.GetTroopCount(troop));
            using (new AllowedThread()) owner.Issue.AlternativeSolutionSentTroops.AddToCounts(troop, 6);
            owner.Issue.StartIssueWithAlternativeSolution();
            Assert.Equal(6, party.MemberRoster.GetTroopCount(troop));
        });

        router.DrainReady();
        router.AutoDrainReady = true;
        Server.PumpGameThread();
        Client.PumpGameThread();

        Assert.Single(Client.NetworkSentMessages.GetMessages<RequestQuestTypeAcceptAlternative>());
        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkQuestTypeAcceptRejected>());
        Client.Call(() =>
        {
            Assert.True(Client.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Assert.True(Client.ObjectManager.TryGetObject<CharacterObject>(troopId, out var troop));
            Assert.Equal(6, party.MemberRoster.GetTroopCount(troop));
        });
        if (!resetBeforeTrigger)
        {
            Client.Call(() =>
            {
                Assert.True(Client.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
                Assert.True(Client.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
                Assert.True(Client.ObjectManager.TryGetObject<CharacterObject>(troopId, out var troop));
                using (new AllowedThread())
                {
                    party.MemberRoster.AddToCounts(troop, -6);
                    owner.Issue.AlternativeSolutionSentTroops.AddToCounts(troop, 6);
                }
                var selectedAgain = TroopRoster.CreateDummyTroopRoster();
                selectedAgain.Add(owner.Issue.AlternativeSolutionSentTroops);
                Common.Messaging.MessageBroker.Instance.Publish(owner,
                    new QuestTypeAlternativeAcceptTriggered(owner, controllerId, selectedAgain, null));
            });
            router.DrainReady();
            Server.PumpGameThread();
            Client.PumpGameThread();
            Client.Call(() =>
            {
                Assert.True(Client.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
                Assert.True(Client.ObjectManager.TryGetObject<CharacterObject>(troopId, out var troop));
                Assert.Equal(6, party.MemberRoster.GetTroopCount(troop));
            });
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void QuestTypeAlternativeAcceptTriggered_AfterAnotherPlayerAccepted_RestoresLateSelection(bool alternativeWinner)
    {
        var fixture = SetupVillageOwner();
        CreateIssueOnBothPeers(fixture);
        var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        var eligibleTroopId = TestEnvironment.CreateRegisteredObject<CharacterObject>();
        var winningCompanionHeroId = alternativeWinner ? TestEnvironment.CreateRegisteredObject<Hero>() : null;
        Client.Resolve<IControllerIdProvider>().SetControllerId("player-A");

        Client.Call(() =>
        {
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(fixture.CompanionHeroId, out var companion));
            Assert.True(Client.ObjectManager.TryGetObject<CharacterObject>(eligibleTroopId, out var eligibleTroop));
            Assert.True(Client.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            using (new AllowedThread())
            {
                Campaign.Current.MainParty = party;
                party.MemberRoster.AddToCounts(companion.CharacterObject, 1);
                party.MemberRoster.AddToCounts(eligibleTroop, 6);
            }

            if (alternativeWinner)
            {
                Assert.True(Client.ObjectManager.TryGetObject<Hero>(winningCompanionHeroId, out var winningCompanion));
                using (new AllowedThread()) winningCompanion.ChangeState(Hero.CharacterStates.Disabled);
                var winningRoster = TroopRoster.CreateDummyTroopRoster();
                winningRoster.AddToCounts(winningCompanion.CharacterObject, 1);
                var packedWinningTroops = Client.Resolve<GameInterface.Services.TroopRosters.Interfaces.ITroopRosterInterface>()
                    .PackTroopRosterData(winningRoster);
                Common.Messaging.MessageBroker.Instance.Publish(owner,
                    new NetworkQuestTypeAlternativeAccepted(fixture.HeroId, "player-B", default, null, packedWinningTroops));
                using (new AllowedThread())
                {
                    owner.Issue.AlternativeSolutionSentTroops.Clear();
                    owner.Issue.AlternativeSolutionSentTroops.AddToCounts(eligibleTroop, 6);
                }
                Common.Messaging.MessageBroker.Instance.Publish(owner,
                    new QuestAlternativeTroopSelectionReset(owner.Issue.AlternativeSolutionSentTroops, new PartyScreenLogic()));
                Assert.Equal(1, owner.Issue.AlternativeSolutionSentTroops.GetTroopCount(winningCompanion.CharacterObject));
                Assert.Equal(0, owner.Issue.AlternativeSolutionSentTroops.GetTroopCount(eligibleTroop));
            }
            else
            {
                Common.Messaging.MessageBroker.Instance.Publish(owner,
                    new NetworkQuestTypeQuestAccepted(fixture.HeroId, "player-B", null));
            }

            var partyCountBeforeLateSelection = party.MemberRoster.TotalManCount;
            var beforeSelection = TroopRoster.CreateDummyTroopRoster();
            beforeSelection.Add(owner.Issue.AlternativeSolutionSentTroops);
            using (new AllowedThread())
            {
                party.MemberRoster.AddToCounts(companion.CharacterObject, -1);
                party.MemberRoster.AddToCounts(eligibleTroop, -6);
                owner.Issue.AlternativeSolutionSentTroops.Clear();
                owner.Issue.AlternativeSolutionSentTroops.AddToCounts(companion.CharacterObject, 1);
                owner.Issue.AlternativeSolutionSentTroops.AddToCounts(eligibleTroop, 6);
            }
            var afterTransfer = TroopRoster.CreateDummyTroopRoster();
            afterTransfer.Add(beforeSelection);
            afterTransfer.Add(owner.Issue.AlternativeSolutionSentTroops);
            Common.Messaging.MessageBroker.Instance.Publish(owner, new QuestAlternativeTroopsTransferredLocally(
                owner.Issue.AlternativeSolutionSentTroops, beforeSelection, afterTransfer));
            owner.Issue.StartIssueWithAlternativeSolution();

            Assert.Equal(partyCountBeforeLateSelection, party.MemberRoster.TotalManCount);
            Assert.Equal(1, party.MemberRoster.GetTroopCount(companion.CharacterObject));
            Assert.Equal(6, party.MemberRoster.GetTroopCount(eligibleTroop));
            Assert.Equal(Hero.CharacterStates.Active, companion.HeroState);
            Assert.Equal(alternativeWinner ? 1 : 0, owner.Issue.AlternativeSolutionSentTroops.TotalManCount);
            Assert.Equal(alternativeWinner, owner.Issue.IsSolvingWithAlternative);
            Assert.Equal(!alternativeWinner, owner.Issue.IsSolvingWithQuest);
        });

        Assert.Empty(Client.NetworkSentMessages.GetMessages<RequestQuestTypeAcceptAlternative>());
    }

    [Fact]
    public void QuestTypeAlternativeAcceptTriggered_LateIdenticalTransferAfterWinner_RestoresLocalTroops()
    {
        var fixture = SetupVillageOwner();
        CreateIssueOnBothPeers(fixture);
        var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        var troopId = TestEnvironment.CreateRegisteredObject<CharacterObject>();
        Client.Resolve<IControllerIdProvider>().SetControllerId("player-A");

        Client.Call(() =>
        {
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(fixture.CompanionHeroId, out var companion));
            Assert.True(Client.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Assert.True(Client.ObjectManager.TryGetObject<CharacterObject>(troopId, out var troop));
            using (new AllowedThread())
            {
                Campaign.Current.MainParty = party;
                party.MemberRoster.AddToCounts(troop, 6);
            }

            var winningRoster = TroopRoster.CreateDummyTroopRoster();
            winningRoster.AddToCounts(companion.CharacterObject, 1);
            winningRoster.AddToCounts(troop, 6);
            var packedWinningTroops = Client.Resolve<GameInterface.Services.TroopRosters.Interfaces.ITroopRosterInterface>()
                .PackTroopRosterData(winningRoster);
            Common.Messaging.MessageBroker.Instance.Publish(owner,
                new NetworkQuestTypeAlternativeAccepted(fixture.HeroId, "player-B", default, null, packedWinningTroops));

            var afterTransfer = TroopRoster.CreateDummyTroopRoster();
            afterTransfer.AddToCounts(companion.CharacterObject, 1);
            afterTransfer.AddToCounts(troop, 12);
            Common.Messaging.MessageBroker.Instance.Publish(owner, new QuestAlternativeTroopsTransferredLocally(
                owner.Issue.AlternativeSolutionSentTroops, winningRoster, afterTransfer));
            var afterMovingThreeBack = TroopRoster.CreateDummyTroopRoster();
            afterMovingThreeBack.AddToCounts(companion.CharacterObject, 1);
            afterMovingThreeBack.AddToCounts(troop, 9);
            Common.Messaging.MessageBroker.Instance.Publish(owner, new QuestAlternativeTroopsTransferredLocally(
                owner.Issue.AlternativeSolutionSentTroops, afterTransfer, afterMovingThreeBack));
            using (new AllowedThread())
            {
                party.MemberRoster.AddToCounts(troop, -6);
                party.MemberRoster.AddToCounts(troop, 3);
            }

            owner.Issue.StartIssueWithAlternativeSolution();
            Assert.Equal(6, party.MemberRoster.GetTroopCount(troop));
            Assert.Equal(6, owner.Issue.AlternativeSolutionSentTroops.GetTroopCount(troop));

            var afterMovingWinnerTroopsBack = TroopRoster.CreateDummyTroopRoster();
            afterMovingWinnerTroopsBack.AddToCounts(companion.CharacterObject, 1);
            afterMovingWinnerTroopsBack.AddToCounts(troop, 3);
            Common.Messaging.MessageBroker.Instance.Publish(owner, new QuestAlternativeTroopsTransferredLocally(
                owner.Issue.AlternativeSolutionSentTroops, winningRoster, afterMovingWinnerTroopsBack));
            using (new AllowedThread()) party.MemberRoster.AddToCounts(troop, 3);
            owner.Issue.StartIssueWithAlternativeSolution();
            Assert.Equal(6, party.MemberRoster.GetTroopCount(troop));
            Assert.Equal(6, owner.Issue.AlternativeSolutionSentTroops.GetTroopCount(troop));

            using (new AllowedThread()) party.MemberRoster.AddToCounts(troop, 0, false, 1);
            var afterWoundedTransfer = TroopRoster.CreateDummyTroopRoster();
            afterWoundedTransfer.AddToCounts(companion.CharacterObject, 1);
            afterWoundedTransfer.AddToCounts(troop, 6, false, 1);
            Common.Messaging.MessageBroker.Instance.Publish(owner, new QuestAlternativeTroopsTransferredLocally(
                owner.Issue.AlternativeSolutionSentTroops, winningRoster, afterWoundedTransfer));
            using (new AllowedThread()) party.MemberRoster.AddToCounts(troop, 0, false, -1);
            owner.Issue.StartIssueWithAlternativeSolution();
            var restored = party.MemberRoster.GetElementCopyAtIndex(party.MemberRoster.FindIndexOfTroop(troop));
            Assert.Equal(6, restored.Number);
            Assert.Equal(1, restored.WoundedNumber);
        });

        Assert.Empty(Client.NetworkSentMessages.GetMessages<RequestQuestTypeAcceptAlternative>());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ManualQuestScreenReset_BeforeCompetingAcceptance_DoesNotReturnTroopsTwice(bool alternativeWinner)
    {
        var fixture = SetupVillageOwner();
        CreateIssueOnBothPeers(fixture);
        var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        var troopId = TestEnvironment.CreateRegisteredObject<CharacterObject>();
        Client.Resolve<IControllerIdProvider>().SetControllerId("player-A");

        Client.Call(() =>
        {
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(fixture.CompanionHeroId, out var companion));
            Assert.True(Client.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Assert.True(Client.ObjectManager.TryGetObject<CharacterObject>(troopId, out var troop));
            var roster = owner.Issue.AlternativeSolutionSentTroops;
            using (new AllowedThread())
            {
                Campaign.Current.MainParty = party;
                party.MemberRoster.AddToCounts(troop, 6);
            }

            var screen = CreateQuestSelectionScreen(roster, party);

            var before = TroopRoster.CreateDummyTroopRoster();
            using (new AllowedThread())
            {
                party.MemberRoster.AddToCounts(troop, -6);
                roster.AddToCounts(troop, 6);
            }
            var after = roster.CloneRosterData();
            Common.Messaging.MessageBroker.Instance.Publish(owner,
                new QuestAlternativeTroopsTransferredLocally(roster, before, after));
            Assert.Equal(0, party.MemberRoster.GetTroopCount(troop));

            screen.Reset(false);
            Assert.Equal(6, party.MemberRoster.GetTroopCount(troop));
            Assert.Equal(0, roster.GetTroopCount(troop));

            if (alternativeWinner)
            {
                var winner = TroopRoster.CreateDummyTroopRoster();
                winner.AddToCounts(companion.CharacterObject, 1);
                var packed = Client.Resolve<GameInterface.Services.TroopRosters.Interfaces.ITroopRosterInterface>()
                    .PackTroopRosterData(winner);
                Common.Messaging.MessageBroker.Instance.Publish(owner,
                    new NetworkQuestTypeAlternativeAccepted(fixture.HeroId, "player-B", default, null, packed));
            }
            else
            {
                Common.Messaging.MessageBroker.Instance.Publish(owner,
                    new NetworkQuestTypeQuestAccepted(fixture.HeroId, "player-B", null));
            }

            Assert.Equal(6, party.MemberRoster.GetTroopCount(troop));
        });
    }

    [Fact]
    public void ManualQuestScreenReset_AfterDone_KeepsSelectedTroopsForAcceptTrigger()
    {
        var fixture = SetupVillageOwner();
        CreateIssueOnBothPeers(fixture);
        var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        var troopId = TestEnvironment.CreateRegisteredObject<CharacterObject>();
        Client.Resolve<IControllerIdProvider>().SetControllerId("player-A");

        Client.Call(() =>
        {
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Client.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Assert.True(Client.ObjectManager.TryGetObject<CharacterObject>(troopId, out var troop));
            using (new AllowedThread())
            {
                Campaign.Current.MainParty = party;
                party.MemberRoster.AddToCounts(troop, 6);
            }

            var roster = owner.Issue.AlternativeSolutionSentTroops;
            var screen = CreateQuestSelectionScreen(roster, party);
            var selected = TroopRoster.CreateDummyTroopRoster();
            selected.AddToCounts(troop, 6);
            Common.Messaging.MessageBroker.Instance.Publish(owner,
                new QuestAlternativeTroopSelectionReset(roster, screen, selected));

            screen.Reset(false);
            Common.Messaging.MessageBroker.Instance.Publish(owner,
                new QuestTypeAlternativeAcceptTriggered(owner, "player-A", null, screen));

            var request = Assert.Single(Client.NetworkSentMessages.GetMessages<RequestQuestTypeAcceptAlternative>());
            var requested = Client.Resolve<GameInterface.Services.TroopRosters.Interfaces.ITroopRosterInterface>()
                .UnpackTroopRosterData(request.SentTroops).ToArray();
            Assert.Equal(6, requested.Sum(element => element.Number));
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AlternativeWinner_RebasesLocalTransfersBeforeLaterQuestScreenEdits(bool screenResetBeforeTrigger)
    {
        var fixture = SetupVillageOwner();
        CreateIssueOnBothPeers(fixture);
        var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        var localTroopId = TestEnvironment.CreateRegisteredObject<CharacterObject>();
        var winningTroopId = TestEnvironment.CreateRegisteredObject<CharacterObject>();
        Client.Resolve<IControllerIdProvider>().SetControllerId("player-A");

        Client.Call(() =>
        {
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(fixture.CompanionHeroId, out var companion));
            Assert.True(Client.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Assert.True(Client.ObjectManager.TryGetObject<CharacterObject>(localTroopId, out var localTroop));
            Assert.True(Client.ObjectManager.TryGetObject<CharacterObject>(winningTroopId, out var winningTroop));
            var roster = owner.Issue.AlternativeSolutionSentTroops;
            var beforeSelection = TroopRoster.CreateDummyTroopRoster();
            using (new AllowedThread())
            {
                Campaign.Current.MainParty = party;
                party.MemberRoster.AddToCounts(localTroop, 6);
                party.MemberRoster.AddToCounts(localTroop, -3);
                roster.AddToCounts(localTroop, 3);
            }
            var afterSelection = TroopRoster.CreateDummyTroopRoster();
            afterSelection.Add(roster);
            Common.Messaging.MessageBroker.Instance.Publish(owner,
                new QuestAlternativeTroopsTransferredLocally(roster, beforeSelection, afterSelection));

            var winningRoster = TroopRoster.CreateDummyTroopRoster();
            winningRoster.AddToCounts(companion.CharacterObject, 1);
            winningRoster.AddToCounts(winningTroop, 2);
            var winningData = Client.Resolve<GameInterface.Services.TroopRosters.Interfaces.ITroopRosterInterface>()
                .PackTroopRosterData(winningRoster);
            Common.Messaging.MessageBroker.Instance.Publish(owner,
                new NetworkQuestTypeAlternativeAccepted(fixture.HeroId, "player-B", default, null, winningData));
            Assert.Equal(6, party.MemberRoster.GetTroopCount(localTroop));
            Assert.Equal(2, roster.GetTroopCount(winningTroop));

            var beforeLateTransfer = TroopRoster.CreateDummyTroopRoster();
            beforeLateTransfer.Add(roster);
            using (new AllowedThread())
            {
                roster.AddToCounts(winningTroop, -1);
                party.MemberRoster.AddToCounts(winningTroop, 1);
            }
            var afterLateTransfer = TroopRoster.CreateDummyTroopRoster();
            afterLateTransfer.Add(roster);
            Common.Messaging.MessageBroker.Instance.Publish(owner,
                new QuestAlternativeTroopsTransferredLocally(roster, beforeLateTransfer, afterLateTransfer));
            if (screenResetBeforeTrigger)
            {
                using (new AllowedThread())
                {
                    party.MemberRoster.AddToCounts(winningTroop, -1);
                    roster.AddToCounts(winningTroop, 1);
                }
                Common.Messaging.MessageBroker.Instance.Publish(owner,
                    new QuestAlternativeTroopSelectionReset(roster, new PartyScreenLogic()));
            }
            owner.Issue.StartIssueWithAlternativeSolution();

            Assert.Equal(6, party.MemberRoster.GetTroopCount(localTroop));
            Assert.Equal(0, party.MemberRoster.GetTroopCount(winningTroop));
            Assert.Equal(2, roster.GetTroopCount(winningTroop));
        });

        Assert.Empty(Client.NetworkSentMessages.GetMessages<RequestQuestTypeAcceptAlternative>());
    }

    [Fact]
    public void AlternativeWinner_ResetForLaterIssue_DoesNotReplayEarlierIssueTroops()
    {
        var fixture = SetupVillageOwner();
        CreateIssueOnBothPeers(fixture);
        var troopId = TestEnvironment.CreateRegisteredObject<CharacterObject>();

        Client.Call(() =>
        {
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Client.ObjectManager.TryGetObject<ItemObject>(fixture.ItemId, out var item));
            Assert.True(Client.ObjectManager.TryGetObject<CharacterObject>(troopId, out var troop));
            var winner = TroopRoster.CreateDummyTroopRoster();
            winner.AddToCounts(troop, 6);
            var packed = Client.Resolve<GameInterface.Services.TroopRosters.Interfaces.ITroopRosterInterface>()
                .PackTroopRosterData(winner);
            Common.Messaging.MessageBroker.Instance.Publish(owner,
                new NetworkQuestTypeAlternativeAccepted(fixture.HeroId, "player-B", default, null, packed));
            Assert.Equal(6, owner.Issue.AlternativeSolutionSentTroops.GetTroopCount(troop));

            using (new AllowedThread()) owner.Issue = new VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssue(owner, item);
            var laterRoster = owner.Issue.AlternativeSolutionSentTroops;
            Common.Messaging.MessageBroker.Instance.Publish(owner,
                new QuestAlternativeTroopSelectionReset(laterRoster, new PartyScreenLogic()));
            Assert.Equal(0, laterRoster.GetTroopCount(troop));
        });
    }

    [Fact]
    public void StartOnServer_ThenRolledBackAsAFailedAccept_ReturnsHeldTroopsInsteadOfLosingThem()
    {
        var fixture = SetupVillageOwner();
        CreateIssueOnBothPeers(fixture);
        var controllerId = ConnectPlayer(fixture);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.CompanionHeroId, out var companion));
            Assert.True(Server.ObjectManager.TryGetObject<CharacterObject>(lastConnectedEligibleTroopId, out var eligibleTroop));
            var playerManager = Server.Resolve<IPlayerManager>();
            Assert.True(playerManager.TryGetPlayer(controllerId, out var player));

            using (new AllowedThread())
            {
                owner.Issue.AlternativeSolutionSentTroops.AddToCounts(companion.CharacterObject, 1);
                owner.Issue.AlternativeSolutionSentTroops.AddToCounts(eligibleTroop, 6);
            }

            AlternativeSolutionStartRunner.StartOnServer(owner, player);
            Assert.True(owner.Issue.IsSolvingWithAlternative);

            using (new IssueFinalizeAuthorityGuard())
            using (new AllowedThread())
            {
                Server.Resolve<IIssueOwnershipRegistry>().SetOwner(owner, controllerId);
                owner.Issue.CompleteIssueWithCancel();
            }

            Assert.Null(owner.Issue);
            Assert.False(Server.Resolve<IIssueOwnershipRegistry>().TryGetOwnerControllerId(owner, out _));
            Assert.True(Server.Resolve<IAwaitingAlternativeSolutionTroopsRegistry>().TryGet(controllerId, out var returned));
            Assert.Equal(7, returned.TotalManCount);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RequestQuestTypeAcceptAlternative_GenuineAccept_BroadcastStateIsServerComputedNotClientSupplied(bool resetBeforeTrigger)
    {
        var fixture = SetupVillageOwner();
        CreateIssueOnBothPeers(fixture);
        var controllerId = ConnectPlayer(fixture);
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(lastConnectedPartyId, out var party));
            Assert.True(Server.ObjectManager.TryGetObject<CharacterObject>(lastConnectedEligibleTroopId, out var eligibleTroop));
            party.MemberRoster.AddToCounts(eligibleTroop, 3);
        });
        OpenConversation(fixture, controllerId);
        TestEnvironment.FlushCoalescer();
        Server.PumpGameThread();
        Client.PumpGameThread();
        var memberCountBeforeSending = 0;
        var selectedCount = 0;
        var router = Server.Resolve<TestNetworkRouter>();
        router.AutoDrainReady = false;

        Client.Call(() =>
        {
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(fixture.CompanionHeroId, out var companion));
            Assert.True(Client.ObjectManager.TryGetObject<CharacterObject>(lastConnectedEligibleTroopId, out var eligibleTroop));
            Assert.True(Client.ObjectManager.TryGetObject<MobileParty>(lastConnectedPartyId, out var party));
            using (new AllowedThread())
            {
                Campaign.Current.MainParty = party;
                Assert.Equal(9, party.MemberRoster.GetTroopCount(eligibleTroop));
                Assert.Equal(1, party.MemberRoster.GetTroopCount(companion.CharacterObject));
                memberCountBeforeSending = party.MemberRoster.TotalManCount;
                party.MemberRoster.AddToCounts(companion.CharacterObject, -1);
                party.MemberRoster.AddToCounts(eligibleTroop, -6);
                owner.Issue.AlternativeSolutionSentTroops.AddToCounts(companion.CharacterObject, 1);
                owner.Issue.AlternativeSolutionSentTroops.AddToCounts(eligibleTroop, 6);
            }

            var selected = TroopRoster.CreateDummyTroopRoster();
            selected.Add(owner.Issue.AlternativeSolutionSentTroops);
            selectedCount = selected.TotalManCount;
            var screen = new PartyScreenLogic();
            if (!resetBeforeTrigger)
                Common.Messaging.MessageBroker.Instance.Publish(owner,
                    new QuestTypeAlternativeAcceptTriggered(owner, controllerId, selected, screen));
            using (new AllowedThread())
            {
                party.MemberRoster.AddToCounts(companion.CharacterObject, 1);
                party.MemberRoster.AddToCounts(eligibleTroop, 6);
            }
            Common.Messaging.MessageBroker.Instance.Publish(owner,
                new QuestAlternativeTroopSelectionReset(owner.Issue.AlternativeSolutionSentTroops, screen, selected));
            if (resetBeforeTrigger)
            {
                using (new AllowedThread()) owner.Issue.AlternativeSolutionSentTroops.Clear();
                Common.Messaging.MessageBroker.Instance.Publish(owner,
                    new QuestTypeAlternativeAcceptTriggered(owner, controllerId, null, null));
                Common.Messaging.MessageBroker.Instance.Publish(owner,
                    new QuestAlternativeTroopSelectionClosed(owner.Issue.AlternativeSolutionSentTroops));
            }
            Assert.Equal(7, selectedCount);
            Assert.Equal(memberCountBeforeSending, party.MemberRoster.TotalManCount);
        });

        router.DrainReady();
        router.AutoDrainReady = true;
        Server.PumpGameThread();
        Client.PumpGameThread();
        TestEnvironment.FlushCoalescer();
        Client.PumpGameThread();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));

            Assert.True(owner.Issue.IsSolvingWithAlternative);
            Assert.True(owner.Issue.AlternativeSolutionReturnTimeForTroops.IsFuture);

            Assert.True(Server.Resolve<IIssueOwnershipRegistry>().TryGetOwnerControllerId(owner, out var ownerControllerId));
            Assert.Equal(controllerId, ownerControllerId);
        });

        var accepted = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkQuestTypeAlternativeAccepted>());

        Client.Call(() =>
        {
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Client.ObjectManager.TryGetObject<MobileParty>(lastConnectedPartyId, out var party));
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(fixture.CompanionHeroId, out var companion));
            Assert.True(Client.ObjectManager.TryGetObject<CharacterObject>(lastConnectedEligibleTroopId, out var eligibleTroop));
            Assert.True(Client.Resolve<IIssueOwnershipRegistry>().IsLocalPeerOwner(owner));
            var acceptedTroops = Client.Resolve<GameInterface.Services.TroopRosters.Interfaces.ITroopRosterInterface>()
                .UnpackTroopRosterData(accepted.SentTroops).ToArray();
            Assert.Equal(7, acceptedTroops.Sum(element => element.Number));
            Assert.Equal(0, party.MemberRoster.GetTroopCount(companion.CharacterObject));
            Assert.Equal(3, party.MemberRoster.GetTroopCount(eligibleTroop));
            owner.Issue.StartIssueWithAlternativeSolution();
            Assert.Equal(3, party.MemberRoster.GetTroopCount(eligibleTroop));
        });

        Assert.Single(Client.NetworkSentMessages.GetMessages<RequestQuestTypeAcceptAlternative>());

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(lastConnectedPartyId, out var party));
            Assert.True(Server.ObjectManager.TryGetObject<CharacterObject>(lastConnectedEligibleTroopId, out var eligibleTroop));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.CompanionHeroId, out var companion));
            Assert.Equal(owner.Issue.AlternativeSolutionReturnTimeForTroops, accepted.State.ReturnTime);
            Assert.Equal(0, party.MemberRoster.GetTroopCount(companion.CharacterObject));
            Assert.Equal(3, party.MemberRoster.GetTroopCount(eligibleTroop));
        });
    }

    [Fact]
    public void RequestQuestTypeAcceptAlternative_EmptyValidatedRoster_RejectedWithoutMutatingIssueState()
    {
        var fixture = SetupVillageOwner();
        CreateIssueOnBothPeers(fixture);
        var controllerId = ConnectPlayer(fixture);
        OpenConversation(fixture, controllerId);

        Client.Call(() =>
        {
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Client.ObjectManager.TryGetId(owner, out var ownerId));
            Assert.True(Client.Resolve<IIssueGenerationRegistry>().TryGetGeneration(owner, out var generation));

            var troopRosterInterface = Client.Resolve<GameInterface.Services.TroopRosters.Interfaces.ITroopRosterInterface>();
            var emptyTroops = troopRosterInterface.PackTroopRosterData(TroopRoster.CreateDummyTroopRoster());

            var network = Client.Resolve<Common.Network.INetwork>();
            network.SendAll(new RequestQuestTypeAcceptAlternative(ownerId, generation, emptyTroops));
        });

        var rejection = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkQuestTypeAcceptRejected>());
        Assert.True(rejection.IsAlternative);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(owner.Issue.IsOngoingWithoutQuest);
            Assert.False(Server.Resolve<IIssueOwnershipRegistry>().TryGetOwnerControllerId(owner, out _));
        });
    }

    [Fact]
    public void RequestQuestTypeAcceptQuest_AnotherPeerOpensConversationWithSameIssueGiver_DoesNotInvalidateFirstPeersTrackedAccept()
    {
        var fixture = SetupVillageOwner();
        CreateIssueOnBothPeers(fixture);
        var controllerId = ConnectPlayer(fixture);
        OpenConversation(fixture, controllerId);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Server.ObjectManager.TryGetId(owner, out var ownerId));
            Assert.True(Server.Resolve<IIssueGenerationRegistry>().TryGetGeneration(owner, out var generation));

            Server.Resolve<IIssueConversationTracker>().Register(ownerId, "player-B-" + Guid.NewGuid(), generation);
        });

        Client.Call(() =>
        {
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Client.ObjectManager.TryGetId(owner, out var ownerId));
            Assert.True(Client.Resolve<IIssueGenerationRegistry>().TryGetGeneration(owner, out var generation));

            var network = Client.Resolve<Common.Network.INetwork>();
            network.SendAll(new RequestQuestTypeAcceptQuest(ownerId, generation));
        });

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Server.Resolve<IIssueOwnershipRegistry>().TryGetOwnerControllerId(owner, out var ownerControllerId));
            Assert.Equal(controllerId, ownerControllerId);
        });

        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkQuestTypeQuestAccepted>());
    }

    [Fact]
    public void DialogueTriggeredAccept_UsesServerConversationGenerationWhenClientRegistryIsStale()
    {
        var fixture = SetupVillageOwner();
        CreateIssueOnBothPeers(fixture);
        var controllerId = ConnectPlayer(fixture);

        Client.Call(() =>
        {
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Client.Resolve<IIssueGenerationRegistry>().SetGeneration(owner, 0);
        });

        OpenConversation(fixture, controllerId);

        Client.Call(() =>
        {
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Client.Resolve<IIssueGenerationRegistry>().TryGetGeneration(owner, out var generation));
            Assert.NotEqual(0, generation);
            Campaign.Current.IssueManager.StartIssueQuest(owner);
        });

        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkQuestTypeQuestAccepted>());
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkQuestTypeAcceptRejected>());
    }

    [Fact]
    public void RequestQuestTypeAcceptQuest_GenuineAccept_SetsOwnershipAndBroadcasts()
    {
        var fixture = SetupVillageOwner();
        CreateIssueOnBothPeers(fixture);
        var controllerId = ConnectPlayer(fixture);
        OpenConversation(fixture, controllerId);

        Client.Call(() =>
        {
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Client.ObjectManager.TryGetId(owner, out var ownerId));
            Assert.True(Client.Resolve<IIssueGenerationRegistry>().TryGetGeneration(owner, out var generation));

            var network = Client.Resolve<Common.Network.INetwork>();
            network.SendAll(new RequestQuestTypeAcceptQuest(ownerId, generation));
        });

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Server.Resolve<IIssueOwnershipRegistry>().TryGetOwnerControllerId(owner, out var ownerControllerId));
            Assert.Equal(controllerId, ownerControllerId);
        });

        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkQuestTypeQuestAccepted>());
    }

    [Fact]
    public void DialogueTriggeredAccept_DoesNotCommitLocallyUntilTheServerApproves()
    {
        var fixture = SetupVillageOwner();
        CreateIssueOnBothPeers(fixture);
        var controllerId = ConnectPlayer(fixture);
        OpenConversation(fixture, controllerId);

        Client.Call(() =>
        {
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Campaign.Current.IssueManager.StartIssueQuest(owner);
            Assert.Null(owner.Issue.IssueQuest);
        });

        Assert.Single(Client.NetworkSentMessages.GetMessages<RequestQuestTypeAcceptQuest>());

        var accepted = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkQuestTypeQuestAccepted>());
        Assert.Equal(controllerId, accepted.OwnerControllerId);

        Client.Call(() =>
        {
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(owner.Issue.IsSolvingWithQuest);
        });
    }
}
