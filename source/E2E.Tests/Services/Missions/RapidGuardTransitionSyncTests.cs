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
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions;

/// <summary>
/// Verifies that reliable action updates continuously assert the owner's native guard on a Controller.None puppet.
/// </summary>
public class RapidGuardTransitionSyncTests : MissionTestEnvironment
{
    public RapidGuardTransitionSyncTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void RemotePlayerRelease_DoesNotReplayLingeringGuardAction()
    {
        RunScenario("peer", context =>
        {
            var agentId = Guid.NewGuid();
            Agent puppet = SpawnRegisteredAgent(
                context, "owner", agentId, AgentControllerType.None,
                out MirrorAgent puppetMirror);
            Agent owner = SpawnAgent(
                context, AgentControllerType.Player,
                out MirrorAgent ownerMirror);

            Agent.MovementControlFlag heldFlags =
                Agent.MovementControlFlag.DefendBlock
                | Agent.MovementControlFlag.DefendRight;
            ownerMirror.MovementFlags = heldFlags;
            ownerMirror.GuardMode = Agent.GuardMode.Right;
            ownerMirror.Action1Index = 202;
            ownerMirror.Action1CodeType = Agent.ActionCodeType.Guard;
            ownerMirror.Action1Direction = Agent.UsageDirection.DefendRight;
            ApplyOwnerAction(context.Component, 1L, agentId, owner);
            context.Component.AgentActionHandler.ApplyRemoteGuardStates();

            puppetMirror.Action1Index = -1;
            puppetMirror.Action1CodeType = Agent.ActionCodeType.Idle;
            puppetMirror.SetActionChannelCalls = 0;
            var release = new AgentActionData(
                owner,
                Agent.MovementControlFlag.None,
                Agent.GuardMode.None);

            context.Component.AgentActionHandler.HandlePacket(null,
                new AgentActionPacket(
                    "owner",
                    new[] { agentId },
                    new[] { release },
                    new[] { 2L }));
            context.Component.AgentActionHandler.ApplyRemoteGuardStates();

            Assert.Equal(-1, puppetMirror.Action1Index);
            Assert.Equal(0, puppetMirror.SetActionChannelCalls);
            Assert.Equal(Agent.GuardMode.None, puppetMirror.GuardMode);
            Assert.Equal(
                Agent.MovementControlFlag.None,
                AgentActionData.GetDefendMovementFlags(
                    puppetMirror.MovementFlags));
        });
    }

    [Fact]
    public void PollActions_MountedGuardDirectionChange_UsesInputBeforeNativeGuardCatchesUp()
    {
        RunScenario("owner", context =>
        {
            var agentId = Guid.NewGuid();

            Agent agent = SpawnRegisteredAgent(
                context, "owner", agentId, AgentControllerType.Player,
                out MirrorAgent mirror);
            context.Mock.SpawnMount(agent);

            mirror.GuardMode = Agent.GuardMode.Left;
            mirror.MovementFlags =
                Agent.MovementControlFlag.DefendBlock
                | Agent.MovementControlFlag.DefendLeft;
            mirror.Action1Index = 202;
            mirror.Action1CodeType =
                Agent.ActionCodeType.DefendLeft1h;
            mirror.Action1Direction = Agent.UsageDirection.DefendLeft;
            context.Component.AgentActionHandler.PollActions();

            AgentActionData leftGuard = Assert.Single(
                Assert.Single(
                    context.Network.NetworkSentPackets
                        .GetPackets<AgentActionPacket>())
                    .Actions);
            Assert.Equal(Agent.GuardMode.Left, leftGuard.GuardMode);
            context.Network.NetworkSentPackets.Packets.Clear();

            mirror.MovementFlags =
                Agent.MovementControlFlag.DefendBlock
                | Agent.MovementControlFlag.DefendRight;
            context.Component.AgentActionHandler.PollActions();

            AgentActionPacket packet = Assert.Single(
                context.Network.NetworkSentPackets.GetPackets<AgentActionPacket>());
            AgentActionData rightGuard = Assert.Single(packet.Actions);
            Assert.Equal(
                Agent.MovementControlFlag.DefendBlock
                    | Agent.MovementControlFlag.DefendRight,
                rightGuard.DefendFlags);
            Assert.Equal(Agent.GuardMode.Right, rightGuard.GuardMode);
            Assert.Equal(2L, Assert.Single(packet.Sequences));

            context.Network.NetworkSentPackets.Packets.Clear();
            context.Component.AgentActionHandler.PollActions();
            Assert.Empty(
                context.Network.NetworkSentPackets
                    .GetPackets<AgentActionPacket>());

            mirror.Action1Direction = Agent.UsageDirection.DefendRight;
            context.Component.AgentActionHandler.PollActions();
            Assert.Empty(
                context.Network.NetworkSentPackets
                    .GetPackets<AgentActionPacket>());

            mirror.MovementFlags =
                Agent.MovementControlFlag.DefendBlock
                | Agent.MovementControlFlag.DefendLeft;
            context.Component.AgentActionHandler.PollActions();

            AgentActionData backToLeft = Assert.Single(
                Assert.Single(
                    context.Network.NetworkSentPackets
                        .GetPackets<AgentActionPacket>())
                    .Actions);
            Assert.Equal(
                Agent.MovementControlFlag.DefendBlock
                    | Agent.MovementControlFlag.DefendLeft,
                backToLeft.DefendFlags);
            Assert.Equal(Agent.GuardMode.Left, backToLeft.GuardMode);
        });
    }

