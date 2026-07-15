using System;
using System.Linq;
using Common.Messaging;
using Common.Util;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Environment.MockEngine;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Messages;
using Missions;
using Missions.Battles;
using Missions.Messages;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions;

/// <summary>
/// BR-033 (Restored Troop Control) / BR-034 (Invalid Former Troops): after a disconnected player reconnects
/// and synchronizes, they resume CONTROL of their previously ASSIGNED surviving troops; troops removed while
/// they were away are not restored. The fixture is the BR-031 adoption (the host temporarily assumed control
/// of the dropped player's survivors, preserving the assignment — the registry's OriginalOwner); the behavior
/// under test is the RECLAIM on the returner's re-entry: the same server-mediated
/// <see cref="NetworkMissionPeerEntered"/> that drives the join catch-up makes every instance return authority
/// to the original owner, the (current) holder releases its live AI control back to an inert puppet, and the
/// returner — caught up by the holder's replay, whose records carry the ASSIGNMENT owner — re-adopts its own
/// agents as locally driven (hero as the player-controlled main agent when alive, troops as local AI).
/// Movement is IPacket (not routable in this harness), so the tests assert registry authority, Controller
/// state and roster/registry contents, never motion.
/// </summary>
public class BattleReconnectControlTests : MissionTestEnvironment
{
    public BattleReconnectControlTests(ITestOutputHelper output) : base(output, numClients: 3) { }

