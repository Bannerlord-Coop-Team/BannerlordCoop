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
using Missions.Agents.Handlers;
using Missions.Agents.Messages;
using Missions.Agents.Packets;
using Missions.Battles;
using Missions.Messages;
using Missions.Tournaments;
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
            Assert.False(data.IsMounted);
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

    [Fact]
    public void RemoteGuardAction_DoesNotReplaceContinuousLocomotionFlags()
    {
        RunScenario("peer", context =>
        {
            var agentId = Guid.NewGuid();
            Agent puppet = SpawnRegisteredAgent(
                context,
                "owner",
                agentId,
                AgentControllerType.None,
                out MirrorAgent puppetMirror);
            Agent owner = SpawnAgent(
                context,
                AgentControllerType.Player,
                out MirrorAgent ownerMirror);
            context.Mock.SpawnMount(puppet);
            context.Mock.SpawnMount(owner);

            puppetMirror.MovementFlags =
                Agent.MovementControlFlag.Forward |
                Agent.MovementControlFlag.TurnLeft;
            ownerMirror.MovementFlags =
                Agent.MovementControlFlag.Backward |
                Agent.MovementControlFlag.TurnRight |
                Agent.MovementControlFlag.DefendBlock |
                Agent.MovementControlFlag.DefendUp;
            ownerMirror.GuardMode = Agent.GuardMode.Up;
            ownerMirror.Action1Index = 202;
            ownerMirror.Action1CodeType = Agent.ActionCodeType.Guard;

            ApplyOwnerAction(context.Component, 1L, agentId, owner);
            context.Component.AgentActionHandler.ApplyRemoteGuardStates();

            Assert.Equal(
                Agent.MovementControlFlag.Forward |
                Agent.MovementControlFlag.TurnLeft |
                Agent.MovementControlFlag.DefendBlock |
                Agent.MovementControlFlag.DefendUp,
                puppetMirror.MovementFlags);
        });
    }

    [Theory]
    [InlineData(
        Agent.MovementControlFlag.DefendRight,
        1,
        Agent.MovementControlFlag.DefendRight,
        Agent.GuardMode.Right)]
    [InlineData(
        Agent.MovementControlFlag.None,
        2,
        Agent.MovementControlFlag.None,
        Agent.GuardMode.None)]
    public void PollActions_UnmountedSwordGuard_UsesNativeDefendUntilRelease(
        Agent.MovementControlFlag nativeDefendAfterRawInputClears,
        int expectedPacketCount,
        Agent.MovementControlFlag expectedDefendFlags,
        Agent.GuardMode expectedGuardMode)
    {
        RunScenario("owner", context =>
        {
            var agentId = Guid.NewGuid();

            Agent agent = SpawnRegisteredAgent(
                context, "owner", agentId, AgentControllerType.Player,
                out MirrorAgent mirror);

            context.Component.AgentActionHandler.PollActions();
            Assert.Empty(context.Network.NetworkSentPackets.GetPackets<AgentActionPacket>());

            mirror.MovementFlags = Agent.MovementControlFlag.DefendRight;
            mirror.Action1CodeType = Agent.ActionCodeType.Guard;
            mirror.Action1Direction = Agent.UsageDirection.DefendRight;
            mirror.DefendMovementFlag = Agent.MovementControlFlag.DefendRight;
            context.Component.AgentActionHandler.PollActions();

            AgentActionPacket heldPacket = Assert.Single(
                context.Network.NetworkSentPackets.GetPackets<AgentActionPacket>());
            AgentActionData heldAction = Assert.Single(heldPacket.Actions);
            Assert.Equal(Agent.MovementControlFlag.DefendRight, heldAction.DefendFlags);
            Assert.Equal(Agent.GuardMode.Right, heldAction.GuardMode);

            mirror.MovementFlags = Agent.MovementControlFlag.None;
            mirror.DefendMovementFlag = nativeDefendAfterRawInputClears;
            context.Component.AgentActionHandler.PollActions();

            AgentActionPacket[] packets = context.Network.NetworkSentPackets
                .GetPackets<AgentActionPacket>()
                .ToArray();
            Assert.Equal(expectedPacketCount, packets.Length);
            AgentActionData lastAction = Assert.Single(packets.Last().Actions);
            Assert.Equal(expectedDefendFlags, lastAction.DefendFlags);
            Assert.Equal(expectedGuardMode, lastAction.GuardMode);
        });
    }

    [Fact]
    public void PollActions_UnmountedGuard_BroadcastsExactDefendFlagChange()
    {
        RunScenario("owner", context =>
        {
            var agentId = Guid.NewGuid();

            Agent agent = SpawnRegisteredAgent(
                context, "owner", agentId, AgentControllerType.Player,
                out MirrorAgent mirror);

            context.Component.AgentActionHandler.PollActions();

            mirror.GuardMode = Agent.GuardMode.Right;
            mirror.MovementFlags =
                Agent.MovementControlFlag.DefendBlock
                | Agent.MovementControlFlag.DefendRight;
            context.Component.AgentActionHandler.PollActions();

            mirror.MovementFlags = Agent.MovementControlFlag.DefendRight;
            context.Component.AgentActionHandler.PollActions();

            AgentActionPacket[] packets = context.Network.NetworkSentPackets
                .GetPackets<AgentActionPacket>()
                .ToArray();
            Assert.Equal(2, packets.Length);
            Assert.Equal(
                Agent.MovementControlFlag.DefendRight,
                Assert.Single(packets[1].Actions).DefendFlags);
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
            Assert.True(heldAction.IsMounted);

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
    public void PollActions_MountedHeldGuard_DoesNotResendForLocomotionOnlyChannelChanges()
    {
        RunScenario("owner", context =>
        {
            var agentId = Guid.NewGuid();

            Agent agent = SpawnRegisteredAgent(
                context, "owner", agentId, AgentControllerType.Player,
                out MirrorAgent mirror);
            context.Mock.SpawnMount(agent);

            context.Component.AgentActionHandler.PollActions();
            Assert.Empty(
                context.Network.NetworkSentPackets.GetPackets<AgentActionPacket>());

            mirror.MovementFlags =
                Agent.MovementControlFlag.DefendBlock
                | Agent.MovementControlFlag.DefendRight;
            mirror.Action1Index = 202;
            mirror.Action1CodeType = Agent.ActionCodeType.Guard;
            mirror.Action1Direction = Agent.UsageDirection.DefendRight;
            context.Component.AgentActionHandler.PollActions();

            Assert.Single(
                context.Network.NetworkSentPackets.GetPackets<AgentActionPacket>());
            context.Network.NetworkSentPackets.Packets.Clear();

            mirror.MovementFlags = Agent.MovementControlFlag.None;
            mirror.DefendMovementFlag = Agent.MovementControlFlag.DefendRight;
            mirror.Action0Index = 101;
            mirror.Action0CodeType = Agent.ActionCodeType.Other;
            context.Component.AgentActionHandler.PollActions();

            mirror.Action0Index = 102;
            mirror.Action0CodeType = Agent.ActionCodeType.Idle;
            context.Component.AgentActionHandler.PollActions();

            Assert.Empty(
                context.Network.NetworkSentPackets.GetPackets<AgentActionPacket>());

            mirror.Action0Index = 303;
            mirror.Action0CodeType = Agent.ActionCodeType.StrikeMedium;
            context.Component.AgentActionHandler.PollActions();

            AgentActionPacket attackPacket = Assert.Single(
                context.Network.NetworkSentPackets.GetPackets<AgentActionPacket>());
            Assert.Equal(2L, Assert.Single(attackPacket.Sequences));
        });
    }

    [Fact]
    public void PollActions_MixedPlayerAndAiBatch_LabelsEachControllerRole()
    {
        RunScenario("owner", context =>
        {
            var playerId = Guid.NewGuid();
            var aiId = Guid.NewGuid();

            SpawnRegisteredAgent(
                context, "owner", playerId, AgentControllerType.Player,
                out MirrorAgent playerMirror);
            SpawnRegisteredAgent(
                context, "owner", aiId, AgentControllerType.AI,
                out MirrorAgent aiMirror);

            playerMirror.MovementFlags =
                Agent.MovementControlFlag.DefendBlock
                | Agent.MovementControlFlag.DefendRight;
            aiMirror.MovementFlags =
                Agent.MovementControlFlag.DefendBlock
                | Agent.MovementControlFlag.DefendLeft;

            context.Component.AgentActionHandler.PollActions();

            AgentActionPacket packet = Assert.Single(
                context.Network.NetworkSentPackets.GetPackets<AgentActionPacket>());
            int playerIndex = Array.IndexOf(packet.AgentIds, playerId);
            int aiIndex = Array.IndexOf(packet.AgentIds, aiId);
            Assert.InRange(playerIndex, 0, packet.Actions.Length - 1);
            Assert.InRange(aiIndex, 0, packet.Actions.Length - 1);
            Assert.True(packet.Actions[playerIndex].IsPlayerControlled);
            Assert.False(packet.Actions[aiIndex].IsPlayerControlled);
        });
    }

    [Fact]
    public void PollActions_PlayerToAiWhileGuarding_BroadcastsControllerRoleTransition()
    {
        RunScenario("owner", context =>
        {
            var agentId = Guid.NewGuid();

            SpawnRegisteredAgent(
                context, "owner", agentId, AgentControllerType.Player,
                out MirrorAgent mirror);
            mirror.MovementFlags =
                Agent.MovementControlFlag.DefendBlock
                | Agent.MovementControlFlag.DefendRight;

            context.Component.AgentActionHandler.PollActions();
            Assert.True(
                Assert.Single(
                    Assert.Single(
                        context.Network.NetworkSentPackets
                            .GetPackets<AgentActionPacket>())
                        .Actions)
                    .IsPlayerControlled);
            context.Network.NetworkSentPackets.Packets.Clear();

            mirror.Controller = AgentControllerType.AI;
            context.Component.AgentActionHandler.PollActions();

            AgentActionPacket packet = Assert.Single(
                context.Network.NetworkSentPackets.GetPackets<AgentActionPacket>());
            Assert.False(Assert.Single(packet.Actions).IsPlayerControlled);
            Assert.Equal(2L, Assert.Single(packet.Sequences));
        });
    }

    [Fact]
    public void PollActions_MountedDefendParryOnOtherChannel_MarksReactionChannel()
    {
        RunScenario("owner", context =>
        {
            var agentId = Guid.NewGuid();

            Agent agent = SpawnRegisteredAgent(
                context, "owner", agentId, AgentControllerType.Player,
                out MirrorAgent mirror);
            context.Mock.SpawnMount(agent);
            mirror.GuardMode = Agent.GuardMode.Right;
            mirror.MovementFlags =
                Agent.MovementControlFlag.DefendBlock
                | Agent.MovementControlFlag.DefendRight;
            mirror.Action0Index = 3102;
            mirror.Action0CodeType = Agent.ActionCodeType.Guard;
            mirror.Action1Index = 3062;
            mirror.Action1CodeType = Agent.ActionCodeType.Guard;

            context.Component.AgentActionHandler.PollActions();
            context.Network.NetworkSentPackets.Packets.Clear();

            mirror.Action0Index = 3104;
            mirror.Action0Progress = 0.2f;
            mirror.Action0Flags = AnimFlags.amf_priority_defend;
            mirror.Action0Stage = Agent.ActionStage.DefendParry;
            context.Component.AgentActionHandler.PollActions();

            AgentActionData data = Assert.Single(
                Assert.Single(
                    context.Network.NetworkSentPackets
                        .GetPackets<AgentActionPacket>())
                    .Actions);
            Assert.Equal(0, data.GuardPresentationChannel);
            Assert.Equal(0, data.GuardActionChannel);
            Assert.True(data.GuardActionIsDefending);
            Assert.True(data.GuardActionIsReaction);
        });
    }

    [Theory]
    [InlineData(CombatCollisionResult.Blocked)]
    [InlineData(CombatCollisionResult.StrikeAgent)]
    public void CollisionAuthority_CapturesNonOwnedDefenderReactionOnce(
        CombatCollisionResult collisionResult)
    {
        RunScenario("attacker", context =>
        {
            var attackerId = Guid.NewGuid();
            var defenderId = Guid.NewGuid();

            Agent attacker = SpawnRegisteredAgent(
                context,
                "attacker",
                attackerId,
                AgentControllerType.Player,
                out _);
            Agent defender = SpawnRegisteredAgent(
                context,
                "defender",
                defenderId,
                AgentControllerType.None,
                out MirrorAgent defenderMirror);
            defenderMirror.GuardMode = Agent.GuardMode.Right;
            defenderMirror.Action1Index = 3102;
            defenderMirror.Action1CodeType =
                Agent.ActionCodeType.Guard;

            context.Component.AgentActionHandler.ObserveBlockedHit(
                defender,
                attacker,
                isBlocked: true,
                isMissile: false,
                collisionResult: collisionResult);

            defenderMirror.Action0Index = 3104;
            defenderMirror.Action0Progress = 0.2f;
            defenderMirror.Action0Flags =
                AnimFlags.amf_priority_defend;
            defenderMirror.Action0CodeType =
                Agent.ActionCodeType.Guard;
            defenderMirror.Action0Stage =
                Agent.ActionStage.DefendParry;

            context.Component.AgentActionHandler
                .ReplayRemoteGuardReactions();
            context.Component.AgentActionHandler
                .ReplayRemoteGuardReactions();

            NetworkAgentGuardReaction message = Assert.Single(
                context.Network.NetworkSentMessages
                    .GetMessages<NetworkAgentGuardReaction>());
            Assert.Equal("attacker", message.SourceControllerId);
            Assert.Equal(attackerId, message.AttackerAgentId);
            Assert.Equal(defenderId, message.AgentId);
            Assert.Equal(0, message.ReactionChannel);
            Assert.Equal(3104, message.ReactionActionIndex);
            Assert.Equal(0.2f, message.Progress, precision: 3);
            Assert.Equal(
                (ulong)AnimFlags.amf_priority_defend,
                message.AnimationFlags);
            Assert.Equal(0, defenderMirror.AdvanceRawVisualActionCalls);
        });
    }

    [Theory]
    [InlineData(false, false, CombatCollisionResult.Blocked)]
    [InlineData(true, true, CombatCollisionResult.Blocked)]
    public void CollisionAuthority_UnblockedOrMissile_DoesNotSendGuardReaction(
        bool isBlocked,
        bool isMissile,
        CombatCollisionResult collisionResult)
    {
        RunScenario("attacker", context =>
        {
            Agent attacker = SpawnRegisteredAgent(
                context,
                "attacker",
                Guid.NewGuid(),
                AgentControllerType.Player,
                out _);
            Agent defender = SpawnRegisteredAgent(
                context,
                "defender",
                Guid.NewGuid(),
                AgentControllerType.None,
                out MirrorAgent defenderMirror);
            defenderMirror.Action1Index = 3104;
            defenderMirror.Action1CodeType =
                Agent.ActionCodeType.Guard;
            defenderMirror.Action1Stage =
                Agent.ActionStage.DefendParry;

            context.Component.AgentActionHandler.ObserveBlockedHit(
                defender,
                attacker,
                isBlocked,
                isMissile,
                collisionResult);
            context.Component.AgentActionHandler
                .ReplayRemoteGuardReactions();

            Assert.Empty(
                context.Network.NetworkSentMessages
                    .GetMessages<NetworkAgentGuardReaction>());
        });
    }

    [Fact]
    public void GuardReactionReceiver_AppliesOnceAndIgnoresStaleDuplicates()
    {
        RunScenario("defender", context =>
        {
            var attackerId = Guid.NewGuid();
            var defenderId = Guid.NewGuid();

            SpawnRegisteredAgent(
                context,
                "attacker",
                attackerId,
                AgentControllerType.None,
                out _);
            SpawnRegisteredAgent(
                context,
                "defender",
                defenderId,
                AgentControllerType.Player,
                out MirrorAgent defenderMirror);
            defenderMirror.GuardMode = Agent.GuardMode.Right;
            defenderMirror.Action1Index = 3102;
            defenderMirror.Action1CodeType =
                Agent.ActionCodeType.Guard;

            NetworkAgentGuardReaction message =
                CreateGuardReactionMessage(
                    attackerId,
                    defenderId,
                    sequence: 2);
            context.Broker.Publish(this, message);
            DrainGameThread();

            Assert.Equal(1, defenderMirror.SetActionChannelCalls);
            Assert.Equal(1, defenderMirror.LastSetActionChannel);
            Assert.Equal(3104, defenderMirror.Action1Index);
            Assert.Equal(
                AnimFlags.amf_priority_defend,
                defenderMirror.LastSetActionFlags);
            Assert.Equal(
                0.2f,
                defenderMirror.LastSetActionStartProgress,
                precision: 3);

            context.Broker.Publish(this, message);
            context.Broker.Publish(
                this,
                CreateGuardReactionMessage(
                    attackerId,
                    defenderId,
                    sequence: 1));
            DrainGameThread();
            context.Component.AgentActionHandler
                .ReplayRemoteGuardReactions();
            context.Component.AgentActionHandler
                .ReplayRemoteGuardReactions();

            Assert.Equal(1, defenderMirror.SetActionChannelCalls);
            Assert.Equal(0, defenderMirror.AdvanceRawVisualActionCalls);
            Assert.Equal(0, defenderMirror.InstallRawVisualActionCalls);
        });
    }

    [Fact]
    public void GuardReactionReceiver_RetriesUntilAttackerRegistrationArrives()
    {
        RunScenario("defender", context =>
        {
            var attackerId = Guid.NewGuid();
            var defenderId = Guid.NewGuid();

            SpawnRegisteredAgent(
                context,
                "defender",
                defenderId,
                AgentControllerType.Player,
                out MirrorAgent defenderMirror);

            context.Broker.Publish(
                this,
                CreateGuardReactionMessage(
                    attackerId,
                    defenderId,
                    sequence: 1));
            DrainGameThread();
            Assert.Equal(0, defenderMirror.SetActionChannelCalls);

            Agent attacker = SpawnAgent(
                context,
                AgentControllerType.None,
                out _);
            Assert.True(context.Registry.TryRegisterAgent(
                "attacker",
                attackerId,
                attacker));
            context.Component.AgentActionHandler
                .ReplayRemoteGuardReactions();

            Assert.Equal(1, defenderMirror.SetActionChannelCalls);
            Assert.Equal(3104, defenderMirror.Action1Index);
        });
    }

    [Fact]
    public void GuardReactionReceiver_RetriesFutureBattleHostAssignment()
    {
        const string MapEventId = "guard-reaction-battle";
        RunBattleScenario("defender", MapEventId, context =>
        {
            var attackerId = Guid.NewGuid();
            var defenderId = Guid.NewGuid();

            SpawnRegisteredAgent(
                context,
                "defender",
                defenderId,
                AgentControllerType.Player,
                out MirrorAgent defenderMirror);

            context.Broker.Publish(
                this,
                CreateGuardReactionMessage(
                    attackerId,
                    defenderId,
                    sequence: 1,
                    sourceControllerId: "battle-host",
                    battleHostEpoch: 2));
            DrainGameThread();
            Assert.Equal(0, defenderMirror.SetActionChannelCalls);

            AssignBattleHost(
                context,
                MapEventId,
                "battle-host",
                Array.Empty<string>(),
                epoch: 2);
            context.Component.AgentActionHandler
                .ReplayRemoteGuardReactions();

            Assert.Equal(1, defenderMirror.SetActionChannelCalls);
            Assert.Equal(3104, defenderMirror.Action1Index);
        });
    }

    [Fact]
    public void GuardReactionReceiver_RestartsNewCollisionUsingSameAction()
    {
        RunScenario("defender", context =>
        {
            var attackerId = Guid.NewGuid();
            var defenderId = Guid.NewGuid();

            SpawnRegisteredAgent(
                context,
                "attacker",
                attackerId,
                AgentControllerType.None,
                out _);
            SpawnRegisteredAgent(
                context,
                "defender",
                defenderId,
                AgentControllerType.Player,
                out MirrorAgent defenderMirror);
            defenderMirror.Action1Index = 3104;
            defenderMirror.Action1Progress = 0.25f;
            defenderMirror.Action1CodeType =
                Agent.ActionCodeType.Guard;
            defenderMirror.Action1Stage =
                Agent.ActionStage.DefendParry;

            context.Broker.Publish(
                this,
                CreateGuardReactionMessage(
                    attackerId,
                    defenderId,
                    sequence: 1,
                    progress: 0.2f));
            DrainGameThread();
            Assert.Equal(0, defenderMirror.SetActionChannelCalls);

            defenderMirror.Action1Progress = 0.8f;
            NetworkAgentGuardReaction secondReaction =
                CreateGuardReactionMessage(
                    attackerId,
                    defenderId,
                    sequence: 2,
                    progress: 0.05f);
            context.Broker.Publish(this, secondReaction);
            DrainGameThread();

            Assert.Equal(1, defenderMirror.SetActionChannelCalls);
            Assert.Equal(3104, defenderMirror.Action1Index);
            Assert.Equal(
                0.05f,
                defenderMirror.Action1Progress,
                precision: 3);

            context.Broker.Publish(this, secondReaction);
            DrainGameThread();
            Assert.Equal(1, defenderMirror.SetActionChannelCalls);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void HeldGuardSnapshot_DoesNotInterruptCurrentRemoteReaction(
        bool defendParry)
    {
        RunScenario("peer", context =>
        {
            var agentId = Guid.NewGuid();
            Agent puppet = SpawnRegisteredAgent(
                context,
                "owner",
                agentId,
                AgentControllerType.None,
                out MirrorAgent puppetMirror);
            Agent owner = SpawnAgent(
                context,
                AgentControllerType.Player,
                out MirrorAgent ownerMirror);
            ownerMirror.GuardMode = Agent.GuardMode.Right;
            ownerMirror.MovementFlags =
                Agent.MovementControlFlag.DefendBlock |
                Agent.MovementControlFlag.DefendRight;
            ownerMirror.Action1Index = 3102;
            ownerMirror.Action1CodeType =
                Agent.ActionCodeType.Guard;

            ApplyOwnerAction(
                context.Component,
                1L,
                agentId,
                owner);
            context.Component.AgentActionHandler
                .ApplyRemoteGuardStates();

            puppetMirror.Action1Index = 3104;
            puppetMirror.Action1CodeType = defendParry
                ? Agent.ActionCodeType.Guard
                : Agent.ActionCodeType.BlockedMelee;
            if (defendParry)
            {
                puppetMirror.Action1Stage =
                    Agent.ActionStage.DefendParry;
            }
            puppetMirror.SetActionChannelCalls = 0;

            var heldGuard = new AgentActionData(owner);
            Assert.True(
                heldGuard.ShouldPreserveCurrentGuardReaction(
                    puppet,
                    1));

            ApplyOwnerAction(
                context.Component,
                2L,
                agentId,
                owner);
            context.Component.AgentActionHandler
                .ApplyRemoteGuardStates();

            Assert.Equal(3104, puppetMirror.Action1Index);
            Assert.Equal(0, puppetMirror.SetActionChannelCalls);
        });
    }

    [Fact]
    public void HeldGuardSnapshot_DoesNotPreserveReactionOnOtherChannel()
    {
        RunScenario("peer", context =>
        {
            var agentId = Guid.NewGuid();
            Agent puppet = SpawnRegisteredAgent(
                context,
                "owner",
                agentId,
                AgentControllerType.None,
                out MirrorAgent puppetMirror);
            Agent owner = SpawnAgent(
                context,
                AgentControllerType.Player,
                out MirrorAgent ownerMirror);
            ownerMirror.GuardMode = Agent.GuardMode.Right;
            ownerMirror.MovementFlags =
                Agent.MovementControlFlag.DefendBlock |
                Agent.MovementControlFlag.DefendRight;
            ownerMirror.Action1Index = 3102;
            ownerMirror.Action1CodeType =
                Agent.ActionCodeType.Guard;
            ownerMirror.Action0Index = 500;
            ownerMirror.Action0CodeType =
                Agent.ActionCodeType.StrikeMedium;

            puppetMirror.Action0Index = 3104;
            puppetMirror.Action0CodeType =
                Agent.ActionCodeType.BlockedMelee;

            var heldGuardWithAttack = new AgentActionData(owner);
            Assert.Equal(1, heldGuardWithAttack.GuardActionChannel);
            Assert.True(heldGuardWithAttack.GuardActionIsDefending);
            Assert.False(
                heldGuardWithAttack.ShouldPreserveCurrentGuardReaction(
                    puppet,
                    0));
        });
    }

    [Fact]
    public void GuardReactionReceiver_DoesNotOverwriteRealAttack()
    {
        RunScenario("defender", context =>
        {
            var attackerId = Guid.NewGuid();
            var defenderId = Guid.NewGuid();

            SpawnRegisteredAgent(
                context,
                "attacker",
                attackerId,
                AgentControllerType.None,
                out _);
            SpawnRegisteredAgent(
                context,
                "defender",
                defenderId,
                AgentControllerType.Player,
                out MirrorAgent defenderMirror);
            defenderMirror.Action0Index = 500;
            defenderMirror.Action0CodeType =
                Agent.ActionCodeType.StrikeMedium;

            context.Broker.Publish(
                this,
                CreateGuardReactionMessage(
                    attackerId,
                    defenderId,
                    sequence: 1));
            DrainGameThread();

            Assert.Equal(0, defenderMirror.SetActionChannelCalls);
            Assert.Equal(500, defenderMirror.Action0Index);
            Assert.Equal(-1, defenderMirror.Action1Index);
        });
    }

    [Fact]
    public void GuardReactionReceiver_RetriesMountMismatchAndRejectedApplyWithoutGuardMetadata()
    {
        RunScenario("defender", context =>
        {
            var attackerId = Guid.NewGuid();
            var defenderId = Guid.NewGuid();

            SpawnRegisteredAgent(
                context,
                "attacker",
                attackerId,
                AgentControllerType.None,
                out _);
            Agent defender = SpawnRegisteredAgent(
                context,
                "defender",
                defenderId,
                AgentControllerType.Player,
                out MirrorAgent defenderMirror);

            context.Broker.Publish(
                this,
                CreateGuardReactionMessage(
                    attackerId,
                    defenderId,
                    sequence: 1,
                    isMounted: true));
            DrainGameThread();
            Assert.Equal(0, defenderMirror.SetActionChannelCalls);

            context.Mock.SpawnMount(defender);
            defenderMirror.SetActionChannelResult = false;
            context.Component.AgentActionHandler
                .ReplayRemoteGuardReactions();
            Assert.Equal(1, defenderMirror.SetActionChannelCalls);
            Assert.Equal(-1, defenderMirror.Action1Index);

            defenderMirror.SetActionChannelResult = true;
            context.Component.AgentActionHandler
                .ReplayRemoteGuardReactions();
            context.Component.AgentActionHandler
                .ReplayRemoteGuardReactions();

            Assert.Equal(2, defenderMirror.SetActionChannelCalls);
            Assert.Equal(3104, defenderMirror.Action1Index);
            Assert.Equal(0, defenderMirror.AdvanceRawVisualActionCalls);
        });
    }

    [Fact]
    public void MountedChannelZeroDefendParry_DoesNotReplayChannelOneGuardRaw()
    {
        RunScenario("peer", context =>
        {
            var agentId = Guid.NewGuid();

            Agent puppet = SpawnRegisteredAgent(
                context,
                "owner",
                agentId,
                AgentControllerType.None,
                out MirrorAgent puppetMirror);
            Agent owner = SpawnAgent(
                context,
                AgentControllerType.Player,
                out MirrorAgent ownerMirror);
            context.Mock.SpawnMount(puppet);
            context.Mock.SpawnMount(owner);
            puppetMirror.HasVisualSkeleton = true;

            ownerMirror.GuardMode = Agent.GuardMode.Up;
            ownerMirror.MovementFlags =
                Agent.MovementControlFlag.DefendBlock
                | Agent.MovementControlFlag.DefendUp;
            ownerMirror.Action1Index = 3062;
            ownerMirror.Action1Progress = 0.6f;
            ownerMirror.Action1Flags = AnimFlags.anf_cyclic;
            ownerMirror.Action1CodeType =
                Agent.ActionCodeType.Guard;
            puppetMirror.Action1Index = 3062;
            puppetMirror.Action1CodeType =
                Agent.ActionCodeType.Guard;

            ApplyOwnerAction(
                context.Component,
                1L,
                agentId,
                owner);
            context.Component.AgentActionHandler
                .ApplyRemoteGuardStates();

            puppetMirror.ActionAnimationIndices[3062] = 3220;
            puppetMirror.AnimationDurations[3220] = 1f;
            puppetMirror.RawVisualAction1Index = 3220;
            puppetMirror.RawVisualAction1Progress = 0.1f;
            puppetMirror.Action0Index = 3104;
            puppetMirror.Action0CodeType =
                Agent.ActionCodeType.Guard;
            puppetMirror.Action0Stage =
                Agent.ActionStage.DefendParry;
            puppetMirror.AdvanceRawVisualActionCalls = 0;
            puppetMirror.InstallRawVisualActionCalls = 0;

            context.Component.AgentActionHandler
                .ReplayRemoteGuardReactions();
            context.Component.AgentActionHandler
                .ReplayRemoteGuardReactions();

            Assert.Equal(
                0.1f,
                puppetMirror.RawVisualAction1Progress,
                precision: 3);
            Assert.Equal(0, puppetMirror.AdvanceRawVisualActionCalls);
            Assert.Equal(0, puppetMirror.InstallRawVisualActionCalls);
        });
    }

    [Fact]
    public void MarkerlessPlayerToAiTransition_DoesNotCarryPriorGuardAction()
    {
        RunScenario("peer", context =>
        {
            var agentId = Guid.NewGuid();

            Agent puppet = SpawnRegisteredAgent(
                context, "owner", agentId, AgentControllerType.None,
                out MirrorAgent puppetMirror);
            puppetMirror.HasVisualSkeleton = true;
            Agent owner = SpawnAgent(
                context, AgentControllerType.Player, out MirrorAgent ownerMirror);
            context.Mock.SpawnMount(puppet);
            context.Mock.SpawnMount(owner);

            ownerMirror.GuardMode = Agent.GuardMode.Right;
            ownerMirror.MovementFlags =
                Agent.MovementControlFlag.DefendBlock
                | Agent.MovementControlFlag.DefendRight;
            ownerMirror.Action1Index = 202;
            ownerMirror.Action1CodeType = Agent.ActionCodeType.Guard;
            puppetMirror.Action1Index = 202;
            puppetMirror.Action1CodeType = Agent.ActionCodeType.Guard;

            ApplyOwnerAction(context.Component, 1L, agentId, owner);
            context.Component.AgentActionHandler.ApplyRemoteGuardStates();

            ownerMirror.Controller = AgentControllerType.AI;
            ownerMirror.Action1Index = -1;
            ownerMirror.Action1CodeType = Agent.ActionCodeType.Idle;
            ApplyOwnerAction(context.Component, 2L, agentId, owner);
            context.Component.AgentActionHandler.ApplyRemoteGuardStates();

            puppetMirror.Action1Index = -1;
            puppetMirror.Action1CodeType = Agent.ActionCodeType.Idle;
            puppetMirror.SkeletonAction1Index = -1;
            puppetMirror.RawVisualAction1Index = 202;
            puppetMirror.SetActionChannelCalls = 0;

            ownerMirror.GuardMode = Agent.GuardMode.None;
            ownerMirror.MovementFlags = Agent.MovementControlFlag.None;
            ApplyOwnerAction(context.Component, 3L, agentId, owner);
            context.Component.AgentActionHandler.ApplyRemoteGuardStates();

            Assert.Equal(-1, puppetMirror.Action1Index);
            Assert.Equal(202, puppetMirror.RawVisualAction1Index);
            Assert.Equal(0, puppetMirror.SetActionChannelCalls);
        });
    }

    [Fact]
    public void MarkerlessHeldGuardAfterMountedReaction_DoesNotReplayPriorReaction()
    {
        RunScenario("peer", context =>
        {
            var controller = context.Instance.Container.Resolve<CoopBattleController>(
                new TypedParameter(typeof(ICoopMissionComponent), context.Component));
            var agentId = Guid.NewGuid();

            Agent puppet = SpawnRegisteredAgent(
                context, "owner", agentId, AgentControllerType.None,
                out MirrorAgent puppetMirror);
            puppetMirror.HasVisualSkeleton = true;
            Agent owner = SpawnAgent(
                context, AgentControllerType.Player, out MirrorAgent ownerMirror);
            context.Mock.SpawnMount(puppet);
            context.Mock.SpawnMount(owner);

            ownerMirror.GuardMode = Agent.GuardMode.Right;
            ownerMirror.MovementFlags =
                Agent.MovementControlFlag.DefendBlock
                | Agent.MovementControlFlag.DefendRight;
            ownerMirror.Action1Index = 202;
            ownerMirror.Action1Progress = 0.2f;
            ownerMirror.Action1CodeType = Agent.ActionCodeType.BlockedMelee;

            ApplyOwnerAction(context.Component, 1L, agentId, owner);
            context.Component.AgentActionHandler.ApplyRemoteGuardStates();

            ownerMirror.Action1Index = -1;
            ownerMirror.Action1Progress = 0f;
            ownerMirror.Action1CodeType = Agent.ActionCodeType.Idle;
            ApplyOwnerAction(context.Component, 2L, agentId, owner);
            context.Component.AgentActionHandler.ApplyRemoteGuardStates();

            Assert.Equal(Agent.GuardMode.Right, puppetMirror.GuardMode);

            puppetMirror.Action1Index = -1;
            puppetMirror.Action1CodeType = Agent.ActionCodeType.Idle;
            puppetMirror.SkeletonAction1Index = -1;
            puppetMirror.RawVisualAction1Index = -1;
            puppetMirror.InstallRawVisualActionCalls = 0;

            controller.OnPreDisplayMissionTick(0.1f);

            Assert.Equal(-1, puppetMirror.Action1Index);
            Assert.Equal(-1, puppetMirror.RawVisualAction1Index);
            Assert.Equal(0, puppetMirror.InstallRawVisualActionCalls);
            Assert.Equal(0, puppetMirror.AdvanceRawVisualActionCalls);
        });
    }

    [Fact]
    public void PollActions_DismountWhileHoldingSameGuard_SendsPostureTransition()
    {
        RunScenario("owner", context =>
        {
            var agentId = Guid.NewGuid();

            Agent agent = SpawnRegisteredAgent(
                context, "owner", agentId, AgentControllerType.Player,
                out MirrorAgent mirror);
            context.Mock.SpawnMount(agent);

            context.Component.AgentActionHandler.PollActions();

            mirror.GuardMode = Agent.GuardMode.Right;
            mirror.MovementFlags =
                Agent.MovementControlFlag.DefendBlock
                | Agent.MovementControlFlag.DefendRight;
            mirror.Action1Index = 202;
            mirror.Action1CodeType = Agent.ActionCodeType.Guard;
            context.Component.AgentActionHandler.PollActions();

            Assert.True(
                Assert.Single(
                    Assert.Single(
                        context.Network.NetworkSentPackets
                            .GetPackets<AgentActionPacket>())
                        .Actions)
                    .IsMounted);
            context.Network.NetworkSentPackets.Packets.Clear();

            mirror.MountAgent = null;
            context.Component.AgentActionHandler.PollActions();

            AgentActionPacket packet = Assert.Single(
                context.Network.NetworkSentPackets.GetPackets<AgentActionPacket>());
            AgentActionData action = Assert.Single(packet.Actions);
            Assert.False(action.IsMounted);
            Assert.Equal(Agent.GuardMode.Right, action.GuardMode);
            Assert.Equal(
                Agent.MovementControlFlag.DefendBlock
                    | Agent.MovementControlFlag.DefendRight,
                action.DefendFlags);
        });
    }

    [Fact]
    public void PollActions_MountedLocomotionNativeDirection_DoesNotStartGuard()
    {
        RunScenario("owner", context =>
        {
            var agentId = Guid.NewGuid();

            Agent agent = SpawnRegisteredAgent(
                context, "owner", agentId, AgentControllerType.Player,
                out MirrorAgent mirror);
            context.Mock.SpawnMount(agent);

            mirror.Action0Index = 174;
            mirror.Action0CodeType = Agent.ActionCodeType.Other;
            mirror.DefendMovementFlag = Agent.MovementControlFlag.DefendLeft;
            context.Component.AgentActionHandler.PollActions();

            mirror.Action0Index = 175;
            mirror.DefendMovementFlag = Agent.MovementControlFlag.DefendRight;
            context.Component.AgentActionHandler.PollActions();

            mirror.Action0Index = 176;
            mirror.DefendMovementFlag = Agent.MovementControlFlag.DefendUp;
            context.Component.AgentActionHandler.PollActions();

            mirror.Action0Index = 177;
            mirror.DefendMovementFlag = Agent.MovementControlFlag.DefendDown;
            context.Component.AgentActionHandler.PollActions();

            Assert.Empty(
                context.Network.NetworkSentPackets.GetPackets<AgentActionPacket>());
        });
    }

    [Fact]
    public void PollActions_MountedHeldGuard_SurvivesPaceReplacingGuardChannel()
    {
        RunScenario("owner", context =>
        {
            var agentId = Guid.NewGuid();

            Agent agent = SpawnRegisteredAgent(
                context, "owner", agentId, AgentControllerType.Player,
                out MirrorAgent mirror);
            context.Mock.SpawnMount(agent);

            context.Component.AgentActionHandler.PollActions();
            Assert.Empty(
                context.Network.NetworkSentPackets.GetPackets<AgentActionPacket>());

            mirror.DefendMovementFlag = Agent.MovementControlFlag.DefendRight;
            mirror.Action1Index = 202;
            mirror.Action1CodeType = Agent.ActionCodeType.Guard;
            mirror.Action1Direction = Agent.UsageDirection.DefendRight;
            context.Component.AgentActionHandler.PollActions();

            Assert.Single(
                context.Network.NetworkSentPackets.GetPackets<AgentActionPacket>());
            context.Network.NetworkSentPackets.Packets.Clear();

            mirror.GuardMode = Agent.GuardMode.None;
            mirror.MovementFlags = Agent.MovementControlFlag.None;
            mirror.Action1Index = 303;
            mirror.Action1CodeType = Agent.ActionCodeType.Other;
            mirror.Action1Direction = Agent.UsageDirection.None;
            mirror.DefendMovementFlag = Agent.MovementControlFlag.DefendBlock;
            context.Component.AgentActionHandler.PollActions();

            mirror.Action1Index = 202;
            mirror.Action1CodeType = Agent.ActionCodeType.Guard;
            mirror.Action1Direction = Agent.UsageDirection.DefendRight;
            context.Component.AgentActionHandler.PollActions();

            Assert.Empty(
                context.Network.NetworkSentPackets.GetPackets<AgentActionPacket>());

            mirror.Action1Index = 204;
            context.Component.AgentActionHandler.PollActions();

            AgentActionPacket parryPacket = Assert.Single(
                context.Network.NetworkSentPackets.GetPackets<AgentActionPacket>());
            Assert.Equal(2L, Assert.Single(parryPacket.Sequences));
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
            ownerMirror.Action1CodeType = Agent.ActionCodeType.Guard;
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
            Assert.Equal(1, action.GuardPresentationChannel);
            Assert.Equal(1, action.GuardActionChannel);
            Assert.True(action.GuardActionIsDefending);
            Assert.True(action.IsMounted);
        });
    }

    [Theory]
    [InlineData(AgentControllerType.Player, true)]
    [InlineData(AgentControllerType.AI, false)]
    public void AgentActionPacket_RoundTripsControllerRole(
        AgentControllerType controllerType,
        bool expectedPlayerControlled)
    {
        RunScenario("owner", context =>
        {
            Agent owner = SpawnAgent(
                context, controllerType, out _);
            var original = new AgentActionPacket(
                "owner",
                new[] { Guid.NewGuid() },
                new[] { new AgentActionData(owner) },
                new[] { 1L });
            var serializer = new ProtoBufSerializer(
                new SerializableTypeMapper());

            byte[] wire = serializer.Serialize(original);
            var result = Assert.IsType<AgentActionPacket>(
                serializer.Deserialize<IPacket>(wire));

            Assert.Equal(
                expectedPlayerControlled,
                Assert.Single(result.Actions).IsPlayerControlled);
        });
    }

    [Theory]
    [InlineData(Agent.ActionCodeType.ParriedMelee, 0)]
    [InlineData(Agent.ActionCodeType.BlockedMelee, 1)]
    [InlineData(Agent.ActionCodeType.Idle, -1)]
    public void AgentActionPacket_RoundTripsMountedGuardPresentationChannel(
        Agent.ActionCodeType actionType,
        int expectedChannel)
    {
        RunScenario("owner", context =>
        {
            Agent owner = SpawnAgent(
                context, AgentControllerType.Player, out MirrorAgent ownerMirror);
            context.Mock.SpawnMount(owner);
            if (expectedChannel == 0)
            {
                ownerMirror.Action0Index = 101;
                ownerMirror.Action0CodeType = actionType;
            }
            else if (expectedChannel == 1)
            {
                ownerMirror.Action1Index = 202;
                ownerMirror.Action1CodeType = actionType;
            }

            var original = new AgentActionPacket(
                "owner",
                new[] { Guid.NewGuid() },
                new[] { new AgentActionData(owner) },
                new[] { 1L });
            var serializer = new ProtoBufSerializer(new SerializableTypeMapper());

            byte[] wire = serializer.Serialize(original);
            var result = Assert.IsType<AgentActionPacket>(
                serializer.Deserialize<IPacket>(wire));

            Assert.Equal(
                expectedChannel,
                Assert.Single(result.Actions).GuardPresentationChannel);
            Assert.Equal(
                expectedChannel,
                Assert.Single(result.Actions).GuardActionChannel);
            Assert.False(
                Assert.Single(result.Actions).GuardActionIsDefending);
            Assert.True(Assert.Single(result.Actions).IsMounted);
        });
    }

    [Theory]
    [InlineData(Agent.ActionCodeType.ParriedMelee)]
    [InlineData(Agent.ActionCodeType.BlockedMelee)]
    public void AgentActionData_OnFootReaction_DoesNotAdvertiseGuardPresentation(
        Agent.ActionCodeType actionType)
    {
        RunScenario("owner", context =>
        {
            Agent owner = SpawnAgent(
                context, AgentControllerType.Player, out MirrorAgent ownerMirror);
            ownerMirror.Action1Index = 202;
            ownerMirror.Action1CodeType = actionType;

            var data = new AgentActionData(owner);
            Assert.Equal(-1, data.GuardPresentationChannel);
            Assert.Equal(-1, data.GuardActionChannel);
            Assert.False(data.GuardActionIsDefending);
        });
    }

    [Fact]
    public void OnFootReactionSnapshot_DoesNotRetainPreviousGuardPresentation()
    {
        RunScenario("peer", context =>
        {
            var agentId = Guid.NewGuid();

            Agent puppet = SpawnRegisteredAgent(
                context, "owner", agentId, AgentControllerType.None,
                out MirrorAgent puppetMirror);
            puppetMirror.HasVisualSkeleton = true;
            Agent owner = SpawnAgent(
                context, AgentControllerType.Player, out MirrorAgent ownerMirror);

            ownerMirror.GuardMode = Agent.GuardMode.Right;
            ownerMirror.MovementFlags =
                Agent.MovementControlFlag.DefendBlock
                | Agent.MovementControlFlag.DefendRight;
            ownerMirror.Action1Index = 202;
            ownerMirror.Action1CodeType = Agent.ActionCodeType.Guard;
            puppetMirror.Action1CodeType = Agent.ActionCodeType.Guard;

            ApplyOwnerAction(context.Component, 1L, agentId, owner);
            context.Component.AgentActionHandler.ApplyRemoteGuardStates();

            ownerMirror.GuardMode = Agent.GuardMode.None;
            ownerMirror.MovementFlags = Agent.MovementControlFlag.None;
            ownerMirror.Action1Index = 303;
            ownerMirror.Action1CodeType = Agent.ActionCodeType.BlockedMelee;
            puppetMirror.Action1CodeType = Agent.ActionCodeType.BlockedMelee;

            ApplyOwnerAction(context.Component, 2L, agentId, owner);
            context.Component.AgentActionHandler.ApplyRemoteGuardStates();

            puppetMirror.Action1Index = -1;
            puppetMirror.Action1CodeType = Agent.ActionCodeType.Idle;
            puppetMirror.SkeletonAction1Index = -1;
            puppetMirror.RawVisualAction1Index = -1;
            context.Component.AgentActionHandler.ReplayRemoteGuardReactions();

            Assert.Equal(Agent.GuardMode.None, puppetMirror.GuardMode);
            Assert.Equal(-1, puppetMirror.RawVisualAction1Index);
            Assert.Equal(0, puppetMirror.AdvanceRawVisualActionCalls);
        });
    }

    [Fact]
    public void DismountSnapshot_DoesNotRetainMountedGuardPresentation()
    {
        RunScenario("peer", context =>
        {
            var controller = context.Instance.Container.Resolve<CoopBattleController>(
                new TypedParameter(typeof(ICoopMissionComponent), context.Component));
            var agentId = Guid.NewGuid();

            Agent puppet = SpawnRegisteredAgent(
                context, "owner", agentId, AgentControllerType.None,
                out MirrorAgent puppetMirror);
            puppetMirror.HasVisualSkeleton = true;
            Agent owner = SpawnAgent(
                context, AgentControllerType.Player, out MirrorAgent ownerMirror);
            context.Mock.SpawnMount(puppet);
            context.Mock.SpawnMount(owner);

            ownerMirror.GuardMode = Agent.GuardMode.Right;
            ownerMirror.MovementFlags =
                Agent.MovementControlFlag.DefendBlock
                | Agent.MovementControlFlag.DefendRight;
            ownerMirror.Action1Index = 202;
            ownerMirror.Action1CodeType = Agent.ActionCodeType.Guard;
            puppetMirror.Action1CodeType = Agent.ActionCodeType.Guard;

            ApplyOwnerAction(context.Component, 1L, agentId, owner);
            context.Component.AgentActionHandler.ApplyRemoteGuardStates();

            Assert.Equal(1, puppetMirror.SetWeaponGuardCalls);

            ownerMirror.MountAgent = null;
            puppetMirror.MountAgent = null;
            ownerMirror.Action1Index = 303;
            ownerMirror.Action1CodeType = Agent.ActionCodeType.Other;
            puppetMirror.Action1Index = 303;
            puppetMirror.Action1CodeType = Agent.ActionCodeType.Other;

            ApplyOwnerAction(context.Component, 2L, agentId, owner);
            context.Component.AgentActionHandler.ApplyRemoteGuardStates();

            puppetMirror.SkeletonAction1Index = 303;
            puppetMirror.RawVisualAction1Index = 303;
            puppetMirror.InstallRawVisualActionCalls = 0;
            controller.OnPreDisplayMissionTick(0.1f);

            Assert.Equal(303, puppetMirror.RawVisualAction1Index);
            Assert.Equal(0, puppetMirror.InstallRawVisualActionCalls);
            Assert.Equal(Agent.GuardMode.Right, puppetMirror.GuardMode);
            Assert.Equal(2, puppetMirror.SetWeaponGuardCalls);
        });
    }

    [Theory]
    [InlineData(Agent.ActionCodeType.ParriedMelee, 0)]
    [InlineData(Agent.ActionCodeType.BlockedMelee, 1)]
    public void TournamentPreDisplayTick_MountedPolearmReaction_DoesNotReplayRawAnimation(
        Agent.ActionCodeType reactionType,
        int actionChannel)
    {
        RunScenario("peer", context =>
        {
            var controller = context.Instance.Container.Resolve<CoopTournamentController>(
                new TypedParameter(typeof(ICoopMissionComponent), context.Component));
            var agentId = Guid.NewGuid();

            Agent puppet = SpawnRegisteredAgent(
                context, "owner", agentId, AgentControllerType.None,
                out MirrorAgent puppetMirror);
            puppetMirror.HasVisualSkeleton = true;
            Agent owner = SpawnAgent(
                context, AgentControllerType.Player, out MirrorAgent ownerMirror);
            context.Mock.SpawnMount(puppet);
            context.Mock.SpawnMount(owner);

            const int reactionAction = 202;
            const int heldGuardAction = 303;
            if (actionChannel == 0)
            {
                ownerMirror.Action0Index = reactionAction;
                ownerMirror.Action0Progress = 0.2f;
                ownerMirror.Action0CodeType = reactionType;
                ownerMirror.Action1Index = heldGuardAction;
                ownerMirror.Action1CodeType = Agent.ActionCodeType.Guard;
            }
            else
            {
                ownerMirror.Action0Index = heldGuardAction;
                ownerMirror.Action0CodeType = Agent.ActionCodeType.Guard;
                ownerMirror.Action1Index = reactionAction;
                ownerMirror.Action1Progress = 0.2f;
                ownerMirror.Action1CodeType = reactionType;
            }

            ApplyOwnerAction(context.Component, 1L, agentId, owner);
            context.Component.AgentActionHandler.ApplyRemoteGuardStates();

            // Mounted native ticking can reclassify and remove the reaction before display.
            if (actionChannel == 0)
            {
                puppetMirror.Action0Index = -1;
                puppetMirror.Action0CodeType = Agent.ActionCodeType.Idle;
                puppetMirror.SkeletonAction0Index = -1;
            }
            else
            {
                puppetMirror.Action1Index = -1;
                puppetMirror.Action1CodeType = Agent.ActionCodeType.Idle;
                puppetMirror.SkeletonAction1Index = -1;
            }
            puppetMirror.SetActionChannelCalls = 0;

            controller.OnPreMissionTick(0.1f);

            Assert.Equal(
                -1,
                actionChannel == 0
                    ? puppetMirror.Action0Index
                    : puppetMirror.Action1Index);
            Assert.Equal(0, puppetMirror.SetActionChannelCalls);

            controller.OnPreDisplayMissionTick(0.1f);

            Assert.Equal(
                -1,
                actionChannel == 0
                    ? puppetMirror.RawVisualAction0Index
                    : puppetMirror.RawVisualAction1Index);
            Assert.Equal(
                0f,
                actionChannel == 0
                    ? puppetMirror.RawVisualAction0Progress
                    : puppetMirror.RawVisualAction1Progress,
                precision: 3);
            Assert.Equal(0, puppetMirror.AdvanceRawVisualActionCalls);
            Assert.Equal(0, puppetMirror.SetActionChannelCalls);

            ownerMirror.Action0Index = -1;
            ownerMirror.Action0Progress = 0f;
            ownerMirror.Action0CodeType = Agent.ActionCodeType.Idle;
            ownerMirror.Action1Index = -1;
            ownerMirror.Action1Progress = 0f;
            ownerMirror.Action1CodeType = Agent.ActionCodeType.Idle;
            ApplyOwnerAction(context.Component, 2L, agentId, owner);
            context.Component.AgentActionHandler.ApplyRemoteGuardStates();

            controller.OnPreDisplayMissionTick(0.1f);

            Assert.Equal(-1, puppetMirror.RawVisualAction0Index);
            Assert.Equal(-1, puppetMirror.RawVisualAction1Index);
            Assert.Equal(0, puppetMirror.AdvanceRawVisualActionCalls);
        });
    }

    [Fact]
    public void MountedReactionPresentation_ArrivingBeforeMountState_DoesNotInstallRawAnimation()
    {
        RunScenario("peer", context =>
        {
            var controller = context.Instance.Container.Resolve<CoopBattleController>(
                new TypedParameter(typeof(ICoopMissionComponent), context.Component));
            var agentId = Guid.NewGuid();

            Agent puppet = SpawnRegisteredAgent(
                context, "owner", agentId, AgentControllerType.None,
                out MirrorAgent puppetMirror);
            puppetMirror.HasVisualSkeleton = true;
            Agent owner = SpawnAgent(
                context, AgentControllerType.Player, out MirrorAgent ownerMirror);
            context.Mock.SpawnMount(owner);

            const int reactionAction = 202;
            ownerMirror.Action1Index = reactionAction;
            ownerMirror.Action1Progress = 0.2f;
            ownerMirror.Action1CodeType = Agent.ActionCodeType.BlockedMelee;
            puppetMirror.Action1CodeType = Agent.ActionCodeType.BlockedMelee;

            ApplyOwnerAction(context.Component, 1L, agentId, owner);
            context.Component.AgentActionHandler.ApplyRemoteGuardStates();

            puppetMirror.Action1Index = -1;
            puppetMirror.Action1CodeType = Agent.ActionCodeType.Idle;
            puppetMirror.SkeletonAction1Index = -1;
            puppetMirror.RawVisualAction1Index = -1;
            context.Mock.SpawnMount(puppet);

            controller.OnPreDisplayMissionTick(0.1f);

            Assert.Equal(-1, puppetMirror.RawVisualAction1Index);
            Assert.Equal(0f, puppetMirror.RawVisualAction1Progress, precision: 3);
            Assert.Equal(0, puppetMirror.InstallRawVisualActionCalls);
            Assert.Equal(0, puppetMirror.InstallAgentVisualActionCalls);
        });
    }

    [Fact]
    public void MissionPreMissionTick_RestoresMountedHeldDefendFlags_ThenClears()
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
            context.Mock.SpawnMount(puppet);
            context.Mock.SpawnMount(owner);

            Agent.MovementControlFlag defendFlags =
                Agent.MovementControlFlag.DefendBlock
                | Agent.MovementControlFlag.DefendRight;
            ownerMirror.MovementFlags = defendFlags;
            ApplyOwnerAction(context.Component, 1L, agentId, owner);
            context.Component.AgentActionHandler.ApplyRemoteGuardStates();

            puppetMirror.MovementFlags = Agent.MovementControlFlag.Forward;
            int guardCommandCount = puppetMirror.SetWeaponGuardCalls;

            controller.OnMissionTick(0f);
            Assert.Equal(
                Agent.MovementControlFlag.Forward,
                puppetMirror.MovementFlags);
            Assert.Equal(guardCommandCount, puppetMirror.SetWeaponGuardCalls);

            controller.OnPreDisplayMissionTick(0f);
            Assert.Equal(
                Agent.MovementControlFlag.Forward,
                puppetMirror.MovementFlags);
            Assert.Equal(guardCommandCount, puppetMirror.SetWeaponGuardCalls);

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
    [InlineData(Agent.GuardMode.Right, Agent.ActionCodeType.Other, 0)]
    [InlineData(Agent.GuardMode.None, Agent.ActionCodeType.Guard, 0)]
    [InlineData(Agent.GuardMode.None, Agent.ActionCodeType.DefendShield, 0)]
    [InlineData(Agent.GuardMode.None, Agent.ActionCodeType.Idle, 1)]
    [InlineData(Agent.GuardMode.None, Agent.ActionCodeType.Other, 1)]
    public void PreMissionTick_PlayerGuard_RecommandsOnlyWhenModeAndDefendingActionAreMissing(
        Agent.GuardMode nativeGuardMode,
        Agent.ActionCodeType nativeActionType,
        int expectedAdditionalGuardCommands)
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
            ownerMirror.Action1Index = 202;
            ownerMirror.Action1Progress = 0.2f;
            ownerMirror.Action1Flags =
                AnimFlags.amf_priority_defend
                | AnimFlags.anf_cyclic
                | AnimFlags.anf_restart;
            ownerMirror.Action1CodeType = Agent.ActionCodeType.Guard;
            puppetMirror.Action1Index = ownerMirror.Action1Index;
            puppetMirror.Action1CodeType = Agent.ActionCodeType.Guard;

            ApplyOwnerAction(context.Component, 1L, agentId, owner);
            context.Component.AgentActionHandler.ApplyRemoteGuardStates();

            Assert.Equal(1, puppetMirror.SetWeaponGuardCalls);

            puppetMirror.MovementFlags = Agent.MovementControlFlag.Forward;
            puppetMirror.GuardMode = nativeGuardMode;
            puppetMirror.Action1Index =
                nativeActionType == Agent.ActionCodeType.Idle ? -1 : 303;
            puppetMirror.Action1Progress = 0.6f;
            puppetMirror.Action1CodeType = nativeActionType;
            puppetMirror.SetActionChannelCalls = 0;

            controller.OnPreMissionTick(0.1f);

            Assert.Equal(
                Agent.MovementControlFlag.Forward | defendFlags,
                puppetMirror.MovementFlags);
            Assert.Equal(
                1 + expectedAdditionalGuardCommands,
                puppetMirror.SetWeaponGuardCalls);
            Assert.Equal(0, puppetMirror.SetActionChannelCalls);
            Assert.Equal(
                nativeActionType == Agent.ActionCodeType.Idle ? -1 : 303,
                puppetMirror.Action1Index);
        });
    }

    [Fact]
    public void RetainedOnFootGuard_RecommandLetsNativeActionOwnPresentation()
    {
        RunScenario("peer", context =>
        {
            var controller = context.Instance.Container.Resolve<CoopBattleController>(
                new TypedParameter(typeof(ICoopMissionComponent), context.Component));
            var agentId = Guid.NewGuid();

            Agent puppet = SpawnRegisteredAgent(
                context, "owner", agentId, AgentControllerType.None,
                out MirrorAgent puppetMirror);
            Agent owner = SpawnAgent(
                context, AgentControllerType.Player, out MirrorAgent ownerMirror);
            ownerMirror.GuardMode = Agent.GuardMode.Right;
            ownerMirror.MovementFlags =
                Agent.MovementControlFlag.DefendBlock
                | Agent.MovementControlFlag.DefendRight;
            ownerMirror.Action1Index = 202;
            ownerMirror.Action1Progress = 0.2f;
            ownerMirror.Action1Flags =
                AnimFlags.amf_priority_defend | AnimFlags.anf_cyclic;
            ownerMirror.Action1CodeType = Agent.ActionCodeType.Guard;
            puppetMirror.Action1Index = ownerMirror.Action1Index;
            puppetMirror.Action1CodeType = ownerMirror.Action1CodeType;

            ApplyOwnerAction(context.Component, 1L, agentId, owner);
            context.Component.AgentActionHandler.ApplyRemoteGuardStates();

            puppetMirror.MovementFlags = Agent.MovementControlFlag.Forward;
            puppetMirror.GuardMode = Agent.GuardMode.None;
            puppetMirror.Action1Index = -1;
            puppetMirror.Action1CodeType = Agent.ActionCodeType.Idle;
            puppetMirror.SkeletonAction1Index = -1;
            puppetMirror.RawVisualAction1Index = -1;
            puppetMirror.SetActionChannelCalls = 0;
            puppetMirror.InstallRawVisualActionCalls = 0;
            controller.OnPreMissionTick(0.1f);

            Assert.Equal(Agent.GuardMode.Right, puppetMirror.GuardMode);
            Assert.Equal(2, puppetMirror.SetWeaponGuardCalls);
            Assert.Equal(-1, puppetMirror.Action1Index);
            Assert.Equal(0, puppetMirror.SetActionChannelCalls);

            puppetMirror.Action1Index = 303;
            puppetMirror.Action1Progress = 0.6f;
            puppetMirror.Action1Flags = AnimFlags.amf_priority_defend;
            puppetMirror.Action1CodeType = Agent.ActionCodeType.DefendShield;
            puppetMirror.SkeletonAction1Index = 303;
            puppetMirror.RawVisualAction1Index = 303;
            puppetMirror.RawVisualAction1Progress = 0.6f;

            controller.OnPreDisplayMissionTick(0.1f);

            Assert.Equal(303, puppetMirror.Action1Index);
            Assert.Equal(0.6f, puppetMirror.Action1Progress);
            Assert.Equal(303, puppetMirror.RawVisualAction1Index);
            Assert.Equal(0.6f, puppetMirror.RawVisualAction1Progress);
            Assert.Equal(0, puppetMirror.InstallRawVisualActionCalls);
            Assert.Equal(0, puppetMirror.SetActionChannelCalls);

            controller.OnPreMissionTick(0.1f);

            Assert.Equal(2, puppetMirror.SetWeaponGuardCalls);
            Assert.Equal(303, puppetMirror.Action1Index);
            Assert.Equal(0, puppetMirror.SetActionChannelCalls);
        });
    }

    [Fact]
    public void PreMissionTick_DifferentNativeDefendingAction_IsPreserved()
    {
        RunScenario("peer", context =>
        {
            var controller = context.Instance.Container.Resolve<CoopBattleController>(
                new TypedParameter(typeof(ICoopMissionComponent), context.Component));
            var agentId = Guid.NewGuid();

            Agent puppet = SpawnRegisteredAgent(
                context, "owner", agentId, AgentControllerType.None,
                out MirrorAgent puppetMirror);
            Agent owner = SpawnAgent(
                context, AgentControllerType.Player, out MirrorAgent ownerMirror);
            ownerMirror.GuardMode = Agent.GuardMode.Right;
            ownerMirror.MovementFlags =
                Agent.MovementControlFlag.DefendBlock
                | Agent.MovementControlFlag.DefendRight;
            ownerMirror.Action1Index = 202;
            ownerMirror.Action1Progress = 0.2f;
            ownerMirror.Action1Flags =
                AnimFlags.amf_priority_defend | AnimFlags.anf_cyclic;
            ownerMirror.Action1CodeType = Agent.ActionCodeType.Guard;
            puppetMirror.Action1Index = ownerMirror.Action1Index;
            puppetMirror.Action1CodeType = Agent.ActionCodeType.Guard;

            ApplyOwnerAction(context.Component, 1L, agentId, owner);
            context.Component.AgentActionHandler.ApplyRemoteGuardStates();

            puppetMirror.MovementFlags = Agent.MovementControlFlag.Forward;
            puppetMirror.Action1Index = 303;
            puppetMirror.Action1Progress = 0.6f;
            puppetMirror.Action1Flags = AnimFlags.amf_priority_defend;
            puppetMirror.Action1CodeType = Agent.ActionCodeType.DefendShield;
            puppetMirror.SetActionChannelCalls = 0;

            controller.OnMissionTick(0.1f);
            controller.OnPreDisplayMissionTick(0.1f);

            Assert.Equal(303, puppetMirror.Action1Index);
            Assert.Equal(0.6f, puppetMirror.Action1Progress);
            Assert.Equal(AnimFlags.amf_priority_defend, puppetMirror.Action1Flags);
            Assert.Equal(Agent.MovementControlFlag.Forward, puppetMirror.MovementFlags);
            Assert.Equal(0, puppetMirror.SetActionChannelCalls);

            controller.OnPreMissionTick(0.1f);

            Assert.Equal(303, puppetMirror.Action1Index);
            Assert.Equal(0.6f, puppetMirror.Action1Progress);
            Assert.Equal(AnimFlags.amf_priority_defend, puppetMirror.Action1Flags);
            Assert.Equal(
                Agent.MovementControlFlag.Forward
                    | Agent.MovementControlFlag.DefendBlock
                    | Agent.MovementControlFlag.DefendRight,
                puppetMirror.MovementFlags);
            Assert.Equal(0, puppetMirror.SetActionChannelCalls);
        });
    }

    [Fact]
    public void PreMissionTick_PostureChange_RecommandsWithoutInjectingGuardAction()
    {
        RunScenario("peer", context =>
        {
            var controller = context.Instance.Container.Resolve<CoopBattleController>(
                new TypedParameter(typeof(ICoopMissionComponent), context.Component));
            var agentId = Guid.NewGuid();

            Agent puppet = SpawnRegisteredAgent(
                context, "owner", agentId, AgentControllerType.None,
                out MirrorAgent puppetMirror);
            Agent owner = SpawnAgent(
                context, AgentControllerType.Player, out MirrorAgent ownerMirror);
            context.Mock.SpawnMount(puppet);
            context.Mock.SpawnMount(owner);
            ownerMirror.GuardMode = Agent.GuardMode.Right;
            ownerMirror.MovementFlags =
                Agent.MovementControlFlag.DefendBlock
                | Agent.MovementControlFlag.DefendRight;
            ownerMirror.Action1Index = 202;
            ownerMirror.Action1Progress = 0.2f;
            ownerMirror.Action1Flags =
                AnimFlags.amf_priority_defend | AnimFlags.anf_cyclic;
            ownerMirror.Action1CodeType = Agent.ActionCodeType.Guard;
            puppetMirror.Action1Index = ownerMirror.Action1Index;
            puppetMirror.Action1CodeType = Agent.ActionCodeType.Guard;

            ApplyOwnerAction(context.Component, 1L, agentId, owner);
            context.Component.AgentActionHandler.ApplyRemoteGuardStates();

            puppetMirror.MountAgent = null;
            puppetMirror.Action1Index = -1;
            puppetMirror.Action1Progress = 0f;
            puppetMirror.Action1CodeType = Agent.ActionCodeType.Idle;
            puppetMirror.SetActionChannelCalls = 0;

            controller.OnMissionTick(0.1f);
            controller.OnPreDisplayMissionTick(0.1f);
            controller.OnPreMissionTick(0.1f);

            Assert.Equal(-1, puppetMirror.Action1Index);
            Assert.Equal(0f, puppetMirror.Action1Progress);
            Assert.Equal(0, puppetMirror.SetActionChannelCalls);

            context.Mock.SpawnMount(puppet);
            controller.OnPreMissionTick(0.1f);

            Assert.Equal(-1, puppetMirror.Action1Index);
            Assert.Equal(0f, puppetMirror.Action1Progress);
            Assert.Equal(0, puppetMirror.SetActionChannelCalls);
        });
    }

    [Fact]
    public void MissionTicks_OnFootGuardDecay_DoesNotInjectOrReplayHeldAction()
    {
        RunScenario("peer", context =>
        {
            var controller = context.Instance.Container.Resolve<CoopBattleController>(
                new TypedParameter(typeof(ICoopMissionComponent), context.Component));
            var agentId = Guid.NewGuid();

            Agent puppet = SpawnRegisteredAgent(
                context, "owner", agentId, AgentControllerType.None,
                out MirrorAgent puppetMirror);
            puppetMirror.HasVisualSkeleton = true;
            Agent owner = SpawnAgent(
                context, AgentControllerType.Player, out MirrorAgent ownerMirror);
            ownerMirror.GuardMode = Agent.GuardMode.Right;
            ownerMirror.MovementFlags =
                Agent.MovementControlFlag.DefendBlock
                | Agent.MovementControlFlag.DefendRight;
            ownerMirror.Action1Index = 202;
            ownerMirror.Action1Progress = 0.2f;
            ownerMirror.Action1Flags =
                AnimFlags.amf_priority_defend | AnimFlags.anf_cyclic;
            ownerMirror.Action1CodeType = Agent.ActionCodeType.Guard;
            puppetMirror.Action1Index = ownerMirror.Action1Index;
            puppetMirror.Action1Progress = ownerMirror.Action1Progress;
            puppetMirror.Action1Flags = ownerMirror.Action1Flags;
            puppetMirror.Action1CodeType = Agent.ActionCodeType.Guard;

            ApplyOwnerAction(context.Component, 1L, agentId, owner);
            context.Component.AgentActionHandler.ApplyRemoteGuardStates();

            puppetMirror.MovementFlags = Agent.MovementControlFlag.Forward;
            puppetMirror.GuardMode = Agent.GuardMode.None;
            puppetMirror.Action1Index = -1;
            puppetMirror.Action1Progress = 0f;
            puppetMirror.Action1Flags = 0;
            puppetMirror.Action1CodeType = Agent.ActionCodeType.Idle;
            puppetMirror.SkeletonAction1Index = -1;
            puppetMirror.RawVisualAction1Index = -1;
            puppetMirror.SetActionChannelCalls = 0;
            puppetMirror.InstallRawVisualActionCalls = 0;

            controller.OnMissionTick(0.1f);

            Assert.Equal(-1, puppetMirror.Action1Index);
            Assert.Equal(-1, puppetMirror.RawVisualAction1Index);
            Assert.Equal(1, puppetMirror.SetWeaponGuardCalls);
            Assert.Equal(
                Agent.MovementControlFlag.Forward,
                puppetMirror.MovementFlags);

            controller.OnPreDisplayMissionTick(0.1f);

            Assert.Equal(-1, puppetMirror.Action1Index);
            Assert.Equal(0f, puppetMirror.Action1Progress);
            Assert.Equal(0, puppetMirror.SetActionChannelCalls);
            Assert.Equal(-1, puppetMirror.SkeletonAction1Index);
            Assert.Equal(-1, puppetMirror.RawVisualAction1Index);
            Assert.Equal(0, puppetMirror.InstallRawVisualActionCalls);
            Assert.Equal(
                Agent.MovementControlFlag.Forward,
                puppetMirror.MovementFlags);
            Assert.Equal(1, puppetMirror.SetWeaponGuardCalls);

            puppetMirror.RawVisualAction1Index = 202;
            puppetMirror.RawVisualAction1Progress = 0.01f;
            controller.OnPreDisplayMissionTick(0.1f);

            Assert.Equal(202, puppetMirror.RawVisualAction1Index);
            Assert.Equal(0.01f, puppetMirror.RawVisualAction1Progress);
            Assert.Equal(0, puppetMirror.AdvanceExistingRawVisualActionCalls);
            Assert.Equal(0, puppetMirror.InstallRawVisualActionCalls);
            Assert.Equal(0, puppetMirror.SetActionChannelCalls);

            puppetMirror.RawVisualAction1Index = -1;
            puppetMirror.RawVisualAction1Progress = 0f;
            controller.OnPreMissionTick(0.1f);

            Assert.Equal(-1, puppetMirror.Action1Index);
            Assert.Equal(-1, puppetMirror.RawVisualAction1Index);
            Assert.Equal(0, puppetMirror.SetActionChannelCalls);
            Assert.Equal(2, puppetMirror.SetWeaponGuardCalls);
            Assert.Equal(
                Agent.MovementControlFlag.Forward
                    | Agent.MovementControlFlag.DefendBlock
                    | Agent.MovementControlFlag.DefendRight,
                puppetMirror.MovementFlags);

            puppetMirror.Action1Index = 303;
            puppetMirror.Action1Progress = 0.6f;
            puppetMirror.Action1Flags = AnimFlags.amf_priority_defend;
            puppetMirror.Action1CodeType = Agent.ActionCodeType.DefendShield;
            puppetMirror.SkeletonAction1Index = 303;
            puppetMirror.RawVisualAction1Index = 303;
            puppetMirror.RawVisualAction1Progress = 0.6f;
            controller.OnPreDisplayMissionTick(0.1f);

            Assert.Equal(303, puppetMirror.Action1Index);
            Assert.Equal(303, puppetMirror.RawVisualAction1Index);
            Assert.Equal(0.6f, puppetMirror.RawVisualAction1Progress);
            Assert.Equal(0, puppetMirror.InstallRawVisualActionCalls);
            Assert.Equal(0, puppetMirror.SetActionChannelCalls);

            ownerMirror.MovementFlags = Agent.MovementControlFlag.None;
            ownerMirror.GuardMode = Agent.GuardMode.None;
            ownerMirror.Action1Index = -1;
            ownerMirror.Action1Progress = 0f;
            ownerMirror.Action1Flags = 0;
            ownerMirror.Action1CodeType = Agent.ActionCodeType.Idle;

            ApplyOwnerAction(context.Component, 2L, agentId, owner);
            context.Component.AgentActionHandler.ApplyRemoteGuardStates();

            Assert.Equal(303, puppetMirror.Action1Index);
            Assert.Equal(303, puppetMirror.RawVisualAction1Index);
            Assert.Equal(0, puppetMirror.SetActionChannelCalls);
            Assert.Equal(Agent.GuardMode.None, puppetMirror.GuardMode);
            Assert.Equal(
                Agent.MovementControlFlag.None,
                AgentActionData.GetDefendMovementFlags(
                    puppetMirror.MovementFlags));

            controller.OnPreDisplayMissionTick(0.1f);

            Assert.Equal(303, puppetMirror.RawVisualAction1Index);
            Assert.Equal(0, puppetMirror.InstallRawVisualActionCalls);
        });
    }

    [Theory]
    [InlineData(Agent.MovementControlFlag.DefendUp, Agent.GuardMode.Up)]
    [InlineData(Agent.MovementControlFlag.DefendDown, Agent.GuardMode.Down)]
    [InlineData(Agent.MovementControlFlag.DefendLeft, Agent.GuardMode.Left)]
    [InlineData(Agent.MovementControlFlag.DefendRight, Agent.GuardMode.Right)]
    public void MountedPuppet_FlagsOnlyGuard_RecommandsOnceAcrossMountStateArrival(
        Agent.MovementControlFlag defendDirection,
        Agent.GuardMode expectedGuardMode)
    {
        RunScenario("peer", context =>
        {
            var controller = context.Instance.Container.Resolve<CoopBattleController>(
                new TypedParameter(typeof(ICoopMissionComponent), context.Component));
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

            puppetMirror.GuardMode = Agent.GuardMode.None;
            puppetMirror.MovementFlags = Agent.MovementControlFlag.Forward;
            context.Mock.SpawnMount(puppet);

            controller.OnMissionTick(0.1f);
            controller.OnPreDisplayMissionTick(0.1f);

            Assert.Equal(Agent.GuardMode.None, puppetMirror.GuardMode);
            Assert.Equal(
                Agent.MovementControlFlag.Forward,
                puppetMirror.MovementFlags);
            Assert.Equal(1, puppetMirror.SetWeaponGuardCalls);

            controller.OnPreMissionTick(0.1f);

            Assert.Equal(expectedGuardMode, puppetMirror.GuardMode);
            Assert.Equal(
                Agent.MovementControlFlag.Forward
                    | Agent.MovementControlFlag.DefendBlock
                    | defendDirection,
                puppetMirror.MovementFlags);
            Assert.Equal(2, puppetMirror.SetWeaponGuardCalls);

            puppetMirror.GuardMode = Agent.GuardMode.None;
            puppetMirror.Action1Index = 303;
            puppetMirror.Action1Progress = 0.6f;
            puppetMirror.Action1CodeType = Agent.ActionCodeType.DefendShield;
            puppetMirror.SkeletonAction1Index = 303;
            puppetMirror.RawVisualAction1Index = 303;
            puppetMirror.RawVisualAction1Progress = 0.6f;
            puppetMirror.InstallRawVisualActionCalls = 0;
            controller.OnPreDisplayMissionTick(0.1f);

            Assert.Equal(303, puppetMirror.Action1Index);
            Assert.Equal(303, puppetMirror.RawVisualAction1Index);
            Assert.Equal(0.6f, puppetMirror.RawVisualAction1Progress);
            Assert.Equal(0, puppetMirror.InstallRawVisualActionCalls);

            controller.OnPreMissionTick(0.1f);

            Assert.Equal(2, puppetMirror.SetWeaponGuardCalls);
            Assert.Equal(Agent.GuardMode.None, puppetMirror.GuardMode);
            Assert.Equal(303, puppetMirror.Action1Index);

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

    [Fact]
    public void ReactionSnapshot_DoesNotRestartGuardUntilDefendingActionReturns()
    {
        RunScenario("peer", context =>
        {
            var controller = context.Instance.Container.Resolve<CoopBattleController>(
                new TypedParameter(typeof(ICoopMissionComponent), context.Component));
            var agentId = Guid.NewGuid();

            Agent puppet = SpawnRegisteredAgent(
                context, "owner", agentId, AgentControllerType.None,
                out MirrorAgent puppetMirror);
            Agent owner = SpawnAgent(
                context, AgentControllerType.Player, out MirrorAgent ownerMirror);
            ownerMirror.GuardMode = Agent.GuardMode.Right;
            ownerMirror.Action1Index = 202;
            ownerMirror.Action1CodeType = Agent.ActionCodeType.Guard;
            puppetMirror.Action1Index = ownerMirror.Action1Index;
            puppetMirror.Action1CodeType = Agent.ActionCodeType.Guard;

            ApplyOwnerAction(context.Component, 1L, agentId, owner);
            context.Component.AgentActionHandler.ApplyRemoteGuardStates();

            Assert.Equal(Agent.GuardMode.Right, puppetMirror.GuardMode);
            Assert.Equal(1, puppetMirror.SetWeaponGuardCalls);

            ownerMirror.GuardMode = Agent.GuardMode.Up;
            ownerMirror.Action1Index = 303;
            ownerMirror.Action1CodeType = Agent.ActionCodeType.StrikeMedium;
            puppetMirror.Action1CodeType = Agent.ActionCodeType.StrikeMedium;
            puppetMirror.GuardMode = Agent.GuardMode.None;

            ApplyOwnerAction(context.Component, 2L, agentId, owner);
            context.Component.AgentActionHandler.ApplyRemoteGuardStates();
            int actionCommandCount = puppetMirror.SetActionChannelCalls;

            controller.OnMissionTick(0.1f);
            controller.OnPreDisplayMissionTick(0.1f);

            Assert.Equal(Agent.GuardMode.None, puppetMirror.GuardMode);
            Assert.Equal(1, puppetMirror.SetWeaponGuardCalls);
            Assert.Equal(actionCommandCount, puppetMirror.SetActionChannelCalls);

            puppetMirror.Action1Index = -1;
            puppetMirror.Action1CodeType = Agent.ActionCodeType.Idle;

            controller.OnMissionTick(0.1f);
            controller.OnPreDisplayMissionTick(0.1f);

            Assert.Equal(Agent.GuardMode.None, puppetMirror.GuardMode);
            Assert.Equal(1, puppetMirror.SetWeaponGuardCalls);
            Assert.Equal(-1, puppetMirror.Action1Index);
            Assert.Equal(actionCommandCount, puppetMirror.SetActionChannelCalls);

            controller.OnPreMissionTick(0.1f);

            Assert.Equal(Agent.GuardMode.Up, puppetMirror.GuardMode);
            Assert.Equal(2, puppetMirror.SetWeaponGuardCalls);
            Assert.Equal(-1, puppetMirror.Action1Index);
            Assert.Equal(actionCommandCount, puppetMirror.SetActionChannelCalls);
        });
    }

    [Fact]
    public void MountedReactionDirectionChange_DoesNotReplayPreviousGuardClip()
    {
        RunScenario("peer", context =>
        {
            var controller = context.Instance.Container.Resolve<CoopBattleController>(
                new TypedParameter(typeof(ICoopMissionComponent), context.Component));
            var agentId = Guid.NewGuid();

            Agent puppet = SpawnRegisteredAgent(
                context, "owner", agentId, AgentControllerType.None,
                out MirrorAgent puppetMirror);
            Agent owner = SpawnAgent(
                context, AgentControllerType.Player, out MirrorAgent ownerMirror);
            context.Mock.SpawnMount(puppet);
            context.Mock.SpawnMount(owner);
            ownerMirror.GuardMode = Agent.GuardMode.Right;
            ownerMirror.Action1Index = 202;
            ownerMirror.Action1CodeType = Agent.ActionCodeType.Guard;
            puppetMirror.Action1CodeType = Agent.ActionCodeType.Guard;

            ApplyOwnerAction(context.Component, 1L, agentId, owner);
            context.Component.AgentActionHandler.ApplyRemoteGuardStates();

            Assert.Equal(Agent.GuardMode.Right, puppetMirror.GuardMode);
            Assert.Equal(1, puppetMirror.SetWeaponGuardCalls);

            ownerMirror.GuardMode = Agent.GuardMode.Up;
            ownerMirror.Action1Index = 303;
            ownerMirror.Action1CodeType = Agent.ActionCodeType.StrikeMedium;
            puppetMirror.Action1Index = 303;
            puppetMirror.Action1CodeType = Agent.ActionCodeType.StrikeMedium;
            puppetMirror.GuardMode = Agent.GuardMode.None;

            ApplyOwnerAction(context.Component, 2L, agentId, owner);
            context.Component.AgentActionHandler.ApplyRemoteGuardStates();

            Assert.Equal(Agent.GuardMode.None, puppetMirror.GuardMode);
            Assert.Equal(1, puppetMirror.SetWeaponGuardCalls);

            puppetMirror.Action1Index = -1;
            puppetMirror.Action1CodeType = Agent.ActionCodeType.Idle;
            puppetMirror.SetActionChannelCalls = 0;

            controller.OnMissionTick(0.1f);
            controller.OnPreDisplayMissionTick(0.1f);

            Assert.Equal(Agent.GuardMode.None, puppetMirror.GuardMode);
            Assert.Equal(1, puppetMirror.SetWeaponGuardCalls);
            Assert.Equal(-1, puppetMirror.Action1Index);
            Assert.Equal(0, puppetMirror.SetActionChannelCalls);

            controller.OnPreMissionTick(0.1f);

            Assert.Equal(Agent.GuardMode.Up, puppetMirror.GuardMode);
            Assert.Equal(2, puppetMirror.SetWeaponGuardCalls);
            Assert.Equal(-1, puppetMirror.Action1Index);
            Assert.Equal(0, puppetMirror.SetActionChannelCalls);
        });
    }

    [Fact]
    public void MissionPreMissionTick_MountedSameDirectionGuard_RecommandsAfterReactionEnds()
    {
        RunScenario("peer", context =>
        {
            var controller = context.Instance.Container.Resolve<CoopBattleController>(
                new TypedParameter(typeof(ICoopMissionComponent), context.Component));
            var agentId = Guid.NewGuid();

            Agent puppet = SpawnRegisteredAgent(
                context, "owner", agentId, AgentControllerType.None,
                out MirrorAgent puppetMirror);
            Agent owner = SpawnAgent(
                context, AgentControllerType.Player, out MirrorAgent ownerMirror);
            context.Mock.SpawnMount(puppet);
            context.Mock.SpawnMount(owner);
            ownerMirror.GuardMode = Agent.GuardMode.Right;
            ownerMirror.MovementFlags =
                Agent.MovementControlFlag.DefendBlock
                | Agent.MovementControlFlag.DefendRight;
            ownerMirror.Action1Index = 202;
            ownerMirror.Action1CodeType = Agent.ActionCodeType.Guard;
            puppetMirror.Action1Index = ownerMirror.Action1Index;
            puppetMirror.Action1CodeType = Agent.ActionCodeType.Guard;

            ApplyOwnerAction(context.Component, 1L, agentId, owner);
            context.Component.AgentActionHandler.ApplyRemoteGuardStates();

            Assert.Equal(Agent.GuardMode.Right, puppetMirror.GuardMode);
            Assert.Equal(1, puppetMirror.SetWeaponGuardCalls);

            puppetMirror.Action1Index = 303;
            puppetMirror.Action1CodeType = Agent.ActionCodeType.BlockedMelee;
            puppetMirror.GuardMode = Agent.GuardMode.None;
            puppetMirror.SetActionChannelCalls = 0;
            controller.OnMissionTick(0.1f);
            controller.OnPreDisplayMissionTick(0.1f);

            Assert.Equal(Agent.GuardMode.None, puppetMirror.GuardMode);
            Assert.Equal(1, puppetMirror.SetWeaponGuardCalls);
            Assert.Equal(0, puppetMirror.SetActionChannelCalls);

            puppetMirror.Action1Index = -1;
            puppetMirror.Action1CodeType = Agent.ActionCodeType.Idle;
            controller.OnMissionTick(0.1f);
            controller.OnPreDisplayMissionTick(0.1f);

            Assert.Equal(Agent.GuardMode.None, puppetMirror.GuardMode);
            Assert.Equal(1, puppetMirror.SetWeaponGuardCalls);
            Assert.Equal(-1, puppetMirror.Action1Index);
            Assert.Equal(0, puppetMirror.SetActionChannelCalls);

            controller.OnPreMissionTick(0.1f);

            Assert.Equal(Agent.GuardMode.Right, puppetMirror.GuardMode);
            Assert.Equal(2, puppetMirror.SetWeaponGuardCalls);
            Assert.Equal(-1, puppetMirror.Action1Index);
            Assert.Equal(0, puppetMirror.SetActionChannelCalls);

            puppetMirror.InstallRawVisualActionCalls = 0;
            puppetMirror.InstallAgentVisualActionCalls = 0;
            for (int nativeTick = 0; nativeTick < 3; nativeTick++)
            {
                int nativeActionIndex = 404 + nativeTick;
                puppetMirror.GuardMode = Agent.GuardMode.None;
                puppetMirror.MovementFlags = Agent.MovementControlFlag.Forward;
                puppetMirror.Action1Index = nativeActionIndex;
                puppetMirror.Action1CodeType = Agent.ActionCodeType.Other;
                puppetMirror.SkeletonAction1Index = nativeActionIndex;
                puppetMirror.RawVisualAction1Index = nativeActionIndex;

                controller.OnPreMissionTick(0.1f);

                Assert.Equal(
                    Agent.MovementControlFlag.Forward
                        | Agent.MovementControlFlag.DefendBlock
                        | Agent.MovementControlFlag.DefendRight,
                    puppetMirror.MovementFlags);
                Assert.Equal(Agent.GuardMode.None, puppetMirror.GuardMode);
                Assert.Equal(2, puppetMirror.SetWeaponGuardCalls);
                Assert.Equal(0, puppetMirror.SetActionChannelCalls);
                Assert.Equal(0, puppetMirror.InstallRawVisualActionCalls);
                Assert.Equal(0, puppetMirror.InstallAgentVisualActionCalls);
            }
        });
    }

    [Fact]
    public void ReceivedMountedReaction_ClearedBeforeDisplay_ReacquiresOnceAfterTransition()
    {
        RunScenario("peer", context =>
        {
            var controller = context.Instance.Container.Resolve<CoopBattleController>(
                new TypedParameter(typeof(ICoopMissionComponent), context.Component));
            var agentId = Guid.NewGuid();

            Agent puppet = SpawnRegisteredAgent(
                context, "owner", agentId, AgentControllerType.None,
                out MirrorAgent puppetMirror);
            Agent owner = SpawnAgent(
                context, AgentControllerType.Player, out MirrorAgent ownerMirror);
            context.Mock.SpawnMount(puppet);
            context.Mock.SpawnMount(owner);

            Agent.MovementControlFlag defendFlags =
                Agent.MovementControlFlag.DefendBlock
                | Agent.MovementControlFlag.DefendRight;
            ownerMirror.GuardMode = Agent.GuardMode.Right;
            ownerMirror.MovementFlags = defendFlags;
            ownerMirror.Action1Index = 202;
            ownerMirror.Action1CodeType = Agent.ActionCodeType.Guard;
            puppetMirror.Action1Index = ownerMirror.Action1Index;
            puppetMirror.Action1CodeType = ownerMirror.Action1CodeType;

            ApplyOwnerAction(context.Component, 1L, agentId, owner);
            context.Component.AgentActionHandler.ApplyRemoteGuardStates();

            Assert.Equal(1, puppetMirror.SetWeaponGuardCalls);

            ownerMirror.Action1Index = 303;
            ownerMirror.Action1Progress = 0.2f;
            ownerMirror.Action1CodeType = Agent.ActionCodeType.BlockedMelee;
            ApplyOwnerAction(context.Component, 2L, agentId, owner);
            context.Component.AgentActionHandler.ApplyRemoteGuardStates();

            Assert.Equal(1, puppetMirror.SetWeaponGuardCalls);

            puppetMirror.GuardMode = Agent.GuardMode.None;
            puppetMirror.Action1Index = -1;
            puppetMirror.Action1CodeType = Agent.ActionCodeType.Idle;
            puppetMirror.SetActionChannelCalls = 0;

            controller.OnPreMissionTick(0.1f);

            Assert.Equal(Agent.GuardMode.None, puppetMirror.GuardMode);
            Assert.Equal(1, puppetMirror.SetWeaponGuardCalls);
            Assert.Equal(-1, puppetMirror.Action1Index);
            Assert.Equal(0, puppetMirror.SetActionChannelCalls);

            ownerMirror.Action1Index = 202;
            ownerMirror.Action1Progress = 0.3f;
            ownerMirror.Action1CodeType = Agent.ActionCodeType.Guard;
            ApplyOwnerAction(context.Component, 3L, agentId, owner);
            context.Component.AgentActionHandler.ApplyRemoteGuardStates();

            Assert.Equal(Agent.GuardMode.Right, puppetMirror.GuardMode);
            Assert.Equal(2, puppetMirror.SetWeaponGuardCalls);

            puppetMirror.SetActionChannelCalls = 0;
            puppetMirror.InstallRawVisualActionCalls = 0;
            puppetMirror.InstallAgentVisualActionCalls = 0;
            for (int nativeTick = 0; nativeTick < 3; nativeTick++)
            {
                int nativeActionIndex = 505 + nativeTick;
                puppetMirror.GuardMode = Agent.GuardMode.None;
                puppetMirror.MovementFlags = Agent.MovementControlFlag.Forward;
                puppetMirror.Action1Index = nativeActionIndex;
                puppetMirror.Action1CodeType = Agent.ActionCodeType.Other;
                puppetMirror.SkeletonAction1Index = nativeActionIndex;
                puppetMirror.RawVisualAction1Index = nativeActionIndex;

                controller.OnPreMissionTick(0.1f);

                Assert.Equal(
                    Agent.MovementControlFlag.Forward | defendFlags,
                    puppetMirror.MovementFlags);
                Assert.Equal(Agent.GuardMode.None, puppetMirror.GuardMode);
                Assert.Equal(2, puppetMirror.SetWeaponGuardCalls);
                Assert.Equal(0, puppetMirror.SetActionChannelCalls);
                Assert.Equal(0, puppetMirror.InstallRawVisualActionCalls);
                Assert.Equal(0, puppetMirror.InstallAgentVisualActionCalls);
            }
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
    public void CatchUpJoiner_MountedGaitSnapshot_PreservesHeldGuardDirection()
    {
        RunScenario("owner", context =>
        {
            var agentId = Guid.NewGuid();

            Agent agent = SpawnRegisteredAgent(
                context, "owner", agentId, AgentControllerType.Player,
                out MirrorAgent mirror);
            context.Mock.SpawnMount(agent);
            context.Component.AgentActionHandler.PollActions();

            mirror.GuardMode = Agent.GuardMode.Right;
            mirror.MovementFlags =
                Agent.MovementControlFlag.DefendBlock
                | Agent.MovementControlFlag.DefendRight;
            mirror.Action1Index = 202;
            mirror.Action1CodeType = Agent.ActionCodeType.Guard;
            mirror.Action1Direction = Agent.UsageDirection.DefendRight;
            context.Component.AgentActionHandler.PollActions();
            context.Network.NetworkSentPackets.Packets.Clear();

            mirror.GuardMode = Agent.GuardMode.None;
            mirror.MovementFlags = Agent.MovementControlFlag.None;
            mirror.DefendMovementFlag = Agent.MovementControlFlag.DefendBlock;
            mirror.Action1Index = 303;
            mirror.Action1CodeType = Agent.ActionCodeType.Other;
            mirror.Action1Direction = Agent.UsageDirection.None;
            context.Component.AgentActionHandler.PollActions();

            Assert.Empty(
                context.Network.NetworkSentPackets.GetPackets<AgentActionPacket>());

            context.Component.AgentActionHandler.CatchUpJoiner("joiner");

            var directSend = Assert.Single(context.Network.DirectPacketSends);
            var packet = Assert.IsType<AgentActionPacket>(directSend.Packet);
            AgentActionData action = Assert.Single(packet.Actions);
            Assert.True(action.IsMounted);
            Assert.Equal(Agent.GuardMode.Right, action.GuardMode);
            Assert.Equal(
                Agent.MovementControlFlag.DefendBlock,
                action.DefendFlags);
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
    public void MissionPreDisplayTick_MountedGuardDecay_DoesNotReplayHeldGuardVisual(
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
            puppetMirror.HasVisualSkeleton = true;
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
            puppetMirror.SkeletonAction0Index = 303;
            puppetMirror.Action1Index = -1;
            puppetMirror.Action1Progress = 0f;
            puppetMirror.Action1Flags = 0;
            puppetMirror.SkeletonAction1Index = -1;
            puppetMirror.RawVisualAction1Index = -1;
            puppetMirror.RawVisualAction1Progress = 0f;
            puppetMirror.SetActionChannelCalls = 0;
            puppetMirror.LastSetActionChannel = -1;

            controller.OnPreDisplayMissionTick(0.1f);

            Assert.Equal(-1, puppetMirror.Action1Index);
            Assert.Equal(0f, puppetMirror.Action1Progress);
            Assert.Equal(0, puppetMirror.SetActionChannelCalls);
            Assert.Equal(-1, puppetMirror.SkeletonAction1Index);
            Assert.Equal(-1, puppetMirror.RawVisualAction1Index);
            Assert.Equal(0f, puppetMirror.RawVisualAction1Progress);
            Assert.Equal(0, puppetMirror.AdvanceRawVisualActionCalls);

            // Repeated display snapshots leave the missing native presentation alone.
            puppetMirror.Action1Index = -1;
            puppetMirror.Action1Progress = 0f;
            puppetMirror.Action1Flags = 0;

            controller.OnPreDisplayMissionTick(0.1f);

            Assert.Equal(-1, puppetMirror.Action1Index);
            Assert.Equal(-1, puppetMirror.SkeletonAction1Index);
            Assert.Equal(-1, puppetMirror.RawVisualAction1Index);
            Assert.Equal(0f, puppetMirror.RawVisualAction1Progress);
            Assert.Equal(0, puppetMirror.AdvanceRawVisualActionCalls);

            // An attack on the other channel is never touched by held-guard presentation.
            puppetMirror.Action0Index = 404;
            puppetMirror.Action0Progress = 0.35f;
            puppetMirror.Action0Flags = (AnimFlags)5;
            puppetMirror.Action0CodeType = Agent.ActionCodeType.StrikeMedium;
            puppetMirror.SkeletonAction0Index = 404;
            puppetMirror.Action1Index = -1;

            controller.OnPreDisplayMissionTick(0.1f);

            Assert.Equal(404, puppetMirror.Action0Index);
            Assert.Equal(0.35f, puppetMirror.Action0Progress);
            Assert.Equal(404, puppetMirror.SkeletonAction0Index);
            Assert.Equal(-1, puppetMirror.Action1Index);
            Assert.Equal(-1, puppetMirror.SkeletonAction1Index);
            Assert.Equal(-1, puppetMirror.RawVisualAction1Index);
            Assert.Equal(0, puppetMirror.SetActionChannelCalls);
            Assert.Equal(0, puppetMirror.AdvanceRawVisualActionCalls);

            // An interrupt that owns the guard channel must not be cleared or overwritten.
            puppetMirror.Action0Index = 303;
            puppetMirror.Action0Progress = 0.8f;
            puppetMirror.Action0Flags = (AnimFlags)3;
            puppetMirror.Action0CodeType = Agent.ActionCodeType.Idle;
            puppetMirror.SkeletonAction0Index = 303;
            puppetMirror.Action1Index = 405;
            puppetMirror.Action1Progress = 0.45f;
            puppetMirror.Action1CodeType = Agent.ActionCodeType.StrikeMedium;
            puppetMirror.SkeletonAction1Index = 405;
            puppetMirror.SetActionChannelCalls = 0;

            controller.OnPreDisplayMissionTick(0.1f);

            Assert.Equal(405, puppetMirror.Action1Index);
            Assert.Equal(405, puppetMirror.SkeletonAction1Index);
            Assert.Equal(0, puppetMirror.SetActionChannelCalls);
            Assert.Equal(0, puppetMirror.AdvanceRawVisualActionCalls);

            // The following native tick clears the interrupt before the next display snapshot.
            puppetMirror.Action1Index = -1;
            puppetMirror.Action1Progress = 0f;
            puppetMirror.Action1CodeType = guardActionType;
            puppetMirror.SkeletonAction1Index = -1;
            puppetMirror.RawVisualAction1Index = -1;
            puppetMirror.RawVisualAction1Progress = 0f;
            controller.OnPreDisplayMissionTick(0.1f);

            Assert.Equal(303, puppetMirror.Action0Index);
            Assert.Equal(0.8f, puppetMirror.Action0Progress);
            Assert.Equal((AnimFlags)3, puppetMirror.Action0Flags);
            Assert.Equal(303, puppetMirror.SkeletonAction0Index);
            Assert.Equal(-1, puppetMirror.Action1Index);
            Assert.Equal(-1, puppetMirror.SkeletonAction1Index);
            Assert.Equal(-1, puppetMirror.RawVisualAction1Index);
            Assert.Equal(0f, puppetMirror.RawVisualAction1Progress);
            Assert.Equal(0, puppetMirror.AdvanceRawVisualActionCalls);
            Assert.Equal(0, puppetMirror.SetActionChannelCalls);
            Assert.Equal(
                Agent.MovementControlFlag.DefendBlock
                    | Agent.MovementControlFlag.DefendLeft,
                AgentActionData.GetDefendMovementFlags(
                    puppetMirror.MovementFlags));

            // Benign mounted locomotion can own the Agent channel while the retained guard stays visible.
            ownerMirror.Action1Index = 303;
            ownerMirror.Action1Progress = 0.4f;
            ownerMirror.Action1Flags = 0;
            ownerMirror.Action1CodeType = Agent.ActionCodeType.Idle;
            puppetMirror.Action1Index = 303;
            puppetMirror.Action1Progress = ownerMirror.Action1Progress;
            puppetMirror.Action1Flags = ownerMirror.Action1Flags;
            puppetMirror.Action1CodeType = ownerMirror.Action1CodeType;
            puppetMirror.SkeletonAction1Index = 303;
            puppetMirror.RawVisualAction1Index = 303;
            puppetMirror.RawVisualAction1Progress = 0.4f;

            ApplyOwnerAction(context.Component, 2L, agentId, owner);
            context.Component.AgentActionHandler.ApplyRemoteGuardStates();

            Assert.Equal(303, puppetMirror.Action1Index);

            // Native rider locomotion remains the only held-guard presentation.
            puppetMirror.Action1Index = 303;
            puppetMirror.Action1Progress = ownerMirror.Action1Progress;
            puppetMirror.Action1Flags = ownerMirror.Action1Flags;
            puppetMirror.Action1CodeType = ownerMirror.Action1CodeType;
            puppetMirror.SkeletonAction1Index = 303;
            puppetMirror.RawVisualAction1Index = 303;
            puppetMirror.RawVisualAction1Progress = 0.4f;
            puppetMirror.SetActionChannelCalls = 0;

            controller.OnPreDisplayMissionTick(0.1f);

            Assert.Equal(303, puppetMirror.Action1Index);
            Assert.Equal(303, puppetMirror.RawVisualAction1Index);
            Assert.Equal(0.4f, puppetMirror.RawVisualAction1Progress, precision: 3);
            Assert.Equal(0, puppetMirror.AdvanceRawVisualActionCalls);

            puppetMirror.Action1Index = -1;
            puppetMirror.Action1Progress = 0f;
            puppetMirror.SkeletonAction1Index = -1;
            puppetMirror.RawVisualAction1Index = -1;
            puppetMirror.RawVisualAction1Progress = 0f;
            controller.OnPreDisplayMissionTick(0.1f);

            Assert.Equal(-1, puppetMirror.Action1Index);
            Assert.Equal(-1, puppetMirror.SkeletonAction1Index);
            Assert.Equal(-1, puppetMirror.RawVisualAction1Index);
            Assert.Equal(0f, puppetMirror.RawVisualAction1Progress);
            Assert.Equal(0, puppetMirror.AdvanceRawVisualActionCalls);
            Assert.Equal(0, puppetMirror.SetActionChannelCalls);

            // A true release clears logical guard state without touching action channels.
            puppetMirror.Action1Index = -1;
            puppetMirror.SetActionChannelCalls = 0;
            puppetMirror.LastSetActionChannel = -1;
            puppetMirror.LastSetActionBlendInPeriod = float.NaN;
            ownerMirror.MovementFlags = Agent.MovementControlFlag.None;
            ownerMirror.GuardMode = Agent.GuardMode.None;
            ownerMirror.Action1Index = -1;
            ownerMirror.Action1Progress = 0f;
            ownerMirror.Action1Flags = 0;
            ownerMirror.Action1CodeType = Agent.ActionCodeType.Idle;

            ApplyOwnerAction(context.Component, 3L, agentId, owner);
            context.Component.AgentActionHandler.ApplyRemoteGuardStates();

            Assert.Equal(-1, puppetMirror.Action1Index);
            Assert.Equal(0, puppetMirror.SetActionChannelCalls);
            Assert.Equal(-1, puppetMirror.SkeletonAction1Index);
            Assert.Equal(-1, puppetMirror.RawVisualAction1Index);
            Assert.Equal(
                Agent.MovementControlFlag.None,
                AgentActionData.GetDefendMovementFlags(puppetMirror.MovementFlags));

            controller.OnPreDisplayMissionTick(0.1f);

            Assert.Equal(-1, puppetMirror.Action1Index);
            Assert.Equal(0, puppetMirror.SetActionChannelCalls);
            Assert.Equal(0, puppetMirror.AdvanceRawVisualActionCalls);
        });
    }

    [Fact]
    public void MissionPhases_MountedGuardModeDrift_PreservesNativeDefendingActionWithoutRecommand()
    {
        RunScenario("peer", context =>
        {
            var controller = context.Instance.Container.Resolve<CoopBattleController>(
                new TypedParameter(typeof(ICoopMissionComponent), context.Component));
            var agentId = Guid.NewGuid();

            Agent puppet = SpawnRegisteredAgent(
                context, "owner", agentId, AgentControllerType.None,
                out MirrorAgent puppetMirror);
            puppetMirror.HasVisualSkeleton = true;
            Agent owner = SpawnAgent(
                context, AgentControllerType.Player, out MirrorAgent ownerMirror);
            context.Mock.SpawnMount(puppet);
            context.Mock.SpawnMount(owner);

            ownerMirror.GuardMode = Agent.GuardMode.Right;
            ownerMirror.MovementFlags =
                Agent.MovementControlFlag.DefendBlock
                | Agent.MovementControlFlag.DefendRight;
            ownerMirror.Action1Index = 202;
            ownerMirror.Action1Progress = 0.2f;
            ownerMirror.Action1Flags =
                AnimFlags.amf_priority_defend | AnimFlags.anf_cyclic;
            ownerMirror.Action1CodeType = Agent.ActionCodeType.Guard;
            puppetMirror.Action1Index = ownerMirror.Action1Index;
            puppetMirror.Action1Progress = 0.6f;
            puppetMirror.Action1CodeType = Agent.ActionCodeType.Guard;

            ApplyOwnerAction(context.Component, 1L, agentId, owner);
            context.Component.AgentActionHandler.ApplyRemoteGuardStates();
            context.Component.AgentActionHandler.ReplayRemoteGuardReactions();

            Assert.Equal(0.6f, puppetMirror.Action1Progress, precision: 3);
            Assert.Equal(0f, puppetMirror.RawVisualAction1Progress, precision: 3);
            Assert.Equal(1, puppetMirror.SetWeaponGuardCalls);

            // Native rider ticking can clear its guard metadata without ending the received guard.
            puppetMirror.GuardMode = Agent.GuardMode.None;
            puppetMirror.MovementFlags = Agent.MovementControlFlag.Forward;
            puppetMirror.Action1Progress = 0f;
            puppetMirror.RawVisualAction1Progress = 0f;
            puppetMirror.AdvanceExistingRawVisualActionCalls = 0;
            puppetMirror.InstallAgentVisualActionCalls = 0;
            puppetMirror.SetActionChannelCalls = 0;

            controller.OnMissionTick(0.1f);

            Assert.Equal(Agent.GuardMode.None, puppetMirror.GuardMode);
            Assert.Equal(
                Agent.MovementControlFlag.Forward,
                puppetMirror.MovementFlags);
            Assert.Equal(202, puppetMirror.Action1Index);
            Assert.Equal(0f, puppetMirror.Action1Progress, precision: 3);
            Assert.Equal(0f, puppetMirror.RawVisualAction1Progress, precision: 3);
            Assert.Equal(1, puppetMirror.SetWeaponGuardCalls);
            Assert.Equal(0, puppetMirror.SetActionChannelCalls);

            controller.OnPreDisplayMissionTick(0.1f);

            Assert.Equal(0f, puppetMirror.Action1Progress, precision: 3);
            Assert.Equal(0f, puppetMirror.RawVisualAction1Progress, precision: 3);
            Assert.Equal(Agent.GuardMode.None, puppetMirror.GuardMode);
            Assert.Equal(
                Agent.MovementControlFlag.Forward,
                puppetMirror.MovementFlags);
            Assert.Equal(202, puppetMirror.Action1Index);
            Assert.Equal(1, puppetMirror.SetWeaponGuardCalls);
            Assert.Equal(0, puppetMirror.SetActionChannelCalls);
            Assert.Equal(0, puppetMirror.AdvanceExistingRawVisualActionCalls);
            Assert.Equal(0, puppetMirror.InstallAgentVisualActionCalls);

            controller.OnPreMissionTick(0.1f);

            Assert.Equal(0f, puppetMirror.Action1Progress, precision: 3);
            Assert.Equal(0f, puppetMirror.RawVisualAction1Progress, precision: 3);
            Assert.Equal(Agent.GuardMode.None, puppetMirror.GuardMode);
            Assert.Equal(
                Agent.MovementControlFlag.Forward
                    | Agent.MovementControlFlag.DefendBlock
                    | Agent.MovementControlFlag.DefendRight,
                puppetMirror.MovementFlags);
            Assert.Equal(1, puppetMirror.SetWeaponGuardCalls);
            Assert.Equal(0, puppetMirror.SetActionChannelCalls);
        });
    }

    [Fact]
    public void PreMissionTick_MountedGuardNativeStateClears_ReappliesFlagsWithoutRecommand()
    {
        RunScenario("peer", context =>
        {
            var controller = context.Instance.Container.Resolve<CoopBattleController>(
                new TypedParameter(typeof(ICoopMissionComponent), context.Component));
            var agentId = Guid.NewGuid();

            Agent puppet = SpawnRegisteredAgent(
                context, "owner", agentId, AgentControllerType.None,
                out MirrorAgent puppetMirror);
            Agent owner = SpawnAgent(
                context, AgentControllerType.Player, out MirrorAgent ownerMirror);
            context.Mock.SpawnMount(puppet);
            context.Mock.SpawnMount(owner);

            Agent.MovementControlFlag defendFlags =
                Agent.MovementControlFlag.DefendBlock
                | Agent.MovementControlFlag.DefendRight;
            ownerMirror.GuardMode = Agent.GuardMode.Right;
            ownerMirror.MovementFlags = defendFlags;
            ownerMirror.Action1Index = 202;
            ownerMirror.Action1Progress = 0.2f;
            ownerMirror.Action1Flags =
                AnimFlags.amf_priority_defend | AnimFlags.anf_cyclic;
            ownerMirror.Action1CodeType = Agent.ActionCodeType.Guard;
            puppetMirror.Action1Index = ownerMirror.Action1Index;
            puppetMirror.Action1CodeType = ownerMirror.Action1CodeType;

            ApplyOwnerAction(context.Component, 1L, agentId, owner);
            context.Component.AgentActionHandler.ApplyRemoteGuardStates();

            Assert.Equal(1, puppetMirror.SetWeaponGuardCalls);
            puppetMirror.SetActionChannelCalls = 0;
            puppetMirror.InstallRawVisualActionCalls = 0;
            puppetMirror.InstallAgentVisualActionCalls = 0;

            for (int nativeTick = 0; nativeTick < 3; nativeTick++)
            {
                int nativeActionIndex = 303 + nativeTick;
                puppetMirror.GuardMode = Agent.GuardMode.None;
                puppetMirror.MovementFlags = Agent.MovementControlFlag.Forward;
                puppetMirror.Action1Index = nativeActionIndex;
                puppetMirror.Action1Progress = 0.3f;
                puppetMirror.Action1Flags = 0;
                puppetMirror.Action1CodeType = Agent.ActionCodeType.Other;
                puppetMirror.SkeletonAction1Index = nativeActionIndex;
                puppetMirror.RawVisualAction1Index = nativeActionIndex;
                puppetMirror.RawVisualAction1Progress = 0.3f;

                controller.OnPreMissionTick(0.1f);

                Assert.Equal(
                    Agent.MovementControlFlag.Forward | defendFlags,
                    puppetMirror.MovementFlags);
                Assert.Equal(Agent.GuardMode.None, puppetMirror.GuardMode);
                Assert.Equal(nativeActionIndex, puppetMirror.Action1Index);
                Assert.Equal(nativeActionIndex, puppetMirror.SkeletonAction1Index);
                Assert.Equal(nativeActionIndex, puppetMirror.RawVisualAction1Index);
                Assert.Equal(1, puppetMirror.SetWeaponGuardCalls);
                Assert.Equal(0, puppetMirror.SetActionChannelCalls);
                Assert.Equal(0, puppetMirror.InstallRawVisualActionCalls);
                Assert.Equal(0, puppetMirror.InstallAgentVisualActionCalls);
            }
        });
    }

    [Fact]
    public void MissionPreDisplayTick_MovingMountedGuard_DoesNotReplaceLocomotionVisual()
    {
        RunScenario("peer", context =>
        {
            var controller = context.Instance.Container.Resolve<CoopBattleController>(
                new TypedParameter(typeof(ICoopMissionComponent), context.Component));
            var agentId = Guid.NewGuid();

            Agent puppet = SpawnRegisteredAgent(
                context, "owner", agentId, AgentControllerType.None,
                out MirrorAgent puppetMirror);
            puppetMirror.HasVisualSkeleton = true;
            Agent owner = SpawnAgent(
                context, AgentControllerType.Player, out MirrorAgent ownerMirror);
            context.Mock.SpawnMount(puppet);
            context.Mock.SpawnMount(owner);

            ownerMirror.GuardMode = Agent.GuardMode.Right;
            ownerMirror.MovementFlags =
                Agent.MovementControlFlag.DefendBlock
                | Agent.MovementControlFlag.DefendRight;
            ownerMirror.Action1Index = 202;
            ownerMirror.Action1Progress = 0.2f;
            ownerMirror.Action1Flags =
                AnimFlags.amf_priority_defend | AnimFlags.anf_cyclic;
            ownerMirror.Action1CodeType = Agent.ActionCodeType.Guard;
            puppetMirror.Action1Index = ownerMirror.Action1Index;
            puppetMirror.Action1CodeType = ownerMirror.Action1CodeType;

            ApplyOwnerAction(context.Component, 1L, agentId, owner);
            context.Component.AgentActionHandler.ApplyRemoteGuardStates();

            Assert.Equal(Agent.GuardMode.Right, puppetMirror.GuardMode);
            Assert.Equal(1, puppetMirror.SetWeaponGuardCalls);

            // Horse movement replaces the guard channel with a benign rider pace action.
            puppetMirror.Action1Index = 303;
            puppetMirror.Action1Progress = 0.6f;
            puppetMirror.Action1CodeType = Agent.ActionCodeType.Other;
            puppetMirror.SkeletonAction1Index = 303;
            puppetMirror.RawVisualAction1Index = 303;
            puppetMirror.RawVisualAction1Progress = 0.6f;
            puppetMirror.GuardMode = Agent.GuardMode.None;
            puppetMirror.SetActionChannelCalls = 0;

            controller.OnPreDisplayMissionTick(0.1f);

            Assert.Equal(303, puppetMirror.Action1Index);
            Assert.Equal(0.6f, puppetMirror.Action1Progress);
            Assert.Equal(0, puppetMirror.SetActionChannelCalls);
            Assert.Equal(303, puppetMirror.SkeletonAction1Index);
            Assert.Equal(303, puppetMirror.RawVisualAction1Index);
            Assert.Equal(0.6f, puppetMirror.RawVisualAction1Progress, precision: 3);
            Assert.Equal(0, puppetMirror.InstallRawVisualActionCalls);
            Assert.Equal(0, puppetMirror.AdvanceExistingRawVisualActionCalls);
            Assert.Equal(Agent.GuardMode.None, puppetMirror.GuardMode);
            Assert.Equal(1, puppetMirror.SetWeaponGuardCalls);

            // Repeated display snapshots leave native locomotion untouched.
            controller.OnPreDisplayMissionTick(0.1f);

            Assert.Equal(303, puppetMirror.Action1Index);
            Assert.Equal(0, puppetMirror.SetActionChannelCalls);
            Assert.Equal(303, puppetMirror.RawVisualAction1Index);
            Assert.Equal(0.6f, puppetMirror.RawVisualAction1Progress, precision: 3);
            Assert.Equal(0, puppetMirror.InstallRawVisualActionCalls);
            Assert.Equal(0, puppetMirror.AdvanceExistingRawVisualActionCalls);
            Assert.Equal(1, puppetMirror.SetWeaponGuardCalls);

            // The next moving native tick remains authoritative for presentation.
            puppetMirror.RawVisualAction1Index = 303;
            puppetMirror.RawVisualAction1Progress = 0.7f;
            puppetMirror.GuardMode = Agent.GuardMode.None;
            controller.OnPreDisplayMissionTick(0.1f);

            Assert.Equal(303, puppetMirror.Action1Index);
            Assert.Equal(0, puppetMirror.SetActionChannelCalls);
            Assert.Equal(303, puppetMirror.RawVisualAction1Index);
            Assert.Equal(0.7f, puppetMirror.RawVisualAction1Progress, precision: 3);
            Assert.Equal(0, puppetMirror.InstallRawVisualActionCalls);
            Assert.Equal(0, puppetMirror.AdvanceExistingRawVisualActionCalls);
            Assert.Equal(Agent.GuardMode.None, puppetMirror.GuardMode);
            Assert.Equal(1, puppetMirror.SetWeaponGuardCalls);

            // A block reaction owns the channel until native returns to locomotion.
            puppetMirror.Action1Index = 404;
            puppetMirror.Action1Progress = 0.45f;
            puppetMirror.Action1CodeType = Agent.ActionCodeType.BlockedMelee;
            puppetMirror.SkeletonAction1Index = 404;
            puppetMirror.RawVisualAction1Index = 404;
            puppetMirror.GuardMode = Agent.GuardMode.None;
            controller.OnPreDisplayMissionTick(0.1f);

            Assert.Equal(404, puppetMirror.Action1Index);
            Assert.Equal(404, puppetMirror.RawVisualAction1Index);
            Assert.Equal(0, puppetMirror.InstallRawVisualActionCalls);
            Assert.Equal(1, puppetMirror.SetWeaponGuardCalls);

            puppetMirror.Action1Index = 303;
            puppetMirror.Action1Progress = 0.8f;
            puppetMirror.Action1CodeType = Agent.ActionCodeType.Other;
            puppetMirror.SkeletonAction1Index = 303;
            puppetMirror.RawVisualAction1Index = 303;
            puppetMirror.RawVisualAction1Progress = 0.8f;
            controller.OnPreDisplayMissionTick(0.1f);

            Assert.Equal(303, puppetMirror.Action1Index);
            Assert.Equal(0, puppetMirror.SetActionChannelCalls);
            Assert.Equal(303, puppetMirror.RawVisualAction1Index);
            Assert.Equal(0.8f, puppetMirror.RawVisualAction1Progress, precision: 3);
            Assert.Equal(0, puppetMirror.InstallRawVisualActionCalls);
            Assert.Equal(Agent.GuardMode.None, puppetMirror.GuardMode);
            Assert.Equal(1, puppetMirror.SetWeaponGuardCalls);
        });
    }

    [Fact]
    public void AgentActionData_UnchangedActions_DoNotRewindNativeProgress()
    {
        RunScenario("peer", context =>
        {
            Agent owner = SpawnAgent(
                context,
                AgentControllerType.Player,
                out MirrorAgent ownerMirror);
            Agent puppet = SpawnAgent(
                context,
                AgentControllerType.None,
                out MirrorAgent puppetMirror);
            context.Mock.SpawnMount(owner);
            context.Mock.SpawnMount(puppet);

            ownerMirror.GuardMode = Agent.GuardMode.Right;
            ownerMirror.MovementFlags =
                Agent.MovementControlFlag.DefendBlock
                | Agent.MovementControlFlag.DefendRight;
            ownerMirror.Action0Index = 5001;
            ownerMirror.Action0Progress = 0.1f;
            ownerMirror.Action0CodeType = Agent.ActionCodeType.Other;
            ownerMirror.Action1Index = 3062;
            ownerMirror.Action1Progress = 0.2f;
            ownerMirror.Action1Flags =
                AnimFlags.amf_priority_defend | AnimFlags.anf_cyclic;
            ownerMirror.Action1CodeType = Agent.ActionCodeType.Guard;

            puppetMirror.Action0Index = ownerMirror.Action0Index;
            puppetMirror.Action0Progress = 0.7f;
            puppetMirror.Action0CodeType = ownerMirror.Action0CodeType;
            puppetMirror.Action1Index = ownerMirror.Action1Index;
            puppetMirror.Action1Progress = 0.8f;
            puppetMirror.Action1Flags = ownerMirror.Action1Flags;
            puppetMirror.Action1CodeType = ownerMirror.Action1CodeType;

            new AgentActionData(owner).Apply(
                puppet,
                context.Instance.Container
                    .Resolve<IAgentVisualActionAccessor>());

            Assert.Equal(0.7f, puppetMirror.Action0Progress, precision: 3);
            Assert.Equal(0.8f, puppetMirror.Action1Progress, precision: 3);
            Assert.Equal(0, puppetMirror.SetCurrentActionProgressCalls);
            Assert.Equal(0, puppetMirror.SetActionChannelCalls);
        });
    }

    [Fact]
    public void AgentActionData_VisibleMountedAction_DoesNotReplayWhenEngineReportsNone()
    {
        RunScenario("peer", context =>
        {
            Agent owner = SpawnAgent(
                context,
                AgentControllerType.Player,
                out MirrorAgent ownerMirror);
            Agent puppet = SpawnAgent(
                context,
                AgentControllerType.None,
                out MirrorAgent puppetMirror);
            context.Mock.SpawnMount(owner);
            context.Mock.SpawnMount(puppet);

            ownerMirror.Action1Index = 3062;
            ownerMirror.Action1Progress = 0.05f;
            ownerMirror.Action1Flags =
                AnimFlags.amf_priority_defend | AnimFlags.anf_cyclic;
            ownerMirror.Action1CodeType = Agent.ActionCodeType.Guard;

            puppetMirror.HasVisualSkeleton = true;
            puppetMirror.Action1Index = -1;
            puppetMirror.SkeletonAction1Index = -1;
            puppetMirror.ActionAnimationIndices[3062] = 3220;
            puppetMirror.RawVisualAction1Index = 3220;
            puppetMirror.RawVisualAction1Progress = 0.8f;

            new AgentActionData(owner).Apply(
                puppet,
                context.Instance.Container
                    .Resolve<IAgentVisualActionAccessor>());

            Assert.Equal(-1, puppetMirror.Action1Index);
            Assert.Equal(3220, puppetMirror.RawVisualAction1Index);
            Assert.Equal(
                0.8f,
                puppetMirror.RawVisualAction1Progress,
                precision: 3);
            Assert.Equal(0, puppetMirror.SetActionChannelCalls);
        });
    }

    [Fact]
    public void MissionPreDisplayTick_MountedGuardVisualTimeline_RemainsNative()
    {
        RunScenario("peer", context =>
        {
            var controller = context.Instance.Container.Resolve<CoopBattleController>(
                new TypedParameter(typeof(ICoopMissionComponent), context.Component));
            var agentId = Guid.NewGuid();

            Agent puppet = SpawnRegisteredAgent(
                context, "owner", agentId, AgentControllerType.None,
                out MirrorAgent puppetMirror);
            puppetMirror.HasVisualSkeleton = true;
            Agent owner = SpawnAgent(
                context, AgentControllerType.Player, out MirrorAgent ownerMirror);
            context.Mock.SpawnMount(puppet);
            context.Mock.SpawnMount(owner);

            ownerMirror.GuardMode = Agent.GuardMode.Right;
            ownerMirror.MovementFlags =
                Agent.MovementControlFlag.DefendBlock
                | Agent.MovementControlFlag.DefendRight;
            ownerMirror.Action1Index = 202;
            ownerMirror.Action1Progress = 0.2f;
            ownerMirror.Action1Flags =
                AnimFlags.amf_priority_defend | AnimFlags.anf_cyclic;
            ownerMirror.Action1CodeType = Agent.ActionCodeType.Guard;
            puppetMirror.Action1Index = ownerMirror.Action1Index;
            puppetMirror.Action1CodeType = ownerMirror.Action1CodeType;

            ApplyOwnerAction(context.Component, 1L, agentId, owner);
            context.Component.AgentActionHandler.ApplyRemoteGuardStates();

            // Native can wrap the exact raw guard clip while the held guard remains active.
            puppetMirror.Action1Index = -1;
            puppetMirror.Action1CodeType = Agent.ActionCodeType.Idle;
            puppetMirror.SkeletonAction1Index = -1;
            puppetMirror.RawVisualAction1Index = 3220;
            puppetMirror.RawVisualAction1Progress = 0.01f;
            puppetMirror.SetActionChannelCalls = 0;

            controller.OnPreDisplayMissionTick(0.1f);

            Assert.Equal(-1, puppetMirror.Action1Index);
            Assert.Equal(3220, puppetMirror.RawVisualAction1Index);
            Assert.Equal(0.01f, puppetMirror.RawVisualAction1Progress, precision: 3);
            Assert.Equal(0, puppetMirror.AdvanceRawVisualActionCalls);
            Assert.Equal(0, puppetMirror.InstallRawVisualActionCalls);
            Assert.Equal(0, puppetMirror.SetActionChannelCalls);

            // Native forward progress remains untouched.
            puppetMirror.RawVisualAction1Progress = 0.24f;
            controller.OnPreDisplayMissionTick(0.1f);

            Assert.Equal(0.24f, puppetMirror.RawVisualAction1Progress, precision: 3);
            Assert.Equal(0, puppetMirror.AdvanceRawVisualActionCalls);

            // A same-guard snapshot must not rewind the retained raw-animation clock.
            ownerMirror.Action1Progress = 0.05f;
            ApplyOwnerAction(context.Component, 2L, agentId, owner);
            context.Component.AgentActionHandler.ApplyRemoteGuardStates();
            puppetMirror.Action1Index = -1;
            puppetMirror.Action1CodeType = Agent.ActionCodeType.Idle;
            puppetMirror.SkeletonAction1Index = -1;
            puppetMirror.RawVisualAction1Progress = 0.01f;
            controller.OnPreDisplayMissionTick(0.1f);

            Assert.Equal(0.01f, puppetMirror.RawVisualAction1Progress, precision: 3);
            Assert.Equal(0, puppetMirror.AdvanceRawVisualActionCalls);
            Assert.Equal(0, puppetMirror.InstallRawVisualActionCalls);

            // A different raw clip owns presentation.
            puppetMirror.RawVisualAction1Index = 303;
            puppetMirror.RawVisualAction1Progress = 0.7f;
            controller.OnPreDisplayMissionTick(0.1f);

            Assert.Equal(303, puppetMirror.RawVisualAction1Index);
            Assert.Equal(0.7f, puppetMirror.RawVisualAction1Progress, precision: 3);
            Assert.Equal(0, puppetMirror.AdvanceRawVisualActionCalls);
            Assert.Equal(0, puppetMirror.InstallRawVisualActionCalls);

            // A missing raw clip is not synthesized over native presentation.
            puppetMirror.RawVisualAction1Index = -1;
            puppetMirror.RawVisualAction1Progress = 0f;
            controller.OnPreDisplayMissionTick(0.1f);

            Assert.Equal(-1, puppetMirror.RawVisualAction1Index);
            Assert.Equal(0f, puppetMirror.RawVisualAction1Progress);
            Assert.Equal(0, puppetMirror.AdvanceRawVisualActionCalls);
            Assert.Equal(0, puppetMirror.InstallRawVisualActionCalls);
            Assert.Equal(0, puppetMirror.SetActionChannelCalls);
        });
    }

    [Fact]
    public void AiOnFootGuard_NativeStateLoss_RecommandsWithoutReplayingActionOrVisual()
    {
        RunScenario("peer", context =>
        {
            var controller = context.Instance.Container.Resolve<CoopBattleController>(
                new TypedParameter(typeof(ICoopMissionComponent), context.Component));
            var agentId = Guid.NewGuid();

            Agent puppet = SpawnRegisteredAgent(
                context, "owner", agentId, AgentControllerType.None,
                out MirrorAgent puppetMirror);
            puppetMirror.HasVisualSkeleton = true;
            Agent owner = SpawnAgent(
                context, AgentControllerType.AI, out MirrorAgent ownerMirror);
            Agent.MovementControlFlag defendFlags =
                Agent.MovementControlFlag.DefendBlock
                | Agent.MovementControlFlag.DefendRight;
            ownerMirror.GuardMode = Agent.GuardMode.Right;
            ownerMirror.MovementFlags = defendFlags;
            ownerMirror.Action1Index = 202;
            ownerMirror.Action1Progress = 0.2f;
            ownerMirror.Action1Flags =
                AnimFlags.amf_priority_defend | AnimFlags.anf_cyclic;
            ownerMirror.Action1CodeType = Agent.ActionCodeType.Guard;
            puppetMirror.Action1Index = ownerMirror.Action1Index;
            puppetMirror.Action1CodeType = ownerMirror.Action1CodeType;

            var action = new AgentActionData(owner);
            Assert.False(action.IsPlayerControlled);
            ApplyOwnerAction(context.Component, 1L, agentId, owner);
            context.Component.AgentActionHandler.ApplyRemoteGuardStates();

            Assert.Equal(Agent.GuardMode.Right, puppetMirror.GuardMode);
            Assert.Equal(1, puppetMirror.SetWeaponGuardCalls);

            puppetMirror.MovementFlags = Agent.MovementControlFlag.Forward;
            puppetMirror.GuardMode = Agent.GuardMode.None;
            puppetMirror.Action1Index = -1;
            puppetMirror.Action1CodeType = Agent.ActionCodeType.Idle;
            puppetMirror.SkeletonAction1Index = -1;
            puppetMirror.RawVisualAction1Index = -1;
            puppetMirror.SetActionChannelCalls = 0;
            puppetMirror.InstallRawVisualActionCalls = 0;

            context.Component.AgentActionHandler.ApplyRemoteGuardStates();

            Assert.Equal(
                Agent.MovementControlFlag.Forward | defendFlags,
                puppetMirror.MovementFlags);
            Assert.Equal(Agent.GuardMode.Right, puppetMirror.GuardMode);
            Assert.Equal(2, puppetMirror.SetWeaponGuardCalls);
            Assert.Equal(-1, puppetMirror.Action1Index);
            Assert.Equal(0, puppetMirror.SetActionChannelCalls);

            controller.OnPreDisplayMissionTick(0.1f);

            Assert.Equal(-1, puppetMirror.Action1Index);
            Assert.Equal(-1, puppetMirror.RawVisualAction1Index);
            Assert.Equal(0, puppetMirror.InstallRawVisualActionCalls);
            Assert.Equal(0, puppetMirror.AdvanceRawVisualActionCalls);
            Assert.Equal(0, puppetMirror.SetActionChannelCalls);
        });
    }

    [Fact]
    public void AiMountedGuard_RetainsLogicalStateWithoutRestoringActionOrVisual()
    {
        RunScenario("peer", context =>
        {
            var controller = context.Instance.Container.Resolve<CoopBattleController>(
                new TypedParameter(typeof(ICoopMissionComponent), context.Component));
            var agentId = Guid.NewGuid();

            Agent puppet = SpawnRegisteredAgent(
                context, "owner", agentId, AgentControllerType.None,
                out MirrorAgent puppetMirror);
            puppetMirror.HasVisualSkeleton = true;
            Agent owner = SpawnAgent(
                context, AgentControllerType.AI, out MirrorAgent ownerMirror);
            context.Mock.SpawnMount(puppet);
            context.Mock.SpawnMount(owner);

            ownerMirror.GuardMode = Agent.GuardMode.Right;
            ownerMirror.MovementFlags =
                Agent.MovementControlFlag.DefendBlock
                | Agent.MovementControlFlag.DefendRight;
            ownerMirror.Action1Index = 202;
            ownerMirror.Action1Progress = 0.2f;
            ownerMirror.Action1Flags =
                AnimFlags.amf_priority_defend | AnimFlags.anf_cyclic;
            ownerMirror.Action1CodeType = Agent.ActionCodeType.Guard;
            puppetMirror.Action1Index = ownerMirror.Action1Index;
            puppetMirror.Action1CodeType = ownerMirror.Action1CodeType;

            var action = new AgentActionData(owner);
            Assert.False(action.IsPlayerControlled);
            ApplyOwnerAction(context.Component, 1L, agentId, owner);
            context.Component.AgentActionHandler.ApplyRemoteGuardStates();

            Assert.Equal(Agent.GuardMode.Right, puppetMirror.GuardMode);
            Assert.Equal(1, puppetMirror.SetWeaponGuardCalls);

            puppetMirror.MovementFlags = Agent.MovementControlFlag.Forward;
            puppetMirror.GuardMode = Agent.GuardMode.None;
            puppetMirror.Action1Index = 303;
            puppetMirror.Action1Progress = 0.6f;
            puppetMirror.Action1CodeType = Agent.ActionCodeType.Other;
            puppetMirror.SkeletonAction1Index = 303;
            puppetMirror.RawVisualAction1Index = 303;
            puppetMirror.RawVisualAction1Progress = 0.6f;
            puppetMirror.SetActionChannelCalls = 0;
            puppetMirror.InstallRawVisualActionCalls = 0;

            controller.OnPreMissionTick(0.1f);

            Assert.Equal(
                Agent.MovementControlFlag.Forward
                    | Agent.MovementControlFlag.DefendBlock
                    | Agent.MovementControlFlag.DefendRight,
                puppetMirror.MovementFlags);
            Assert.Equal(Agent.GuardMode.None, puppetMirror.GuardMode);
            Assert.Equal(303, puppetMirror.Action1Index);
            Assert.Equal(0, puppetMirror.SetActionChannelCalls);
            Assert.Equal(1, puppetMirror.SetWeaponGuardCalls);

            controller.OnPreDisplayMissionTick(0.1f);

            Assert.Equal(303, puppetMirror.Action1Index);
            Assert.Equal(303, puppetMirror.RawVisualAction1Index);
            Assert.Equal(0.6f, puppetMirror.RawVisualAction1Progress, precision: 3);
            Assert.Equal(0, puppetMirror.InstallRawVisualActionCalls);
            Assert.Equal(0, puppetMirror.SetActionChannelCalls);
        });
    }

    [Fact]
    public void PlayerMountedReaction_VisibleNativeTimeline_DoesNotWriteSkeleton()
    {
        RunScenario("peer", context =>
        {
            var controller = context.Instance.Container.Resolve<CoopBattleController>(
                new TypedParameter(typeof(ICoopMissionComponent), context.Component));
            var agentId = Guid.NewGuid();

            Agent puppet = SpawnRegisteredAgent(
                context, "owner", agentId, AgentControllerType.None,
                out MirrorAgent puppetMirror);
            puppetMirror.HasVisualSkeleton = true;
            Agent owner = SpawnAgent(
                context, AgentControllerType.Player, out MirrorAgent ownerMirror);
            context.Mock.SpawnMount(puppet);
            context.Mock.SpawnMount(owner);

            ownerMirror.Action1Index = 202;
            ownerMirror.Action1Progress = 0.2f;
            ownerMirror.Action1CodeType = Agent.ActionCodeType.BlockedMelee;

            ApplyOwnerAction(context.Component, 1L, agentId, owner);
            context.Component.AgentActionHandler.ApplyRemoteGuardStates();

            puppetMirror.Action1Index = 202;
            puppetMirror.Action1Progress = 0.35f;
            puppetMirror.Action1CodeType = Agent.ActionCodeType.BlockedMelee;
            puppetMirror.SkeletonAction1Index = 202;
            puppetMirror.RawVisualAction1Index = 202;
            puppetMirror.RawVisualAction1Progress = 0.2f;
            puppetMirror.InstallRawVisualActionCalls = 0;
            puppetMirror.AdvanceExistingRawVisualActionCalls = 0;

            controller.OnPreDisplayMissionTick(0.1f);

            Assert.Equal(202, puppetMirror.Action1Index);
            Assert.Equal(0.35f, puppetMirror.Action1Progress, precision: 3);
            Assert.Equal(0.2f, puppetMirror.RawVisualAction1Progress, precision: 3);
            Assert.Equal(0, puppetMirror.InstallRawVisualActionCalls);
            Assert.Equal(0, puppetMirror.AdvanceExistingRawVisualActionCalls);
        });
    }

    [Fact]
    public void PlayerMountedReaction_OverwrittenVisual_DoesNotWriteSkeleton()
    {
        RunScenario("peer", context =>
        {
            var controller = context.Instance.Container.Resolve<CoopBattleController>(
                new TypedParameter(typeof(ICoopMissionComponent), context.Component));
            var agentId = Guid.NewGuid();

            Agent puppet = SpawnRegisteredAgent(
                context, "owner", agentId, AgentControllerType.None,
                out MirrorAgent puppetMirror);
            puppetMirror.HasVisualSkeleton = true;
            Agent owner = SpawnAgent(
                context, AgentControllerType.Player, out MirrorAgent ownerMirror);
            context.Mock.SpawnMount(puppet);
            context.Mock.SpawnMount(owner);

            ownerMirror.Action1Index = 202;
            ownerMirror.Action1Progress = 0.2f;
            ownerMirror.Action1CodeType = Agent.ActionCodeType.BlockedMelee;

            ApplyOwnerAction(context.Component, 1L, agentId, owner);
            context.Component.AgentActionHandler.ApplyRemoteGuardStates();

            puppetMirror.Action1Index = 202;
            puppetMirror.Action1Progress = 0.35f;
            puppetMirror.Action1CodeType = Agent.ActionCodeType.BlockedMelee;
            puppetMirror.SkeletonAction1Index = -1;
            puppetMirror.RawVisualAction1Index = 303;
            puppetMirror.RawVisualAction1Progress = 0.6f;
            puppetMirror.InstallRawVisualActionCalls = 0;

            controller.OnPreDisplayMissionTick(0.1f);

            Assert.Equal(202, puppetMirror.Action1Index);
            Assert.Equal(0.35f, puppetMirror.Action1Progress, precision: 3);
            Assert.Equal(303, puppetMirror.RawVisualAction1Index);
            Assert.Equal(0.6f, puppetMirror.RawVisualAction1Progress, precision: 3);
            Assert.Equal(0, puppetMirror.InstallRawVisualActionCalls);
        });
    }

    [Fact]
    public void PlayerMountedReaction_DoesNotReplaceExistingLocomotionVisual()
    {
        RunScenario("peer", context =>
        {
            var controller = context.Instance.Container.Resolve<CoopBattleController>(
                new TypedParameter(typeof(ICoopMissionComponent), context.Component));
            var agentId = Guid.NewGuid();

            Agent puppet = SpawnRegisteredAgent(
                context, "owner", agentId, AgentControllerType.None,
                out MirrorAgent puppetMirror);
            puppetMirror.HasVisualSkeleton = true;
            Agent owner = SpawnAgent(
                context, AgentControllerType.Player, out MirrorAgent ownerMirror);
            context.Mock.SpawnMount(puppet);
            context.Mock.SpawnMount(owner);

            ownerMirror.Action1Index = 202;
            ownerMirror.Action1Progress = 0.2f;
            ownerMirror.Action1CodeType = Agent.ActionCodeType.BlockedMelee;

            ApplyOwnerAction(context.Component, 1L, agentId, owner);
            context.Component.AgentActionHandler.ApplyRemoteGuardStates();

            puppetMirror.Action1Index = 303;
            puppetMirror.Action1Progress = 0.6f;
            puppetMirror.Action1CodeType = Agent.ActionCodeType.Other;
            puppetMirror.SkeletonAction1Index = 303;
            puppetMirror.RawVisualAction1Index = 303;
            puppetMirror.RawVisualAction1Progress = 0.6f;
            puppetMirror.SetActionChannelCalls = 0;
            puppetMirror.InstallRawVisualActionCalls = 0;

            controller.OnPreDisplayMissionTick(0.1f);

            Assert.Equal(303, puppetMirror.Action1Index);
            Assert.Equal(0, puppetMirror.SetActionChannelCalls);
            Assert.Equal(303, puppetMirror.RawVisualAction1Index);
            Assert.Equal(0.6f, puppetMirror.RawVisualAction1Progress, precision: 3);
            Assert.Equal(0, puppetMirror.InstallRawVisualActionCalls);

            controller.OnPreDisplayMissionTick(0.1f);

            Assert.Equal(303, puppetMirror.Action1Index);
            Assert.Equal(0, puppetMirror.SetActionChannelCalls);
            Assert.Equal(303, puppetMirror.RawVisualAction1Index);
            Assert.Equal(0.6f, puppetMirror.RawVisualAction1Progress, precision: 3);
            Assert.Equal(0, puppetMirror.InstallRawVisualActionCalls);
            Assert.Equal(0, puppetMirror.AdvanceExistingRawVisualActionCalls);
        });
    }

    [Fact]
    public void MissionPhases_MountedGuardMetadata_DoesNotReplaceOverwrittenSkeleton()
    {
        RunScenario("peer", context =>
        {
            var controller = context.Instance.Container.Resolve<CoopBattleController>(
                new TypedParameter(typeof(ICoopMissionComponent), context.Component));
            var agentId = Guid.NewGuid();

            Agent puppet = SpawnRegisteredAgent(
                context, "owner", agentId, AgentControllerType.None,
                out MirrorAgent puppetMirror);
            puppetMirror.HasVisualSkeleton = true;
            Agent owner = SpawnAgent(
                context, AgentControllerType.Player, out MirrorAgent ownerMirror);
            context.Mock.SpawnMount(puppet);
            context.Mock.SpawnMount(owner);

            ownerMirror.GuardMode = Agent.GuardMode.Right;
            ownerMirror.MovementFlags =
                Agent.MovementControlFlag.DefendBlock
                | Agent.MovementControlFlag.DefendRight;
            ownerMirror.Action1Index = 202;
            ownerMirror.Action1Progress = 0.2f;
            ownerMirror.Action1Flags =
                AnimFlags.amf_priority_defend | AnimFlags.anf_cyclic;
            ownerMirror.Action1CodeType = Agent.ActionCodeType.Guard;
            puppetMirror.Action1Index = ownerMirror.Action1Index;
            puppetMirror.Action1Progress = ownerMirror.Action1Progress;
            puppetMirror.Action1CodeType = ownerMirror.Action1CodeType;

            ApplyOwnerAction(context.Component, 1L, agentId, owner);
            context.Component.AgentActionHandler.ApplyRemoteGuardStates();

            // Native rider gait replaced only the skeleton; Agent metadata still reports the guard.
            puppetMirror.SkeletonAction1Index = 303;
            puppetMirror.RawVisualAction1Index = 303;
            puppetMirror.RawVisualAction1Progress = 0.65f;
            puppetMirror.InstallRawVisualActionCalls = 0;
            puppetMirror.InstallAgentVisualActionCalls = 0;
            puppetMirror.SetActionChannelCalls = 0;

            controller.OnMissionTick(0.1f);

            Assert.Equal(202, puppetMirror.Action1Index);
            Assert.Equal(303, puppetMirror.SkeletonAction1Index);
            Assert.Equal(303, puppetMirror.RawVisualAction1Index);
            Assert.Equal(0, puppetMirror.InstallRawVisualActionCalls);
            Assert.Equal(0, puppetMirror.InstallAgentVisualActionCalls);
            Assert.Equal(0, puppetMirror.SetActionChannelCalls);

            controller.OnPreMissionTick(0.1f);

            Assert.Equal(202, puppetMirror.Action1Index);
            Assert.Equal(303, puppetMirror.SkeletonAction1Index);
            Assert.Equal(303, puppetMirror.RawVisualAction1Index);
            Assert.Equal(0, puppetMirror.InstallRawVisualActionCalls);
            Assert.Equal(0, puppetMirror.InstallAgentVisualActionCalls);
            Assert.Equal(0, puppetMirror.SetActionChannelCalls);

            controller.OnPreDisplayMissionTick(0.1f);

            Assert.Equal(202, puppetMirror.Action1Index);
            Assert.Equal(303, puppetMirror.SkeletonAction1Index);
            Assert.Equal(303, puppetMirror.RawVisualAction1Index);
            Assert.Equal(0.65f, puppetMirror.RawVisualAction1Progress, precision: 3);
            Assert.Equal(0, puppetMirror.InstallRawVisualActionCalls);
            Assert.Equal(0, puppetMirror.InstallAgentVisualActionCalls);
            Assert.Equal(0, puppetMirror.SetActionChannelCalls);
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
    public void GuardApply_SkipsRedundantGuardCommandsAndReset()
    {
        RunScenario(null, context =>
        {
            Agent puppet = SpawnAgent(context, AgentControllerType.None, out MirrorAgent mirror);

            AgentActionData.ApplyGuardState(puppet, Agent.GuardMode.None);
            Assert.Equal(0, mirror.ResetGuardCalls);

            AgentActionData.ApplyGuardState(puppet, Agent.GuardMode.Up);
            AgentActionData.ApplyGuardState(puppet, Agent.GuardMode.Up);
            Assert.Equal(1, mirror.SetWeaponGuardCalls);

            AgentActionData.ApplyGuardState(puppet, Agent.GuardMode.None);
            AgentActionData.ApplyGuardState(puppet, Agent.GuardMode.None);
            Assert.Equal(Agent.GuardMode.None, mirror.GuardMode);
            Assert.Equal(1, mirror.ResetGuardCalls);
        });
    }

    private static NetworkAgentGuardReaction
        CreateGuardReactionMessage(
            Guid attackerId,
            Guid defenderId,
            long sequence,
            bool isMounted = false,
            string sourceControllerId = "attacker",
            int battleHostEpoch = 0,
            float progress = 0.2f)
    {
        return new NetworkAgentGuardReaction(
            sourceControllerId,
            sequence: sequence,
            battleHostEpoch,
            attackerAgentId: attackerId,
            agentId: defenderId,
            reactionChannel: 1,
            reactionActionIndex: 3104,
            progress,
            animationFlags:
                (ulong)AnimFlags.amf_priority_defend,
            isMounted);
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