    [Fact]
    public void AfterNativePoll_MountedPlayerRetainsInputDirectionAcrossNativeFlagRewrite()
    {
        RunScenario("owner", context =>
        {
            var agentId = Guid.NewGuid();

            Agent agent = SpawnRegisteredAgent(
                context, "owner", agentId, AgentControllerType.Player,
                out MirrorAgent mirror);
            context.Mock.SpawnMount(agent);

            mirror.GuardMode = Agent.GuardMode.Up;
            mirror.MovementFlags =
                Agent.MovementControlFlag.DefendBlock
                | Agent.MovementControlFlag.DefendUp;
            mirror.Action1Index = 3062;
            mirror.Action1CodeType = Agent.ActionCodeType.DefendUp1h;
            mirror.Action1Direction = Agent.UsageDirection.DefendUp;
            context.Component.AgentActionHandler.PollActions();
            context.Network.NetworkSentPackets.Packets.Clear();

            mirror.MovementFlags =
                Agent.MovementControlFlag.DefendBlock
                | Agent.MovementControlFlag.DefendRight;
            context.Component.AgentActionHandler.PollActions();

            AgentActionPacket requestedRightPacket = Assert.Single(
                context.Network.NetworkSentPackets
                    .GetPackets<AgentActionPacket>());
            AgentActionData requestedRight =
                Assert.Single(requestedRightPacket.Actions);
            Assert.Equal(Agent.GuardMode.Right, requestedRight.GuardMode);
            Assert.Equal(2L, Assert.Single(requestedRightPacket.Sequences));
            context.Network.NetworkSentPackets.Packets.Clear();

            mirror.MovementFlags =
                Agent.MovementControlFlag.DefendBlock
                | Agent.MovementControlFlag.DefendUp;
            context.Component.AgentActionHandler
                .PollActionsAfterNativeTick();

            Assert.Empty(
                context.Network.NetworkSentPackets
                    .GetPackets<AgentActionPacket>());

            mirror.GuardMode = Agent.GuardMode.Right;
            mirror.Action1Index = 3070;
            mirror.Action1CodeType = Agent.ActionCodeType.DefendRight1h;
            mirror.Action1Direction = Agent.UsageDirection.DefendRight;
            context.Component.AgentActionHandler
                .PollActionsAfterNativeTick();

            AgentActionPacket realizedRightPacket = Assert.Single(
                context.Network.NetworkSentPackets
                    .GetPackets<AgentActionPacket>());
            AgentActionData realizedRight =
                Assert.Single(realizedRightPacket.Actions);
            Assert.Equal(3070, realizedRight.Action1Index);
            Assert.Equal(Agent.GuardMode.Right, realizedRight.GuardMode);
            Assert.Equal(3L, Assert.Single(realizedRightPacket.Sequences));
            context.Network.NetworkSentPackets.Packets.Clear();

            mirror.MovementFlags =
                Agent.MovementControlFlag.DefendBlock
                | Agent.MovementControlFlag.DefendUp;
            context.Component.AgentActionHandler.PollActions();

            AgentActionPacket backToUpPacket = Assert.Single(
                context.Network.NetworkSentPackets
                    .GetPackets<AgentActionPacket>());
            AgentActionData backToUp = Assert.Single(backToUpPacket.Actions);
            Assert.Equal(Agent.GuardMode.Up, backToUp.GuardMode);
            Assert.Equal(4L, Assert.Single(backToUpPacket.Sequences));
        });
    }

