using Common.Messaging;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface.Services.BattleWreckages;
using GameInterface.Services.BattleWreckages.Interfaces;
using GameInterface.Services.BattleWreckages.Messages;
using GameInterface.Services.Entity;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.MobileParties.Messages.Behavior;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.BattleWreckages;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using Xunit.Abstractions;

namespace E2E.Tests.Services.BattleWreckages;

public class BattleWreckageLifetimeTests : BattleWreckageTestBase
{
    public BattleWreckageLifetimeTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public void ServerCreate_BattleWreckage_SyncAllClients()
    {
        // Act
        var battleWreckageId = CreateServerWreckage();

        // Assert
        foreach (var client in Clients)
        {
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<BattleWreckage>(battleWreckageId, out var battleWreckage));
                Assert.Contains(battleWreckage, Campaign.Current.Wreckages);
                Assert.Equal(BattleWreckage.WreckageType.Small, battleWreckage.WreckageTypeCategory);
            });
        }
    }

    [Fact]
    public void ServerCreate_BattleWreckage_AnnouncesOnClientsAfterItsFieldsArrive()
    {
        // Arrange, the map visual reads the wreckage's type and position once, when the interactable is created
        var typesWhenAnnounced = new Dictionary<EnvironmentInstance, List<BattleWreckage.WreckageType>>();
        foreach (var client in Clients)
        {
            var types = new List<BattleWreckage.WreckageType>();
            typesWhenAnnounced[client] = types;

            client.Call(() =>
            {
                CampaignEvents.MapInteractableCreated.AddNonSerializedListener(types, point =>
                {
                    if (point is BattleWreckage battleWreckage) types.Add(battleWreckage.WreckageTypeCategory);
                });
            });
        }

        // Deliver on the poller thread like a live client, so each handler's game thread work queues up behind the
        // deferred create and field values instead of running inline
        Server.Resolve<TestNetworkRouter>().ReceiveContext = TestNetworkReceiveContext.PollerThread;

        // Act
        CreateServerWreckage();

        foreach (var client in Clients) client.PumpGameThread();

        // Assert
        foreach (var client in Clients)
        {
            var type = Assert.Single(typesWhenAnnounced[client]);
            Assert.Equal(BattleWreckage.WreckageType.Small, type);
        }
    }

    [Fact]
    public void ServerCreate_BattleWreckage_SyncsRosters()
    {
        // Arrange
        var battleWreckageId = CreateServerWreckage();

        // Act
        var serverRosterIds = new List<string>();
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<BattleWreckage>(battleWreckageId, out var battleWreckage));

            var character = GameObjectCreator.CreateInitializedObject<CharacterObject>();
            battleWreckage.AttackerDiedInBattle.AddToCounts(character, 3);

            foreach (var roster in GetRosters(battleWreckage))
            {
                Assert.True(Server.ObjectManager.TryGetId(roster, out var rosterId));
                serverRosterIds.Add(rosterId);
            }
        });

        // Ownerless roster changes are coalesced and sent on the server's next tick
        TestEnvironment.FlushCoalescer();

        // Assert
        foreach (var client in Clients)
        {
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<BattleWreckage>(battleWreckageId, out var battleWreckage));

                var clientRosterIds = new List<string>();
                foreach (var roster in GetRosters(battleWreckage))
                {
                    Assert.True(client.ObjectManager.TryGetId(roster, out var rosterId));
                    clientRosterIds.Add(rosterId);
                }

                Assert.Equal(serverRosterIds, clientRosterIds);
                Assert.Equal(3, battleWreckage.AttackerDiedInBattle.TotalManCount);
            });
        }
    }

    [Fact]
    public void ServerDestroy_BattleWreckage_SyncAllClients()
    {
        // Arrange
        var battleWreckageId = CreateServerWreckage();

        var clientWreckages = new Dictionary<EnvironmentInstance, BattleWreckage>();
        foreach (var client in Clients)
        {
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<BattleWreckage>(battleWreckageId, out var battleWreckage));
                clientWreckages[client] = battleWreckage;
            });
        }

        // The map visual is removed on MapInteractableDestroyed
        var destroyedWreckages = new Dictionary<EnvironmentInstance, List<BattleWreckage>>();
        foreach (var client in Clients)
        {
            var destroyed = new List<BattleWreckage>();
            destroyedWreckages[client] = destroyed;

            client.Call(() =>
            {
                CampaignEvents.MapInteractableDestroyed.AddNonSerializedListener(destroyed, point =>
                {
                    if (point is BattleWreckage battleWreckage) destroyed.Add(battleWreckage);
                });
            });
        }

        // Deliver on the poller thread like a live client, so the remove and the registry's destroy queue on the game
        // thread in send order, and the remove has to find the wreckage before the destroy drops its id
        Server.Resolve<TestNetworkRouter>().ReceiveContext = TestNetworkReceiveContext.PollerThread;

        // Act
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<BattleWreckage>(battleWreckageId, out var battleWreckage));

            battleWreckage.DestroyWreckage();
        });

        foreach (var client in Clients) client.PumpGameThread();

        // Assert
        foreach (var client in Clients)
        {
            Assert.Same(clientWreckages[client], Assert.Single(destroyedWreckages[client]));

            client.Call(() =>
            {
                Assert.False(client.ObjectManager.TryGetObject<BattleWreckage>(battleWreckageId, out _));
                Assert.DoesNotContain(clientWreckages[client], Campaign.Current.Wreckages);
            });
        }
    }

    [Fact]
    public void ServerHourlyTick_DestroysIdleWreckagesAndExpiresStaleInterrupts()
    {
        // Arrange, both wreckages are past their destroy time and a player investigates one of them
        var heldWreckageId = CreateServerWreckage();
        var idleWreckageId = CreateServerWreckage();

        var clock = UseMapClock();
        try
        {
            Server.Call(() =>
            {
                Assert.True(Server.ObjectManager.TryGetObject<BattleWreckage>(heldWreckageId, out var heldWreckage));
                Assert.True(Server.ObjectManager.TryGetObject<BattleWreckage>(idleWreckageId, out var idleWreckage));
                StartMapClock();
                AddBattleWreckageBehavior();
                var hero = CreatePartyLeader();
                var campaignBehaviorInterface = Server.Resolve<IBattleWreckageCampaignBehaviorInterface>();
                var encounterTracker = Server.Resolve<IBattleWreckageEncounterTracker>();

                campaignBehaviorInterface.StartWreckageEncounter(heldWreckage, hero);

                // Act
                campaignBehaviorInterface.OnHourlyTick();

                // Assert, the investigation keeps its wreckage alive
                Assert.Contains(heldWreckage, Campaign.Current.Wreckages);
                Assert.DoesNotContain(idleWreckage, Campaign.Current.Wreckages);

                // Act, an interrupt that nobody resumes within the expiry window ends
                campaignBehaviorInterface.LeaveWreckageEncounter(hero, BattleWreckageCampaignBehavior.WreckageLeaveReason.PlayerInterrupted);
                AdvanceHours(BattleWreckageCampaignBehavior.InterruptedSnapshotExpiryAsHours - 1);
                campaignBehaviorInterface.OnHourlyTick();
                Assert.True(encounterTracker.TryGetEncounter(hero, out _));

                AdvanceHours(2);
                campaignBehaviorInterface.OnHourlyTick();

                // Assert
                Assert.False(encounterTracker.TryGetEncounter(hero, out _));
                var left = Server.InternalMessages.GetMessages<WreckageEncounterLeft>().ToList();
                Assert.Equal(2, left.Count);
                Assert.Equal(BattleWreckageCampaignBehavior.WreckageLeaveReason.PlayerInterrupted, left[0].Reason);
                Assert.Equal(BattleWreckageCampaignBehavior.WreckageLeaveReason.WreckageExpired, left[1].Reason);
            });
        }
        finally
        {
            clock.UnpatchAll(clock.Id);
        }

        foreach (var client in Clients)
        {
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<BattleWreckage>(heldWreckageId, out _));
                Assert.False(client.ObjectManager.TryGetObject<BattleWreckage>(idleWreckageId, out _));
            });
        }
    }

    [Fact]
    public void ServerMoveToWreckage_SyncsInteractableToAllClients()
    {
        // Arrange, a client's move to a wreckage goes through the same snapshot and wire mapping
        var battleWreckageId = CreateServerWreckage();
        var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();

        // Act
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<BattleWreckage>(battleWreckageId, out var battleWreckage));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));

            party.SetMoveGoToInteractablePoint(battleWreckage, MobileParty.NavigationType.Default);
            TestEnvironment.FlushCoalescer();
        });

        // Assert
        foreach (var client in Clients)
        {
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<BattleWreckage>(battleWreckageId, out var battleWreckage));
                Assert.True(client.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
                Assert.Same(battleWreckage, party.Ai.AiBehaviorInteractable);
            });
        }
    }

    [Fact]
    public void ClientReachesWreckage_BeforeMoveEcho_KeepsHoldEverywhere()
    {
        // Arrange, the first client controls a party that the player sends to a wreckage
        var battleWreckageId = CreateServerWreckage();
        var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        var owner = Clients.First();
        var player = new Player("WreckagePlayer", null, partyId, null, null);

        Server.Call(() =>
        {
            var playerManager = Server.Resolve<IPlayerManager>();
            Assert.True(playerManager.AddPlayer(player));
            playerManager.SetPeer(player.ControllerId, owner.NetPeer);
        });

        owner.Call(() =>
        {
            owner.Resolve<IControllerIdProvider>().SetControllerId(player.ControllerId);
            Assert.True(owner.Resolve<IPlayerManager>().AddPlayer(player));

            Assert.True(owner.ObjectManager.TryGetObject<BattleWreckage>(battleWreckageId, out var battleWreckage));
            Assert.True(owner.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Assert.True(party.IsControlledByThisInstance());

            party.SetMoveGoToInteractablePoint(battleWreckage, MobileParty.NavigationType.Default);
            MessageBroker.Instance.Publish(party.Ai, new PartyBehaviorChangeAttempted(party));
        });

        // The server echoes the move, but the client reaches the wreckage before it applies the echo
        Server.PumpGameThread();
        TestEnvironment.FlushCoalescer();

        // Act
        owner.Call(() =>
        {
            Assert.True(owner.ObjectManager.TryGetObject<BattleWreckage>(battleWreckageId, out var battleWreckage));
            Assert.True(owner.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            battleWreckage.OnPartyInteraction(party);
        });
        owner.PumpGameThread();
        Server.PumpGameThread();
        TestEnvironment.FlushCoalescer();

        // Assert, the echo doesn't put the wreckage back, which would start a second encounter that resumes the first
        foreach (var instance in new[] { Server }.Concat(Clients))
        {
            instance.PumpGameThread();
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
                Assert.Null(party.Ai.AiBehaviorInteractable);
                Assert.Equal(AiBehavior.Hold, party.DefaultBehavior);
            });
        }
    }

    private static TroopRoster[] GetRosters(BattleWreckage battleWreckage) => new[]
    {
        battleWreckage.AttackerDiedInBattle,
        battleWreckage.DefenderDiedInBattle,
        battleWreckage.AttackerWoundedInBattle,
        battleWreckage.DefenderWoundedInBattle,
    };
}
