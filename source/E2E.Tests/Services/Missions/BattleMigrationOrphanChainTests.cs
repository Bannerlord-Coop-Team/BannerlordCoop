using System;
using System.Linq;
using Common.Messaging;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Environment.MockEngine;
using GameInterface.Services.MapEvents;
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
/// BR-031 (Temporary Host Control) across BR-016 (Host Migration Continuity) when two departures STACK:
/// player C disconnects mid-battle and the host H adopts C's surviving agents â€” an adoption that is
/// holder-LOCAL by design (only H's registry re-keys them to H; every other registry still keys them to the
/// absent C). When H then departs too, the promoted successor S must take over EVERYTHING the departed host
/// was driving â€” not only the agents S's registry keys to H (H's own), but also the agents keyed to ANY
/// controller that is no longer in the mission (C's adopted survivors). Without that sweep they are orphaned:
/// no client drives them, and â€” because the joiner catch-up replays only agents a client HOLDS â€” a returning
/// C gets nothing replayed and the BR-033 reclaim never sees them. Full-pipeline over the mock mesh in the
/// BattleReconnectControlTests style: server-mediated membership messages in, registry/Controller state
/// asserted (movement is IPacket and not assertable here).
/// </summary>
public class BattleMigrationOrphanChainTests : MissionTestEnvironment
{
    public BattleMigrationOrphanChainTests(ITestOutputHelper output) : base(output, numClients: 4) { }

    /// <summary>The MapEventParty id wrapping a player's party â€” the attribution the spawn records carry.</summary>
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

    /// <summary>Simulate this instance receiving an owner's ORIGINAL spawn broadcast over the mesh.</summary>
    private static void ReceiveSpawnRecords(EnvironmentInstance instance, params BattleAgentSpawnData[] records)
    {
        instance.Call(() => instance.Resolve<IMessageBroker>().Publish(instance, new NetworkSpawnBattleAgents(records)));
    }

    private static void Publish<T>(EnvironmentInstance instance, T message) where T : IMessage
    {
        instance.Call(() => instance.Resolve<IMessageBroker>().Publish(instance, message));
    }

    /// <summary>The server promoted a new host on this client: assignment update + (promoted only) migration event.</summary>
    private static void PromoteHost(EnvironmentInstance client, string mapEventId, BattleHostAssignment newAssignment,
        string previousHost, bool isPromotedClient)
    {
        client.Call(() =>
        {
            client.Resolve<IBattleHostRegistry>().Set(mapEventId, newAssignment);
            if (isPromotedClient)
                client.Resolve<IMessageBroker>().Publish(client, new BattleHostMigrated(mapEventId, previousHost));
        });
    }