    [Fact]
    public void MountedPuppet_GuardDirectionChange_PairsActionWithMovementGuardRefresh()
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
            puppetMirror.SetWeaponGuardOverwritesDefendFlags = true;

            ownerMirror.GuardMode = Agent.GuardMode.Left;
            ownerMirror.MovementFlags =
                Agent.MovementControlFlag.DefendBlock
                | Agent.MovementControlFlag.DefendLeft;
            ownerMirror.Action1Index = 3078;
            ownerMirror.Action1CodeType = Agent.ActionCodeType.Guard;
            ownerMirror.Action1Direction = Agent.UsageDirection.DefendLeft;
            puppetMirror.Action1Index = 3078;
            puppetMirror.Action1CodeType = Agent.ActionCodeType.Guard;
            puppetMirror.Action1Direction =
                Agent.UsageDirection.DefendLeft;
            puppetMirror.Action1Stage =
                Agent.ActionStage.DefendParry;
            puppetMirror.RejectSetActionChannelWithoutIgnorePriority = true;

            ApplyOwnerAction(context.Component, 1L, agentId, owner);
            context.Component.AgentActionHandler.ApplyRemoteGuardStates();

            Assert.Equal(Agent.GuardMode.Left, puppetMirror.GuardMode);
            Assert.Equal(1, puppetMirror.SetWeaponGuardCalls);
            Assert.Equal(0, puppetMirror.ResetGuardCalls);
            int actionCommandCount = puppetMirror.SetActionChannelCalls;

            ownerMirror.GuardMode = Agent.GuardMode.Right;
            ownerMirror.MovementFlags =
                Agent.MovementControlFlag.DefendBlock
                | Agent.MovementControlFlag.DefendRight;
            ownerMirror.Action1Direction = Agent.UsageDirection.DefendRight;
            ApplyOwnerAction(context.Component, 2L, agentId, owner);
            context.Component.AgentActionHandler.ApplyRemoteGuardStates();

            Assert.Equal(Agent.GuardMode.Left, puppetMirror.GuardMode);
            Assert.Equal(
                Agent.MovementControlFlag.DefendBlock,
                AgentActionData.GetDefendMovementFlags(
                    puppetMirror.MovementFlags));
            Assert.Equal(1, puppetMirror.SetWeaponGuardCalls);
            Assert.Equal(0, puppetMirror.ResetGuardCalls);
            Assert.Equal(actionCommandCount, puppetMirror.SetActionChannelCalls);

            ownerMirror.Action1Index = 3070;
            ownerMirror.Action1Direction = Agent.UsageDirection.DefendRight;
            var realizedRight = new AgentActionData(owner);
            puppetMirror.SetMovementFlagsCalls = 0;
            Assert.Equal(Agent.GuardMode.Right, realizedRight.GuardMode);
            Assert.Equal(1, realizedRight.GuardActionChannel);
            Assert.False(
                realizedRight.ShouldPreserveCurrentGuardReaction(
                    puppet,
                    1));
            ApplyOwnerAction(context.Component, 3L, agentId, owner);
            context.Component.AgentActionHandler.ApplyRemoteGuardStates();

            Assert.Equal(3078, puppetMirror.Action1Index);
            Assert.Equal(
                Agent.MovementControlFlag.DefendBlock,
                AgentActionData.GetDefendMovementFlags(
                    puppetMirror.MovementFlags));
            Assert.Equal(
                actionCommandCount,
                puppetMirror.SetActionChannelCalls);
            Assert.Equal(1, puppetMirror.SetWeaponGuardCalls);
            Assert.Equal(0, puppetMirror.ResetGuardCalls);
            Assert.Equal(1, puppetMirror.SetMovementFlagsCalls);

