using System;
using System.Linq;
using Common;
using Common.Messaging;
using E2E.Tests.Environment.Mock;
using E2E.Tests.Environment.MockEngine;
using GameInterface.Services.MapEvents;
using Missions;
using Missions.Agents.Packets;
using Missions.Messages;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions;

/// <summary>
/// Verifies that reliable action updates continuously assert the owner's native guard on a Controller.None puppet.
/// </summary>
public class BattleBlockingSyncTests : MissionTestEnvironment
{
    public BattleBlockingSyncTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void PollActions_GuardOnlyTransition_SendsActionPacket()
    {
        using var fixture = new MissionEngineFixture();
        var owner = Clients.First();
        SetControllerId(owner, "owner");

        owner.Call(() =>
        {
            var mock = fixture.CreateMission(owner);
            var component = owner.Resolve<ICoopMissionComponent>();
            var registry = owner.Resolve<INetworkAgentRegistry>();
            var network = owner.Resolve<MockBattleNetwork>();
            var agentId = Guid.NewGuid();

            var agent = mock.SpawnAgent(new AgentBuildData(Game.Current.PlayerTroop)
                .Controller(AgentControllerType.Player));
            Assert.True(registry.TryRegisterAgent("owner", agentId, agent));
            Assert.True(AgentMirror.TryGet(agent, out var mirror));

            int action0 = agent.GetCurrentAction(0).Index;
            int action1 = agent.GetCurrentAction(1).Index;
            component.AgentActionHandler.PollActions();
            Assert.Empty(network.NetworkSentPackets.GetPackets<AgentActionPacket>());

            mirror.GuardMode = Agent.GuardMode.Left;
            component.AgentActionHandler.PollActions();

            AgentActionPacket packet = Assert.Single(
                network.NetworkSentPackets.GetPackets<AgentActionPacket>());
            Assert.Equal(agentId, Assert.Single(packet.AgentIds));
            AgentActionData data = Assert.Single(packet.Actions);
            Assert.Equal(action0, data.Action0Index);
            Assert.Equal(action1, data.Action1Index);
            Assert.Equal(Agent.GuardMode.Left, data.GuardMode);
            Assert.Equal(1L, Assert.Single(packet.Sequences));
            Assert.Equal(0, packet.BattleHostEpoch);
        });
    }

    [Theory]
    [InlineData("owner", 7)]
    [InlineData("other-host", 0)]
    public void PollActions_StampsEpochOnlyForBattleHost(
        string hostControllerId,
        int expectedEpoch)
    {
        using var fixture = new MissionEngineFixture();
        var owner = Clients.First();
        SetControllerId(owner, "owner");

        owner.Call(() =>
        {
            const string mapEventId = "mapEvent1";
            BattleSpawnGate.BeginBattle(mapEventId);
            try
            {
                var mock = fixture.CreateMission(owner);
                var component = owner.Resolve<ICoopMissionComponent>();
                var registry = owner.Resolve<INetworkAgentRegistry>();
                var network = owner.Resolve<MockBattleNetwork>();
                var hosts = owner.Resolve<IBattleHostRegistry>();
                var agentId = Guid.NewGuid();

                hosts.Set(
                    mapEventId,
                    new BattleHostAssignment(
                        hostControllerId,
                        Array.Empty<string>(),
                        epoch: 7));

                var agent = mock.SpawnAgent(new AgentBuildData(Game.Current.PlayerTroop)
                    .Controller(AgentControllerType.Player));
                Assert.True(registry.TryRegisterAgent("owner", agentId, agent));
                Assert.True(AgentMirror.TryGet(agent, out var mirror));

                component.AgentActionHandler.PollActions();
                Assert.Empty(network.NetworkSentPackets.GetPackets<AgentActionPacket>());

                mirror.GuardMode = Agent.GuardMode.Right;
                component.AgentActionHandler.PollActions();

                AgentActionPacket packet = Assert.Single(
                    network.NetworkSentPackets.GetPackets<AgentActionPacket>());
                Assert.Equal(expectedEpoch, packet.BattleHostEpoch);
            }
            finally
            {
                BattleSpawnGate.EndBattle();
            }
        });
    }

    [Fact]
    public void CatchUpJoiner_HeldGuard_SendsCurrentStateToJoiningPeer()
    {
        using var fixture = new MissionEngineFixture();
        var owner = Clients.First();
        SetControllerId(owner, "owner");

        owner.Call(() =>
        {
            var mock = fixture.CreateMission(owner);
            var component = owner.Resolve<ICoopMissionComponent>();
            var registry = owner.Resolve<INetworkAgentRegistry>();
            var network = owner.Resolve<MockBattleNetwork>();
            var agentId = Guid.NewGuid();

            var agent = mock.SpawnAgent(new AgentBuildData(Game.Current.PlayerTroop)
                .Controller(AgentControllerType.Player));
            Assert.True(registry.TryRegisterAgent("owner", agentId, agent));
            Assert.True(AgentMirror.TryGet(agent, out var mirror));

            mirror.GuardMode = Agent.GuardMode.Right;
            component.AgentActionHandler.PollActions();
            network.NetworkSentPackets.Packets.Clear();

            component.AgentActionHandler.CatchUpJoiner("joiner");

            var directSend = Assert.Single(network.DirectPacketSends);
            Assert.Equal("joiner", directSend.ControllerId);
            var packet = Assert.IsType<AgentActionPacket>(directSend.Packet);
            Assert.Equal(agentId, Assert.Single(packet.AgentIds));
            Assert.Equal(Agent.GuardMode.Right, Assert.Single(packet.Actions).GuardMode);
            Assert.Equal(2L, Assert.Single(packet.Sequences));
        });
    }