    private static void AssertAuthority(EnvironmentInstance instance, Guid agentId, string expectedAuthority, string expectedOriginalOwner)
    {
        instance.Call(() =>
        {
            var registry = instance.Resolve<INetworkAgentRegistry>();
            Assert.True(registry.TryGetAgentInfo(agentId, out var info), $"agent {agentId} is not registered on this instance");
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

    private static void AssertAlive(EnvironmentInstance instance, Guid agentId)
    {
        instance.Call(() =>
        {
            var registry = instance.Resolve<INetworkAgentRegistry>();
            Assert.True(registry.TryGetAgentInfo(agentId, out var info));
            Assert.True(AgentMirror.TryGet(info.Agent, out var mirror));
            Assert.True(mirror.IsActive, "the adopted agent must stay alive in place, not be despawned");
        });
    }

    /// <summary>
    /// The stacked-departure core: C disconnects (H adopts â€” the BR-031 fixture, holder-local), then H
    /// departs and S is promoted. S must now HOLD everything the departed host was driving: H's own agents
    /// AND C's adopted survivors â€” the latter are keyed to the ABSENT C in S's registry (the adoption never
    /// propagated), so a sweep that only looks for CurrentAuthority == H strands them driverless forever.
    /// After the migration every swept agent is S's (assignment intact) and AI-driven on S.
    /// </summary>
    [Fact]
    [Trait("Requirement", "BR-031")]
    [Trait("Requirement", "BR-016")]
    public void HostDeparture_PromotedSuccessor_AdoptsEarlierDisconnectersOrphanedAgents()
    {
        using var fixture = new MissionEngineFixture();
        var (mapEventId, partyIds) = SetupCoopBattle("H", "S", "C");
        var clients = Clients.ToArray();
        var host = clients[0];
        var successor = clients[1];

        var hMepId = GetMapEventPartyId(mapEventId, partyIds[0]);
        var cMepId = GetMapEventPartyId(mapEventId, partyIds[2]);
        var troopCharacterId = CreateRegisteredObject<CharacterObject>();

        var cHeroAgentId = Guid.NewGuid();
        var cTroopAgentId = Guid.NewGuid();
        var hTroopAgentId = Guid.NewGuid();

        var assignment = new BattleHostAssignment("H", new[] { "S" });

        try
        {
            BattleSpawnGate.BeginBattle(mapEventId);

            var (hostController, hostMock) = StandUpBattleClient(fixture, host, mapEventId, assignment);
            var (successorController, successorMock) = StandUpBattleClient(fixture, successor, mapEventId, assignment);

            // Server-mediated membership intros â€” the same signal MissionContext mirrors. Sent before any
            // agents exist, so the join-info replays they trigger are empty.
            Publish(host, new NetworkMissionPeerEntered("S", mapEventId));
            Publish(host, new NetworkMissionPeerEntered("C", mapEventId));
            Publish(successor, new NetworkMissionPeerEntered("H", mapEventId));
            Publish(successor, new NetworkMissionPeerEntered("C", mapEventId));

            // C's original spawn broadcast, as H and S received it before the drop; and one of H's own
            // troops as S received it (S's registry keys it to H).
            var cRecords = new[]
            {
                new BattleAgentSpawnData(cHeroAgentId, troopCharacterId, default, BattleSideEnum.Attacker, 100f, "C", cMepId, 41, new Equipment(), default),
                new BattleAgentSpawnData(cTroopAgentId, troopCharacterId, default, BattleSideEnum.Attacker, 100f, "C", cMepId, 42, new Equipment(), default),
            };
            ReceiveSpawnRecords(host, cRecords);
            ReceiveSpawnRecords(successor, cRecords);
            ReceiveSpawnRecords(successor,
                new BattleAgentSpawnData(hTroopAgentId, troopCharacterId, default, BattleSideEnum.Attacker, 100f, "H", hMepId, 43, new Equipment(), default));

            // C drops ungracefully (server fan-out to both remaining members). BR-031: the host adopts;
            // the successor does not (holder-local adoption â€” its registry still keys C's agents to C).
            Publish(host, new MissionPeerDisconnected("C", mapEventId));
            Publish(successor, new MissionPeerDisconnected("C", mapEventId));

            AssertAuthority(host, cTroopAgentId, "H", "C");
            AssertAuthority(successor, cTroopAgentId, "C", "C");

            // H drops too: the server promotes S (assignment broadcast; the migration event fires on S).
            Publish(successor, new MissionPeerDisconnected("H", mapEventId));
            PromoteHost(successor, mapEventId, new BattleHostAssignment("S", Array.Empty<string>(), epoch: 2), "H", isPromotedClient: true);

            // The promoted successor holds the departed host's OWN agents...
            AssertAuthority(successor, hTroopAgentId, "S", "H");
            AssertController(successor, hTroopAgentId, AgentControllerType.AI);

            // ...AND the earlier disconnecter's adopted survivors, which were keyed to the absent C in S's
            // registry: swept to S, assignment (OriginalOwner) intact, alive in place and AI-driven on S.
            AssertAuthority(successor, cHeroAgentId, "S", "C");
            AssertAuthority(successor, cTroopAgentId, "S", "C");
            AssertController(successor, cHeroAgentId, AgentControllerType.AI);
            AssertController(successor, cTroopAgentId, AgentControllerType.AI);
            AssertAlive(successor, cHeroAgentId);
            AssertAlive(successor, cTroopAgentId);

            GC.KeepAlive(hostController);
            GC.KeepAlive(successorController);
            GC.KeepAlive(hostMock);
            GC.KeepAlive(successorMock);
        }
        finally
        {
            BattleSpawnGate.EndBattle();
        }
    }

    /// <summary>
    /// Composition with the BR-033 reclaim: after the stacked departure (C dropped, H adopted, H departed,
    /// S swept), C re-enters. Because S now HOLDS C's agents, the joiner catch-up replays them to C (the
    /// replay carries the ASSIGNMENT owner) and the reclaim returns them: authority back at C on S, S's
    /// temporary AI control released, and the returner locally controls its restored troops. Without the
    /// sweep neither happens â€” S holds nothing of C's, so C returns to a battle that has forgotten it.
    /// </summary>
    [Fact]
    [Trait("Requirement", "BR-016")]
    [Trait("Requirement", "BR-031")]
    public void ReturningDisconnecter_ReclaimsItsAgents_FromTheSweptSuccessor()
    {
        using var fixture = new MissionEngineFixture();
        var (mapEventId, partyIds) = SetupCoopBattle("H", "S", "C");
        var clients = Clients.ToArray();
        var host = clients[0];
        var successor = clients[1];
        var returner = clients[2];

        var cMepId = GetMapEventPartyId(mapEventId, partyIds[2]);
        var troopCharacterId = CreateRegisteredObject<CharacterObject>();

        var cTroopAgentId = Guid.NewGuid();

        try
        {
            BattleSpawnGate.BeginBattle(mapEventId);

            var (hostController, hostMock) = StandUpBattleClient(fixture, host, mapEventId, new BattleHostAssignment("H", new[] { "S" }));
            var (successorController, successorMock) = StandUpBattleClient(fixture, successor, mapEventId, new BattleHostAssignment("H", new[] { "S" }));

            Publish(successor, new NetworkMissionPeerEntered("H", mapEventId));
            Publish(successor, new NetworkMissionPeerEntered("C", mapEventId));

            var cRecords = new[]
            {
                new BattleAgentSpawnData(cTroopAgentId, troopCharacterId, default, BattleSideEnum.Attacker, 100f, "C", cMepId, 51, new Equipment(), default),
            };
            ReceiveSpawnRecords(host, cRecords);
            ReceiveSpawnRecords(successor, cRecords);

            // C drops; H adopts (holder-local). Then H drops; S is promoted and must sweep C's orphans.
            Publish(host, new MissionPeerDisconnected("C", mapEventId));
            Publish(successor, new MissionPeerDisconnected("C", mapEventId));
            Publish(successor, new MissionPeerDisconnected("H", mapEventId));
            PromoteHost(successor, mapEventId, new BattleHostAssignment("S", Array.Empty<string>(), epoch: 2), "H", isPromotedClient: true);

            AssertAuthority(successor, cTroopAgentId, "S", "C");

            // C reconnects: a fresh mission on its client, then the server tells the remaining member it
            // entered â€” the same exchange that drives the join-info/catch-up flow (S replays what it HOLDS,
            // then the reclaim hands authority back).
            var (returnerController, returnerMock) = StandUpBattleClient(
                fixture, returner, mapEventId, new BattleHostAssignment("S", Array.Empty<string>(), epoch: 2));
            Publish(successor, new NetworkMissionPeerEntered("C", mapEventId));

            // The holder released the swept adoption back to the returning owner (BR-033 composes).
            AssertAuthority(successor, cTroopAgentId, "C", "C");
            AssertController(successor, cTroopAgentId, AgentControllerType.None);

            // The returner got its troop replayed (it HELD by S post-sweep) and controls it locally.
            returner.Call(() =>
            {
                var registry = returner.Resolve<INetworkAgentRegistry>();
                Assert.True(registry.TryGetAgentInfo(cTroopAgentId, out var info),
                    "the returner's troop was not replayed â€” the swept holder must replay what it holds");
                Assert.Equal("C", info.CurrentAuthority);
                Assert.Equal("C", info.OriginalOwner);
                Assert.True(registry.IsLocallyControlled(cTroopAgentId));
                Assert.True(AgentMirror.TryGet(info.Agent, out var mirror));
                Assert.Equal(AgentControllerType.AI, mirror.Controller);
            });

            GC.KeepAlive(hostController);
            GC.KeepAlive(successorController);
            GC.KeepAlive(returnerController);
            GC.KeepAlive(hostMock);
            GC.KeepAlive(successorMock);
            GC.KeepAlive(returnerMock);
        }
        finally
        {
            BattleSpawnGate.EndBattle();
        }
    }

    /// <summary>
    /// The sweep works REPEATEDLY: C drops (H adopts), H drops (S promoted, sweeps), S drops (T promoted,
    /// sweeps). On T, C's agents were still keyed to the absent C (neither H's nor S's holder-local adoption
    /// ever touched T's registry), so T's sweep must catch them â€” and the assignment (OriginalOwner == C)
    /// survives the whole chain, keeping the BR-033 reclaim possible at every generation.
    /// </summary>
    [Fact]
    [Trait("Requirement", "BR-016")]
    [Trait("Requirement", "BR-031")]
    public void ChainOfThreeDepartures_EachPromotedHost_SweepsTheOrphans_AssignmentSurvives()
    {
        using var fixture = new MissionEngineFixture();
        var (mapEventId, partyIds) = SetupCoopBattle("H", "S", "T", "C");
        var clients = Clients.ToArray();
        var host = clients[0];
        var successor = clients[1];
        var third = clients[2];

        var cMepId = GetMapEventPartyId(mapEventId, partyIds[3]);
        var troopCharacterId = CreateRegisteredObject<CharacterObject>();

        var cTroopAgentId = Guid.NewGuid();

        try
        {
            BattleSpawnGate.BeginBattle(mapEventId);

            var initialAssignment = new BattleHostAssignment("H", new[] { "S", "T" });
            var (hostController, hostMock) = StandUpBattleClient(fixture, host, mapEventId, initialAssignment);
            var (successorController, successorMock) = StandUpBattleClient(fixture, successor, mapEventId, initialAssignment);
            var (thirdController, thirdMock) = StandUpBattleClient(fixture, third, mapEventId, initialAssignment);

            // Membership intros on every member, before any agents exist.
            foreach (var (member, others) in new[]
            {
                (host, new[] { "S", "T", "C" }),
                (successor, new[] { "H", "T", "C" }),
                (third, new[] { "H", "S", "C" }),
            })
            {
                foreach (var other in others)
                    Publish(member, new NetworkMissionPeerEntered(other, mapEventId));
            }

            var cRecords = new[]
            {
                new BattleAgentSpawnData(cTroopAgentId, troopCharacterId, default, BattleSideEnum.Attacker, 100f, "C", cMepId, 61, new Equipment(), default),
            };
            ReceiveSpawnRecords(host, cRecords);
            ReceiveSpawnRecords(successor, cRecords);
            ReceiveSpawnRecords(third, cRecords);

            // 1) C drops: H adopts (BR-031); S and T keep C's troop keyed to C (holder-local).
            Publish(host, new MissionPeerDisconnected("C", mapEventId));
            Publish(successor, new MissionPeerDisconnected("C", mapEventId));
            Publish(third, new MissionPeerDisconnected("C", mapEventId));

            AssertAuthority(host, cTroopAgentId, "H", "C");
            AssertAuthority(successor, cTroopAgentId, "C", "C");
            AssertAuthority(third, cTroopAgentId, "C", "C");

            // 2) H drops: S promoted; S's sweep catches C's orphaned troop. T only records the new host.
            Publish(successor, new MissionPeerDisconnected("H", mapEventId));
            Publish(third, new MissionPeerDisconnected("H", mapEventId));
            var secondAssignment = new BattleHostAssignment("S", new[] { "T" }, epoch: 2);
            PromoteHost(successor, mapEventId, secondAssignment, "H", isPromotedClient: true);
            PromoteHost(third, mapEventId, secondAssignment, "H", isPromotedClient: false);

            AssertAuthority(successor, cTroopAgentId, "S", "C");
            AssertController(successor, cTroopAgentId, AgentControllerType.AI);
            AssertAuthority(third, cTroopAgentId, "C", "C"); // holder-local: T's bookkeeping unchanged

            // 3) S drops: T promoted; on T the troop is STILL keyed to C â€” the sweep must catch it again.
            Publish(third, new MissionPeerDisconnected("S", mapEventId));
            PromoteHost(third, mapEventId, new BattleHostAssignment("T", Array.Empty<string>(), epoch: 3), "S", isPromotedClient: true);

            AssertAuthority(third, cTroopAgentId, "T", "C");
            AssertController(third, cTroopAgentId, AgentControllerType.AI);
            AssertAlive(third, cTroopAgentId);

            GC.KeepAlive(hostController);
            GC.KeepAlive(successorController);
            GC.KeepAlive(thirdController);
            GC.KeepAlive(hostMock);
            GC.KeepAlive(successorMock);
            GC.KeepAlive(thirdMock);
        }
        finally
        {
            BattleSpawnGate.EndBattle();
        }
    }

    /// <summary>
    /// Scope of the sweep: it takes ONLY absent controllers' agents. A still-connected player D's agents are
    /// untouched (assignment and authority stay D's, still an inert puppet), and a DUPLICATE migration event
    /// neither double-adopts (the promoted host's holdings are stable) nor disturbs D.
    /// </summary>
    [Fact]
    [Trait("Requirement", "BR-031")]
    [Trait("Requirement", "BR-016")]
    public void Sweep_LeavesConnectedPlayersUntouched_AndDuplicateMigrationDoesNotDoubleAdopt()
    {
        using var fixture = new MissionEngineFixture();
        var (mapEventId, partyIds) = SetupCoopBattle("H", "S", "C", "D");
        var clients = Clients.ToArray();
        var successor = clients[1];

        var hMepId = GetMapEventPartyId(mapEventId, partyIds[0]);
        var cMepId = GetMapEventPartyId(mapEventId, partyIds[2]);
        var dMepId = GetMapEventPartyId(mapEventId, partyIds[3]);
        var troopCharacterId = CreateRegisteredObject<CharacterObject>();

        var hTroopAgentId = Guid.NewGuid();
        var cTroopAgentId = Guid.NewGuid();
        var dTroopAgentId = Guid.NewGuid();

        try
        {
            BattleSpawnGate.BeginBattle(mapEventId);

            var (successorController, successorMock) = StandUpBattleClient(
                fixture, successor, mapEventId, new BattleHostAssignment("H", new[] { "S", "D" }));

            // All three other members are present as far as S knows.
            Publish(successor, new NetworkMissionPeerEntered("H", mapEventId));
            Publish(successor, new NetworkMissionPeerEntered("C", mapEventId));
            Publish(successor, new NetworkMissionPeerEntered("D", mapEventId));

            ReceiveSpawnRecords(successor,
                new BattleAgentSpawnData(hTroopAgentId, troopCharacterId, default, BattleSideEnum.Attacker, 100f, "H", hMepId, 71, new Equipment(), default),
                new BattleAgentSpawnData(cTroopAgentId, troopCharacterId, default, BattleSideEnum.Attacker, 100f, "C", cMepId, 72, new Equipment(), default),
                new BattleAgentSpawnData(dTroopAgentId, troopCharacterId, default, BattleSideEnum.Attacker, 100f, "D", dMepId, 73, new Equipment(), default));

            // C drops (S is not the host: adopts nothing), then H drops and S is promoted.
            Publish(successor, new MissionPeerDisconnected("C", mapEventId));
            Publish(successor, new MissionPeerDisconnected("H", mapEventId));
            PromoteHost(successor, mapEventId, new BattleHostAssignment("S", new[] { "D" }, epoch: 2), "H", isPromotedClient: true);

            // Swept: the departed host's own troop AND the absent C's orphan. Untouched: connected D's troop.
            AssertAuthority(successor, hTroopAgentId, "S", "H");
            AssertAuthority(successor, cTroopAgentId, "S", "C");
            AssertAuthority(successor, dTroopAgentId, "D", "D");
            AssertController(successor, dTroopAgentId, AgentControllerType.None);

            // A duplicate migration broadcast must not double-adopt or disturb the connected player.
            int heldBefore = 0;
            successor.Call(() => heldBefore = successor.Resolve<INetworkAgentRegistry>().GetAgents("S").Count);

            Publish(successor, new BattleHostMigrated(mapEventId, "H"));

            successor.Call(() =>
            {
                var registry = successor.Resolve<INetworkAgentRegistry>();
                Assert.Equal(heldBefore, registry.GetAgents("S").Count);
            });
            AssertAuthority(successor, dTroopAgentId, "D", "D");
            AssertController(successor, dTroopAgentId, AgentControllerType.None);
            AssertAuthority(successor, cTroopAgentId, "S", "C");

            GC.KeepAlive(successorController);
            GC.KeepAlive(successorMock);
        }
        finally
        {
            BattleSpawnGate.EndBattle();
        }
    }
}
