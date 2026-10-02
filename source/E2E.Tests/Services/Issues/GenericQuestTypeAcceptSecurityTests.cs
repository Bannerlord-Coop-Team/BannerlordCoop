using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using GameInterface.Services.Entity;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Generic.AcceptMirror;
using GameInterface.Services.Issues.Handlers;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Issues.Messages;
using GameInterface.Services.MapEvents;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using GameInterface.Services.TroopRosters.Interfaces;
using HarmonyLib;
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

    public GenericQuestTypeAcceptSecurityTests(ITestOutputHelper output)
    {
        TestQuestTypeFixture.EnsureVillageNeedsToolsRegistered();
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

    [Theory]
    [InlineData(false, false, null)]
    [InlineData(true, false, null)]
    [InlineData(false, true, null)]
    [InlineData(true, true, null)]
    [InlineData(false, true, "previous-owner")]
    [InlineData(true, true, "previous-owner")]
    public void AcceptedMirror_SeesTheAcceptedOwnerAndRestoresPriorOwnershipIfItFails(
        bool alternative, bool fail, string previousOwner)
    {
        var fixture = SetupVillageOwner();
        CreateIssueOnBothPeers(fixture);
        Client.Call(() =>
        {
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var giver));
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(fixture.CompanionHeroId, out var companion));
            var ownership = Client.Resolve<IIssueOwnershipRegistry>();
            if (previousOwner != null) ownership.SetOwner(giver, previousOwner);
            string ownerSeenByMirror = null;
            void ApplyMirror(Hero owner, int fields)
            {
                ownership.TryGetOwnerControllerId(owner, out ownerSeenByMirror);
                if (fail) throw new InvalidOperationException("test mirror failure");
            }

            var direct = new Mock<IRaceArbitratedAcceptMirrorStrategy<int>>();
            direct.Setup(strategy => strategy.MirrorQuestAccepted(giver, 42)).Callback<Hero, int>(ApplyMirror);
            var companionStrategy = new Mock<IAlternativeAcceptMirrorStrategy<int>>();
            companionStrategy.Setup(strategy => strategy.MirrorAlternativeAccepted(giver, 42)).Callback<Hero, int>(ApplyMirror);
            var previousDescriptor = QuestTypeRegistry.Get(TestIssueType);
            var descriptor = QuestDescriptorBuilder
                .For<VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssue, VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssueQuest>("OwnershipTest")
                .WithQuestSolutionAccept(direct.Object).WithAlternativeAccept(companionStrategy.Object).Build();
            QuestTypeRegistry.Register(descriptor);
            try
            {
                var fields = GenericAcceptFieldsSerializer.Serialize(42);
                if (alternative)
                {
                    var troops = TroopRoster.CreateDummyTroopRoster();
                    troops.AddToCounts(companion.CharacterObject, 1);
                    Client.SimulateMessage(Server.NetPeer, new NetworkQuestTypeAlternativeAccepted(
                        fixture.HeroId, "accepted-owner", default, fields,
                        Client.Resolve<ITroopRosterInterface>().PackTroopRosterData(troops)));
                }
                else
                {
                    Client.SimulateMessage(Server.NetPeer,
                        new NetworkQuestTypeQuestAccepted(fixture.HeroId, "accepted-owner", fields));
                }

                Assert.Equal("accepted-owner", ownerSeenByMirror);
                ownership.TryGetOwnerControllerId(giver, out var recordedOwner);
                Assert.Equal(fail ? previousOwner : "accepted-owner", recordedOwner);
            }
            finally
            {
                QuestTypeRegistry.Register(previousDescriptor);
            }
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

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    public void RoamingGiver_RequiresThisPlayersEngagementWithThisLordsParty(
        bool samePlayer, bool sameGiver, bool expected)
    {
        var fixture = SetupVillageOwner();
        var controllerId = ConnectPlayerAwayFromIssueGiver(fixture);
        var giverPartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var giver));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(giverPartyId, out var giverParty));
            Assert.True(Server.Resolve<IPlayerManager>().TryGetPlayer(controllerId, out var player));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(player.MobilePartyId, out var playerParty));
            Assert.True(Server.ObjectManager.TryGetId(giverParty.Party, out var giverPartyBaseId));
            Assert.True(Server.ObjectManager.TryGetId(playerParty.Party, out var playerPartyBaseId));
            using (new AllowedThread())
            {
                giver.StayingInSettlement = null;
                giver.PartyBelongedTo = giverParty;
            }
            Assert.Null(giver.CurrentSettlement);

            var tracker = Server.Resolve<ConversationPartyTracker>();
            Assert.True(tracker.TryBeginEngagement(Client.NetPeer,
                samePlayer ? playerPartyBaseId : "another-player-party",
                sameGiver ? giverPartyBaseId : "another-lord-party", false));
            try
            {
                Assert.Equal(expected, Server.Resolve<IssueConversationHandler>()
                    .IsRequesterPresentWithIssueGiver(controllerId, giver));
            }
            finally
            {
                tracker.TryEndEngagement(Client.NetPeer, out _, out _);
            }
            Assert.False(Server.Resolve<IssueConversationHandler>()
                .IsRequesterPresentWithIssueGiver(controllerId, giver));
        });
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    public void ArmyMemberGiver_RequiresAttachmentToTheArmyThisPlayerEncountered(
        bool samePlayer, bool attached, bool expected)
    {
        var fixture = SetupVillageOwner();
        var controllerId = ConnectPlayerAwayFromIssueGiver(fixture);
        var giverPartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        var armyId = TestEnvironment.CreateRegisteredObject<Army>();
        Server.Call(() =>
        {
            var giver = Server.GetRegisteredObject<Hero>(fixture.HeroId);
            var giverParty = Server.GetRegisteredObject<MobileParty>(giverPartyId);
            var army = Server.GetRegisteredObject<Army>(armyId);
            Assert.True(Server.Resolve<IPlayerManager>().TryGetPlayer(controllerId, out var player));
            var playerParty = Server.GetRegisteredObject<MobileParty>(player.MobilePartyId);
            Assert.True(Server.ObjectManager.TryGetId(playerParty.Party, out var playerPartyId));
            Assert.True(Server.ObjectManager.TryGetId(army.LeaderParty.Party, out var leaderPartyId));
            using (new AllowedThread())
            {
                giver.StayingInSettlement = null;
                giver.PartyBelongedTo = giverParty;
                giverParty.Army = army;
                if (attached) giverParty.AttachedTo = army.LeaderParty;
            }
            var tracker = Server.Resolve<ConversationPartyTracker>();
            Assert.True(tracker.TryBeginEngagement(Client.NetPeer,
                samePlayer ? playerPartyId : "another-player-party", leaderPartyId, false));
            try
            {
                Assert.Equal(expected, Server.Resolve<IssueConversationHandler>()
                    .IsRequesterPresentWithIssueGiver(controllerId, giver));
                giverParty.AttachedTo = null;
                Assert.False(Server.Resolve<IssueConversationHandler>()
                    .IsRequesterPresentWithIssueGiver(controllerId, giver));
            }
            finally
            {
                tracker.TryEndEngagement(Client.NetPeer, out _, out _);
            }
        });
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, false, true)]
    public void HorseQuestConflict_UsesTheSolversControllerWithoutResolvingOtherPlayersParties(
        bool sameOwner, bool registerOtherOwner, bool expected)
    {
        var fixture = SetupVillageOwner();
        var controllerId = ConnectPlayer(fixture);
        var otherGiverId = TestEnvironment.CreateRegisteredObject<Hero>();
        Server.Call(() =>
        {
            Assert.True(Server.Resolve<IPlayerManager>().TryGetPlayer(controllerId, out var player));
            var hero = Server.GetRegisteredObject<Hero>(player.HeroId);
            var party = Server.GetRegisteredObject<MobileParty>(player.MobilePartyId);
            var giver = Server.GetRegisteredObject<Hero>(otherGiverId);
            if (registerOtherOwner)
                Assert.True(Server.Resolve<IPlayerManager>().AddPlayer(new Player("other-solver", otherGiverId, null, "", "")));
            var active = ObjectHelper.SkipConstructor<LordNeedsHorsesIssueBehavior.LordNeedsHorsesIssue>();
            active._issueState = IssueBase.IssueState.SolvingWithAlternativeSolution;
            var ownership = Server.Resolve<IIssueOwnershipRegistry>();
            ownership.SetOwner(giver, sameOwner ? controllerId : "other-solver");
            Campaign.Current.IssueManager._issues.Add(giver, active);
            try
            {
                using (new MainHeroSubstitutionScope(hero, party))
                    Assert.Equal(expected, Server.Resolve<ILordNeedsHorsesQuest>().HasConflictingQuest(
                        ObjectHelper.SkipConstructor<LordNeedsHorsesIssueBehavior.LordNeedsHorsesIssue>()));
            }
            finally
            {
                Campaign.Current.IssueManager._issues.Remove(giver);
                ownership.Clear(giver);
            }
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

    [Fact]
    public void RequestQuestTypeAcceptAlternative_Rejected_RestoresSentTroopsToTheClickingClientsMainParty()
    {
        var fixture = SetupVillageOwner();
        CreateIssueOnBothPeers(fixture);

        var controllerId = "player-A-" + Guid.NewGuid();
        var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        var eligibleTroopId = TestEnvironment.CreateRegisteredObject<CharacterObject>();
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
                memberCountBeforeSending = party.MemberRoster.TotalManCount;
                owner.Issue.AlternativeSolutionSentTroops.AddToCounts(companion.CharacterObject, 1);
                owner.Issue.AlternativeSolutionSentTroops.AddToCounts(eligibleTroop, 6);
            }
        });

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

        var alternativeRejection = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkQuestTypeAcceptRejected>());
        Assert.True(alternativeRejection.IsAlternative);

        Client.Call(() =>
        {
            Assert.True(Client.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));

            Assert.Equal(memberCountBeforeSending + 7, party.MemberRoster.TotalManCount);
            Assert.Equal(0, owner.Issue.AlternativeSolutionSentTroops.TotalManCount);
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
    [InlineData(6, false)]
    [InlineData(10, false)]
    [InlineData(6, true)]
    public void RequestQuestTypeAcceptAlternative_GenuineAccept_ConservesSelectedXpAndBroadcastsServerState(
        int sentCount, bool gainXpAfterSelection)
    {
        var fixture = SetupVillageOwner();
        CreateIssueOnBothPeers(fixture);
        var controllerId = ConnectPlayer(fixture);
        var targetId = TestEnvironment.CreateRegisteredObject<CharacterObject>();
        foreach (var instance in new[] { Server }.Concat(TestEnvironment.Clients))
        {
            instance.Call(() =>
            {
                var troop = instance.GetRegisteredObject<CharacterObject>(lastConnectedEligibleTroopId);
                var target = instance.GetRegisteredObject<CharacterObject>(targetId);
                using (new AllowedThread())
                {
                    target.Level = 26;
                    troop.Level = 20;
                    troop.UpgradeTargets = new[] { target };
                }
            });
        }
        string partyId = null;
        int xpPerTroop = 0;
        Server.Call(() =>
        {
            Assert.True(Server.Resolve<IPlayerManager>().TryGetPlayer(controllerId, out var player));
            partyId = player.MobilePartyId;
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Assert.True(Server.ObjectManager.TryGetObject<CharacterObject>(lastConnectedEligibleTroopId, out var troop));
            xpPerTroop = troop.GetUpgradeXpCost(party.Party, 0);
            Assert.True(xpPerTroop > 0);
            party.MemberRoster.AddToCounts(troop, 4, false, 0, 9 * xpPerTroop);
            Assert.Equal(9 * xpPerTroop, party.MemberRoster.GetElementXp(troop));
        });
        OpenConversation(fixture, controllerId);
        int claimedXp = (sentCount - 1) * xpPerTroop;

        Client.Call(() =>
        {
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(fixture.CompanionHeroId, out var companion));
            Assert.True(Client.ObjectManager.TryGetObject<CharacterObject>(lastConnectedEligibleTroopId, out var eligibleTroop));
            using (new AllowedThread())
            {
                owner.Issue.AlternativeSolutionSentTroops.AddToCounts(companion.CharacterObject, 1);
                owner.Issue.AlternativeSolutionSentTroops.AddToCounts(eligibleTroop, sentCount, false, 0, claimedXp);
            }
        });

        if (gainXpAfterSelection)
        {
            Server.Call(() =>
            {
                var party = Server.GetRegisteredObject<MobileParty>(partyId);
                var troop = Server.GetRegisteredObject<CharacterObject>(lastConnectedEligibleTroopId);
                party.MemberRoster.AddXpToTroop(troop, xpPerTroop);
                Assert.Equal(10 * xpPerTroop, party.MemberRoster.GetElementXp(troop));
            });
        }
        Client.Call(() =>
        {
            var owner = Client.GetRegisteredObject<Hero>(fixture.HeroId);
            owner.Issue.StartIssueWithAlternativeSolution();
        });

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));

            Assert.True(owner.Issue.IsSolvingWithAlternative);
            Assert.True(owner.Issue.AlternativeSolutionReturnTimeForTroops.IsFuture);

            Assert.True(Server.Resolve<IIssueOwnershipRegistry>().TryGetOwnerControllerId(owner, out var ownerControllerId));
            Assert.Equal(controllerId, ownerControllerId);
        });

        var accepted = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkQuestTypeAlternativeAccepted>());

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.Equal(owner.Issue.AlternativeSolutionReturnTimeForTroops, accepted.State.ReturnTime);
        });

        TestEnvironment.FlushCoalescer();
        int expectedSentXp = claimedXp + (gainXpAfterSelection ? xpPerTroop : 0);
        int expectedTotalXp = (gainXpAfterSelection ? 10 : 9) * xpPerTroop;
        foreach (var instance in new[] { Server, Client })
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
                Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
                Assert.True(instance.ObjectManager.TryGetObject<CharacterObject>(lastConnectedEligibleTroopId, out var troop));
                Assert.Equal(10 - sentCount, party.MemberRoster.GetTroopCount(troop));
                var retainedXp = sentCount == 10 ? 0 : party.MemberRoster.GetElementXp(troop);
                Assert.Equal((10 - sentCount) * xpPerTroop, retainedXp);
                Assert.Equal(expectedSentXp, owner.Issue.AlternativeSolutionSentTroops.GetElementXp(troop));
                Assert.Equal(expectedTotalXp, retainedXp + owner.Issue.AlternativeSolutionSentTroops.GetElementXp(troop));
            });
        }
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