    [Fact]
    public void ActionPacket_BeforeAgentRegistration_AppliesAfterRegistration()
    {
        using var fixture = new MissionEngineFixture();
        var peer = Clients.First();
        SetControllerId(peer, "peer");

        peer.Call(() =>
        {
            var mock = fixture.CreateMission(peer);
            var component = peer.Resolve<ICoopMissionComponent>();
            var registry = peer.Resolve<INetworkAgentRegistry>();
            var agentId = Guid.NewGuid();

            var owner = mock.SpawnAgent(new AgentBuildData(Game.Current.PlayerTroop)
                .Controller(AgentControllerType.Player));
            Assert.True(AgentMirror.TryGet(owner, out var ownerMirror));
            ownerMirror.GuardMode = Agent.GuardMode.Down;

            component.AgentActionHandler.HandlePacket(null,
                new AgentActionPacket(
                    "owner",
                    new[] { agentId },
                    new[] { new AgentActionData(owner) },
                    new[] { 1L }));

            var puppet = mock.SpawnAgent(new AgentBuildData(Game.Current.PlayerTroop)
                .Controller(AgentControllerType.None));
            Assert.True(registry.TryRegisterAgent("owner", agentId, puppet));
            Assert.True(AgentMirror.TryGet(puppet, out var puppetMirror));

            component.AgentActionHandler.ApplyRemoteGuardStates();

            Assert.Equal(Agent.GuardMode.Down, puppetMirror.GuardMode);
            Assert.Equal(1, puppetMirror.SetWeaponGuardCalls);
        });
    }

    [Fact]
    public void ActionTick_ReassertsHeldGuardAfterNativeDecay_WithoutAnotherPacket()
    {
        using var fixture = new MissionEngineFixture();
        var peer = Clients.First();
        SetControllerId(peer, "peer");

        var agentId = Guid.NewGuid();

        peer.Call(() =>
        {
            var mock = fixture.CreateMission(peer);
            var component = peer.Resolve<ICoopMissionComponent>();
            var registry = peer.Resolve<INetworkAgentRegistry>();
            BasicCharacterObject character = Game.Current.PlayerTroop;

            var puppet = mock.SpawnAgent(new AgentBuildData(character).Controller(AgentControllerType.None));
            Assert.True(registry.TryRegisterAgent("owner", agentId, puppet));

            var owner = mock.SpawnAgent(new AgentBuildData(character).Controller(AgentControllerType.Player));
            Assert.True(AgentMirror.TryGet(owner, out var ownerMirror));
            Assert.True(AgentMirror.TryGet(puppet, out var puppetMirror));

            ownerMirror.GuardMode = Agent.GuardMode.Left;
            ApplyOwnerAction(component, 1L, agentId, owner);
            component.AgentActionHandler.ApplyRemoteGuardStates();

            Assert.Equal(Agent.GuardMode.Left, puppetMirror.GuardMode);
            Assert.Equal(1, puppetMirror.SetWeaponGuardCalls);
            Assert.Equal(0, puppetMirror.ResetGuardCalls);

            puppetMirror.GuardMode = Agent.GuardMode.None;
            component.AgentActionHandler.ApplyRemoteGuardStates();

            Assert.Equal(Agent.GuardMode.Left, puppetMirror.GuardMode);
            Assert.Equal(2, puppetMirror.SetWeaponGuardCalls);

            ownerMirror.GuardMode = Agent.GuardMode.None;
            ApplyOwnerAction(component, 2L, agentId, owner);
            component.AgentActionHandler.ApplyRemoteGuardStates();
            component.AgentActionHandler.ApplyRemoteGuardStates();

            Assert.Equal(Agent.GuardMode.None, puppetMirror.GuardMode);
            Assert.Equal(1, puppetMirror.ResetGuardCalls);
        });
    }

