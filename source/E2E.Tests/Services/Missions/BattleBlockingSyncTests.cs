using System;
using System.Linq;
using Autofac;
using Common;
using Common.Messaging;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Environment.Mock;
using E2E.Tests.Environment.MockEngine;
using GameInterface.Services.MapEvents;
using Missions;
using Missions.Agents.Packets;
using Missions.Battles;
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
        RunScenario("owner", context =>
        {
            var agentId = Guid.NewGuid();

            Agent agent = SpawnRegisteredAgent(
                context, "owner", agentId, AgentControllerType.Player,
                out MirrorAgent mirror);

            int action0 = agent.GetCurrentAction(0).Index;
            int action1 = agent.GetCurrentAction(1).Index;
            context.Component.AgentActionHandler.PollActions();
            Assert.Empty(context.Network.NetworkSentPackets.GetPackets<AgentActionPacket>());

            mirror.GuardMode = Agent.GuardMode.Left;
            context.Component.AgentActionHandler.PollActions();

            AgentActionPacket packet = Assert.Single(
                context.Network.NetworkSentPackets.GetPackets<AgentActionPacket>());
            Assert.Equal(agentId, Assert.Single(packet.AgentIds));
            AgentActionData data = Assert.Single(packet.Actions);
            Assert.Equal(action0, data.Action0Index);
            Assert.Equal(action1, data.Action1Index);
            Assert.Equal(Agent.GuardMode.Left, data.GuardMode);
            Assert.Equal(1L, Assert.Single(packet.Sequences));
            Assert.Equal(0, packet.BattleHostEpoch);
        });
    }

    [Fact]
    public void PollActions_DefendFlagsWithoutGuard_SendsActionPacket()
    {
        RunScenario("owner", context =>
        {
            var agentId = Guid.NewGuid();

            Agent agent = SpawnRegisteredAgent(
                context, "owner", agentId, AgentControllerType.Player,
                out MirrorAgent mirror);

            context.Component.AgentActionHandler.PollActions();
            Assert.Empty(context.Network.NetworkSentPackets.GetPackets<AgentActionPacket>());

            Agent.MovementControlFlag defendFlags =
                Agent.MovementControlFlag.DefendBlock
                | Agent.MovementControlFlag.DefendLeft;
            mirror.MovementFlags = defendFlags;
            context.Component.AgentActionHandler.PollActions();

            AgentActionPacket packet = Assert.Single(
                context.Network.NetworkSentPackets.GetPackets<AgentActionPacket>());
            AgentActionData data = Assert.Single(packet.Actions);
            Assert.Equal(defendFlags, data.DefendFlags);
            Assert.Equal(Agent.GuardMode.None, data.GuardMode);
        });
    }

    [Fact]
    public void MissionPreTick_ReassertsHeldDefendFlagsWithoutGuardMode_ThenClears()
    {
        RunScenario("peer", context =>
        {
            var controller = context.Instance.Container.Resolve<CoopBattleController>(
                new TypedParameter(typeof(ICoopMissionComponent), context.Component));
            var agentId = Guid.NewGuid();

            Agent puppet = SpawnRegisteredAgent(
                context, "owner", agentId, AgentControllerType.None,
                out MirrorAgent puppetMirror);
            Agent owner = SpawnAgent(context, AgentControllerType.Player, out MirrorAgent ownerMirror);

            Agent.MovementControlFlag defendFlags =
                Agent.MovementControlFlag.DefendBlock
                | Agent.MovementControlFlag.DefendRight;
            ownerMirror.MovementFlags = defendFlags;
            ApplyOwnerAction(context.Component, 1L, agentId, owner);
            context.Component.AgentActionHandler.ApplyRemoteGuardStates();

            puppetMirror.MovementFlags = Agent.MovementControlFlag.Forward;
            controller.OnPreMissionTick(0f);

            Assert.Equal(
                Agent.MovementControlFlag.Forward | defendFlags,
                puppetMirror.MovementFlags);

            puppetMirror.MovementFlags = Agent.MovementControlFlag.Forward;
            controller.OnPreMissionTick(0f);
            Assert.Equal(
                Agent.MovementControlFlag.Forward | defendFlags,
                puppetMirror.MovementFlags);

            ownerMirror.MovementFlags = Agent.MovementControlFlag.None;
            ApplyOwnerAction(context.Component, 2L, agentId, owner);
            context.Component.AgentActionHandler.ApplyRemoteGuardStates();

            puppetMirror.MovementFlags = Agent.MovementControlFlag.Forward;
            controller.OnPreMissionTick(0f);

            Assert.Equal(
                Agent.MovementControlFlag.Forward,
                puppetMirror.MovementFlags);
        });
    }

    [Theory]
    [InlineData("owner", 7)]
    [InlineData("other-host", 0)]
    public void PollActions_StampsEpochOnlyForBattleHost(
        string hostControllerId,
        int expectedEpoch)
    {
        const string mapEventId = "mapEvent1";
        RunBattleScenario("owner", mapEventId, context =>
        {
            var agentId = Guid.NewGuid();

            context.Hosts.Set(
                mapEventId,
                new BattleHostAssignment(
                    hostControllerId,
                    Array.Empty<string>(),
                    epoch: 7));

            Agent agent = SpawnRegisteredAgent(
                context, "owner", agentId, AgentControllerType.Player,
                out MirrorAgent mirror);

            context.Component.AgentActionHandler.PollActions();
            Assert.Empty(context.Network.NetworkSentPackets.GetPackets<AgentActionPacket>());

            mirror.GuardMode = Agent.GuardMode.Right;
            context.Component.AgentActionHandler.PollActions();

            AgentActionPacket packet = Assert.Single(
                context.Network.NetworkSentPackets.GetPackets<AgentActionPacket>());
            Assert.Equal(expectedEpoch, packet.BattleHostEpoch);
        });
    }

    [Fact]
    public void CatchUpJoiner_HeldGuard_SendsCurrentStateToJoiningPeer()
    {
        RunScenario("owner", context =>
        {
            var agentId = Guid.NewGuid();

            Agent agent = SpawnRegisteredAgent(
                context, "owner", agentId, AgentControllerType.Player,
                out MirrorAgent mirror);

            mirror.GuardMode = Agent.GuardMode.Right;
            context.Component.AgentActionHandler.PollActions();
            context.Network.NetworkSentPackets.Packets.Clear();

            context.Component.AgentActionHandler.CatchUpJoiner("joiner");

            var directSend = Assert.Single(context.Network.DirectPacketSends);
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
        RunScenario("peer", context =>
        {
            var agentId = Guid.NewGuid();

            Agent owner = SpawnAgent(context, AgentControllerType.Player, out MirrorAgent ownerMirror);
            ownerMirror.GuardMode = Agent.GuardMode.Down;

            context.Component.AgentActionHandler.HandlePacket(null,
                new AgentActionPacket(
                    "owner",
                    new[] { agentId },
                    new[] { new AgentActionData(owner) },
                    new[] { 1L }));

            Agent puppet = SpawnRegisteredAgent(
                context, "owner", agentId, AgentControllerType.None,
                out MirrorAgent puppetMirror);

            context.Component.AgentActionHandler.ApplyRemoteGuardStates();

            Assert.Equal(Agent.GuardMode.Down, puppetMirror.GuardMode);
            Assert.Equal(1, puppetMirror.SetWeaponGuardCalls);
        });
    }

    [Fact]
    public void ActionTick_ReassertsHeldGuardAfterNativeDecay_WithoutAnotherPacket()
    {
        var agentId = Guid.NewGuid();

        RunScenario("peer", context =>
        {
            Agent puppet = SpawnRegisteredAgent(
                context, "owner", agentId, AgentControllerType.None,
                out MirrorAgent puppetMirror);
            Agent owner = SpawnAgent(context, AgentControllerType.Player, out MirrorAgent ownerMirror);

            ownerMirror.GuardMode = Agent.GuardMode.Left;
            ApplyOwnerAction(context.Component, 1L, agentId, owner);
            context.Component.AgentActionHandler.ApplyRemoteGuardStates();

            Assert.Equal(Agent.GuardMode.Left, puppetMirror.GuardMode);
            Assert.Equal(1, puppetMirror.SetWeaponGuardCalls);
            Assert.Equal(0, puppetMirror.ResetGuardCalls);

            puppetMirror.GuardMode = Agent.GuardMode.None;
            context.Component.AgentActionHandler.ApplyRemoteGuardStates();

            Assert.Equal(Agent.GuardMode.Left, puppetMirror.GuardMode);
            Assert.Equal(2, puppetMirror.SetWeaponGuardCalls);

            ownerMirror.GuardMode = Agent.GuardMode.None;
            ApplyOwnerAction(context.Component, 2L, agentId, owner);
            context.Component.AgentActionHandler.ApplyRemoteGuardStates();
            context.Component.AgentActionHandler.ApplyRemoteGuardStates();

            Assert.Equal(Agent.GuardMode.None, puppetMirror.GuardMode);
            Assert.Equal(1, puppetMirror.ResetGuardCalls);
        });
    }

    [Fact]
    public void MigratedHostAction_RemainsAuthoritativeAfterOldHostRejoins()
    {
        const string mapEventId = "mapEvent1";
        RunBattleScenario("observer", mapEventId, context =>
        {
                var migratedAgentId = Guid.NewGuid();
                var activeOwnerAgentId = Guid.NewGuid();

                context.Broker.Publish(this, new NetworkMissionPeerEntered("A", mapEventId));
                context.Broker.Publish(this, new NetworkMissionPeerEntered("B", mapEventId));
                context.Broker.Publish(this, new NetworkMissionPeerEntered("D", mapEventId));
                AssignBattleHost(context, mapEventId, "A", new[] { "B", "D" }, epoch: 1);
                DrainGameThread();

                var rejoinedOwnerAgentId = Guid.NewGuid();
                Agent migratedPuppet = SpawnRegisteredAgent(
                    context, "A", migratedAgentId, AgentControllerType.None,
                    out MirrorAgent migratedMirror);
                Agent activeOwnerPuppet = SpawnRegisteredAgent(
                    context, "D", activeOwnerAgentId, AgentControllerType.None,
                    out MirrorAgent activeOwnerMirror);
                Agent rejoinedOwnerPuppet = SpawnAgent(
                    context, AgentControllerType.None, out MirrorAgent rejoinedOwnerMirror);

                context.Broker.Publish(this, new MissionPeerDisconnected("A", mapEventId));
                AssignBattleHost(context, mapEventId, "B", new[] { "D" }, epoch: 2);

                context.Broker.Publish(this, new NetworkMissionPeerEntered("A", mapEventId));
                bool rejoinedAgentRegistered = false;
                GameThread.RunSafe(() =>
                    rejoinedAgentRegistered = context.Registry.TryRegisterAgent(
                        "A",
                        rejoinedOwnerAgentId,
                        rejoinedOwnerPuppet));
                DrainGameThread();
                Assert.True(rejoinedAgentRegistered);

                Agent hostAgent = SpawnAgent(context, AgentControllerType.Player, out MirrorAgent hostMirror);
                hostMirror.GuardMode = Agent.GuardMode.Right;
                var guard = new AgentActionData(hostAgent);

                context.Component.AgentActionHandler.HandlePacket(null,
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
                    context.Component, "A", 1L, rejoinedOwnerAgentId, hostAgent);
                DrainGameThread();
                context.Component.AgentActionHandler.ApplyRemoteGuardStates();

                Assert.Equal(Agent.GuardMode.Right, migratedMirror.GuardMode);
                Assert.Equal(1, migratedMirror.SetWeaponGuardCalls);
                Assert.Equal(Agent.GuardMode.None, activeOwnerMirror.GuardMode);
                Assert.Equal(0, activeOwnerMirror.SetWeaponGuardCalls);
                Assert.Equal(Agent.GuardMode.Up, rejoinedOwnerMirror.GuardMode);
                Assert.Equal(1, rejoinedOwnerMirror.SetWeaponGuardCalls);

                migratedMirror.GuardMode = Agent.GuardMode.None;
                context.Component.AgentActionHandler.ApplyRemoteGuardStates();

                Assert.Equal(Agent.GuardMode.Right, migratedMirror.GuardMode);
                Assert.Equal(2, migratedMirror.SetWeaponGuardCalls);
                Assert.Equal(Agent.GuardMode.None, activeOwnerMirror.GuardMode);
                Assert.Equal(Agent.GuardMode.Up, rejoinedOwnerMirror.GuardMode);
        });
    }

    [Fact]
    public void QueuedOldHostRegistration_BeforeAssignment_IsMigrated()
    {
        const string mapEventId = "mapEvent1";
        RunBattleScenario("observer", mapEventId, context =>
        {
                var agentId = Guid.NewGuid();

                context.Broker.Publish(this, new NetworkMissionPeerEntered("A", mapEventId));
                context.Broker.Publish(this, new NetworkMissionPeerEntered("B", mapEventId));
                AssignBattleHost(context, mapEventId, "A", new[] { "B" }, epoch: 1);
                DrainGameThread();

                Agent puppet = SpawnAgent(context, AgentControllerType.None, out MirrorAgent puppetMirror);

                bool oldHostAgentRegistered = false;
                GameThread.RunSafe(() =>
                    oldHostAgentRegistered = context.Registry.TryRegisterAgent(
                        "A",
                        agentId,
                        puppet));

                context.Broker.Publish(this, new MissionPeerDisconnected("A", mapEventId));
                AssignBattleHost(context, mapEventId, "B", Array.Empty<string>(), epoch: 2);
                DrainGameThread();
                Assert.True(oldHostAgentRegistered);

                Agent hostAgent = SpawnAgent(context, AgentControllerType.Player, out MirrorAgent hostMirror);
                hostMirror.GuardMode = Agent.GuardMode.Left;

                ApplyOwnerAction(
                    context.Component, "B", 1L, agentId, hostAgent, battleHostEpoch: 2);
                DrainGameThread();
                context.Component.AgentActionHandler.ApplyRemoteGuardStates();

                Assert.Equal(Agent.GuardMode.Left, puppetMirror.GuardMode);
                Assert.Equal(1, puppetMirror.SetWeaponGuardCalls);
        });
    }

    [Fact]
    public void LateOldHostRegistration_UsesPendingSuccessorAction()
    {
        const string mapEventId = "mapEvent1";
        RunBattleScenario("observer", mapEventId, context =>
        {
                var agentId = Guid.NewGuid();

                context.Broker.Publish(this, new NetworkMissionPeerEntered("A", mapEventId));
                context.Broker.Publish(this, new NetworkMissionPeerEntered("B", mapEventId));
                AssignBattleHost(context, mapEventId, "A", new[] { "B" }, epoch: 1);
                DrainGameThread();

                Agent hostAgent = SpawnAgent(context, AgentControllerType.Player, out MirrorAgent hostMirror);
                hostMirror.GuardMode = Agent.GuardMode.Down;

                ApplyOwnerAction(
                    context.Component, "B", 1L, agentId, hostAgent, battleHostEpoch: 2);
                context.Broker.Publish(this, new MissionPeerDisconnected("A", mapEventId));
                AssignBattleHost(context, mapEventId, "B", Array.Empty<string>(), epoch: 2);
                DrainGameThread();

                context.Broker.Publish(this, new NetworkMissionPeerEntered("A", mapEventId));
                AssignBattleHost(context, mapEventId, "B", Array.Empty<string>(), epoch: 2);
                DrainGameThread();

                Agent puppet = SpawnRegisteredAgent(
                    context, "A", agentId, AgentControllerType.None,
                    out MirrorAgent puppetMirror);

                context.Component.AgentActionHandler.ApplyRemoteGuardStates();

                Assert.Equal(Agent.GuardMode.Down, puppetMirror.GuardMode);
                Assert.Equal(1, puppetMirror.SetWeaponGuardCalls);
        });
    }

    [Fact]
    public void DepartedHostGuard_ClearsWhenSuccessorTakesAuthority()
    {
        const string mapEventId = "mapEvent1";
        RunBattleScenario("observer", mapEventId, context =>
        {
                var agentId = Guid.NewGuid();

                context.Broker.Publish(this, new NetworkMissionPeerEntered("A", mapEventId));
                context.Broker.Publish(this, new NetworkMissionPeerEntered("B", mapEventId));
                AssignBattleHost(context, mapEventId, "A", new[] { "B" }, epoch: 1);
                DrainGameThread();

                Agent puppet = SpawnRegisteredAgent(
                    context, "A", agentId, AgentControllerType.None,
                    out MirrorAgent puppetMirror);

                Agent oldHostAgent = SpawnAgent(
                    context, AgentControllerType.Player, out MirrorAgent oldHostMirror);
                Agent.MovementControlFlag defendFlags =
                    Agent.MovementControlFlag.DefendBlock
                    | Agent.MovementControlFlag.DefendLeft;
                oldHostMirror.MovementFlags = defendFlags;
                oldHostMirror.GuardMode = Agent.GuardMode.Left;

                ApplyOwnerAction(
                    context.Component, "A", 1L, agentId, oldHostAgent, battleHostEpoch: 1);
                DrainGameThread();
                context.Component.AgentActionHandler.ApplyRemoteGuardStates();
                Assert.Equal(Agent.GuardMode.Left, puppetMirror.GuardMode);
                Assert.Equal(
                    defendFlags,
                    AgentActionData.GetDefendMovementFlags(puppetMirror.MovementFlags));

                context.Broker.Publish(this, new MissionPeerDisconnected("A", mapEventId));
                AssignBattleHost(context, mapEventId, "B", Array.Empty<string>(), epoch: 2);
                DrainGameThread();
                context.Component.AgentActionHandler.ApplyRemoteGuardStates();

                Assert.Equal(Agent.GuardMode.None, puppetMirror.GuardMode);
                Assert.Equal(
                    Agent.MovementControlFlag.None,
                    AgentActionData.GetDefendMovementFlags(puppetMirror.MovementFlags));
                Assert.Equal(1, puppetMirror.ResetGuardCalls);
        });
    }

    [Fact]
    public void SuccessorAction_BeforeHostAssignment_WaitsForMigration()
    {
        const string mapEventId = "mapEvent1";
        RunBattleScenario("observer", mapEventId, context =>
        {
                var agentId = Guid.NewGuid();

                context.Broker.Publish(this, new NetworkMissionPeerEntered("A", mapEventId));
                context.Broker.Publish(this, new NetworkMissionPeerEntered("B", mapEventId));
                AssignBattleHost(context, mapEventId, "A", new[] { "B" }, epoch: 1);
                DrainGameThread();

                Agent puppet = SpawnRegisteredAgent(
                    context, "A", agentId, AgentControllerType.None,
                    out MirrorAgent puppetMirror);

                Agent successorAgent = SpawnAgent(
                    context, AgentControllerType.Player, out MirrorAgent successorMirror);
                successorMirror.GuardMode = Agent.GuardMode.Down;

                ApplyOwnerAction(
                    context.Component, "B", 1L, agentId, successorAgent, battleHostEpoch: 2);
                DrainGameThread();
                context.Component.AgentActionHandler.ApplyRemoteGuardStates();
                Assert.Equal(Agent.GuardMode.None, puppetMirror.GuardMode);

                context.Broker.Publish(this, new MissionPeerDisconnected("A", mapEventId));
                AssignBattleHost(context, mapEventId, "B", Array.Empty<string>(), epoch: 2);
                DrainGameThread();
                context.Component.AgentActionHandler.ApplyRemoteGuardStates();

                Assert.Equal(Agent.GuardMode.Down, puppetMirror.GuardMode);
                Assert.Equal(1, puppetMirror.SetWeaponGuardCalls);
        });
    }

    [Fact]
    public void LaterSuccessorAction_WaitsAcrossMigrationsAfterOldHostRejoins()
    {
        const string mapEventId = "mapEvent1";
        RunBattleScenario("observer", mapEventId, context =>
        {
                var agentId = Guid.NewGuid();

                context.Broker.Publish(this, new NetworkMissionPeerEntered("A", mapEventId));
                context.Broker.Publish(this, new NetworkMissionPeerEntered("B", mapEventId));
                context.Broker.Publish(this, new NetworkMissionPeerEntered("C", mapEventId));
                AssignBattleHost(context, mapEventId, "A", new[] { "B", "C" }, epoch: 1);
                DrainGameThread();

                Agent puppet = SpawnRegisteredAgent(
                    context, "A", agentId, AgentControllerType.None,
                    out MirrorAgent puppetMirror);

                Agent finalHostAgent = SpawnAgent(
                    context, AgentControllerType.Player, out MirrorAgent finalHostMirror);
                Agent intermediateHostAgent = SpawnAgent(
                    context, AgentControllerType.Player, out MirrorAgent intermediateHostMirror);
                finalHostMirror.GuardMode = Agent.GuardMode.Up;
                intermediateHostMirror.GuardMode = Agent.GuardMode.Down;

                ApplyOwnerAction(
                    context.Component, "C", 1L, agentId, finalHostAgent, battleHostEpoch: 3);
                ApplyOwnerAction(
                    context.Component, "B", 1L, agentId, intermediateHostAgent, battleHostEpoch: 2);
                DrainGameThread();
                context.Component.AgentActionHandler.ApplyRemoteGuardStates();
                Assert.Equal(Agent.GuardMode.None, puppetMirror.GuardMode);
                Assert.Equal(0, puppetMirror.SetWeaponGuardCalls);

                context.Broker.Publish(this, new MissionPeerDisconnected("A", mapEventId));
                AssignBattleHost(context, mapEventId, "B", new[] { "C" }, epoch: 2);
                DrainGameThread();
                context.Component.AgentActionHandler.ApplyRemoteGuardStates();
                Assert.Equal(Agent.GuardMode.None, puppetMirror.GuardMode);
                Assert.Equal(0, puppetMirror.SetWeaponGuardCalls);

                context.Broker.Publish(this, new NetworkMissionPeerEntered("A", mapEventId));
                context.Broker.Publish(this, new MissionPeerDisconnected("B", mapEventId));
                AssignBattleHost(context, mapEventId, "C", Array.Empty<string>(), epoch: 3);
                DrainGameThread();
                context.Component.AgentActionHandler.ApplyRemoteGuardStates();

                Assert.Equal(Agent.GuardMode.Up, puppetMirror.GuardMode);
                Assert.Equal(1, puppetMirror.SetWeaponGuardCalls);
        });
    }

    [Fact]
    public void SupersededPendingAction_DoesNotDiscardFutureHostAction()
    {
        const string mapEventId = "mapEvent1";
        RunBattleScenario("observer", mapEventId, context =>
        {
                var agentId = Guid.NewGuid();

                context.Broker.Publish(this, new NetworkMissionPeerEntered("A", mapEventId));
                context.Broker.Publish(this, new NetworkMissionPeerEntered("B", mapEventId));
                AssignBattleHost(context, mapEventId, "A", new[] { "B" }, epoch: 1);
                DrainGameThread();

                Agent oldHostAgent = SpawnAgent(
                    context, AgentControllerType.Player, out MirrorAgent oldHostMirror);
                Agent successorAgent = SpawnAgent(
                    context, AgentControllerType.Player, out MirrorAgent successorMirror);
                oldHostMirror.GuardMode = Agent.GuardMode.Left;
                successorMirror.GuardMode = Agent.GuardMode.Right;

                ApplyOwnerAction(
                    context.Component, "B", 1L, agentId, successorAgent, battleHostEpoch: 2);
                ApplyOwnerAction(
                    context.Component, "A", 1L, agentId, oldHostAgent, battleHostEpoch: 1);
                DrainGameThread();

                Agent puppet = SpawnRegisteredAgent(
                    context, "A", agentId, AgentControllerType.None,
                    out MirrorAgent puppetMirror);

                context.Component.AgentActionHandler.ApplyRemoteGuardStates();
                Assert.Equal(Agent.GuardMode.None, puppetMirror.GuardMode);

                context.Broker.Publish(this, new MissionPeerDisconnected("A", mapEventId));
                AssignBattleHost(context, mapEventId, "B", Array.Empty<string>(), epoch: 2);
                DrainGameThread();
                context.Component.AgentActionHandler.ApplyRemoteGuardStates();

                Assert.Equal(Agent.GuardMode.Right, puppetMirror.GuardMode);
                Assert.Equal(1, puppetMirror.SetWeaponGuardCalls);
        });
    }

    [Fact]
    public void AuthorityTransfer_DiscardsPreviousOwnersGuardStateAndPackets()
    {
        RunScenario("peer", context =>
        {
            var agentId = Guid.NewGuid();

            Agent puppet = SpawnRegisteredAgent(
                context, "owner-a", agentId, AgentControllerType.None,
                out MirrorAgent puppetMirror);

            Agent ownerA = SpawnAgent(context, AgentControllerType.Player, out MirrorAgent ownerAMirror);
            Agent.MovementControlFlag defendFlags =
                Agent.MovementControlFlag.DefendBlock
                | Agent.MovementControlFlag.DefendLeft;
            ownerAMirror.MovementFlags = defendFlags;
            ownerAMirror.GuardMode = Agent.GuardMode.Left;

            ApplyOwnerAction(context.Component, "owner-a", 1L, agentId, ownerA);
            context.Component.AgentActionHandler.ApplyRemoteGuardStates();
            Assert.Equal(Agent.GuardMode.Left, puppetMirror.GuardMode);
            Assert.Equal(
                defendFlags,
                AgentActionData.GetDefendMovementFlags(puppetMirror.MovementFlags));

            Assert.True(context.Registry.TryTransferAuthority("owner-b", agentId));
            context.Component.AgentActionHandler.ApplyRemoteGuardStates();
            Assert.Equal(Agent.GuardMode.None, puppetMirror.GuardMode);
            Assert.Equal(
                Agent.MovementControlFlag.None,
                AgentActionData.GetDefendMovementFlags(puppetMirror.MovementFlags));
            Assert.Equal(1, puppetMirror.ResetGuardCalls);

            ApplyOwnerAction(context.Component, "owner-a", 2L, agentId, ownerA);
            context.Component.AgentActionHandler.ApplyRemoteGuardStates();
            Assert.Equal(Agent.GuardMode.None, puppetMirror.GuardMode);
            Assert.Equal(1, puppetMirror.SetWeaponGuardCalls);

            Agent ownerB = SpawnAgent(context, AgentControllerType.Player, out MirrorAgent ownerBMirror);
            ownerBMirror.GuardMode = Agent.GuardMode.Right;

            ApplyOwnerAction(context.Component, "owner-b", 1L, agentId, ownerB);
            context.Component.AgentActionHandler.ApplyRemoteGuardStates();
            Assert.Equal(Agent.GuardMode.Right, puppetMirror.GuardMode);
            Assert.Equal(2, puppetMirror.SetWeaponGuardCalls);
        });
    }

    [Fact]
    public void LocalAuthorityTransfer_ClearsRemoteGuardBeforePolling()
    {
        RunScenario("owner-b", context =>
        {
            var controller = context.Instance.Container.Resolve<CoopBattleController>(
                new TypedParameter(typeof(ICoopMissionComponent), context.Component));
            var agentId = Guid.NewGuid();

            Agent puppet = SpawnRegisteredAgent(
                context, "owner-a", agentId, AgentControllerType.None,
                out MirrorAgent puppetMirror);

            Agent ownerA = SpawnAgent(context, AgentControllerType.Player, out MirrorAgent ownerAMirror);
            Agent.MovementControlFlag defendFlags =
                Agent.MovementControlFlag.DefendBlock
                | Agent.MovementControlFlag.DefendLeft;
            ownerAMirror.MovementFlags = defendFlags;
            ownerAMirror.GuardMode = Agent.GuardMode.Left;

            ApplyOwnerAction(context.Component, "owner-a", 1L, agentId, ownerA);
            context.Component.AgentActionHandler.ApplyRemoteGuardStates();
            Assert.Equal(Agent.GuardMode.Left, puppetMirror.GuardMode);
            Assert.Equal(
                defendFlags,
                AgentActionData.GetDefendMovementFlags(puppetMirror.MovementFlags));

            Assert.True(context.Registry.TryTransferAuthority("owner-b", agentId));
            controller.OnPreMissionTick(0f);
            Assert.Equal(
                defendFlags,
                AgentActionData.GetDefendMovementFlags(puppetMirror.MovementFlags));
            context.Component.AgentActionHandler.PollActions();

            Assert.Equal(Agent.GuardMode.None, puppetMirror.GuardMode);
            Assert.Equal(
                Agent.MovementControlFlag.None,
                AgentActionData.GetDefendMovementFlags(puppetMirror.MovementFlags));
            Assert.Equal(1, puppetMirror.ResetGuardCalls);
            Assert.Empty(context.Network.NetworkSentPackets.GetPackets<AgentActionPacket>());
        });
    }

    [Fact]
    public void OlderCatchUp_AfterRegistration_DoesNotReplaceBufferedRelease()
    {
        RunScenario("peer", context =>
        {
            var agentId = Guid.NewGuid();

            Agent owner = SpawnAgent(context, AgentControllerType.Player, out MirrorAgent ownerMirror);
            ownerMirror.GuardMode = Agent.GuardMode.Left;
            var heldGuard = new AgentActionData(owner);
            ownerMirror.GuardMode = Agent.GuardMode.None;
            var releasedGuard = new AgentActionData(owner);

            context.Component.AgentActionHandler.HandlePacket(null,
                new AgentActionPacket(
                    "owner",
                    new[] { agentId },
                    new[] { releasedGuard },
                    new[] { 2L }));

            Agent puppet = SpawnAgent(
                context, AgentControllerType.None, out MirrorAgent puppetMirror);
            puppetMirror.GuardMode = Agent.GuardMode.Left;
            Assert.True(context.Registry.TryRegisterAgent("owner", agentId, puppet));

            context.Component.AgentActionHandler.HandlePacket(null,
                new AgentActionPacket(
                    "owner",
                    new[] { agentId },
                    new[] { heldGuard },
                    new[] { 1L }));

            context.Component.AgentActionHandler.ApplyRemoteGuardStates();
            context.Component.AgentActionHandler.ApplyRemoteGuardStates();

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
        RunScenario(null, context =>
        {
            Agent puppet = SpawnAgent(context, AgentControllerType.None, out MirrorAgent mirror);

            AgentActionData.ApplyGuardState(puppet, guardMode);

            Assert.Equal(guardMode, mirror.GuardMode);
            Assert.Equal(1, mirror.SetWeaponGuardCalls);
            Assert.Equal(0, mirror.ResetGuardCalls);
        });
    }

    [Fact]
    public void GuardApply_ReassertsHeldGuard_AndSkipsRedundantReset()
    {
        RunScenario(null, context =>
        {
            Agent puppet = SpawnAgent(context, AgentControllerType.None, out MirrorAgent mirror);

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

    private static Agent SpawnAgent(
        BlockingSyncContext context,
        AgentControllerType controllerType,
        out MirrorAgent mirror)
    {
        Agent agent = context.Mock.SpawnAgent(new AgentBuildData(Game.Current.PlayerTroop)
            .Controller(controllerType));
        Assert.True(AgentMirror.TryGet(agent, out mirror));
        return agent;
    }

    private static Agent SpawnRegisteredAgent(
        BlockingSyncContext context,
        string controllerId,
        Guid agentId,
        AgentControllerType controllerType,
        out MirrorAgent mirror)
    {
        Agent agent = SpawnAgent(context, controllerType, out mirror);
        Assert.True(context.Registry.TryRegisterAgent(controllerId, agentId, agent));
        return agent;
    }

    private static void RunInBattle(string mapEventId, Action action)
    {
        BattleSpawnGate.BeginBattle(mapEventId);
        try
        {
            action();
        }
        finally
        {
            BattleSpawnGate.EndBattle();
        }
    }

    private void RunScenario(
        string controllerId,
        Action<BlockingSyncContext> action)
    {
        using var fixture = new MissionEngineFixture();
        EnvironmentInstance instance = Clients.First();
        if (controllerId != null) SetControllerId(instance, controllerId);

        instance.Call(() => action(new BlockingSyncContext(fixture, instance)));
    }

    private void RunBattleScenario(
        string controllerId,
        string mapEventId,
        Action<BlockingSyncContext> action)
    {
        using var fixture = new MissionEngineFixture();
        EnvironmentInstance instance = Clients.First();
        SetControllerId(instance, controllerId);

        instance.Call(() => RunInBattle(
            mapEventId,
            () => action(new BlockingSyncContext(fixture, instance))));
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
        BlockingSyncContext context,
        string mapEventId,
        string hostControllerId,
        string[] successorControllerIds,
        int epoch)
    {
        context.Hosts.Set(
            mapEventId,
            new BattleHostAssignment(
                hostControllerId,
                successorControllerIds,
                epoch));
        context.Broker.Publish(
            typeof(BattleBlockingSyncTests),
            new NetworkBattleHostAssigned(
                mapEventId,
                hostControllerId,
                successorControllerIds,
                epoch));
    }

    private sealed class BlockingSyncContext
    {
        public EnvironmentInstance Instance { get; }
        public MockMission Mock { get; }
        public ICoopMissionComponent Component { get; }
        public INetworkAgentRegistry Registry { get; }
        public MockBattleNetwork Network { get; }
        public IMessageBroker Broker { get; }
        public IBattleHostRegistry Hosts { get; }

        public BlockingSyncContext(
            MissionEngineFixture fixture,
            EnvironmentInstance instance)
        {
            Instance = instance;
            Mock = fixture.CreateMission(instance);
            Component = instance.Resolve<ICoopMissionComponent>();
            Registry = instance.Resolve<INetworkAgentRegistry>();
            Network = instance.Resolve<MockBattleNetwork>();
            Broker = instance.Resolve<IMessageBroker>();
            Hosts = instance.Resolve<IBattleHostRegistry>();
        }
    }
}