    /// <summary>The MapEventParty id wrapping a player's party — the attribution the spawn records carry.</summary>
    private string GetMapEventPartyId(string mapEventId, string partyId)
    {
        string mepId = null;
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(mapEventId, out var mapEvent));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            var mep = mapEvent.AttackerSide.Parties.Concat(mapEvent.DefenderSide.Parties)
                .Last(p => p.Party == party.Party);
            Assert.True(Server.ObjectManager.TryGetId(mep, out mepId));
        }, MapEventDisabledMethods);
        Assert.NotNull(mepId);
        return mepId;
    }

    /// <summary>
    /// A registered troop character every instance resolves, plus one linked to the RETURNING client's own
    /// hero (statics are per-instance in this harness, so the link is made under the returner's scope — on
    /// every other instance the character is just some hero, not the local main hero).
    /// </summary>
    private (string heroCharacterId, string troopCharacterId) CreateBattleCharacters(EnvironmentInstance returner)
    {
        var heroCharacterId = CreateRegisteredObject<CharacterObject>();
        var troopCharacterId = CreateRegisteredObject<CharacterObject>();

        returner.Call(() =>
        {
            using (new AllowedThread())
            {
                Assert.True(returner.ObjectManager.TryGetObject<CharacterObject>(heroCharacterId, out var heroCharacter));
                heroCharacter.HeroObject = Hero.MainHero;
            }
        });

        return (heroCharacterId, troopCharacterId);
    }

    /// <summary>Stand a client up inside the battle: mock mission, controller, session, host assignment.</summary>
    private static (CoopBattleController controller, MockMission mock) StandUpBattleClient(
        MissionEngineFixture fixture, EnvironmentInstance client, string mapEventId, BattleHostAssignment assignment)
    {
        CoopBattleController controller = null;
        MockMission mock = null;
        client.Call(() =>
        {
            mock = fixture.CreateMission(client);
            controller = client.Resolve<CoopBattleController>();
            controller.Session.TryBegin(mapEventId);
            client.Resolve<IBattleHostRegistry>().Set(mapEventId, assignment);
        });
        return (controller, mock);
    }

    /// <summary>Simulate this instance receiving the returner's ORIGINAL spawn broadcast over the mesh.</summary>
    private static void ReceiveSpawnRecords(EnvironmentInstance instance, BattleAgentSpawnData[] records)
    {
        instance.Call(() => instance.Resolve<IMessageBroker>().Publish(instance, new NetworkSpawnBattleAgents(records)));
    }

    private static void Publish<T>(EnvironmentInstance instance, T message) where T : IMessage
    {
        instance.Call(() => instance.Resolve<IMessageBroker>().Publish(instance, message));
    }

    private static void AssertAuthority(EnvironmentInstance instance, Guid agentId, string expectedAuthority, string expectedOriginalOwner)
    {
        instance.Call(() =>
        {
            var registry = instance.Resolve<INetworkAgentRegistry>();
            Assert.True(registry.TryGetAgentInfo(agentId, out var info), $"agent {agentId} is not registered on {instance.GetType().Name}");
            Assert.Equal(expectedAuthority, info.CurrentAuthority);
            Assert.Equal(expectedOriginalOwner, info.OriginalOwner);
        });
    }

    private static void AssertController(EnvironmentInstance instance, Guid agentId, AgentControllerType expected)
    {
        instance.Call(() =>
        {
            var registry = instance.Resolve<INetworkAgentRegistry>();
            Assert.True(registry.TryGetAgentInfo(agentId, out var info));
            Assert.True(AgentMirror.TryGet(info.Agent, out var mirror));
            Assert.Equal(expected, mirror.Controller);
        });
    }

    /// <summary>
    /// BR-033 core, on every instance: "C" disconnects mid-battle (the host "H" adopts its surviving troops —
    /// BR-031, the fixture), then re-enters through the same server-mediated entry that drives the join
    /// catch-up. Afterwards EVERY instance has the survivors' CurrentAuthority back at "C" (assignment intact),
    /// the host is no longer AI-driving them (released back to inert puppets), and the returner is locally
    /// controlling them — its hero re-adopted as the player-controlled main agent, its troops as local AI.
    /// </summary>
    [Fact]
    [Trait("Requirement", "BR-033")]
    public void Reconnect_RestoresControlOfSurvivingTroops_OnEveryInstance()
    {
        using var fixture = new MissionEngineFixture();
        var (mapEventId, partyIds) = SetupCoopBattle("H", "B", "C");
        var clients = Clients.ToArray();
        var host = clients[0];
        var bystander = clients[1];
        var returner = clients[2];

        var cMepId = GetMapEventPartyId(mapEventId, partyIds[2]);
        var (heroCharacterId, troopCharacterId) = CreateBattleCharacters(returner);

        var heroAgentId = Guid.NewGuid();
        var troopAgentId = Guid.NewGuid();

        var assignment = new BattleHostAssignment("H", new[] { "B", "C" });

        try
        {
            BattleSpawnGate.BeginBattle(mapEventId);

            var (hostController, hostMock) = StandUpBattleClient(fixture, host, mapEventId, assignment);
            var (bystanderController, bystanderMock) = StandUpBattleClient(fixture, bystander, mapEventId, assignment);

            // C's original spawn broadcast (its hero + one troop), as H and B received it before the drop.
            var records = new[]
            {
                new BattleAgentSpawnData(heroAgentId, heroCharacterId, default, BattleSideEnum.Attacker, 100f, "C", cMepId, 11),
                new BattleAgentSpawnData(troopAgentId, troopCharacterId, default, BattleSideEnum.Attacker, 100f, "C", cMepId, 12),
            };
            ReceiveSpawnRecords(host, records);
            ReceiveSpawnRecords(bystander, records);

            // Fixture sanity: both clients hold C's agents as inert puppets assigned to C.
            AssertAuthority(host, troopAgentId, "C", "C");
            AssertAuthority(bystander, troopAgentId, "C", "C");

            // C drops ungracefully (server-mediated). BR-031: the host adopts; the bystander does not.
            Publish(host, new MissionPeerDisconnected("C", mapEventId));
            Publish(bystander, new MissionPeerDisconnected("C", mapEventId));

            AssertAuthority(host, heroAgentId, "H", "C");
            AssertAuthority(host, troopAgentId, "H", "C");
            AssertController(host, troopAgentId, AgentControllerType.AI);
            AssertAuthority(bystander, troopAgentId, "C", "C");

            // C reconnects: a fresh mission on its client, then the server tells the existing members it
            // entered the instance — the same exchange that drives the join-info/catch-up flow.
            var (returnerController, returnerMock) = StandUpBattleClient(fixture, returner, mapEventId, assignment);

            Publish(host, new NetworkMissionPeerEntered("C", mapEventId));
            Publish(bystander, new NetworkMissionPeerEntered("C", mapEventId));

            // Every instance has authority back at the original owner, assignment untouched.
            AssertAuthority(host, heroAgentId, "C", "C");
            AssertAuthority(host, troopAgentId, "C", "C");
            AssertAuthority(bystander, heroAgentId, "C", "C");
            AssertAuthority(bystander, troopAgentId, "C", "C");

            // The host released its temporary live control: C's agents are inert puppets again, not host AI.
            AssertController(host, heroAgentId, AgentControllerType.None);
            AssertController(host, troopAgentId, AgentControllerType.None);
            AssertController(bystander, troopAgentId, AgentControllerType.None);

            // The returner controls its own agents locally: hero re-adopted as the player-controlled main
            // agent; the troop is a locally driven AI combatant (its movement polling covers GetAgents("C")).
            returner.Call(() =>
            {
                var registry = returner.Resolve<INetworkAgentRegistry>();

                Assert.True(registry.TryGetAgentInfo(heroAgentId, out var heroInfo), "the returner's hero was not restored");
                Assert.Equal("C", heroInfo.CurrentAuthority);
                Assert.Equal("C", heroInfo.OriginalOwner);
                Assert.True(registry.IsLocallyControlled(heroAgentId));
                Assert.True(AgentMirror.TryGet(heroInfo.Agent, out var heroMirror));
                Assert.Equal(AgentControllerType.Player, heroMirror.Controller);
                Assert.Same(heroInfo.Agent, returnerMock.MainAgent);

                Assert.True(registry.TryGetAgentInfo(troopAgentId, out var troopInfo), "the returner's troop was not restored");
                Assert.Equal("C", troopInfo.CurrentAuthority);
                Assert.Equal("C", troopInfo.OriginalOwner);
                Assert.True(registry.IsLocallyControlled(troopAgentId));
                Assert.True(AgentMirror.TryGet(troopInfo.Agent, out var troopMirror));
                Assert.Equal(AgentControllerType.AI, troopMirror.Controller);
            });

            GC.KeepAlive(hostController);
            GC.KeepAlive(bystanderController);
            GC.KeepAlive(returnerController);
            GC.KeepAlive(hostMock);
            GC.KeepAlive(bystanderMock);
        }
        finally
        {
            BattleSpawnGate.EndBattle();
        }
    }

    /// <summary>
    /// BR-034: agents of the disconnected player killed while they were away (through the real replicated
    /// death path) are NOT restored on reconnect — only the survivors transfer back. The dead stay out of
    /// every registry and nothing re-registers or re-spawns them on the returner (baseline-relative roster
    /// asserts). The hero is among the dead here, so no main agent is re-adopted either — the troops still
    /// return to the player's control.
    /// </summary>
    [Fact]
    [Trait("Requirement", "BR-034")]
    public void TroopsRemovedWhileDisconnected_AreNotRestored_OnReconnect()
    {
        using var fixture = new MissionEngineFixture();
        var (mapEventId, partyIds) = SetupCoopBattle("H", "B", "C");
        var clients = Clients.ToArray();
        var host = clients[0];
        var bystander = clients[1];
        var returner = clients[2];

        var cMepId = GetMapEventPartyId(mapEventId, partyIds[2]);
        var (heroCharacterId, troopCharacterId) = CreateBattleCharacters(returner);

        var heroAgentId = Guid.NewGuid();
        var survivorAgentId = Guid.NewGuid();
        var casualtyAgentId = Guid.NewGuid();

        var assignment = new BattleHostAssignment("H", new[] { "B", "C" });

        try
        {
            BattleSpawnGate.BeginBattle(mapEventId);

            var (hostController, hostMock) = StandUpBattleClient(fixture, host, mapEventId, assignment);
            var (bystanderController, bystanderMock) = StandUpBattleClient(fixture, bystander, mapEventId, assignment);

            var records = new[]
            {
                new BattleAgentSpawnData(heroAgentId, heroCharacterId, default, BattleSideEnum.Attacker, 100f, "C", cMepId, 21),
                new BattleAgentSpawnData(survivorAgentId, troopCharacterId, default, BattleSideEnum.Attacker, 100f, "C", cMepId, 22),
                new BattleAgentSpawnData(casualtyAgentId, troopCharacterId, default, BattleSideEnum.Attacker, 100f, "C", cMepId, 23),
            };
            ReceiveSpawnRecords(host, records);
            ReceiveSpawnRecords(bystander, records);

            // C drops; the host adopts all three (BR-031).
            Publish(host, new MissionPeerDisconnected("C", mapEventId));
            Publish(bystander, new MissionPeerDisconnected("C", mapEventId));
            AssertAuthority(host, casualtyAgentId, "H", "C");

            // While C is away, its hero and one troop die under host control — the REAL replicated death
            // path: the authority broadcasts the death, every client deregisters, the server is told.
            host.Call(() =>
            {
                var registry = host.Resolve<INetworkAgentRegistry>();
                Assert.True(registry.TryGetAgentInfo(casualtyAgentId, out var casualtyInfo));
                host.Resolve<IMessageBroker>().Publish(host, new BattleAgentDied(casualtyInfo.Agent, null, wounded: false));
                Assert.True(registry.TryGetAgentInfo(heroAgentId, out var heroInfo));
                host.Resolve<IMessageBroker>().Publish(host, new BattleAgentDied(heroInfo.Agent, null, wounded: false));
            });

            // Dead and deregistered everywhere before the reconnect.
            host.Call(() =>
            {
                var registry = host.Resolve<INetworkAgentRegistry>();
                Assert.False(registry.TryGetAgentInfo(casualtyAgentId, out _));
                Assert.False(registry.TryGetAgentInfo(heroAgentId, out _));
            });
            bystander.Call(() =>
            {
                var registry = bystander.Resolve<INetworkAgentRegistry>();
                Assert.False(registry.TryGetAgentInfo(casualtyAgentId, out _));
                Assert.False(registry.TryGetAgentInfo(heroAgentId, out _));
            });

            // C reconnects.
            var (returnerController, returnerMock) = StandUpBattleClient(fixture, returner, mapEventId, assignment);
            int returnerAgentBaseline = returnerMock.Agents.Count;

            Publish(host, new NetworkMissionPeerEntered("C", mapEventId));
            Publish(bystander, new NetworkMissionPeerEntered("C", mapEventId));

            // Only the SURVIVOR transferred back...
            AssertAuthority(host, survivorAgentId, "C", "C");
            AssertAuthority(bystander, survivorAgentId, "C", "C");
            AssertController(host, survivorAgentId, AgentControllerType.None);

            // ...and the dead were not restored: absent from every registry, and nothing re-registered or
            // re-spawned them on the returner (exactly one agent arrived relative to the baseline).
            host.Call(() =>
            {
                var registry = host.Resolve<INetworkAgentRegistry>();
                Assert.False(registry.TryGetAgentInfo(casualtyAgentId, out _));
                Assert.False(registry.TryGetAgentInfo(heroAgentId, out _));
            });
            bystander.Call(() =>
            {
                var registry = bystander.Resolve<INetworkAgentRegistry>();
                Assert.False(registry.TryGetAgentInfo(casualtyAgentId, out _));
                Assert.False(registry.TryGetAgentInfo(heroAgentId, out _));
            });
            returner.Call(() =>
            {
                var registry = returner.Resolve<INetworkAgentRegistry>();
                Assert.True(registry.TryGetAgentInfo(survivorAgentId, out var survivorInfo), "the surviving troop was not restored");
                Assert.Equal("C", survivorInfo.CurrentAuthority);
                Assert.True(AgentMirror.TryGet(survivorInfo.Agent, out var survivorMirror));
                Assert.Equal(AgentControllerType.AI, survivorMirror.Controller);

                Assert.False(registry.TryGetAgentInfo(casualtyAgentId, out _), "a troop killed while the player was away must not be restored");
                Assert.False(registry.TryGetAgentInfo(heroAgentId, out _), "a hero killed while the player was away must not be restored");

                Assert.Equal(returnerAgentBaseline + 1, returnerMock.Agents.Count);
                Assert.Null(returnerMock.MainAgent); // the dead hero is not re-adopted
            });

            GC.KeepAlive(hostController);
            GC.KeepAlive(bystanderController);
            GC.KeepAlive(returnerController);
            GC.KeepAlive(hostMock);
            GC.KeepAlive(bystanderMock);
        }
        finally
        {
            BattleSpawnGate.EndBattle();
        }
    }

    /// <summary>
    /// BR-033 against a MIGRATED host: the original host "A" left too, "B" was promoted (BR-014) and holds
    /// the adoption of C's troops. The reclaim works against whoever holds host authority NOW: the new host
    /// releases the agents and the returner gets them back.
    /// </summary>
    [Fact]
    [Trait("Requirement", "BR-033")]
    public void Reclaim_ReturnsAgents_FromAMigratedHost()
    {
        using var fixture = new MissionEngineFixture();
        var (mapEventId, partyIds) = SetupCoopBattle("A", "B", "C");
        var clients = Clients.ToArray();
        var newHost = clients[1];   // "B"
        var returner = clients[2];  // "C"

        var cMepId = GetMapEventPartyId(mapEventId, partyIds[2]);
        var (_, troopCharacterId) = CreateBattleCharacters(returner);

        var troopAgentId = Guid.NewGuid();

        try
        {
            BattleSpawnGate.BeginBattle(mapEventId);

            // B stands in the battle under the ORIGINAL host A...
            var (newHostController, newHostMock) = StandUpBattleClient(
                fixture, newHost, mapEventId, new BattleHostAssignment("A", new[] { "B", "C" }));

            ReceiveSpawnRecords(newHost, new[]
            {
                new BattleAgentSpawnData(troopAgentId, troopCharacterId, default, BattleSideEnum.Attacker, 100f, "C", cMepId, 31),
            });

            // ...then A leaves and the server promotes B (BR-014): the host assignment moves to B.
            newHost.Call(() =>
            {
                newHost.Resolve<IBattleHostRegistry>().Set(mapEventId, new BattleHostAssignment("B", new[] { "C" }));
                newHost.Resolve<IMessageBroker>().Publish(newHost, new BattleHostMigrated(mapEventId, "A"));
            });

            // C drops while B is the (migrated) host: B adopts C's surviving troop (BR-031).
            Publish(newHost, new MissionPeerDisconnected("C", mapEventId));
            AssertAuthority(newHost, troopAgentId, "B", "C");
            AssertController(newHost, troopAgentId, AgentControllerType.AI);

            // C reconnects against the migrated host.
            var (returnerController, returnerMock) = StandUpBattleClient(
                fixture, returner, mapEventId, new BattleHostAssignment("B", new[] { "C" }));

            Publish(newHost, new NetworkMissionPeerEntered("C", mapEventId));

            // The migrated host released the adoption back to the returning owner.
            AssertAuthority(newHost, troopAgentId, "C", "C");
            AssertController(newHost, troopAgentId, AgentControllerType.None);

            // The returner controls the troop locally.
            returner.Call(() =>
            {
                var registry = returner.Resolve<INetworkAgentRegistry>();
                Assert.True(registry.TryGetAgentInfo(troopAgentId, out var info), "the returner's troop was not restored");
                Assert.Equal("C", info.CurrentAuthority);
                Assert.Equal("C", info.OriginalOwner);
                Assert.True(registry.IsLocallyControlled(troopAgentId));
                Assert.True(AgentMirror.TryGet(info.Agent, out var mirror));
                Assert.Equal(AgentControllerType.AI, mirror.Controller);
            });

            GC.KeepAlive(newHostController);
            GC.KeepAlive(returnerController);
            GC.KeepAlive(newHostMock);
            GC.KeepAlive(returnerMock);
        }
        finally
        {
            BattleSpawnGate.EndBattle();
        }
    }

    /// <summary>
    /// Scope and idempotence of the reclaim, on the holding host: a peer-entered for a DIFFERENT instance
    /// reclaims nothing; the real re-entry returns only the returner's own agents (another connected player's
    /// assignment is untouched — BR-022 — and the host's own agents stay the host's); a DUPLICATE peer-entered
    /// does not double-transfer or disturb the released state; and a controller that never owned agents in
    /// this battle triggers nothing.
    /// </summary>
    [Fact]
    [Trait("Requirement", "BR-033")]
    public void Reclaim_IsInstanceScoped_Idempotent_AndLeavesOtherAssignmentsUntouched()
    {
        using var fixture = new MissionEngineFixture();
        var host = Clients.First();
        SetControllerId(host, "H");

        var mapEventId = "battle-reclaim-scope";
        var survivorId = Guid.NewGuid();
        var otherPlayersAgentId = Guid.NewGuid();
        var hostOwnAgentId = Guid.NewGuid();

        try
        {
            BattleSpawnGate.BeginBattle(mapEventId);

            host.Call(() =>
            {
                var mock = fixture.CreateMission(host);
                var controller = host.Resolve<CoopBattleController>();
                var registry = host.Resolve<INetworkAgentRegistry>();

                controller.Session.TryBegin(mapEventId);
                host.Resolve<IBattleHostRegistry>().Set(mapEventId, new BattleHostAssignment("H", Array.Empty<string>()));

                var team = new MockTeam(BattleSideEnum.Attacker);
                BasicCharacterObject character = Game.Current.PlayerTroop;

                // C's surviving troop (an inert puppet), another CONNECTED player's ("D") troop, and one of
                // the host's own agents.
                var survivor = mock.SpawnAgent(new AgentBuildData(character).Controller(AgentControllerType.None).Team(team.Shell));
                Assert.True(registry.TryRegisterAgent("C", survivorId, survivor));
                var otherPlayersAgent = mock.SpawnAgent(new AgentBuildData(character).Controller(AgentControllerType.None).Team(team.Shell));
                Assert.True(registry.TryRegisterAgent("D", otherPlayersAgentId, otherPlayersAgent));
                var hostOwnAgent = mock.SpawnAgent(new AgentBuildData(character).Controller(AgentControllerType.AI).Team(team.Shell));
                Assert.True(registry.TryRegisterAgent("H", hostOwnAgentId, hostOwnAgent));

                var broker = host.Resolve<IMessageBroker>();

                // C drops; the host adopts C's survivor (BR-031). D is still connected — untouched.
                broker.Publish(host, new MissionPeerDisconnected("C", mapEventId));
                Assert.True(registry.TryGetAgentInfo(survivorId, out var adopted));
                Assert.Equal("H", adopted.CurrentAuthority);

                // A re-entry notification for a DIFFERENT instance must not reclaim anything here.
                broker.Publish(host, new NetworkMissionPeerEntered("C", "some-other-instance"));
                Assert.True(registry.TryGetAgentInfo(survivorId, out var stillHeld));
                Assert.Equal("H", stillHeld.CurrentAuthority);

                // The real re-entry: C's survivor goes back to C; D's and the host's own agents are untouched.
                broker.Publish(host, new NetworkMissionPeerEntered("C", mapEventId));

                Assert.True(registry.TryGetAgentInfo(survivorId, out var reclaimed));
                Assert.Equal("C", reclaimed.CurrentAuthority);
                Assert.Equal("C", reclaimed.OriginalOwner);
                Assert.True(AgentMirror.TryGet(survivor, out var survivorMirror));
                Assert.Equal(AgentControllerType.None, survivorMirror.Controller);

                Assert.True(registry.TryGetAgentInfo(otherPlayersAgentId, out var otherInfo));
                Assert.Equal("D", otherInfo.CurrentAuthority);
                Assert.Equal("D", otherInfo.OriginalOwner);
                Assert.True(registry.TryGetAgentInfo(hostOwnAgentId, out var hostOwnInfo));
                Assert.Equal("H", hostOwnInfo.CurrentAuthority);

                // A DUPLICATE re-entry does not double-transfer: still exactly one agent under C, same state.
                broker.Publish(host, new NetworkMissionPeerEntered("C", mapEventId));
                Assert.Equal(1, registry.GetAgents("C").Count);
                Assert.True(registry.TryGetAgentInfo(survivorId, out var afterDuplicate));
                Assert.Equal("C", afterDuplicate.CurrentAuthority);
                Assert.True(AgentMirror.TryGet(survivor, out var survivorMirrorAfter));
                Assert.Equal(AgentControllerType.None, survivorMirrorAfter.Controller);

                // A controller that never owned agents in this battle triggers nothing.
                broker.Publish(host, new NetworkMissionPeerEntered("E", mapEventId));
                Assert.Equal(0, registry.GetAgents("E").Count);
                Assert.True(registry.TryGetAgentInfo(otherPlayersAgentId, out var otherAfter));
                Assert.Equal("D", otherAfter.CurrentAuthority);
                Assert.True(registry.TryGetAgentInfo(hostOwnAgentId, out var hostOwnAfter));
                Assert.Equal("H", hostOwnAfter.CurrentAuthority);

                GC.KeepAlive(controller);
            });
        }
        finally
        {
            BattleSpawnGate.EndBattle();
        }
    }
}