    [Fact]
    public void MigratedHostAction_RemainsAuthoritativeAfterOldHostRejoins()
    {
        using var fixture = new MissionEngineFixture();
        var observer = Clients.First();
        SetControllerId(observer, "observer");

        observer.Call(() =>
        {
            const string mapEventId = "mapEvent1";
            BattleSpawnGate.BeginBattle(mapEventId);
            try
            {
                var mock = fixture.CreateMission(observer);
                var component = observer.Resolve<ICoopMissionComponent>();
                var registry = observer.Resolve<INetworkAgentRegistry>();
                var broker = observer.Resolve<IMessageBroker>();
                var hosts = observer.Resolve<IBattleHostRegistry>();
                var migratedAgentId = Guid.NewGuid();
                var activeOwnerAgentId = Guid.NewGuid();

                broker.Publish(this, new NetworkMissionPeerEntered("A", mapEventId));
                broker.Publish(this, new NetworkMissionPeerEntered("B", mapEventId));
                broker.Publish(this, new NetworkMissionPeerEntered("D", mapEventId));
                AssignBattleHost(
                    broker,
                    hosts,
                    mapEventId,
                    "A",
                    new[] { "B", "D" },
                    epoch: 1);
                DrainGameThread();

                var migratedPuppet = mock.SpawnAgent(new AgentBuildData(Game.Current.PlayerTroop)
                    .Controller(AgentControllerType.None));
                var activeOwnerPuppet = mock.SpawnAgent(new AgentBuildData(Game.Current.PlayerTroop)
                    .Controller(AgentControllerType.None));
                var rejoinedOwnerPuppet = mock.SpawnAgent(new AgentBuildData(Game.Current.PlayerTroop)
                    .Controller(AgentControllerType.None));
                var rejoinedOwnerAgentId = Guid.NewGuid();
                Assert.True(registry.TryRegisterAgent("A", migratedAgentId, migratedPuppet));
                Assert.True(registry.TryRegisterAgent("D", activeOwnerAgentId, activeOwnerPuppet));
                Assert.True(AgentMirror.TryGet(migratedPuppet, out var migratedMirror));
                Assert.True(AgentMirror.TryGet(activeOwnerPuppet, out var activeOwnerMirror));
                Assert.True(AgentMirror.TryGet(rejoinedOwnerPuppet, out var rejoinedOwnerMirror));

                broker.Publish(this, new MissionPeerDisconnected("A", mapEventId));
                AssignBattleHost(
                    broker,
                    hosts,
                    mapEventId,
                    "B",
                    new[] { "D" },
                    epoch: 2);

                broker.Publish(this, new NetworkMissionPeerEntered("A", mapEventId));
                bool rejoinedAgentRegistered = false;
                GameThread.RunSafe(() =>
                    rejoinedAgentRegistered = registry.TryRegisterAgent(
                        "A",
                        rejoinedOwnerAgentId,
                        rejoinedOwnerPuppet));
                DrainGameThread();
                Assert.True(rejoinedAgentRegistered);

                var hostAgent = mock.SpawnAgent(new AgentBuildData(Game.Current.PlayerTroop)
                    .Controller(AgentControllerType.Player));
                Assert.True(AgentMirror.TryGet(hostAgent, out var hostMirror));
                hostMirror.GuardMode = Agent.GuardMode.Right;
                var guard = new AgentActionData(hostAgent);

                component.AgentActionHandler.HandlePacket(null,
                    new AgentActionPacket(
                        "B",
                        new[]
                        {
                            migratedAgentId,
                            activeOwnerAgentId
                        },
                        new[] { guard, guard },
                        new[] { 1L, 1L },
                        battleHostEpoch: 2));
                hostMirror.GuardMode = Agent.GuardMode.Up;
                ApplyOwnerAction(
                    component,
                    "A",
                    1L,
                    rejoinedOwnerAgentId,
                    hostAgent);
                DrainGameThread();
                component.AgentActionHandler.ApplyRemoteGuardStates();

                Assert.Equal(Agent.GuardMode.Right, migratedMirror.GuardMode);
                Assert.Equal(1, migratedMirror.SetWeaponGuardCalls);
                Assert.Equal(Agent.GuardMode.None, activeOwnerMirror.GuardMode);
                Assert.Equal(0, activeOwnerMirror.SetWeaponGuardCalls);
                Assert.Equal(Agent.GuardMode.Up, rejoinedOwnerMirror.GuardMode);
                Assert.Equal(1, rejoinedOwnerMirror.SetWeaponGuardCalls);

                migratedMirror.GuardMode = Agent.GuardMode.None;
                component.AgentActionHandler.ApplyRemoteGuardStates();

                Assert.Equal(Agent.GuardMode.Right, migratedMirror.GuardMode);
                Assert.Equal(2, migratedMirror.SetWeaponGuardCalls);
                Assert.Equal(Agent.GuardMode.None, activeOwnerMirror.GuardMode);
                Assert.Equal(Agent.GuardMode.Up, rejoinedOwnerMirror.GuardMode);
            }
            finally
            {
                BattleSpawnGate.EndBattle();
            }
        });
    }

