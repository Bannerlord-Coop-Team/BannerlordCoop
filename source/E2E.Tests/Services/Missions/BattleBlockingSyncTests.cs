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
public class BattleBlockingSyncTests : MissionTestEnvironment
{
    public BattleBlockingSyncTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void PollActions_PlayerGuardInput_SendsActionPacket()
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

            mirror.MovementFlags =
                Agent.MovementControlFlag.DefendLeft;
            mirror.GuardMode = Agent.GuardMode.Left;
            context.Component.AgentActionHandler.PollActions();

            AgentActionPacket packet = Assert.Single(
                context.Network.NetworkSentPackets.GetPackets<AgentActionPacket>());
            Assert.Equal(agentId, Assert.Single(packet.AgentIds));
            AgentActionData data = Assert.Single(packet.Actions);
            Assert.Equal(action0, data.Action0Index);
            Assert.Equal(action1, data.Action1Index);
            Assert.Equal(
                Agent.MovementControlFlag.DefendBlock
                    | Agent.MovementControlFlag.DefendLeft,
                data.DefendFlags);
            Assert.Equal(Agent.GuardMode.Left, data.GuardMode);
            Assert.False(data.IsMounted);
            Assert.Equal(1L, Assert.Single(packet.Sequences));
            Assert.Equal(0, packet.BattleHostEpoch);
        });
    }

    [Fact]
    public void PollActions_PreNative_EmitsOnlyMainPlayerInput()
    {
        RunScenario("owner", context =>
        {
            var playerId = Guid.NewGuid();
            var aiId = Guid.NewGuid();

            SpawnRegisteredAgent(
                context,
                "owner",
                playerId,
                AgentControllerType.Player,
                out MirrorAgent playerMirror);
            SpawnRegisteredAgent(
                context,
                "owner",
                aiId,
                AgentControllerType.AI,
                out MirrorAgent aiMirror);

            context.Component.AgentActionHandler.PollActionsAfterNativeTick();
            Assert.Empty(
                context.Network.NetworkSentPackets
                    .GetPackets<AgentActionPacket>());

            playerMirror.MovementFlags =
                Agent.MovementControlFlag.DefendLeft;
            playerMirror.GuardMode = Agent.GuardMode.Left;
            aiMirror.Action0Index = 1001;
            aiMirror.Action0CodeType = Agent.ActionCodeType.ReleaseMelee;

            context.Component.AgentActionHandler.PollActions();

            AgentActionPacket packet = Assert.Single(
                context.Network.NetworkSentPackets
                    .GetPackets<AgentActionPacket>());
            Assert.Equal(playerId, Assert.Single(packet.AgentIds));
            AgentActionData action = Assert.Single(packet.Actions);
            Assert.Equal(Agent.GuardMode.Left, action.GuardMode);
            Assert.DoesNotContain(aiId, packet.AgentIds);
        });
    }

    [Fact]
    public void PollActionsAfterNativeTick_FormerMainPlayer_DoesNotRetainStaleInput()
    {
        RunScenario("owner", context =>
        {
            var formerMainAgentId = Guid.NewGuid();
            SpawnRegisteredAgent(
                context,
                "owner",
                formerMainAgentId,
                AgentControllerType.Player,
                out MirrorAgent formerMainAgentMirror);

            formerMainAgentMirror.MovementFlags =
                Agent.MovementControlFlag.DefendLeft;
            formerMainAgentMirror.GuardMode = Agent.GuardMode.Left;
            context.Component.AgentActionHandler.PollActions();
            context.Network.NetworkSentPackets.Packets.Clear();

            SpawnRegisteredAgent(
                context,
                "owner",
                Guid.NewGuid(),
                AgentControllerType.Player,
                out _);
            formerMainAgentMirror.MovementFlags =
                Agent.MovementControlFlag.None;
            formerMainAgentMirror.GuardMode = Agent.GuardMode.None;

            context.Component.AgentActionHandler.PollActionsAfterNativeTick();

            AgentActionPacket packet = Assert.Single(
                context.Network.NetworkSentPackets
                    .GetPackets<AgentActionPacket>());
            Assert.Equal(
                formerMainAgentId,
                Assert.Single(packet.AgentIds));
            Assert.Equal(
                Agent.GuardMode.None,
                Assert.Single(packet.Actions).GuardMode);
        });
    }

    [Theory]
    [InlineData(Agent.ActionCodeType.ReleaseMelee)]
    [InlineData(Agent.ActionCodeType.EquipUnequip)]
    public void PollActionsAfterNativeTick_LocalAiDiscreteAction_SendsOnce(
        Agent.ActionCodeType actionType)
    {
        RunScenario("owner", context =>
        {
            SpawnRegisteredAgent(
                context,
                "owner",
                Guid.NewGuid(),
                AgentControllerType.Player,
                out _);
            var aiId = Guid.NewGuid();
            SpawnRegisteredAgent(
                context,
                "owner",
                aiId,
                AgentControllerType.AI,
                out MirrorAgent aiMirror);

            context.Component.AgentActionHandler.PollActionsAfterNativeTick();
            Assert.Empty(
                context.Network.NetworkSentPackets
                    .GetPackets<AgentActionPacket>());

            aiMirror.Action0Index = 1001;
            aiMirror.Action0CodeType = actionType;

            context.Component.AgentActionHandler.PollActionsAfterNativeTick();

            AgentActionPacket packet = Assert.Single(
                context.Network.NetworkSentPackets
                    .GetPackets<AgentActionPacket>());
            Assert.Equal(aiId, Assert.Single(packet.AgentIds));
            Assert.Equal(1001, Assert.Single(packet.Actions).Action0Index);
            Assert.Equal(1L, Assert.Single(packet.Sequences));

            context.Network.NetworkSentPackets.Packets.Clear();
            context.Component.AgentActionHandler.PollActionsAfterNativeTick();
            Assert.Empty(
                context.Network.NetworkSentPackets
                    .GetPackets<AgentActionPacket>());
        });
    }

    [Fact]
    public void PollActionsAfterNativeTick_LocalAiGuard_SendsOnce()
    {
        RunScenario("owner", context =>
        {
            SpawnRegisteredAgent(
                context,
                "owner",
                Guid.NewGuid(),
                AgentControllerType.Player,
                out _);
            var aiId = Guid.NewGuid();
            SpawnRegisteredAgent(
                context,
                "owner",
                aiId,
                AgentControllerType.AI,
                out MirrorAgent aiMirror);

            context.Component.AgentActionHandler.PollActionsAfterNativeTick();
            Assert.Empty(
                context.Network.NetworkSentPackets
                    .GetPackets<AgentActionPacket>());

            aiMirror.MovementFlags =
                Agent.MovementControlFlag.DefendBlock
                | Agent.MovementControlFlag.DefendLeft;
            aiMirror.GuardMode = Agent.GuardMode.Left;
            aiMirror.Action1Index = 202;
            aiMirror.Action1CodeType = Agent.ActionCodeType.Guard;
            aiMirror.Action1Direction = Agent.UsageDirection.DefendLeft;

            context.Component.AgentActionHandler.PollActionsAfterNativeTick();

            AgentActionPacket packet = Assert.Single(
                context.Network.NetworkSentPackets
                    .GetPackets<AgentActionPacket>());
            Assert.Equal(aiId, Assert.Single(packet.AgentIds));
            AgentActionData action = Assert.Single(packet.Actions);
            Assert.Equal(
                Agent.MovementControlFlag.DefendBlock
                    | Agent.MovementControlFlag.DefendLeft,
                action.DefendFlags);
            Assert.Equal(Agent.GuardMode.Left, action.GuardMode);
            Assert.Equal(1L, Assert.Single(packet.Sequences));

            context.Network.NetworkSentPackets.Packets.Clear();
            context.Component.AgentActionHandler.PollActionsAfterNativeTick();
            Assert.Empty(
                context.Network.NetworkSentPackets
                    .GetPackets<AgentActionPacket>());
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
    [InlineData(Agent.MovementControlFlag.DefendRight)]
    [InlineData(Agent.MovementControlFlag.None)]
    public void PollActions_UnmountedSwordGuard_RawReleaseIgnoresNativeDefend(
        Agent.MovementControlFlag nativeDefendAfterRawInputClears)
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
            mirror.MovementFlags =
                Agent.MovementControlFlag.DefendRight;
            mirror.DefendMovementFlag =
                Agent.MovementControlFlag.DefendRight;
            context.Component.AgentActionHandler.PollActions();

            AgentActionPacket heldPacket = Assert.Single(
                context.Network.NetworkSentPackets.GetPackets<AgentActionPacket>());
            AgentActionData heldAction = Assert.Single(heldPacket.Actions);
            Assert.Equal(
                Agent.MovementControlFlag.DefendBlock
                    | Agent.MovementControlFlag.DefendRight,
                heldAction.DefendFlags);
            Assert.Equal(Agent.GuardMode.Right, heldAction.GuardMode);

            mirror.MovementFlags = Agent.MovementControlFlag.None;
            mirror.DefendMovementFlag = nativeDefendAfterRawInputClears;
            context.Component.AgentActionHandler.PollActions();

            AgentActionPacket[] packets = context.Network.NetworkSentPackets
                .GetPackets<AgentActionPacket>()
                .ToArray();
            Assert.Equal(2, packets.Length);
            AgentActionData lastAction = Assert.Single(packets.Last().Actions);
            Assert.Equal(
                Agent.MovementControlFlag.None,
                lastAction.DefendFlags);
            Assert.Equal(Agent.GuardMode.None, lastAction.GuardMode);
        });
    }

    [Theory]
    [InlineData(
        Agent.MovementControlFlag.DefendBlock,
        Agent.ActionCodeType.DefendShield,
        Agent.UsageDirection.DefendRight,
        Agent.MovementControlFlag.None,
        Agent.MovementControlFlag.DefendBlock
            | Agent.MovementControlFlag.DefendRight,
        Agent.GuardMode.Right)]
    [InlineData(
        Agent.MovementControlFlag.DefendRight,
        Agent.ActionCodeType.Guard,
        Agent.UsageDirection.DefendRight,
        Agent.MovementControlFlag.DefendRight,
        Agent.MovementControlFlag.DefendBlock
            | Agent.MovementControlFlag.DefendRight,
        Agent.GuardMode.Right)]
    [InlineData(
        Agent.MovementControlFlag.DefendBlock,
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
            Assert.Equal(
                Agent.MovementControlFlag.DefendBlock
                    | Agent.MovementControlFlag.DefendLeft,
                action.DefendFlags);
            Assert.Equal(Agent.GuardMode.Left, action.GuardMode);
            Assert.Equal(1, action.GuardPresentationChannel);
            Assert.Equal(1, action.GuardActionChannel);
            Assert.True(action.GuardActionIsDefending);
            Assert.True(action.IsMounted);
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
                Agent.MovementControlFlag.Forward | defendFlags,
                puppetMirror.MovementFlags);
            Assert.Equal(guardCommandCount, puppetMirror.SetWeaponGuardCalls);

            controller.OnPreDisplayMissionTick(0f);
            Assert.Equal(
                Agent.MovementControlFlag.Forward | defendFlags,
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
    public void MissionTicks_OnFootGuardDecay_RetainsInputWithoutReplayingAction()
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
                Agent.MovementControlFlag.Forward
                    | Agent.MovementControlFlag.DefendBlock
                    | Agent.MovementControlFlag.DefendRight,
                puppetMirror.MovementFlags);

            controller.OnPreDisplayMissionTick(0.1f);

            Assert.Equal(-1, puppetMirror.Action1Index);
            Assert.Equal(0f, puppetMirror.Action1Progress);
            Assert.Equal(0, puppetMirror.SetActionChannelCalls);
            Assert.Equal(-1, puppetMirror.SkeletonAction1Index);
            Assert.Equal(-1, puppetMirror.RawVisualAction1Index);
            Assert.Equal(0, puppetMirror.InstallRawVisualActionCalls);
            Assert.Equal(
                Agent.MovementControlFlag.Forward
                    | Agent.MovementControlFlag.DefendBlock
                    | Agent.MovementControlFlag.DefendRight,
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
            context.Component.AgentActionHandler.ApplyRemoteGuardStates();

            Assert.Equal(expectedGuardMode, puppetMirror.GuardMode);
            Assert.Equal(
                Agent.MovementControlFlag.Forward
                    | Agent.MovementControlFlag.DefendBlock
                    | defendDirection,
                puppetMirror.MovementFlags);
            Assert.Equal(2, puppetMirror.SetWeaponGuardCalls);

            context.Component.AgentActionHandler.ApplyRemoteGuardStates();

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

    [Fact]
    public void ReactionSnapshot_DoesNotRestartGuardUntilDefendingActionReturns()
    {
        RunScenario("peer", context =>
        {
            var agentId = Guid.NewGuid();

            Agent puppet = SpawnRegisteredAgent(
                context, "owner", agentId, AgentControllerType.None,
                out MirrorAgent puppetMirror);
            Agent owner = SpawnAgent(
                context, AgentControllerType.Player, out MirrorAgent ownerMirror);
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
            puppetMirror.Action1CodeType = Agent.ActionCodeType.StrikeMedium;
            puppetMirror.GuardMode = Agent.GuardMode.None;

            ApplyOwnerAction(context.Component, 2L, agentId, owner);
            context.Component.AgentActionHandler.ApplyRemoteGuardStates();
            context.Component.AgentActionHandler.ApplyRemoteGuardStates();

            Assert.Equal(Agent.GuardMode.None, puppetMirror.GuardMode);
            Assert.Equal(1, puppetMirror.SetWeaponGuardCalls);

            puppetMirror.Action1Index = -1;
            puppetMirror.Action1CodeType = Agent.ActionCodeType.Idle;
            context.Component.AgentActionHandler.ApplyRemoteGuardStates();

            Assert.Equal(Agent.GuardMode.Up, puppetMirror.GuardMode);
            Assert.Equal(2, puppetMirror.SetWeaponGuardCalls);
        });
    }

    [Fact]
    public void MountedReactionDirectionChange_DoesNotReplayPreviousGuardClip()
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
            context.Component.AgentActionHandler.ApplyRemoteGuardStates();

            Assert.Equal(Agent.GuardMode.Up, puppetMirror.GuardMode);
            Assert.Equal(2, puppetMirror.SetWeaponGuardCalls);
            Assert.Equal(-1, puppetMirror.Action1Index);
            Assert.Equal(0, puppetMirror.SetActionChannelCalls);
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
            mirror.MovementFlags =
                Agent.MovementControlFlag.DefendRight;
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
            mirror.MovementFlags =
                Agent.MovementControlFlag.DefendRight;
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
            var blow = new Blow(attacker.Index);
            var collisionData = new AttackCollisionData
            {
                _collisionResult = (int)collisionResult
            };

            context.Component.AgentActionHandler.ObserveBlockedHit(
                defender,
                attacker,
                isBlocked: true,
                in blow,
                in collisionData);

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

    [Fact]
    public void CollisionAuthority_RemoteDefenderWithoutNativeReaction_UsesGuardParry()
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
            defenderMirror.Action1Index = 3102;
            defenderMirror.Action1CodeType =
                Agent.ActionCodeType.Guard;
            defenderMirror.Action1Stage =
                Agent.ActionStage.Defend;
            var blow = new Blow(attacker.Index);
            var collisionData = new AttackCollisionData
            {
                _collisionResult =
                    (int)CombatCollisionResult.Blocked
            };

            context.Component.AgentActionHandler.ObserveBlockedHit(
                defender,
                attacker,
                isBlocked: true,
                in blow,
                in collisionData);
            context.Component.AgentActionHandler
                .ReplayRemoteGuardReactions();

            NetworkAgentGuardReaction message = Assert.Single(
                context.Network.NetworkSentMessages
                    .GetMessages<NetworkAgentGuardReaction>());
            Assert.Equal(attackerId, message.AttackerAgentId);
            Assert.Equal(defenderId, message.AgentId);
            Assert.Equal(1, message.ReactionChannel);
            Assert.Equal(3104, message.ReactionActionIndex);
            Assert.Equal(0f, message.Progress);
            Assert.Equal(
                (ulong)AnimFlags.amf_priority_defend,
                message.AnimationFlags);
            Assert.Equal(1, defenderMirror.SetActionChannelCalls);
            Assert.Equal(3104, defenderMirror.Action1Index);
        });
    }

    [Theory]
    [InlineData(
        "act_defend_up_1h_passive",
        "act_defend_up_1h_parry_light")]
    [InlineData(
        "act_defend_right_2h_active_left_stance",
        "act_defend_right_2h_parry_light_left_stance")]
    [InlineData(
        "act_defend_shield_left_1h_passive_down",
        "act_defend_shield_left_1h_parry_light_down")]
    [InlineData(null, null)]
    [InlineData("act_strike_left_1h", null)]
    public void GuardReactionActionResolver_MapsHeldGuardToLightParry(
        string? guardActionName,
        string? expected)
    {
        Assert.Equal(
            expected,
            GuardReactionActionResolver
                .GetParryLightActionName(guardActionName));
    }

    [Fact]
    public void CollisionAuthority_LocalDefenderWaitsForNativeReaction()
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
                "attacker",
                Guid.NewGuid(),
                AgentControllerType.Player,
                out MirrorAgent defenderMirror);
            defenderMirror.Action1Index = 3102;
            defenderMirror.Action1CodeType =
                Agent.ActionCodeType.Guard;
            defenderMirror.Action1Stage =
                Agent.ActionStage.Defend;
            var blow = new Blow(attacker.Index);
            var collisionData = new AttackCollisionData
            {
                _collisionResult =
                    (int)CombatCollisionResult.Blocked
            };

            context.Component.AgentActionHandler.ObserveBlockedHit(
                defender,
                attacker,
                isBlocked: true,
                in blow,
                in collisionData);
            context.Component.AgentActionHandler
                .ReplayRemoteGuardReactions();

            Assert.Empty(
                context.Network.NetworkSentMessages
                    .GetMessages<NetworkAgentGuardReaction>());
            Assert.Equal(0, defenderMirror.SetActionChannelCalls);
            Assert.Equal(3102, defenderMirror.Action1Index);
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
            var blow = new Blow(attacker.Index);
            blow.WeaponRecord._isMissile = isMissile;
            var collisionData = new AttackCollisionData
            {
                _collisionResult = (int)collisionResult
            };

            context.Component.AgentActionHandler.ObserveBlockedHit(
                defender,
                attacker,
                isBlocked,
                in blow,
                in collisionData);
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

    private static NetworkAgentGuardReaction CreateGuardReactionMessage(
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
            animationFlags: (ulong)AnimFlags.amf_priority_defend,
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
        if (controllerType == AgentControllerType.Player
            && context.Registry.IsLocallyControlled(agent))
        {
            context.Mock.MainAgent = agent;
        }
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
