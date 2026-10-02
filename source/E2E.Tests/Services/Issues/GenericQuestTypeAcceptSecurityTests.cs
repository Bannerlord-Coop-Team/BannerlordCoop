using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Data;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Generic.AcceptMirror;
using GameInterface.Services.Issues.Handlers;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Issues.Messages;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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

        foreach (var instance in new[] { Server }.Concat(TestEnvironment.Clients))
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
    public void ExtortionAlternativeAccepted_NotifiesOnlyItsNewOwnerOnce()
    {
        var fixture = SetupVillageOwner();
        var controllerId = "extortion-owner";
        var notifications = new List<(EnvironmentInstance Client, Hero Solver, bool IsOwner, bool ReceivedApply)>();
        var listener = new object();
        NetworkQuestTypeAlternativeAccepted accepted = default;
        foreach (var instance in new[] { Server }.Concat(TestEnvironment.Clients))
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var giver));
                var potential = new PotentialIssueData(
                    (in PotentialIssueData _, Hero hero) => new ExtortionByDesertersIssueBehavior.ExtortionByDesertersIssue(hero),
                    typeof(ExtortionByDesertersIssueBehavior.ExtortionByDesertersIssue), IssueBase.IssueFrequency.VeryCommon);
                if (giver.Issue == null)
                {
                    using (new AllowedThread())
                        Assert.True(Campaign.Current.IssueManager.CreateNewIssue(in potential, giver));
                }
                Assert.IsType<ExtortionByDesertersIssueBehavior.ExtortionByDesertersIssue>(giver.Issue);

                if (instance == Server)
                {
                    Assert.True(instance.ObjectManager.TryGetObject<Hero>(fixture.CompanionHeroId, out var companion));
                    var troops = TroopRoster.CreateDummyTroopRoster();
                    using (new AllowedThread()) troops.AddToCounts(companion.CharacterObject, 1);
                    var state = new AlternativeSolutionVanillaState(CampaignTime.DaysFromNow(5),
                        CampaignTime.DaysFromNow(4), 0.1f, 1, 1500, null);
                    accepted = new NetworkQuestTypeAlternativeAccepted(fixture.HeroId, controllerId, state,
                        GenericAcceptFieldsSerializer.Serialize(new ExtortionAlternativeAcceptFields(0.4f, state)),
                        instance.Resolve<GameInterface.Services.TroopRosters.Interfaces.ITroopRosterInterface>()
                            .PackTroopRosterData(troops));
                    return;
                }

                instance.Resolve<IControllerIdProvider>().SetControllerId(instance == Client ? controllerId : "extortion-observer");
                CampaignEvents.OnIssueUpdatedEvent.AddNonSerializedListener(listener, (issue, status, solver) =>
                {
                    if (issue == giver.Issue && status == IssueBase.IssueUpdateDetails.PlayerSentTroopsToQuest)
                        notifications.Add((instance, solver, instance.Resolve<IIssueOwnershipRegistry>().IsLocalPeerOwner(giver),
                            AllowedThread.IsThisThreadAllowed()));
                });
            });
        }

        try
        {
            Server.Call(() => Server.Resolve<Common.Network.INetwork>().SendAll(accepted));
            foreach (var client in TestEnvironment.Clients)
                client.Call(() =>
                {
                    Assert.True(client.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var giver));
                    Assert.True(giver.Issue.IsSolvingWithAlternative);
                    Assert.Single(giver.Issue.JournalEntries);
                });
            var notification = Assert.Single(notifications);
            Assert.Same(Client, notification.Client);
            Assert.True(notification.IsOwner);
            Assert.True(notification.ReceivedApply);
            Client.Call(() => Assert.Same(Hero.MainHero, notification.Solver));

            Server.Call(() => Server.Resolve<Common.Network.INetwork>().SendAll(accepted));
            Assert.Single(notifications);
        }
        finally
        {
            foreach (var client in TestEnvironment.Clients)
                client.Call(() => CampaignEventDispatcher.Instance.RemoveListeners(listener));
        }
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

    [Fact]
    public void FailedAlternativeStart_RestoresCompanionAndTroopsExactlyOnce()
    {
        var fixture = SetupVillageOwner();
        CreateIssueOnBothPeers(fixture);
        var controllerId = ConnectPlayer(fixture);
        var upgradeTargetId = TestEnvironment.CreateRegisteredObject<CharacterObject>();
        foreach (var instance in new[] { Server }.Concat(TestEnvironment.Clients))
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<CharacterObject>(lastConnectedEligibleTroopId, out var troop));
                Assert.True(instance.ObjectManager.TryGetObject<CharacterObject>(upgradeTargetId, out var target));
                target.Level = 30;
                troop.UpgradeTargets = new[] { target };
            });
        }

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.CompanionHeroId, out var companion));
            Assert.True(Server.ObjectManager.TryGetObject<CharacterObject>(lastConnectedEligibleTroopId, out var troop));
            Assert.True(Server.Resolve<IPlayerManager>().TryGetPlayer(controllerId, out var player));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(player.MobilePartyId, out var party));
            var claimed = TroopRoster.CreateDummyTroopRoster();
            using (new AllowedThread())
            {
                companion.ChangeState(Hero.CharacterStates.Active);
                claimed.AddToCounts(companion.CharacterObject, 1);
                claimed.AddToCounts(troop, 6, xpChange: 600);
            }
            party.MemberRoster.AddToCounts(troop, 4, xpChange: 1000);

            var originalCount = party.MemberRoster.TotalManCount;
            Assert.Equal(1000, party.MemberRoster.GetElementXp(troop));
            var snapshot = new AlternativeSolutionStartSnapshot(owner.Issue);
            var originalDueTime = owner.Issue.IssueDueTime;
            var originalDifficulty = owner.Issue.IssueDifficultyMultiplier;
            var originalStoredDifficulty = owner.Issue._issueDifficultyMultiplier;
            var originalJournalCount = owner.Issue.JournalEntries.Count;
            var originallyTried = owner.Issue.IsTriedToSolveBefore;
            AlternativeSolutionStartRunner.StartOnServerFromClaim(owner, player, claimed);
            Assert.Equal(400, party.MemberRoster.GetElementXp(troop));
            Assert.True(companion.IsDisabled);
            var handler = Server.Resolve<GenericQuestTypeAcceptHandler>();
            var rollback = AccessTools.Method(typeof(GenericQuestTypeAcceptHandler), "RollbackFailedAlternativeAcceptStart");
            var failure = Assert.Throws<TargetInvocationException>(() =>
                rollback.Invoke(handler, new object[] { owner, "missing-player", snapshot }));
            Assert.IsType<InvalidOperationException>(failure.InnerException);
            Assert.Equal(7, owner.Issue.AlternativeSolutionSentTroops.TotalManCount);
            rollback.Invoke(handler, new object[] { owner, controllerId, snapshot });
            rollback.Invoke(handler, new object[] { owner, controllerId, snapshot });

            Assert.True(companion.IsActive);
            Assert.Same(party, companion.PartyBelongedTo);
            Assert.Equal(originalCount, party.MemberRoster.TotalManCount);
            Assert.Equal(1000, party.MemberRoster.GetElementXp(troop));
            Assert.Equal(0, owner.Issue.AlternativeSolutionSentTroops.TotalManCount);
            Assert.True(owner.Issue.IsOngoingWithoutQuest);
            Assert.Equal(originalDueTime, owner.Issue.IssueDueTime);
            Assert.Equal(originalDifficulty, owner.Issue.IssueDifficultyMultiplier);
            Assert.Equal(originalStoredDifficulty, owner.Issue._issueDifficultyMultiplier);
            Assert.Equal(originalJournalCount, owner.Issue.JournalEntries.Count);
            Assert.Equal(originallyTried, owner.Issue.IsTriedToSolveBefore);
            Assert.False(Server.Resolve<IIssueOwnershipRegistry>().TryGetOwnerControllerId(owner, out _));
        });

        TestEnvironment.FlushCoalescer();
        foreach (var client in TestEnvironment.Clients)
        {
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<Hero>(fixture.CompanionHeroId, out var companion));
                // The bootstrap replaces ChangeState, so client hero-state replication requires a live run.
                Assert.NotNull(companion.PartyBelongedTo);
                Assert.Equal(1, companion.PartyBelongedTo.MemberRoster.GetTroopCount(companion.CharacterObject));
                Assert.True(client.ObjectManager.TryGetObject<CharacterObject>(lastConnectedEligibleTroopId, out var troop));
                Assert.Equal(10, companion.PartyBelongedTo.MemberRoster.GetTroopCount(troop));
                // Observer parties receive counts; only their controller receives troop XP.
                Assert.Equal(client == Client ? 1000 : 0, companion.PartyBelongedTo.MemberRoster.GetElementXp(troop));
            });
        }
    }

    [Fact]
    public void PrivateAlternativeSelection_IsSentWithoutPredictingAClientTroopTransfer()
    {
        var fixture = SetupVillageOwner();
        CreateIssueOnBothPeers(fixture);
        var controllerId = ConnectPlayer(fixture);
        OpenConversation(fixture, controllerId);
        var upgradeTargetId = TestEnvironment.CreateRegisteredObject<CharacterObject>();
        foreach (var instance in new[] { Server }.Concat(TestEnvironment.Clients))
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<CharacterObject>(lastConnectedEligibleTroopId, out var troop));
                Assert.True(instance.ObjectManager.TryGetObject<CharacterObject>(upgradeTargetId, out var target));
                target.Level = 30;
                troop.UpgradeTargets = new[] { target };
            });
        }
        var originalCount = 0;
        Server.Call(() =>
        {
            Assert.True(Server.Resolve<IPlayerManager>().TryGetPlayer(controllerId, out var player));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(player.MobilePartyId, out var party));
            Assert.True(Server.ObjectManager.TryGetObject<CharacterObject>(lastConnectedEligibleTroopId, out var troop));
            party.MemberRoster.AddToCounts(troop, 4, xpChange: 1000);
            originalCount = party.MemberRoster.TotalManCount;
        });

        Client.Call(() =>
        {
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(fixture.CompanionHeroId, out var companion));
            Assert.True(Client.ObjectManager.TryGetObject<CharacterObject>(lastConnectedEligibleTroopId, out var troop));
            var selection = TroopRoster.CreateDummyTroopRoster();
            selection.AddToCounts(companion.CharacterObject, 1);
            selection.AddToCounts(troop, 6, xpChange: 600);
            Assert.Equal(0, owner.Issue.AlternativeSolutionSentTroops.TotalManCount);

            Common.Messaging.MessageBroker.Instance.Publish(owner,
                new QuestTypeAlternativeAcceptTriggered(owner, controllerId, selection));
        });

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Server.Resolve<IPlayerManager>().TryGetPlayer(controllerId, out var player));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(player.MobilePartyId, out var party));
            Assert.True(owner.Issue.IsSolvingWithAlternative);
            Assert.Equal(7, owner.Issue.AlternativeSolutionSentTroops.TotalManCount);
            Assert.Equal(originalCount - 7, party.MemberRoster.TotalManCount);
            Assert.True(Server.ObjectManager.TryGetObject<CharacterObject>(lastConnectedEligibleTroopId, out var troop));
            Assert.Equal(400, party.MemberRoster.GetElementXp(troop));
            Assert.Equal(600, owner.Issue.AlternativeSolutionSentTroops.GetElementXp(troop));
            Campaign.Current.IssueManager.TryToMakeTroopsReturn(owner.Issue);
            Assert.True(Server.Resolve<IAwaitingAlternativeSolutionTroopsRegistry>().TryGet(controllerId, out var returning));
            Assert.Equal(600, returning.GetElementXp(troop));
        });
        var accepted = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkQuestTypeAlternativeAccepted>());
        Assert.Equal(controllerId, accepted.OwnerControllerId);
    }

    [Fact]
    public void RequestQuestTypeAcceptAlternative_GenuineAccept_BroadcastStateIsServerComputedNotClientSupplied()
    {
        var fixture = SetupVillageOwner();
        CreateIssueOnBothPeers(fixture);
        var controllerId = ConnectPlayer(fixture);
        OpenConversation(fixture, controllerId);

        Client.Call(() =>
        {
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(fixture.CompanionHeroId, out var companion));
            Assert.True(Client.ObjectManager.TryGetObject<CharacterObject>(lastConnectedEligibleTroopId, out var eligibleTroop));
            using (new AllowedThread())
            {
                owner.Issue.AlternativeSolutionSentTroops.AddToCounts(companion.CharacterObject, 1);
                owner.Issue.AlternativeSolutionSentTroops.AddToCounts(eligibleTroop, 6);
            }

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