    [Fact]
    public void QueuedOldHostRegistration_BeforeAssignment_IsMigrated()
    {
        using var fixture = new MissionEngineFixture();
        var observer = Clients.First();
        SetControllerId(observer, "observer");

        observer.Call(() =>
        {
            const string mapEventId = "mapEvent1";
            BattleSpawnGate.BeginBattle(mapEventId);
            try
            {
                var mock = fixture.CreateMission(observer);
                var component = observer.Resolve<ICoopMissionComponent>();
                var registry = observer.Resolve<INetworkAgentRegistry>();
                var broker = observer.Resolve<IMessageBroker>();
                var hosts = observer.Resolve<IBattleHostRegistry>();
                var agentId = Guid.NewGuid();

                broker.Publish(this, new NetworkMissionPeerEntered("A", mapEventId));
                broker.Publish(this, new NetworkMissionPeerEntered("B", mapEventId));
                AssignBattleHost(
                    broker,
                    hosts,
                    mapEventId,
                    "A",
                    new[] { "B" },
                    epoch: 1);
                DrainGameThread();

                var puppet = mock.SpawnAgent(new AgentBuildData(Game.Current.PlayerTroop)
                    .Controller(AgentControllerType.None));
                Assert.True(AgentMirror.TryGet(puppet, out var puppetMirror));

                bool oldHostAgentRegistered = false;
                GameThread.RunSafe(() =>
                    oldHostAgentRegistered = registry.TryRegisterAgent(
                        "A",
                        agentId,
                        puppet));

                broker.Publish(this, new MissionPeerDisconnected("A", mapEventId));
                AssignBattleHost(
                    broker,
                    hosts,
                    mapEventId,
                    "B",
                    Array.Empty<string>(),
                    epoch: 2);
                DrainGameThread();
                Assert.True(oldHostAgentRegistered);

                var hostAgent = mock.SpawnAgent(new AgentBuildData(Game.Current.PlayerTroop)
                    .Controller(AgentControllerType.Player));
                Assert.True(AgentMirror.TryGet(hostAgent, out var hostMirror));
                hostMirror.GuardMode = Agent.GuardMode.Left;

                ApplyOwnerAction(
                    component,
                    "B",
                    1L,
                    agentId,
                    hostAgent,
                    battleHostEpoch: 2);
                DrainGameThread();
                component.AgentActionHandler.ApplyRemoteGuardStates();

                Assert.Equal(Agent.GuardMode.Left, puppetMirror.GuardMode);
                Assert.Equal(1, puppetMirror.SetWeaponGuardCalls);
            }
            finally
            {
                BattleSpawnGate.EndBattle();
            }
        });
    }

    [Fact]
    public void LateOldHostRegistration_UsesPendingSuccessorAction()
    {
        using var fixture = new MissionEngineFixture();
        var observer = Clients.First();
        SetControllerId(observer, "observer");

        observer.Call(() =>
        {
            const string mapEventId = "mapEvent1";
            BattleSpawnGate.BeginBattle(mapEventId);
            try
            {
                var mock = fixture.CreateMission(observer);
                var component = observer.Resolve<ICoopMissionComponent>();
                var registry = observer.Resolve<INetworkAgentRegistry>();
                var broker = observer.Resolve<IMessageBroker>();
                var hosts = observer.Resolve<IBattleHostRegistry>();
                var agentId = Guid.NewGuid();

                broker.Publish(this, new NetworkMissionPeerEntered("A", mapEventId));
                broker.Publish(this, new NetworkMissionPeerEntered("B", mapEventId));
                AssignBattleHost(
                    broker,
                    hosts,
                    mapEventId,
                    "A",
                    new[] { "B" },
                    epoch: 1);
                DrainGameThread();

                var hostAgent = mock.SpawnAgent(new AgentBuildData(Game.Current.PlayerTroop)
                    .Controller(AgentControllerType.Player));
                Assert.True(AgentMirror.TryGet(hostAgent, out var hostMirror));
                hostMirror.GuardMode = Agent.GuardMode.Down;

                ApplyOwnerAction(
                    component,
                    "B",
                    1L,
                    agentId,
                    hostAgent,
                    battleHostEpoch: 2);
                broker.Publish(this, new MissionPeerDisconnected("A", mapEventId));
                AssignBattleHost(
                    broker,
                    hosts,
                    mapEventId,
                    "B",
                    Array.Empty<string>(),
                    epoch: 2);
                DrainGameThread();

                broker.Publish(this, new NetworkMissionPeerEntered("A", mapEventId));
                AssignBattleHost(
                    broker,
                    hosts,
                    mapEventId,
                    "B",
                    Array.Empty<string>(),
                    epoch: 2);
                DrainGameThread();

                var puppet = mock.SpawnAgent(new AgentBuildData(Game.Current.PlayerTroop)
                    .Controller(AgentControllerType.None));
                Assert.True(registry.TryRegisterAgent("A", agentId, puppet));
                Assert.True(AgentMirror.TryGet(puppet, out var puppetMirror));

                component.AgentActionHandler.ApplyRemoteGuardStates();

                Assert.Equal(Agent.GuardMode.Down, puppetMirror.GuardMode);
                Assert.Equal(1, puppetMirror.SetWeaponGuardCalls);
            }
            finally
            {
                BattleSpawnGate.EndBattle();
            }
        });
    }

