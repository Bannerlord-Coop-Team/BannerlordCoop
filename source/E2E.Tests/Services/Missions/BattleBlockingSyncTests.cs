using System;
using System.Linq;
using Autofac;
using Common;
using Common.Messaging;
using Common.PacketHandlers;
using Common.Serialization;
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
    public void PollActions_DefendFlagsWithoutNativeGuard_SendsEffectiveGuard()
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
            Assert.Equal(Agent.GuardMode.Left, data.GuardMode);
        });
    }

    [Theory]
    [InlineData(
        Agent.MovementControlFlag.DefendBlock,
        Agent.ActionCodeType.DefendShield,
        Agent.UsageDirection.DefendRight,
        Agent.MovementControlFlag.None,
        Agent.MovementControlFlag.DefendBlock,
        Agent.GuardMode.Right)]
    [InlineData(
        Agent.MovementControlFlag.None,
        Agent.ActionCodeType.Guard,
        Agent.UsageDirection.DefendRight,
        Agent.MovementControlFlag.DefendRight,
        Agent.MovementControlFlag.DefendRight,
        Agent.GuardMode.Right)]
    [InlineData(
        Agent.MovementControlFlag.None,
        Agent.ActionCodeType.Guard,
        Agent.UsageDirection.DefendAny,
        Agent.MovementControlFlag.None,
        Agent.MovementControlFlag.DefendBlock,
        Agent.GuardMode.None)]
    public void PollActions_MountedGuard_SendsAndClearsEffectiveDefendState(
        Agent.MovementControlFlag movementFlags,
        Agent.ActionCodeType actionType,
        Agent.UsageDirection actionDirection,
        Agent.MovementControlFlag nativeDefendFlag,
        Agent.MovementControlFlag expectedDefendFlag,
        Agent.GuardMode expectedGuardMode)
    {
        RunScenario("owner", context =>
        {
            var agentId = Guid.NewGuid();

            Agent agent = SpawnRegisteredAgent(
                context, "owner", agentId, AgentControllerType.Player,
                out MirrorAgent mirror);
            context.Mock.SpawnMount(agent);

            context.Component.AgentActionHandler.PollActions();
            Assert.Empty(context.Network.NetworkSentPackets.GetPackets<AgentActionPacket>());

            mirror.MovementFlags = movementFlags;
            mirror.Action1CodeType = actionType;
            mirror.Action1Direction = actionDirection;
            mirror.DefendMovementFlag = nativeDefendFlag;
            context.Component.AgentActionHandler.PollActions();

            AgentActionPacket heldPacket = Assert.Single(
                context.Network.NetworkSentPackets.GetPackets<AgentActionPacket>());
            AgentActionData heldAction = Assert.Single(heldPacket.Actions);
            Assert.Equal(expectedDefendFlag, heldAction.DefendFlags);
            Assert.Equal(expectedGuardMode, heldAction.GuardMode);

            mirror.MovementFlags = Agent.MovementControlFlag.None;
            mirror.Action1CodeType = Agent.ActionCodeType.Idle;
            mirror.Action1Direction = Agent.UsageDirection.None;
            mirror.DefendMovementFlag = Agent.MovementControlFlag.None;
            context.Component.AgentActionHandler.PollActions();

            AgentActionPacket releasePacket = context.Network.NetworkSentPackets
                .GetPackets<AgentActionPacket>()
                .Last();
            AgentActionData releaseAction = Assert.Single(releasePacket.Actions);
            Assert.Equal(Agent.MovementControlFlag.None, releaseAction.DefendFlags);
            Assert.Equal(Agent.GuardMode.None, releaseAction.GuardMode);
            Assert.Equal(2L, Assert.Single(releasePacket.Sequences));
        });
    }

    [Fact]
    public void AgentActionPacket_RoundTripsEffectiveMountedGuard()
    {
        RunScenario("owner", context =>
        {
            var agentId = Guid.NewGuid();
            Agent owner = SpawnAgent(
                context, AgentControllerType.Player, out MirrorAgent ownerMirror);
            context.Mock.SpawnMount(owner);
            ownerMirror.MovementFlags = Agent.MovementControlFlag.DefendBlock;
            ownerMirror.Action1Direction = Agent.UsageDirection.DefendLeft;

            var original = new AgentActionPacket(
                "owner",
                new[] { agentId },
                new[] { new AgentActionData(owner) },
                new[] { 1L });
            var serializer = new ProtoBufSerializer(new SerializableTypeMapper());

            byte[] wire = serializer.Serialize(original);
            var result = Assert.IsType<AgentActionPacket>(
                serializer.Deserialize<IPacket>(wire));

            AgentActionData action = Assert.Single(result.Actions);
            Assert.Equal(Agent.MovementControlFlag.DefendBlock, action.DefendFlags);
            Assert.Equal(Agent.GuardMode.Left, action.GuardMode);
        });
    }

    [Fact]
    public void MissionPreDisplayTick_ReassertsHeldDefendFlagsWithoutGuardMode_ThenClears()
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
            controller.OnPreDisplayMissionTick(0f);

            Assert.Equal(
                Agent.MovementControlFlag.Forward | defendFlags,
                puppetMirror.MovementFlags);

            puppetMirror.MovementFlags = Agent.MovementControlFlag.Forward;
            controller.OnPreDisplayMissionTick(0f);
            Assert.Equal(
                Agent.MovementControlFlag.Forward | defendFlags,
                puppetMirror.MovementFlags);

            ownerMirror.MovementFlags = Agent.MovementControlFlag.None;
            ApplyOwnerAction(context.Component, 2L, agentId, owner);
            context.Component.AgentActionHandler.ApplyRemoteGuardStates();

            puppetMirror.MovementFlags = Agent.MovementControlFlag.Forward;
            controller.OnPreDisplayMissionTick(0f);

            Assert.Equal(
                Agent.MovementControlFlag.Forward,
                puppetMirror.MovementFlags);
        });
    }

    [Theory]
    [InlineData(Agent.MovementControlFlag.DefendUp, Agent.GuardMode.Up)]
    [InlineData(Agent.MovementControlFlag.DefendDown, Agent.GuardMode.Down)]
    [InlineData(Agent.MovementControlFlag.DefendLeft, Agent.GuardMode.Left)]
    [InlineData(Agent.MovementControlFlag.DefendRight, Agent.GuardMode.Right)]
    public void MountedPuppet_FlagsOnlyGuard_ReassertsAcrossMountStateArrival(
        Agent.MovementControlFlag defendDirection,
        Agent.GuardMode expectedGuardMode)
    {
        RunScenario("peer", context =>
        {
            var agentId = Guid.NewGuid();

            Agent puppet = SpawnRegisteredAgent(
                context, "owner", agentId, AgentControllerType.None,
                out MirrorAgent puppetMirror);
            Agent owner = SpawnAgent(
                context, AgentControllerType.Player, out MirrorAgent ownerMirror);
            context.Mock.SpawnMount(owner);

            ownerMirror.MovementFlags =
                Agent.MovementControlFlag.DefendBlock
                | defendDirection;
            ApplyOwnerAction(context.Component, 1L, agentId, owner);
            context.Component.AgentActionHandler.ApplyRemoteGuardStates();

            Assert.Equal(expectedGuardMode, puppetMirror.GuardMode);
            Assert.Equal(1, puppetMirror.SetWeaponGuardCalls);

            context.Mock.SpawnMount(puppet);
            context.Component.AgentActionHandler.ReassertRemoteDefendStates();

            Assert.Equal(expectedGuardMode, puppetMirror.GuardMode);
            Assert.Equal(2, puppetMirror.SetWeaponGuardCalls);

            Agent.GuardMode explicitGuardMode = expectedGuardMode == Agent.GuardMode.Up
                ? Agent.GuardMode.Down
                : Agent.GuardMode.Up;
            ownerMirror.GuardMode = explicitGuardMode;
            ApplyOwnerAction(context.Component, 2L, agentId, owner);
            context.Component.AgentActionHandler.ApplyRemoteGuardStates();

            Assert.Equal(explicitGuardMode, puppetMirror.GuardMode);
            Assert.Equal(3, puppetMirror.SetWeaponGuardCalls);
        });
    }

    [Theory]
    [InlineData(Agent.UsageDirection.DefendDown, Agent.GuardMode.Down, 1)]
    [InlineData(Agent.UsageDirection.DefendUp, Agent.GuardMode.Up, 1)]
    [InlineData(Agent.UsageDirection.DefendRight, Agent.GuardMode.Right, 1)]
    [InlineData(Agent.UsageDirection.DefendLeft, Agent.GuardMode.Left, 1)]
    [InlineData(Agent.UsageDirection.DefendLeft, Agent.GuardMode.Left, 0)]
    public void MountedPuppet_BlockOnlyGuard_UsesExactActionDirection(
        Agent.UsageDirection actionDirection,
        Agent.GuardMode expectedGuardMode,
        int actionChannel)
    {
        RunScenario("peer", context =>
        {
            var agentId = Guid.NewGuid();

            Agent puppet = SpawnRegisteredAgent(
                context, "owner", agentId, AgentControllerType.None,
                out MirrorAgent puppetMirror);
            Agent owner = SpawnAgent(
                context, AgentControllerType.Player, out MirrorAgent ownerMirror);
            context.Mock.SpawnMount(puppet);
            context.Mock.SpawnMount(owner);

            if (actionChannel == 0)
            {
                ownerMirror.Action0Direction = actionDirection;
            }
            else
            {
                ownerMirror.Action1Direction = actionDirection;
            }
            ownerMirror.MovementFlags = Agent.MovementControlFlag.DefendBlock;

            ApplyOwnerAction(context.Component, 1L, agentId, owner);
            context.Component.AgentActionHandler.ApplyRemoteGuardStates();

            Assert.Equal(expectedGuardMode, puppetMirror.GuardMode);
            Assert.Equal(1, puppetMirror.SetWeaponGuardCalls);

            ownerMirror.MovementFlags = Agent.MovementControlFlag.None;
            ApplyOwnerAction(context.Component, 2L, agentId, owner);
            context.Component.AgentActionHandler.ApplyRemoteGuardStates();

            Assert.Equal(Agent.GuardMode.None, puppetMirror.GuardMode);
            Assert.Equal(1, puppetMirror.ResetGuardCalls);
        });
    }

    [Theory]
    [InlineData(Agent.UsageDirection.None)]
    [InlineData(Agent.UsageDirection.AttackUp)]
    [InlineData(Agent.UsageDirection.AttackRight)]
    [InlineData(Agent.UsageDirection.DefendAny)]
    public void DirectionlessAction_DoesNotDeriveGuard(
        Agent.UsageDirection actionDirection)
    {
        RunScenario("owner", context =>
        {
            Agent owner = SpawnAgent(
                context, AgentControllerType.Player, out MirrorAgent ownerMirror);
            ownerMirror.MovementFlags = Agent.MovementControlFlag.DefendBlock;
            ownerMirror.Action1Direction = actionDirection;

            Assert.Equal(
                Agent.GuardMode.None,
                new AgentActionData(owner).GuardMode);
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

    [Theory]
    [InlineData(Agent.ActionCodeType.Guard)]
    [InlineData(Agent.ActionCodeType.DefendShield)]
    public void MissionPreDisplayTick_MountedGuardDecay_ReplaysWithVanillaBlendOnGuardChannel(
        Agent.ActionCodeType guardActionType)
    {
        var agentId = Guid.NewGuid();

        RunScenario("peer", context =>
        {
            var controller = context.Instance.Container.Resolve<CoopBattleController>(
                new TypedParameter(typeof(ICoopMissionComponent), context.Component));
            Agent puppet = SpawnRegisteredAgent(
                context, "owner", agentId, AgentControllerType.None,
                out MirrorAgent puppetMirror);
            Agent owner = SpawnAgent(context, AgentControllerType.Player, out MirrorAgent ownerMirror);
            context.Mock.SpawnMount(puppet);
            context.Mock.SpawnMount(owner);

            ownerMirror.Action0Index = 101;
            ownerMirror.Action0Progress = 0.2f;
            ownerMirror.Action0Flags = (AnimFlags)1;
            ownerMirror.Action1Index = 202;
            ownerMirror.Action1Progress = 0.002f;
            ownerMirror.Action1Flags =
                AnimFlags.amf_priority_defend | AnimFlags.anf_cyclic;
            ownerMirror.Action1CodeType = guardActionType;
            ownerMirror.MovementFlags =
                Agent.MovementControlFlag.DefendBlock
                | Agent.MovementControlFlag.DefendLeft;
            puppetMirror.Action0Index = ownerMirror.Action0Index;
            puppetMirror.Action0Progress = ownerMirror.Action0Progress;
            puppetMirror.Action0Flags = ownerMirror.Action0Flags;
            puppetMirror.Action1Index = ownerMirror.Action1Index;
            puppetMirror.Action1Progress = ownerMirror.Action1Progress;
            puppetMirror.Action1Flags = ownerMirror.Action1Flags;
            puppetMirror.Action1CodeType = guardActionType;

            ApplyOwnerAction(context.Component, 1L, agentId, owner);
            context.Component.AgentActionHandler.ApplyRemoteGuardStates();

            Assert.Equal(101, puppetMirror.Action0Index);
            Assert.Equal(202, puppetMirror.Action1Index);

            // Native advances channel 0 normally while removing the mounted guard from channel 1.
            puppetMirror.Action0Index = 303;
            puppetMirror.Action0Progress = 0.8f;
            puppetMirror.Action0Flags = (AnimFlags)3;
            puppetMirror.Action1Index = -1;
            puppetMirror.Action1Progress = 0f;
            puppetMirror.Action1Flags = 0;
            puppetMirror.SetActionChannelCalls = 0;
            puppetMirror.LastSetActionChannel = -1;
            puppetMirror.LastSetActionBlendInPeriod = float.NaN;

            controller.OnPreDisplayMissionTick(0.1f);

            Assert.Equal(202, puppetMirror.Action1Index);
            Assert.Equal(0.102f, puppetMirror.Action1Progress, precision: 3);
            Assert.Equal(-0.2f, puppetMirror.LastSetActionBlendInPeriod);

            puppetMirror.Action1Index = -1;
            puppetMirror.Action1Progress = 0f;
            puppetMirror.LastSetActionBlendInPeriod = float.NaN;
            controller.OnPreDisplayMissionTick(0.1f);

            Assert.Equal(303, puppetMirror.Action0Index);
            Assert.Equal(0.8f, puppetMirror.Action0Progress);
            Assert.Equal((AnimFlags)3, puppetMirror.Action0Flags);
            Assert.Equal(202, puppetMirror.Action1Index);
            Assert.Equal(0.202f, puppetMirror.Action1Progress, precision: 3);
            Assert.Equal(
                AnimFlags.amf_priority_defend | AnimFlags.anf_cyclic,
                puppetMirror.Action1Flags);
            Assert.Equal(2, puppetMirror.SetActionChannelCalls);
            Assert.Equal(1, puppetMirror.LastSetActionChannel);
            Assert.Equal(-0.2f, puppetMirror.LastSetActionBlendInPeriod);
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