            context.Component.AgentActionHandler
                .RefreshRemoteGuardStatesAfterMovement();

            Assert.Equal(3078, puppetMirror.Action1Index);
            Assert.Equal(
                Agent.MovementControlFlag.DefendBlock,
                AgentActionData.GetDefendMovementFlags(
                    puppetMirror.MovementFlags));
            Assert.Equal(1, puppetMirror.SetMovementFlagsCalls);

            context.Component.AgentActionHandler
                .ReplayRemoteGuardReactions();
            context.Component.AgentActionHandler
                .RefreshRemoteGuardStatesAfterMovement();

            Assert.Equal(
                Agent.MovementControlFlag.DefendBlock
                    | Agent.MovementControlFlag.DefendRight,
                AgentActionData.GetDefendMovementFlags(
                    puppetMirror.MovementFlags));
            Assert.Equal(2, puppetMirror.SetMovementFlagsCalls);

            puppetMirror.Action1Index = 3070;
            puppetMirror.Action1CodeType =
                Agent.ActionCodeType.DefendRight1h;
            puppetMirror.Action1Direction =
                Agent.UsageDirection.DefendRight;
            context.Component.AgentActionHandler
                .ReplayRemoteGuardReactions();
            context.Component.AgentActionHandler.ApplyRemoteGuardStates();