    [Fact]
    public void DepartedHostGuard_ClearsWhenSuccessorTakesAuthority()
    {
        using var fixture = new MissionEngineFixture();
        var observer = Clients.First();
        SetControllerId(observer, "observer");

        observer.Call(() =>
        {
            const string mapEventId = "mapEvent1";
            BattleSpawnGate.BeginBattle(mapEventId);
            try
            {
                var mock = fixture.CreateMission(observer);
                var component = observer.Resolve<ICoopMissionComponent>();
                var registry = observer.Resolve<INetworkAgentRegistry>();
                var broker = observer.Resolve<IMessageBroker>();
                var hosts = observer.Resolve<IBattleHostRegistry>();
                var agentId = Guid.NewGuid();

                broker.Publish(this, new NetworkMissionPeerEntered("A", mapEventId));
                broker.Publish(this, new NetworkMissionPeerEntered("B", mapEventId));
                AssignBattleHost(
                    broker,
                    hosts,
                    mapEventId,
                    "A",
                    new[] { "B" },
                    epoch: 1);
                DrainGameThread();

                var puppet = mock.SpawnAgent(new AgentBuildData(Game.Current.PlayerTroop)
                    .Controller(AgentControllerType.None));
                Assert.True(registry.TryRegisterAgent("A", agentId, puppet));
                Assert.True(AgentMirror.TryGet(puppet, out var puppetMirror));

                var oldHostAgent = mock.SpawnAgent(new AgentBuildData(Game.Current.PlayerTroop)
                    .Controller(AgentControllerType.Player));
                Assert.True(AgentMirror.TryGet(oldHostAgent, out var oldHostMirror));
                oldHostMirror.GuardMode = Agent.GuardMode.Left;

                ApplyOwnerAction(
                    component,
                    "A",
                    1L,
                    agentId,
                    oldHostAgent,
                    battleHostEpoch: 1);
                DrainGameThread();
                component.AgentActionHandler.ApplyRemoteGuardStates();
                Assert.Equal(Agent.GuardMode.Left, puppetMirror.GuardMode);

                broker.Publish(this, new MissionPeerDisconnected("A", mapEventId));
                AssignBattleHost(
                    broker,
                    hosts,
                    mapEventId,
                    "B",
                    Array.Empty<string>(),
                    epoch: 2);
                DrainGameThread();
                component.AgentActionHandler.ApplyRemoteGuardStates();

                Assert.Equal(Agent.GuardMode.None, puppetMirror.GuardMode);
                Assert.Equal(1, puppetMirror.ResetGuardCalls);
            }
            finally
            {
                BattleSpawnGate.EndBattle();
            }
        });
    }

    [Fact]
    public void SuccessorAction_BeforeHostAssignment_WaitsForMigration()
    {
        using var fixture = new MissionEngineFixture();
        var observer = Clients.First();
        SetControllerId(observer, "observer");

        observer.Call(() =>
        {
            const string mapEventId = "mapEvent1";
            BattleSpawnGate.BeginBattle(mapEventId);
            try
            {
                var mock = fixture.CreateMission(observer);
                var component = observer.Resolve<ICoopMissionComponent>();
                var registry = observer.Resolve<INetworkAgentRegistry>();
                var broker = observer.Resolve<IMessageBroker>();
                var hosts = observer.Resolve<IBattleHostRegistry>();
                var agentId = Guid.NewGuid();

                broker.Publish(this, new NetworkMissionPeerEntered("A", mapEventId));
                broker.Publish(this, new NetworkMissionPeerEntered("B", mapEventId));
                AssignBattleHost(
                    broker,
                    hosts,
                    mapEventId,
                    "A",
                    new[] { "B" },
                    epoch: 1);
                DrainGameThread();

                var puppet = mock.SpawnAgent(new AgentBuildData(Game.Current.PlayerTroop)
                    .Controller(AgentControllerType.None));
                Assert.True(registry.TryRegisterAgent("A", agentId, puppet));
                Assert.True(AgentMirror.TryGet(puppet, out var puppetMirror));

                var successorAgent = mock.SpawnAgent(new AgentBuildData(Game.Current.PlayerTroop)
                    .Controller(AgentControllerType.Player));
                Assert.True(AgentMirror.TryGet(successorAgent, out var successorMirror));
                successorMirror.GuardMode = Agent.GuardMode.Down;

                ApplyOwnerAction(
                    component,
                    "B",
                    1L,
                    agentId,
                    successorAgent,
                    battleHostEpoch: 2);
                DrainGameThread();
                component.AgentActionHandler.ApplyRemoteGuardStates();
                Assert.Equal(Agent.GuardMode.None, puppetMirror.GuardMode);

                broker.Publish(this, new MissionPeerDisconnected("A", mapEventId));
                AssignBattleHost(
                    broker,
                    hosts,
                    mapEventId,
                    "B",
                    Array.Empty<string>(),
                    epoch: 2);
                DrainGameThread();
                component.AgentActionHandler.ApplyRemoteGuardStates();

                Assert.Equal(Agent.GuardMode.Down, puppetMirror.GuardMode);
                Assert.Equal(1, puppetMirror.SetWeaponGuardCalls);
            }
            finally
            {
                BattleSpawnGate.EndBattle();
            }
        });
    }

    [Fact]
    public void LaterSuccessorAction_WaitsAcrossMigrationsAfterOldHostRejoins()
    {
        using var fixture = new MissionEngineFixture();
        var observer = Clients.First();
        SetControllerId(observer, "observer");

        observer.Call(() =>
        {
            const string mapEventId = "mapEvent1";
            BattleSpawnGate.BeginBattle(mapEventId);
            try
            {
                var mock = fixture.CreateMission(observer);
                var component = observer.Resolve<ICoopMissionComponent>();
                var registry = observer.Resolve<INetworkAgentRegistry>();
                var broker = observer.Resolve<IMessageBroker>();
                var hosts = observer.Resolve<IBattleHostRegistry>();
                var agentId = Guid.NewGuid();

                broker.Publish(this, new NetworkMissionPeerEntered("A", mapEventId));
                broker.Publish(this, new NetworkMissionPeerEntered("B", mapEventId));
                broker.Publish(this, new NetworkMissionPeerEntered("C", mapEventId));
                AssignBattleHost(
                    broker,
                    hosts,
                    mapEventId,
                    "A",
                    new[] { "B", "C" },
                    epoch: 1);
                DrainGameThread();

                var puppet = mock.SpawnAgent(new AgentBuildData(Game.Current.PlayerTroop)
                    .Controller(AgentControllerType.None));
                Assert.True(registry.TryRegisterAgent("A", agentId, puppet));
                Assert.True(AgentMirror.TryGet(puppet, out var puppetMirror));

                var finalHostAgent = mock.SpawnAgent(new AgentBuildData(Game.Current.PlayerTroop)
                    .Controller(AgentControllerType.Player));
                var intermediateHostAgent = mock.SpawnAgent(new AgentBuildData(Game.Current.PlayerTroop)
                    .Controller(AgentControllerType.Player));
                Assert.True(AgentMirror.TryGet(finalHostAgent, out var finalHostMirror));
                Assert.True(AgentMirror.TryGet(
                    intermediateHostAgent,
                    out var intermediateHostMirror));
                finalHostMirror.GuardMode = Agent.GuardMode.Up;
                intermediateHostMirror.GuardMode = Agent.GuardMode.Down;

                ApplyOwnerAction(
                    component,
                    "C",
                    1L,
                    agentId,
                    finalHostAgent,
                    battleHostEpoch: 3);
                ApplyOwnerAction(
                    component,
                    "B",
                    1L,
                    agentId,
                    intermediateHostAgent,
                    battleHostEpoch: 2);
                DrainGameThread();
                component.AgentActionHandler.ApplyRemoteGuardStates();
                Assert.Equal(Agent.GuardMode.None, puppetMirror.GuardMode);
                Assert.Equal(0, puppetMirror.SetWeaponGuardCalls);

                broker.Publish(this, new MissionPeerDisconnected("A", mapEventId));
                AssignBattleHost(
                    broker,
                    hosts,
                    mapEventId,
                    "B",
                    new[] { "C" },
                    epoch: 2);
                DrainGameThread();
                component.AgentActionHandler.ApplyRemoteGuardStates();
                Assert.Equal(Agent.GuardMode.None, puppetMirror.GuardMode);
                Assert.Equal(0, puppetMirror.SetWeaponGuardCalls);

                broker.Publish(this, new NetworkMissionPeerEntered("A", mapEventId));
                broker.Publish(this, new MissionPeerDisconnected("B", mapEventId));
                AssignBattleHost(
                    broker,
                    hosts,
                    mapEventId,
                    "C",
                    Array.Empty<string>(),
                    epoch: 3);
                DrainGameThread();
                component.AgentActionHandler.ApplyRemoteGuardStates();

                Assert.Equal(Agent.GuardMode.Up, puppetMirror.GuardMode);
                Assert.Equal(1, puppetMirror.SetWeaponGuardCalls);
            }
            finally
            {
                BattleSpawnGate.EndBattle();
            }
        });
    }

    [Fact]
    public void SupersededPendingAction_DoesNotDiscardFutureHostAction()
    {
        using var fixture = new MissionEngineFixture();
        var observer = Clients.First();
        SetControllerId(observer, "observer");

        observer.Call(() =>
        {
            const string mapEventId = "mapEvent1";
            BattleSpawnGate.BeginBattle(mapEventId);
            try
            {
                var mock = fixture.CreateMission(observer);
                var component = observer.Resolve<ICoopMissionComponent>();
                var registry = observer.Resolve<INetworkAgentRegistry>();
                var broker = observer.Resolve<IMessageBroker>();
                var hosts = observer.Resolve<IBattleHostRegistry>();
                var agentId = Guid.NewGuid();

                broker.Publish(this, new NetworkMissionPeerEntered("A", mapEventId));
                broker.Publish(this, new NetworkMissionPeerEntered("B", mapEventId));
                AssignBattleHost(
                    broker,
                    hosts,
                    mapEventId,
                    "A",
                    new[] { "B" },
                    epoch: 1);
                DrainGameThread();

                var oldHostAgent = mock.SpawnAgent(new AgentBuildData(Game.Current.PlayerTroop)
                    .Controller(AgentControllerType.Player));
                var successorAgent = mock.SpawnAgent(new AgentBuildData(Game.Current.PlayerTroop)
                    .Controller(AgentControllerType.Player));
                Assert.True(AgentMirror.TryGet(oldHostAgent, out var oldHostMirror));
                Assert.True(AgentMirror.TryGet(successorAgent, out var successorMirror));
                oldHostMirror.GuardMode = Agent.GuardMode.Left;
                successorMirror.GuardMode = Agent.GuardMode.Right;

                ApplyOwnerAction(
                    component,
                    "B",
                    1L,
                    agentId,
                    successorAgent,
                    battleHostEpoch: 2);
                ApplyOwnerAction(
                    component,
                    "A",
                    1L,
                    agentId,
                    oldHostAgent,
                    battleHostEpoch: 1);
                DrainGameThread();

                var puppet = mock.SpawnAgent(new AgentBuildData(Game.Current.PlayerTroop)
                    .Controller(AgentControllerType.None));
                Assert.True(registry.TryRegisterAgent("A", agentId, puppet));
                Assert.True(AgentMirror.TryGet(puppet, out var puppetMirror));

                component.AgentActionHandler.ApplyRemoteGuardStates();
                Assert.Equal(Agent.GuardMode.None, puppetMirror.GuardMode);

                broker.Publish(this, new MissionPeerDisconnected("A", mapEventId));
                AssignBattleHost(
                    broker,
                    hosts,
                    mapEventId,
                    "B",
                    Array.Empty<string>(),
                    epoch: 2);
                DrainGameThread();
                component.AgentActionHandler.ApplyRemoteGuardStates();

                Assert.Equal(Agent.GuardMode.Right, puppetMirror.GuardMode);
                Assert.Equal(1, puppetMirror.SetWeaponGuardCalls);
            }
            finally
            {
                BattleSpawnGate.EndBattle();
            }
        });
    }