            Assert.Equal(3070, puppetMirror.Action1Index);
            Assert.Equal(actionCommandCount, puppetMirror.SetActionChannelCalls);
            Assert.Equal(1, puppetMirror.SetWeaponGuardCalls);
            Assert.Equal(0, puppetMirror.ResetGuardCalls);
        });
    }

    [Fact]
    public void MountedPuppet_GuardRecovery_DoesNotRestartTheActionTimeline()
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
                Agent.MovementControlFlag.Forward
                | Agent.MovementControlFlag.DefendBlock
                | Agent.MovementControlFlag.DefendRight;
            ownerMirror.Action1Index = 3070;
            ownerMirror.Action1CodeType =
                Agent.ActionCodeType.DefendRight1h;
            ownerMirror.Action1Direction =
                Agent.UsageDirection.DefendRight;
            puppetMirror.Action1Index = ownerMirror.Action1Index;
            puppetMirror.Action1CodeType = ownerMirror.Action1CodeType;
            puppetMirror.Action1Direction = ownerMirror.Action1Direction;

            ApplyOwnerAction(context.Component, 1L, agentId, owner);
            context.Component.AgentActionHandler.ApplyRemoteGuardStates();

            int actionCommands = puppetMirror.SetActionChannelCalls;
            int guardResets = puppetMirror.ResetGuardCalls;
            int guardCommands = puppetMirror.SetWeaponGuardCalls;

            puppetMirror.Action1Index = 4000;
            puppetMirror.Action1CodeType =
                Agent.ActionCodeType.BlockedMelee;
            puppetMirror.GuardMode = Agent.GuardMode.None;
            controller.OnMissionTick(0.1f);

            Assert.Equal(Agent.GuardMode.None, puppetMirror.GuardMode);
            Assert.Equal(actionCommands, puppetMirror.SetActionChannelCalls);
            Assert.Equal(guardResets, puppetMirror.ResetGuardCalls);

            puppetMirror.Action1Index = -1;
            puppetMirror.Action1CodeType = Agent.ActionCodeType.Idle;
            controller.OnMissionTick(0.1f);

            Assert.Equal(Agent.GuardMode.None, puppetMirror.GuardMode);
            Assert.Equal(
                guardCommands,
                puppetMirror.SetWeaponGuardCalls);
            Assert.Equal(actionCommands, puppetMirror.SetActionChannelCalls);
            Assert.Equal(guardResets, puppetMirror.ResetGuardCalls);

            controller.OnPreMissionTick(0.1f);

            Assert.Equal(Agent.GuardMode.None, puppetMirror.GuardMode);
            Assert.Equal(
                guardCommands,
                puppetMirror.SetWeaponGuardCalls);
            Assert.Equal(actionCommands, puppetMirror.SetActionChannelCalls);
            Assert.Equal(guardResets, puppetMirror.ResetGuardCalls);

            puppetMirror.Action1Index = 3078;
            puppetMirror.Action1CodeType =
                Agent.ActionCodeType.DefendLeft1h;
            puppetMirror.Action1Direction =
                Agent.UsageDirection.DefendLeft;
            controller.OnMissionTick(0.1f);

            Assert.Equal(3078, puppetMirror.Action1Index);
            Assert.Equal(Agent.GuardMode.None, puppetMirror.GuardMode);
            Assert.Equal(
                Agent.UsageDirection.AttackRight,
                puppetMirror.LastSetWeaponGuardDirection);
            Assert.Equal(actionCommands, puppetMirror.SetActionChannelCalls);
            Assert.Equal(guardResets, puppetMirror.ResetGuardCalls);
        });
    }

    [Fact]
    public void AgentActionData_MountedGuardTransition_LeavesGuardCommandAfterMovementFlags()
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
            ownerMirror.Action1Index = 3070;
            ownerMirror.Action1Progress = 0.05f;
            ownerMirror.Action1Flags =
                AnimFlags.amf_priority_defend | AnimFlags.anf_cyclic;
            ownerMirror.Action1CodeType =
                Agent.ActionCodeType.DefendRight1h;
            ownerMirror.Action1Direction =
                Agent.UsageDirection.DefendRight;

            puppetMirror.Action1Index = 3078;
            puppetMirror.Action1CodeType =
                Agent.ActionCodeType.DefendLeft1h;
            puppetMirror.Action1Direction =
                Agent.UsageDirection.DefendLeft;
            puppetMirror.SetWeaponGuardOverwritesDefendFlags = true;

            new AgentActionData(owner).Apply(
                puppet,
                context.Instance.Container
                    .Resolve<IAgentVisualActionAccessor>());

            Assert.Equal(1, puppetMirror.SetMovementFlagsCalls);
            Assert.True(
                puppetMirror.LastMovementFlagsWriteSequence <
                puppetMirror.LastWeaponGuardWriteSequence);
            Assert.Equal(Agent.GuardMode.Right, puppetMirror.GuardMode);
            Assert.Equal(3070, puppetMirror.Action1Index);
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
            puppetMirror.ActionAnimationIndices[202] = 3220;

            ApplyOwnerAction(context.Component, 1L, agentId, owner);
            context.Component.AgentActionHandler.ApplyRemoteGuardStates();
            int guardCommandsBeforeNativeTick =
                puppetMirror.SetWeaponGuardCalls;
            int guardResetsBeforeNativeTick =
                puppetMirror.ResetGuardCalls;

            // Native can wrap the exact raw guard clip while the held guard remains active.
            puppetMirror.Action1Index = -1;
            puppetMirror.Action1CodeType = Agent.ActionCodeType.Idle;
            puppetMirror.SkeletonAction1Index = -1;
            puppetMirror.RawVisualAction1Index = 3220;
            puppetMirror.RawVisualAction1Progress = 0.01f;
            puppetMirror.SetActionChannelCalls = 0;

            controller.OnMissionTick(0.1f);

            Assert.Equal(
                guardCommandsBeforeNativeTick,
                puppetMirror.SetWeaponGuardCalls);
            Assert.Equal(
                guardResetsBeforeNativeTick,
                puppetMirror.ResetGuardCalls);
            Assert.Equal(-1, puppetMirror.Action1Index);
            Assert.Equal(3220, puppetMirror.RawVisualAction1Index);
            Assert.Equal(0.01f, puppetMirror.RawVisualAction1Progress, precision: 3);
            Assert.Equal(0, puppetMirror.SetActionChannelCalls);

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
    private static void ApplyOwnerAction(
        ICoopMissionComponent component,
        long sequence,
        Guid agentId,
        Agent owner)
    {
        ApplyOwnerAction(component, "owner", sequence, agentId, owner);
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
    private void RunScenario(
        string controllerId,
        Action<BlockingSyncContext> action)
    {
        using var fixture = new MissionEngineFixture();
        EnvironmentInstance instance = Clients.First();
        if (controllerId != null) SetControllerId(instance, controllerId);

        instance.Call(() => action(new BlockingSyncContext(fixture, instance)));
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