    [Fact]
    public void AuthorityTransfer_DiscardsPreviousOwnersGuardStateAndPackets()
    {
        using var fixture = new MissionEngineFixture();
        var peer = Clients.First();
        SetControllerId(peer, "peer");

        peer.Call(() =>
        {
            var mock = fixture.CreateMission(peer);
            var component = peer.Resolve<ICoopMissionComponent>();
            var registry = peer.Resolve<INetworkAgentRegistry>();
            var agentId = Guid.NewGuid();

            var puppet = mock.SpawnAgent(new AgentBuildData(Game.Current.PlayerTroop)
                .Controller(AgentControllerType.None));
            Assert.True(registry.TryRegisterAgent("owner-a", agentId, puppet));
            Assert.True(AgentMirror.TryGet(puppet, out var puppetMirror));

            var ownerA = mock.SpawnAgent(new AgentBuildData(Game.Current.PlayerTroop)
                .Controller(AgentControllerType.Player));
            Assert.True(AgentMirror.TryGet(ownerA, out var ownerAMirror));
            ownerAMirror.GuardMode = Agent.GuardMode.Left;

            ApplyOwnerAction(component, "owner-a", 1L, agentId, ownerA);
            component.AgentActionHandler.ApplyRemoteGuardStates();
            Assert.Equal(Agent.GuardMode.Left, puppetMirror.GuardMode);

            Assert.True(registry.TryTransferAuthority("owner-b", agentId));
            component.AgentActionHandler.ApplyRemoteGuardStates();
            Assert.Equal(Agent.GuardMode.None, puppetMirror.GuardMode);
            Assert.Equal(1, puppetMirror.ResetGuardCalls);

            ApplyOwnerAction(component, "owner-a", 2L, agentId, ownerA);
            component.AgentActionHandler.ApplyRemoteGuardStates();
            Assert.Equal(Agent.GuardMode.None, puppetMirror.GuardMode);
            Assert.Equal(1, puppetMirror.SetWeaponGuardCalls);

            var ownerB = mock.SpawnAgent(new AgentBuildData(Game.Current.PlayerTroop)
                .Controller(AgentControllerType.Player));
            Assert.True(AgentMirror.TryGet(ownerB, out var ownerBMirror));
            ownerBMirror.GuardMode = Agent.GuardMode.Right;

            ApplyOwnerAction(component, "owner-b", 1L, agentId, ownerB);
            component.AgentActionHandler.ApplyRemoteGuardStates();
            Assert.Equal(Agent.GuardMode.Right, puppetMirror.GuardMode);
            Assert.Equal(2, puppetMirror.SetWeaponGuardCalls);
        });
    }

    [Fact]
    public void LocalAuthorityTransfer_ClearsRemoteGuardBeforePolling()
    {
        using var fixture = new MissionEngineFixture();
        var peer = Clients.First();
        SetControllerId(peer, "owner-b");

        peer.Call(() =>
        {
            var mock = fixture.CreateMission(peer);
            var component = peer.Resolve<ICoopMissionComponent>();
            var registry = peer.Resolve<INetworkAgentRegistry>();
            var network = peer.Resolve<MockBattleNetwork>();
            var agentId = Guid.NewGuid();

            var puppet = mock.SpawnAgent(new AgentBuildData(Game.Current.PlayerTroop)
                .Controller(AgentControllerType.None));
            Assert.True(registry.TryRegisterAgent("owner-a", agentId, puppet));
            Assert.True(AgentMirror.TryGet(puppet, out var puppetMirror));

            var ownerA = mock.SpawnAgent(new AgentBuildData(Game.Current.PlayerTroop)
                .Controller(AgentControllerType.Player));
            Assert.True(AgentMirror.TryGet(ownerA, out var ownerAMirror));
            ownerAMirror.GuardMode = Agent.GuardMode.Left;

            ApplyOwnerAction(component, "owner-a", 1L, agentId, ownerA);
            component.AgentActionHandler.ApplyRemoteGuardStates();
            Assert.Equal(Agent.GuardMode.Left, puppetMirror.GuardMode);

            Assert.True(registry.TryTransferAuthority("owner-b", agentId));
            component.AgentActionHandler.PollActions();

            Assert.Equal(Agent.GuardMode.None, puppetMirror.GuardMode);
            Assert.Equal(1, puppetMirror.ResetGuardCalls);
            Assert.Empty(network.NetworkSentPackets.GetPackets<AgentActionPacket>());
        });
    }

    [Fact]
    public void OlderCatchUp_AfterRegistration_DoesNotReplaceBufferedRelease()
    {
        using var fixture = new MissionEngineFixture();
        var peer = Clients.First();
        SetControllerId(peer, "peer");

        peer.Call(() =>
        {
            var mock = fixture.CreateMission(peer);
            var component = peer.Resolve<ICoopMissionComponent>();
            var registry = peer.Resolve<INetworkAgentRegistry>();
            var agentId = Guid.NewGuid();

            var owner = mock.SpawnAgent(new AgentBuildData(Game.Current.PlayerTroop)
                .Controller(AgentControllerType.Player));
            Assert.True(AgentMirror.TryGet(owner, out var ownerMirror));
            ownerMirror.GuardMode = Agent.GuardMode.Left;
            var heldGuard = new AgentActionData(owner);
            ownerMirror.GuardMode = Agent.GuardMode.None;
            var releasedGuard = new AgentActionData(owner);

            component.AgentActionHandler.HandlePacket(null,
                new AgentActionPacket(
                    "owner",
                    new[] { agentId },
                    new[] { releasedGuard },
                    new[] { 2L }));

            var puppet = mock.SpawnAgent(new AgentBuildData(Game.Current.PlayerTroop)
                .Controller(AgentControllerType.None));
            Assert.True(AgentMirror.TryGet(puppet, out var puppetMirror));
            puppetMirror.GuardMode = Agent.GuardMode.Left;
            Assert.True(registry.TryRegisterAgent("owner", agentId, puppet));

            component.AgentActionHandler.HandlePacket(null,
                new AgentActionPacket(
                    "owner",
                    new[] { agentId },
                    new[] { heldGuard },
                    new[] { 1L }));

            component.AgentActionHandler.ApplyRemoteGuardStates();
            component.AgentActionHandler.ApplyRemoteGuardStates();

            Assert.Equal(Agent.GuardMode.None, puppetMirror.GuardMode);
            Assert.Equal(0, puppetMirror.SetWeaponGuardCalls);
            Assert.Equal(1, puppetMirror.ResetGuardCalls);
        });
    }

    [Theory]
    [InlineData(Agent.GuardMode.Up)]
    [InlineData(Agent.GuardMode.Down)]
    [InlineData(Agent.GuardMode.Left)]
    [InlineData(Agent.GuardMode.Right)]
    public void GuardApply_MapsEveryGuardDirection(Agent.GuardMode guardMode)
    {
        using var fixture = new MissionEngineFixture();
        var peer = Clients.First();

        peer.Call(() =>
        {
            var mock = fixture.CreateMission(peer);
            var puppet = mock.SpawnAgent(new AgentBuildData(Game.Current.PlayerTroop)
                .Controller(AgentControllerType.None));

            AgentActionData.ApplyGuardState(puppet, guardMode);

            Assert.True(AgentMirror.TryGet(puppet, out var mirror));
            Assert.Equal(guardMode, mirror.GuardMode);
            Assert.Equal(1, mirror.SetWeaponGuardCalls);
            Assert.Equal(0, mirror.ResetGuardCalls);
        });
    }

    [Fact]
    public void GuardApply_ReassertsHeldGuard_AndSkipsRedundantReset()
    {
        using var fixture = new MissionEngineFixture();
        var peer = Clients.First();

        peer.Call(() =>
        {
            var mock = fixture.CreateMission(peer);
            var puppet = mock.SpawnAgent(new AgentBuildData(Game.Current.PlayerTroop)
                .Controller(AgentControllerType.None));
            Assert.True(AgentMirror.TryGet(puppet, out var mirror));

            AgentActionData.ApplyGuardState(puppet, Agent.GuardMode.None);
            Assert.Equal(0, mirror.ResetGuardCalls);

            AgentActionData.ApplyGuardState(puppet, Agent.GuardMode.Up);
            AgentActionData.ApplyGuardState(puppet, Agent.GuardMode.Up);
            Assert.Equal(2, mirror.SetWeaponGuardCalls);

            AgentActionData.ApplyGuardState(puppet, Agent.GuardMode.None);
            AgentActionData.ApplyGuardState(puppet, Agent.GuardMode.None);
            Assert.Equal(Agent.GuardMode.None, mirror.GuardMode);
            Assert.Equal(1, mirror.ResetGuardCalls);
        });
    }

    private static void ApplyOwnerAction(
        ICoopMissionComponent component,
        long sequence,
        Guid agentId,
        Agent owner)
    {
        ApplyOwnerAction(component, "owner", sequence, agentId, owner);
    }

    private static void DrainGameThread()
    {
        GameThread.Run(() => { }, blocking: true);
    }

    private static void ApplyOwnerAction(
        ICoopMissionComponent component,
        string controllerId,
        long sequence,
        Guid agentId,
        Agent owner,
        int battleHostEpoch = 0)
    {
        component.AgentActionHandler.HandlePacket(null,
            new AgentActionPacket(
                controllerId,
                new[] { agentId },
                new[] { new AgentActionData(owner) },
                new[] { sequence },
                battleHostEpoch));
    }

    private static void AssignBattleHost(
        IMessageBroker broker,
        IBattleHostRegistry hosts,
        string mapEventId,
        string hostControllerId,
        string[] successorControllerIds,
        int epoch)
    {
        hosts.Set(
            mapEventId,
            new BattleHostAssignment(
                hostControllerId,
                successorControllerIds,
                epoch));
        broker.Publish(
            typeof(BattleBlockingSyncTests),
            new NetworkBattleHostAssigned(
                mapEventId,
                hostControllerId,
                successorControllerIds,
                epoch));
    }
}
