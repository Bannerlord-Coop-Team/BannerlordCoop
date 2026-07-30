using System;
using System.Linq;
using System.Reflection;
using Common.PacketHandlers;
using Common.Serialization;
using E2E.Tests.Environment.Mock;
using E2E.Tests.Environment.MockEngine;
using Missions;
using Missions.Agents;
using Missions.Agents.Packets;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using Xunit;
using Xunit.Abstractions;
using AgentData = Missions.Agents.Packets.AgentData;

namespace E2E.Tests.Services.Missions;

public class MountedPuppetMovementTests : MissionTestEnvironment
{
    public MountedPuppetMovementTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void MovementPacket_DisablesPuppetHorseAi_AndRestoresTheOwnerDirectionsAfterTeleport()
    {
        using var fixture = new MissionEngineFixture();
        var peer = Clients.First();
        SetControllerId(peer, "peer");

        peer.Call(() =>
        {
            var mock = fixture.CreateMission(peer);
            var registry = peer.Resolve<INetworkAgentRegistry>();
            var component = peer.Resolve<ICoopMissionComponent>();
            var riderId = Guid.NewGuid();
            var horseId = Guid.NewGuid();

            Agent puppetRider = SpawnRider(mock);
            Agent puppetHorse = mock.SpawnMount(puppetRider);
            Assert.True(AgentMirror.TryGet(puppetRider, out var puppetRiderMirror));
            Assert.True(AgentMirror.TryGet(puppetHorse, out var puppetHorseMirror));
            puppetHorseMirror.Controller = AgentControllerType.AI;
            Assert.True(registry.TryRegisterAgent("owner", riderId, puppetRider));
            Assert.True(registry.TryRegisterAgent("owner", horseId, puppetHorse));

            Agent sourceHorse = mock.SpawnMount();
            Assert.True(AgentMirror.TryGet(sourceHorse, out var sourceHorseMirror));
            sourceHorseMirror.Position = new Vec3(1f, 0f, 0f);
            sourceHorseMirror.MovementDirection = new Vec2(0f, 1f);
            sourceHorseMirror.LookDirection = new Vec3(0f, 1f, 0f);
            sourceHorseMirror.RealGlobalVelocity = new Vec3(3f, 4f, 12f);

            AgentData data = CreateMountedData(
                riderPosition: new Vec3(1f, 0f, 1f),
                riderDirection: new Vec2(1f, 0f),
                ownerSpeed: 2f,
                mountData: new AgentMountData(sourceHorse, horseId),
                riderLookDirection: new Vec3(-1f, 0f, 0f));

            component.AgentMovementHandler.HandlePacket(
                null,
                new MovementPacket(new[] { riderId }, new[] { data }));

            Assert.Equal(AgentControllerType.None, puppetHorseMirror.Controller);
            Assert.Equal(5f, puppetHorseMirror.MaximumSpeedLimit);
            Assert.False(puppetHorseMirror.LastMaximumSpeedLimitIsMultiplier);
            Assert.Equal(1, puppetHorseMirror.SetMaximumSpeedLimitCalls);

            component.AgentMovementHandler.Interpolator.Tick(1f / 60f);

            Assert.Equal(data.MovementDirection, puppetRiderMirror.MovementDirection);
            Assert.Equal(sourceHorseMirror.MovementDirection, puppetHorseMirror.MovementDirection);
            Assert.Equal(1, puppetHorseMirror.TeleportToPositionCalls);
            Assert.InRange(puppetHorseMirror.Position.X, 0.19f, 0.21f);
        });
    }

    [Fact]
    public void ApplyMount_CapsAStationaryPuppetAtZeroUsingHorizontalSpeed()
    {
        using var fixture = new MissionEngineFixture();
        var peer = Clients.First();

        peer.Call(() =>
        {
            var mock = fixture.CreateMission(peer);
            Agent sourceHorse = mock.SpawnMount();
            Agent puppetHorse = mock.SpawnMount();
            Assert.True(AgentMirror.TryGet(sourceHorse, out var sourceHorseMirror));
            Assert.True(AgentMirror.TryGet(puppetHorse, out var puppetHorseMirror));
            sourceHorseMirror.RealGlobalVelocity = new Vec3(0f, 0f, 3f);
            puppetHorseMirror.Controller = AgentControllerType.None;

            new AgentMountData(sourceHorse).ApplyMount(puppetHorse);

            Assert.Equal(0f, puppetHorseMirror.MaximumSpeedLimit);
            Assert.False(puppetHorseMirror.LastMaximumSpeedLimitIsMultiplier);
            Assert.Equal(1, puppetHorseMirror.SetMaximumSpeedLimitCalls);
        });
    }

    [Fact]
    public void AgentData_AppliesRiderAndMountLocomotionFlagsWithoutClearingDefend()
    {
        using var fixture = new MissionEngineFixture();
        var peer = Clients.First();

        peer.Call(() =>
        {
            var mock = fixture.CreateMission(peer);
            Agent sourceRider = SpawnRider(mock);
            Agent sourceHorse = mock.SpawnMount(sourceRider);
            Agent puppetRider = SpawnRider(mock);
            Agent puppetHorse = mock.SpawnMount(puppetRider);
            Assert.True(AgentMirror.TryGet(sourceRider, out var sourceRiderMirror));
            Assert.True(AgentMirror.TryGet(sourceHorse, out var sourceHorseMirror));
            Assert.True(AgentMirror.TryGet(puppetRider, out var puppetRiderMirror));
            Assert.True(AgentMirror.TryGet(puppetHorse, out var puppetHorseMirror));
            puppetRiderMirror.ClearLocomotionFlagsOnContinuousStateWrite = true;
            puppetHorseMirror.ClearLocomotionFlagsOnContinuousStateWrite = true;

            sourceRiderMirror.MovementFlags =
                Agent.MovementControlFlag.Forward |
                Agent.MovementControlFlag.TurnLeft |
                Agent.MovementControlFlag.DefendBlock |
                Agent.MovementControlFlag.DefendUp;
            sourceHorseMirror.MovementFlags =
                Agent.MovementControlFlag.Forward |
                Agent.MovementControlFlag.TurnLeft;
            puppetRiderMirror.MovementFlags =
                Agent.MovementControlFlag.Backward |
                Agent.MovementControlFlag.DefendBlock |
                Agent.MovementControlFlag.DefendRight;
            puppetHorseMirror.MovementFlags =
                Agent.MovementControlFlag.Backward |
                Agent.MovementControlFlag.DefendDown;

            new AgentData(sourceRider).Apply(puppetRider);

            Assert.Equal(
                Agent.MovementControlFlag.Forward |
                Agent.MovementControlFlag.TurnLeft |
                Agent.MovementControlFlag.DefendBlock |
                Agent.MovementControlFlag.DefendRight,
                puppetRiderMirror.MovementFlags);
            Assert.Equal(
                Agent.MovementControlFlag.Forward |
                Agent.MovementControlFlag.TurnLeft |
                Agent.MovementControlFlag.DefendDown,
                puppetHorseMirror.MovementFlags);
        });
    }

    [Fact]
    public void ApplyMount_SettlesAStationaryPuppetGaitAndClearsItsInput()
    {
        using var fixture = new MissionEngineFixture();
        var peer = Clients.First();

        peer.Call(() =>
        {
            var mock = fixture.CreateMission(peer);
            Agent sourceHorse = mock.SpawnMount();
            Agent puppetHorse = mock.SpawnMount();
            Assert.True(AgentMirror.TryGet(sourceHorse, out var sourceHorseMirror));
            Assert.True(AgentMirror.TryGet(puppetHorse, out var puppetHorseMirror));
            sourceHorseMirror.RealGlobalVelocity = Vec3.Zero;
            sourceHorseMirror.Action0Index = 101;
            sourceHorseMirror.InputVector = Vec2.Forward;
            puppetHorseMirror.Action0Index = 101;
            puppetHorseMirror.InputVector = Vec2.Forward;

            new AgentMountData(
                sourceHorse,
                mountAction0Speed: 1f,
                mountAction0IsLocomotion: true).ApplyMount(puppetHorse);

            Assert.Equal(ActionIndexCache.act_none.Index, puppetHorseMirror.Action0Index);
            Assert.Equal(Vec2.Zero, puppetHorseMirror.InputVector);
            Assert.Equal(1, puppetHorseMirror.SetActionChannelCalls);
            Assert.Equal(0, puppetHorseMirror.LastSetActionChannel);
        });
    }

    [Fact]
    public void ApplyMount_PreservesStationaryTurnInput()
    {
        using var fixture = new MissionEngineFixture();
        var peer = Clients.First();

        peer.Call(() =>
        {
            var mock = fixture.CreateMission(peer);
            Agent sourceHorse = mock.SpawnMount();
            Agent puppetHorse = mock.SpawnMount();
            Assert.True(AgentMirror.TryGet(sourceHorse, out var sourceHorseMirror));
            Assert.True(AgentMirror.TryGet(puppetHorse, out var puppetHorseMirror));
            sourceHorseMirror.RealGlobalVelocity = Vec3.Zero;
            sourceHorseMirror.Action0Index = 101;
            sourceHorseMirror.Action0Progress = 0.9f;
            sourceHorseMirror.Action0Flags =
                AnimFlags.amf_priority_defend | AnimFlags.anf_cyclic;
            sourceHorseMirror.InputVector = new Vec2(0.4f, 0.6f);
            puppetHorseMirror.Action0Index = ActionIndexCache.act_none.Index;

            var mountData = new AgentMountData(
                sourceHorse,
                mountAction0Speed: 0.8f,
                mountAction0IsLocomotion: true,
                mountAction0TurnDirection: AgentMountData.TurnRight,
                mountAction0TurnActionIndex: 902);
            mountData.ApplyMount(puppetHorse);

            Assert.Equal(0UL, mountData.MountAction0Flag);
            Assert.Equal(0f, mountData.MountAction0Progress);
            Assert.Equal(1f, mountData.MountAction0Speed);
            Assert.False(mountData.MountAction0IsLocomotion);
            Assert.Equal(sourceHorseMirror.InputVector, puppetHorseMirror.InputVector);
            Assert.Equal(
                Agent.MovementControlFlag.TurnRight,
                puppetHorseMirror.MovementFlags);
            Assert.Equal(1, puppetHorseMirror.SetActionChannelCalls);
            Assert.Equal(1, puppetHorseMirror.SetMovementFlagsCalls);
            Assert.True(puppetHorseMirror.LastSetActionIgnorePriority);
            Assert.Equal(0f, puppetHorseMirror.LastSetActionStartProgress);
            Assert.Equal((AnimFlags)0, puppetHorseMirror.LastSetActionFlags);

            puppetHorseMirror.Action0Progress = 0.4f;
            mountData.ApplyMount(puppetHorse);

            Assert.Equal(0.4f, puppetHorseMirror.Action0Progress);
            Assert.Equal(0, puppetHorseMirror.SetCurrentActionProgressCalls);
            Assert.Equal(1, puppetHorseMirror.SetMovementFlagsCalls);

            sourceHorseMirror.Action0Index = 902;
            sourceHorseMirror.Action0Progress = 0.35f;
            sourceHorseMirror.Action0Flags = AnimFlags.anf_cyclic;
            var nativeTurnData = new AgentMountData(
                sourceHorse,
                mountAction0Speed: 0.7f,
                mountAction0TurnDirection: AgentMountData.TurnRight,
                mountAction0TurnActionIndex: 902);
            nativeTurnData.ApplyMount(puppetHorse);

            Assert.Equal(2, puppetHorseMirror.SetActionChannelCalls);
            Assert.Equal(0.35f, puppetHorseMirror.LastSetActionStartProgress);
            Assert.Equal(AnimFlags.anf_cyclic, puppetHorseMirror.LastSetActionFlags);
            Assert.Equal(0.7f, puppetHorseMirror.Action0Speed);
        });
    }

    [Fact]
    public void ApplyMount_ClearsThePuppetGaitWhenTheOwnerActionIsNone()
    {
        using var fixture = new MissionEngineFixture();
        var peer = Clients.First();

        peer.Call(() =>
        {
            var mock = fixture.CreateMission(peer);
            Agent sourceHorse = mock.SpawnMount();
            Agent puppetHorse = mock.SpawnMount();
            Assert.True(AgentMirror.TryGet(sourceHorse, out var sourceHorseMirror));
            Assert.True(AgentMirror.TryGet(puppetHorse, out var puppetHorseMirror));
            sourceHorseMirror.RealGlobalVelocity = Vec3.Zero;
            sourceHorseMirror.Action0Index = ActionIndexCache.act_none.Index;
            puppetHorseMirror.Action0Index = 101;

            new AgentMountData(sourceHorse).ApplyMount(puppetHorse);

            Assert.Equal(ActionIndexCache.act_none.Index, puppetHorseMirror.Action0Index);
            Assert.Equal(1, puppetHorseMirror.SetActionChannelCalls);
        });
    }

    [Fact]
    public void ApplyMount_UpdatesThePlaybackSpeedOfAnExistingMovingGait()
    {
        using var fixture = new MissionEngineFixture();
        var peer = Clients.First();

        peer.Call(() =>
        {
            var mock = fixture.CreateMission(peer);
            Agent sourceHorse = mock.SpawnMount();
            Agent puppetHorse = mock.SpawnMount();
            Assert.True(AgentMirror.TryGet(sourceHorse, out var sourceHorseMirror));
            Assert.True(AgentMirror.TryGet(puppetHorse, out var puppetHorseMirror));
            sourceHorseMirror.RealGlobalVelocity = new Vec3(3f, 4f, 0f);
            sourceHorseMirror.Action0Index = 101;
            puppetHorseMirror.Action0Index = 101;

            new AgentMountData(sourceHorse, mountAction0Speed: 0.65f).ApplyMount(puppetHorse);

            Assert.Equal(0.65f, puppetHorseMirror.Action0Speed);
            Assert.Equal(1, puppetHorseMirror.SetCurrentActionSpeedCalls);
            Assert.Equal(0, puppetHorseMirror.SetActionChannelCalls);
        });
    }

    [Fact]
    public void IsLocomotionAction_RecognizesWalkStrafeActionsWithCrossAnimations()
    {
        Assert.True(AgentMountData.IsLocomotionAction(
            "act_horse_forward_walk_strafe_right",
            "horse_strafe_r_cross_fast"));
    }

    [Theory]
    [InlineData("act_horse_turn_right", "", AgentMountData.TurnRight)]
    [InlineData("act_horse_turn_left", "", AgentMountData.TurnLeft)]
    [InlineData("", "rider_horse_rotate_right", AgentMountData.TurnRight)]
    [InlineData("", "rider_horse_rotate_left", AgentMountData.TurnLeft)]
    [InlineData("act_horse_forward_walk", "rider_forward_walk", AgentMountData.NoTurn)]
    public void GetTurnDirection_ClassifiesNativeStationaryTurnActions(
        string actionName,
        string animationName,
        int expected)
    {
        Assert.Equal(
            expected,
            AgentMountData.GetTurnDirection(actionName, animationName));
    }

    [Theory]
    [InlineData(0f, 1f, -1f, 0f, AgentMountData.TurnLeft)]
    [InlineData(0f, 1f, 1f, 0f, AgentMountData.TurnRight)]
    [InlineData(0f, 1f, 0.001f, 1f, AgentMountData.NoTurn)]
    [InlineData(0f, 1f, 0f, -1f, AgentMountData.TurnRight)]
    public void GetTurnDirection_DerivesStationaryTurnsFromFacingChanges(
        float previousX,
        float previousY,
        float currentX,
        float currentY,
        int expected)
    {
        Assert.Equal(
            expected,
            AgentMountData.GetTurnDirection(
                new Vec2(previousX, previousY),
                new Vec2(currentX, currentY)));
    }

    [Fact]
    public void PollMovement_StartsAndBroadcastsAStationaryTurnWhenFacingChangesDuringIdle()
    {
        using var fixture = new MissionEngineFixture();
        var peer = Clients.First();
        SetControllerId(peer, "peer");

        peer.Call(() =>
        {
            var mock = fixture.CreateMission(peer);
            var registry = peer.Resolve<INetworkAgentRegistry>();
            var component = peer.Resolve<ICoopMissionComponent>();
            var network = Assert.IsType<MockBattleNetwork>(peer.Resolve<IBattleNetwork>());
            var horseId = Guid.NewGuid();

            Agent sourceHorse = mock.SpawnMount();
            Assert.True(AgentMirror.TryGet(sourceHorse, out var sourceHorseMirror));
            sourceHorseMirror.MovementDirection = Vec2.Forward;
            sourceHorseMirror.RealGlobalVelocity = new Vec3(1f, 0f, 0f);
            sourceHorseMirror.Action0Index = 101;
            Assert.True(registry.TryRegisterAgent("peer", horseId, sourceHorse));

            component.AgentMovementHandler.PollMovement(0f);
            sourceHorseMirror.RealGlobalVelocity = Vec3.Zero;
            component.AgentMovementHandler.PollMovement(0.025f);
            Assert.Equal(0, sourceHorseMirror.SetActionChannelCalls);
            network.NetworkSentPackets.Packets.Clear();

            sourceHorseMirror.MovementDirection = new Vec2(-0.007f, 0.999975f);
            component.AgentMovementHandler.PollMovement(0.025f);
            Assert.Equal(0, sourceHorseMirror.SetActionChannelCalls);
            sourceHorseMirror.MovementDirection = new Vec2(-0.014f, 0.999902f);
            component.AgentMovementHandler.PollMovement(0.025f);
            Assert.Equal(0, sourceHorseMirror.SetActionChannelCalls);
            network.NetworkSentPackets.Packets.Clear();

            sourceHorseMirror.MovementDirection = new Vec2(-0.021f, 0.99978f);
            component.AgentMovementHandler.PollMovement(0.025f);

            int turnActionIndex = ActionIndexCache.Create("act_horse_turn_left").Index;
            Assert.Equal(101, sourceHorseMirror.Action0Index);
            Assert.Equal(0, sourceHorseMirror.SetActionChannelCalls);
            Assert.Equal(
                Agent.MovementControlFlag.TurnLeft,
                sourceHorseMirror.MovementFlags);

            AgentMountData sentMount = Assert.Single(
                Assert.Single(network.NetworkSentPackets.GetPackets<MountMovementPacket>())
                    .Mounts);
            Assert.Equal(AgentMountData.TurnLeft, sentMount.MountAction0TurnDirection);
            Assert.Equal(turnActionIndex, sentMount.MountAction0TurnActionIndex);

            sourceHorseMirror.MovementDirection = new Vec2(-0.2f, 0.98f);
            component.AgentMovementHandler.PollMovement(0.025f);
            Assert.Equal(0, sourceHorseMirror.SetActionChannelCalls);
            Assert.Equal(
                Agent.MovementControlFlag.TurnLeft,
                sourceHorseMirror.MovementFlags);
        });
    }

    [Fact]
    public void PollMovement_StartsAStationaryTurnWhenFacingChangesAsTheMountStops()
    {
        using var fixture = new MissionEngineFixture();
        var peer = Clients.First();
        SetControllerId(peer, "peer");

        peer.Call(() =>
        {
            var mock = fixture.CreateMission(peer);
            var registry = peer.Resolve<INetworkAgentRegistry>();
            var component = peer.Resolve<ICoopMissionComponent>();
            var network = Assert.IsType<MockBattleNetwork>(peer.Resolve<IBattleNetwork>());
            var horseId = Guid.NewGuid();

            Agent sourceHorse = mock.SpawnMount();
            Assert.True(AgentMirror.TryGet(sourceHorse, out var sourceHorseMirror));
            sourceHorseMirror.MovementDirection = Vec2.Forward;
            sourceHorseMirror.RealGlobalVelocity = new Vec3(1f, 0f, 0f);
            sourceHorseMirror.Action0Index = 101;
            Assert.True(registry.TryRegisterAgent("peer", horseId, sourceHorse));

            component.AgentMovementHandler.PollMovement(0f);
            network.NetworkSentPackets.Packets.Clear();

            sourceHorseMirror.MovementDirection = new Vec2(-1f, 0f);
            sourceHorseMirror.RealGlobalVelocity = Vec3.Zero;
            component.AgentMovementHandler.PollMovement(0.025f);

            int turnActionIndex = ActionIndexCache.Create("act_horse_turn_left").Index;
            Assert.Equal(101, sourceHorseMirror.Action0Index);
            Assert.Equal(0, sourceHorseMirror.SetActionChannelCalls);
            Assert.Equal(
                Agent.MovementControlFlag.TurnLeft,
                sourceHorseMirror.MovementFlags);

            AgentMountData sentMount = Assert.Single(
                Assert.Single(network.NetworkSentPackets.GetPackets<MountMovementPacket>())
                    .Mounts);
            Assert.Equal(AgentMountData.TurnLeft, sentMount.MountAction0TurnDirection);
            Assert.Equal(turnActionIndex, sentMount.MountAction0TurnActionIndex);
        });
    }

    [Fact]
    public void PollMovement_ClearsASyntheticStationaryTurnAfterFacingStabilizes()
    {
        using var fixture = new MissionEngineFixture();
        var peer = Clients.First();
        SetControllerId(peer, "peer");

        peer.Call(() =>
        {
            var mock = fixture.CreateMission(peer);
            var registry = peer.Resolve<INetworkAgentRegistry>();
            var component = peer.Resolve<ICoopMissionComponent>();
            var network = Assert.IsType<MockBattleNetwork>(peer.Resolve<IBattleNetwork>());
            var horseId = Guid.NewGuid();

            Agent sourceHorse = mock.SpawnMount();
            Assert.True(AgentMirror.TryGet(sourceHorse, out var sourceHorseMirror));
            sourceHorseMirror.MovementDirection = Vec2.Forward;
            sourceHorseMirror.RealGlobalVelocity = new Vec3(1f, 0f, 0f);
            sourceHorseMirror.Action0Index = 101;
            Assert.True(registry.TryRegisterAgent("peer", horseId, sourceHorse));

            component.AgentMovementHandler.PollMovement(0f);
            sourceHorseMirror.RealGlobalVelocity = Vec3.Zero;
            component.AgentMovementHandler.PollMovement(0.025f);
            sourceHorseMirror.MovementDirection = new Vec2(-0.2f, 0.98f);
            component.AgentMovementHandler.PollMovement(0.025f);
            Assert.Equal(
                Agent.MovementControlFlag.TurnLeft,
                sourceHorseMirror.MovementFlags);

            network.NetworkSentPackets.Packets.Clear();
            for (int i = 0; i < 20; i++)
                component.AgentMovementHandler.PollMovement(0.025f);

            Assert.Equal(
                Agent.MovementControlFlag.None,
                sourceHorseMirror.MovementFlags);
            AgentMountData finalMount = network.NetworkSentPackets
                .GetPackets<MountMovementPacket>()
                .Last()
                .Mounts
                .Single();
            Assert.Equal(
                AgentMountData.NoTurn,
                finalMount.MountAction0TurnDirection);
        });
    }

    [Theory]
    [InlineData("", "horse", AgentMountData.TurnRight, "act_horse_turn_right")]
    [InlineData("", "horse", AgentMountData.TurnLeft, "act_horse_turn_left")]
    [InlineData("", "camel", AgentMountData.TurnRight, "act_camel_turn_right")]
    [InlineData("", "camel", AgentMountData.TurnLeft, "act_camel_turn_left")]
    [InlineData("act_camel_turn_right", "horse", AgentMountData.TurnRight, "act_camel_turn_right")]
    [InlineData("act_horse_walk_turn_right_head", "horse", AgentMountData.TurnRight, "act_horse_turn_right")]
    public void GetStationaryTurnActionName_UsesNativeMountActions(
        string authoritativeActionName,
        string monsterUsage,
        int direction,
        string expected)
    {
        Assert.Equal(
            expected,
            AgentMountData.GetStationaryTurnActionName(
                authoritativeActionName,
                monsterUsage,
                direction));
    }

    [Theory]
    [InlineData("act_horse_turn_right", true)]
    [InlineData("act_horse_turn_left", true)]
    [InlineData("act_camel_turn_right", true)]
    [InlineData("act_camel_turn_left", true)]
    [InlineData("act_horse_walk_turn_right_head", false)]
    [InlineData("act_camel_trot_turn_left_head", false)]
    [InlineData("act_horse_turn_right_head", false)]
    public void IsStationaryTurnAction_RejectsGaitAndHeadTurnActions(
        string actionName,
        bool expected)
    {
        Assert.Equal(
            expected,
            AgentMountData.IsStationaryTurnAction(actionName));
    }

    [Fact]
    public void ResolveAction0Index_SelectsTheMountSpecificStationaryTurnAction()
    {
        Assert.Equal(
            902,
            AgentMountData.ResolveAction0Index(
                actionIndex: 101,
                speed: 0f,
                isLocomotion: true,
                turnDirection: AgentMountData.TurnRight,
                turnActionIndex: 902));
    }

    [Fact]
    public void ResolveAction0Index_ClearsTheGaitAfterAStationaryTurnSettles()
    {
        Assert.Equal(
            AgentMountData.NoActionIndex,
            AgentMountData.ResolveAction0Index(
                actionIndex: 101,
                speed: 0f,
                isLocomotion: true,
                turnDirection: AgentMountData.NoTurn,
                turnActionIndex: 902));
    }

    [Fact]
    public void MountedFollow_ReplaysTheLatestOwnerInputsAfterNativeClearsThePuppet()
    {
        using var fixture = new MissionEngineFixture();
        var peer = Clients.First();

        peer.Call(() =>
        {
            var mock = fixture.CreateMission(peer);
            Agent sourceRider = SpawnRider(mock);
            Agent sourceHorse = mock.SpawnMount(sourceRider);
            Agent puppetRider = SpawnRider(mock);
            Agent puppetHorse = mock.SpawnMount(puppetRider);
            Assert.True(AgentMirror.TryGet(sourceRider, out var sourceRiderMirror));
            Assert.True(AgentMirror.TryGet(sourceHorse, out var sourceHorseMirror));
            Assert.True(AgentMirror.TryGet(puppetRider, out var puppetRiderMirror));
            Assert.True(AgentMirror.TryGet(puppetHorse, out var puppetHorseMirror));
            puppetRiderMirror.ClearLocomotionFlagsOnContinuousStateWrite = true;
            puppetHorseMirror.ClearLocomotionFlagsOnContinuousStateWrite = true;

            sourceRiderMirror.Position = new Vec3(1f, 0f, 1f);
            sourceRiderMirror.MovementDirection = new Vec2(1f, 0f);
            sourceRiderMirror.LookDirection = new Vec3(-1f, 0f, 0f);
            sourceRider.MovementInputVector = new Vec2(0f, 1f);
            sourceRiderMirror.MovementFlags =
                Agent.MovementControlFlag.Forward |
                Agent.MovementControlFlag.TurnLeft;
            sourceHorseMirror.Position = new Vec3(1f, 0f, 0f);
            sourceHorseMirror.MovementDirection = new Vec2(1f, 0f);
            sourceHorseMirror.LookDirection = new Vec3(-1f, 0f, 0f);
            sourceHorseMirror.RealGlobalVelocity = new Vec3(1f, 0f, 0f);
            sourceHorse.MovementInputVector = new Vec2(0f, 1f);
            sourceHorseMirror.MovementFlags =
                Agent.MovementControlFlag.Forward |
                Agent.MovementControlFlag.TurnLeft;

            var data = new AgentData(sourceRider);
            data.Apply(puppetRider);
            var interpolator = new AgentPositionInterpolator();
            interpolator.SetMountedRiderTarget(puppetRider, data);

            puppetRiderMirror.MovementFlags =
                Agent.MovementControlFlag.DefendBlock |
                Agent.MovementControlFlag.DefendUp;
            puppetRiderMirror.LookDirection = new Vec3(1f, 0f, 0f);
            puppetRider.MovementInputVector = Vec2.Zero;
            puppetHorseMirror.MovementFlags = Agent.MovementControlFlag.None;
            puppetHorseMirror.LookDirection = new Vec3(1f, 0f, 0f);
            puppetHorse.MovementInputVector = Vec2.Zero;

            interpolator.Tick(1f / 60f);

            Assert.Equal(
                Agent.MovementControlFlag.Forward |
                Agent.MovementControlFlag.TurnLeft |
                Agent.MovementControlFlag.DefendBlock |
                Agent.MovementControlFlag.DefendUp,
                puppetRiderMirror.MovementFlags);
            Assert.Equal(
                Agent.MovementControlFlag.Forward |
                Agent.MovementControlFlag.TurnLeft,
                puppetHorseMirror.MovementFlags);
            Assert.Equal(sourceRiderMirror.LookDirection, puppetRiderMirror.LookDirection);
            Assert.Equal(sourceHorseMirror.LookDirection, puppetHorseMirror.LookDirection);
            Assert.Equal(sourceRider.MovementInputVector, puppetRider.MovementInputVector);
            Assert.Equal(sourceHorse.MovementInputVector, puppetHorse.MovementInputVector);
        });
    }

    [Fact]
    public void MountedDisplayReplay_RestoresLatestOwnerLookWithoutChangingMovementState()
    {
        using var fixture = new MissionEngineFixture();
        var peer = Clients.First();

        peer.Call(() =>
        {
            var mock = fixture.CreateMission(peer);
            Agent sourceRider = SpawnRider(mock);
            Agent sourceHorse = mock.SpawnMount(sourceRider);
            Agent puppetRider = SpawnRider(mock);
            Agent puppetHorse = mock.SpawnMount(puppetRider);
            Assert.True(AgentMirror.TryGet(sourceRider, out var sourceRiderMirror));
            Assert.True(AgentMirror.TryGet(sourceHorse, out var sourceHorseMirror));
            Assert.True(AgentMirror.TryGet(puppetRider, out var puppetRiderMirror));
            Assert.True(AgentMirror.TryGet(puppetHorse, out var puppetHorseMirror));

            sourceRiderMirror.Position = new Vec3(1f, 0f, 1f);
            sourceRiderMirror.MovementDirection = new Vec2(1f, 0f);
            sourceRiderMirror.LookDirection = new Vec3(-1f, 0f, 0f);
            sourceRider.MovementInputVector = new Vec2(0f, 1f);
            sourceRiderMirror.MovementFlags =
                Agent.MovementControlFlag.Forward |
                Agent.MovementControlFlag.TurnLeft;
            sourceHorseMirror.Position = new Vec3(1f, 0f, 0f);
            sourceHorseMirror.MovementDirection = new Vec2(1f, 0f);
            sourceHorseMirror.LookDirection = new Vec3(-1f, 0f, 0f);
            sourceHorse.MovementInputVector = new Vec2(0f, 1f);
            sourceHorseMirror.MovementFlags =
                Agent.MovementControlFlag.Forward |
                Agent.MovementControlFlag.TurnLeft;

            var data = new AgentData(sourceRider);
            data.Apply(puppetRider);
            var interpolator = new AgentPositionInterpolator();
            interpolator.SetMountedRiderTarget(
                puppetRider,
                data);
            interpolator.Tick(1f / 60f);
            int teleportCalls = puppetHorseMirror.TeleportToPositionCalls;

            puppetRiderMirror.MovementFlags =
                Agent.MovementControlFlag.DefendBlock |
                Agent.MovementControlFlag.DefendRight;
            puppetRiderMirror.MovementDirection = new Vec2(0f, -1f);
            puppetRiderMirror.LookDirection = new Vec3(1f, 0f, 0f);
            puppetRider.MovementInputVector = Vec2.Zero;
            puppetHorseMirror.MovementFlags = Agent.MovementControlFlag.None;
            puppetHorseMirror.MovementDirection = new Vec2(-1f, 0f);
            puppetHorseMirror.LookDirection = new Vec3(1f, 0f, 0f);
            puppetHorse.MovementInputVector = Vec2.Zero;

            interpolator.ReplayLookDirections();

            Assert.Equal(
                Agent.MovementControlFlag.DefendBlock |
                Agent.MovementControlFlag.DefendRight,
                puppetRiderMirror.MovementFlags);
            Assert.Equal(
                Agent.MovementControlFlag.None,
                puppetHorseMirror.MovementFlags);
            Assert.Equal(
                new Vec2(0f, -1f),
                puppetRiderMirror.MovementDirection);
            Assert.Equal(
                new Vec2(-1f, 0f),
                puppetHorseMirror.MovementDirection);
            Assert.Equal(
                sourceRiderMirror.LookDirection,
                puppetRiderMirror.LookDirection);
            Assert.Equal(
                sourceHorseMirror.LookDirection,
                puppetHorseMirror.LookDirection);
            Assert.Equal(
                Vec2.Zero,
                puppetRider.MovementInputVector);
            Assert.Equal(
                Vec2.Zero,
                puppetHorse.MovementInputVector);
            Assert.Equal(
                teleportCalls,
                puppetHorseMirror.TeleportToPositionCalls);
        });
    }

    [Fact]
    public void MountedTargetFrame_ReportsTheLatestOwnerUpdateSequence()
    {
        using var fixture = new MissionEngineFixture();
        var peer = Clients.First();

        peer.Call(() =>
        {
            var mock = fixture.CreateMission(peer);
            Agent sourceRider = SpawnRider(mock);
            mock.SpawnMount(sourceRider);
            Agent puppetRider = SpawnRider(mock);
            mock.SpawnMount(puppetRider);
            Assert.True(AgentMirror.TryGet(
                sourceRider,
                out var sourceRiderMirror));

            var interpolator = new AgentPositionInterpolator();
            sourceRiderMirror.LookDirection =
                new Vec3(1f, 0f, 0f);
            interpolator.SetMountedRiderTarget(
                puppetRider,
                new AgentData(sourceRider));

            Assert.True(
                interpolator.TryGetTargetFrame(
                    puppetRider,
                    out Vec3 firstPosition,
                    out Vec3 firstLook,
                    out long firstSequence));
            Assert.Equal(sourceRiderMirror.Position, firstPosition);
            Assert.Equal(new Vec3(1f, 0f, 0f), firstLook);

            sourceRiderMirror.LookDirection =
                new Vec3(0f, 1f, 0f);
            interpolator.SetMountedRiderTarget(
                puppetRider,
                new AgentData(sourceRider));

            Assert.True(
                interpolator.TryGetTargetFrame(
                    puppetRider,
                    out Vec3 secondPosition,
                    out Vec3 secondLook,
                    out long secondSequence));
            Assert.Equal(sourceRiderMirror.Position, secondPosition);
            Assert.Equal(new Vec3(0f, 1f, 0f), secondLook);
            Assert.True(secondSequence > firstSequence);
        });
    }

    [Fact]
    public void OnFootFollow_ReplaysTheLatestOwnerInputsAfterNativeClearsThePuppet()
    {
        using var fixture = new MissionEngineFixture();
        var peer = Clients.First();

        peer.Call(() =>
        {
            var mock = fixture.CreateMission(peer);
            Agent source = SpawnRider(mock);
            Agent puppet = SpawnRider(mock);
            Assert.True(AgentMirror.TryGet(source, out var sourceMirror));
            Assert.True(AgentMirror.TryGet(puppet, out var puppetMirror));
            puppetMirror.ClearLocomotionFlagsOnContinuousStateWrite = true;

            sourceMirror.Position = new Vec3(1f, 0f, 0f);
            sourceMirror.MovementDirection = new Vec2(1f, 0f);
            sourceMirror.LookDirection = new Vec3(-1f, 0f, 0f);
            sourceMirror.RealGlobalVelocity = new Vec3(1f, 0f, 0f);
            source.MovementInputVector = new Vec2(0f, 1f);
            sourceMirror.MovementFlags =
                Agent.MovementControlFlag.Forward |
                Agent.MovementControlFlag.TurnRight;

            var data = new AgentData(source);
            data.Apply(puppet);
            Vec2 expectedInput = puppet.MovementInputVector;
            var interpolator = new AgentPositionInterpolator();
            interpolator.SetRiderTarget(puppet, data);

            puppetMirror.MovementFlags =
                Agent.MovementControlFlag.DefendBlock |
                Agent.MovementControlFlag.DefendRight;
            puppetMirror.LookDirection = new Vec3(1f, 0f, 0f);
            puppet.MovementInputVector = Vec2.Zero;

            interpolator.Tick(1f / 60f);

            Assert.Equal(
                Agent.MovementControlFlag.Forward |
                Agent.MovementControlFlag.TurnRight |
                Agent.MovementControlFlag.DefendBlock |
                Agent.MovementControlFlag.DefendRight,
                puppetMirror.MovementFlags);
            Assert.Equal(sourceMirror.LookDirection, puppetMirror.LookDirection);
            Assert.Equal(expectedInput, puppet.MovementInputVector);
        });
    }

    [Fact]
    public void OnFootDisplayReplay_RestoresLatestOwnerLookWithoutChangingMovementState()
    {
        using var fixture = new MissionEngineFixture();
        var peer = Clients.First();

        peer.Call(() =>
        {
            var mock = fixture.CreateMission(peer);
            Agent source = SpawnRider(mock);
            Agent puppet = SpawnRider(mock);
            Assert.True(AgentMirror.TryGet(source, out var sourceMirror));
            Assert.True(AgentMirror.TryGet(puppet, out var puppetMirror));

            sourceMirror.Position = new Vec3(1f, 0f, 0f);
            sourceMirror.MovementDirection = new Vec2(1f, 0f);
            sourceMirror.LookDirection = new Vec3(-1f, 0f, 0f);
            sourceMirror.RealGlobalVelocity = new Vec3(1f, 0f, 0f);
            source.MovementInputVector = new Vec2(0f, 1f);
            sourceMirror.MovementFlags =
                Agent.MovementControlFlag.Forward |
                Agent.MovementControlFlag.TurnRight;

            var data = new AgentData(source);
            data.Apply(puppet);
            var interpolator = new AgentPositionInterpolator();
            interpolator.SetRiderTarget(puppet, data);
            interpolator.Tick(1f / 60f);
            int targetFrameCalls =
                puppetMirror.SetTargetPositionAndDirectionCalls;

            puppetMirror.MovementFlags =
                Agent.MovementControlFlag.DefendBlock |
                Agent.MovementControlFlag.DefendLeft;
            puppetMirror.MovementDirection = new Vec2(0f, -1f);
            puppetMirror.LookDirection = new Vec3(1f, 0f, 0f);
            puppet.MovementInputVector = Vec2.Zero;

            interpolator.ReplayLookDirections();

            Assert.Equal(
                Agent.MovementControlFlag.DefendBlock |
                Agent.MovementControlFlag.DefendLeft,
                puppetMirror.MovementFlags);
            Assert.Equal(
                new Vec2(0f, -1f),
                puppetMirror.MovementDirection);
            Assert.Equal(sourceMirror.LookDirection, puppetMirror.LookDirection);
            Assert.Equal(Vec2.Zero, puppet.MovementInputVector);
            Assert.Equal(
                targetFrameCalls,
                puppetMirror.SetTargetPositionAndDirectionCalls);
        });
    }

    [Fact]
    public void OnFootFollow_DiscardsTheTargetWhenThePuppetReachesZeroHealth()
    {
        using var fixture = new MissionEngineFixture();
        var peer = Clients.First();

        peer.Call(() =>
        {
            var mock = fixture.CreateMission(peer);
            Agent source = SpawnRider(mock);
            Agent puppet = SpawnRider(mock);
            Assert.True(AgentMirror.TryGet(source, out var sourceMirror));
            Assert.True(AgentMirror.TryGet(puppet, out var puppetMirror));

            sourceMirror.Position = new Vec3(1f, 0f, 0f);
            sourceMirror.MovementDirection = new Vec2(1f, 0f);
            sourceMirror.LookDirection = new Vec3(-1f, 0f, 0f);
            source.MovementInputVector = new Vec2(0f, 1f);
            sourceMirror.MovementFlags =
                Agent.MovementControlFlag.Forward |
                Agent.MovementControlFlag.TurnRight;

            var interpolator = new AgentPositionInterpolator();
            interpolator.SetRiderTarget(puppet, new AgentData(source));

            puppetMirror.Health = 0f;
            puppetMirror.MovementDirection = new Vec2(0f, -1f);
            puppetMirror.LookDirection = new Vec3(1f, 0f, 0f);
            puppet.MovementInputVector = Vec2.Zero;
            puppetMirror.MovementFlags =
                Agent.MovementControlFlag.DefendBlock |
                Agent.MovementControlFlag.DefendLeft;

            interpolator.Tick(1f / 60f);

            Assert.Equal(new Vec2(0f, -1f), puppetMirror.MovementDirection);
            Assert.Equal(new Vec3(1f, 0f, 0f), puppetMirror.LookDirection);
            Assert.Equal(Vec2.Zero, puppet.MovementInputVector);
            Assert.Equal(
                Agent.MovementControlFlag.DefendBlock |
                Agent.MovementControlFlag.DefendLeft,
                puppetMirror.MovementFlags);
            Assert.False(
                interpolator.TryGetTargetMovementFlags(
                    puppet,
                    out _,
                    out _));
        });
    }

    [Fact]
    public void MountMovementPacket_RoundTripsHorizontalMountSpeed()
    {
        using var fixture = new MissionEngineFixture();
        var peer = Clients.First();

        peer.Call(() =>
        {
            var mock = fixture.CreateMission(peer);
            Agent sourceHorse = mock.SpawnMount();
            Assert.True(AgentMirror.TryGet(sourceHorse, out var sourceHorseMirror));
            sourceHorseMirror.RealGlobalVelocity = new Vec3(3f, 4f, 12f);
            var horseId = Guid.NewGuid();
            var serializer = new ProtoBufSerializer(new SerializableTypeMapper());

            byte[] wire = serializer.Serialize(
                new MountMovementPacket(
                    new[] { horseId },
                    new[] { new AgentMountData(sourceHorse, horseId) }));
            var result = Assert.IsType<MountMovementPacket>(serializer.Deserialize<IPacket>(wire));

            Assert.Equal(5f, Assert.Single(result.Mounts).MountSpeed);
        });
    }

    [Fact]
    public void MovementPacket_DoesNotChangeALocallyControlledHorse()
    {
        using var fixture = new MissionEngineFixture();
        var peer = Clients.First();
        SetControllerId(peer, "peer");

        peer.Call(() =>
        {
            var mock = fixture.CreateMission(peer);
            var registry = peer.Resolve<INetworkAgentRegistry>();
            var component = peer.Resolve<ICoopMissionComponent>();
            var riderId = Guid.NewGuid();
            var horseId = Guid.NewGuid();

            Agent rider = SpawnRider(mock);
            Agent horse = mock.SpawnMount(rider);
            Assert.True(AgentMirror.TryGet(horse, out var horseMirror));
            horseMirror.Controller = AgentControllerType.AI;
            Assert.True(registry.TryRegisterAgent("peer", riderId, rider));
            Assert.True(registry.TryRegisterAgent("peer", horseId, horse));

            Agent sourceHorse = mock.SpawnMount();
            AgentData data = CreateMountedData(
                riderPosition: new Vec3(1f, 0f, 1f),
                riderDirection: new Vec2(1f, 0f),
                ownerSpeed: 2f,
                mountData: new AgentMountData(sourceHorse, horseId));

            component.AgentMovementHandler.HandlePacket(
                null,
                new MovementPacket(new[] { riderId }, new[] { data }));

            Assert.Equal(AgentControllerType.AI, horseMirror.Controller);
            Assert.Equal(-1f, horseMirror.MaximumSpeedLimit);
            Assert.Equal(0, horseMirror.SetMaximumSpeedLimitCalls);
            Assert.Equal(0, horseMirror.TeleportToPositionCalls);
        });
    }

    [Fact]
    public void MountedFollow_CatchesUpThenStopsAtThePositionEpsilon()
    {
        using var fixture = new MissionEngineFixture();
        var peer = Clients.First();

        peer.Call(() =>
        {
            var mock = fixture.CreateMission(peer);
            Agent rider = SpawnRider(mock);
            Agent horse = mock.SpawnMount(rider);
            Assert.True(AgentMirror.TryGet(horse, out var horseMirror));

            var target = new Vec3(0.02f, 0f, 0f);
            var interpolator = new AgentPositionInterpolator();
            interpolator.SetMountedRiderTarget(rider, target, Vec2.Forward, Vec2.Forward, target);

            for (int i = 0; i < 60; i++)
                interpolator.Tick(1f / 60f);

            int settledCallCount = horseMirror.TeleportToPositionCalls;
            Assert.True(settledCallCount > 0);
            Assert.InRange(horseMirror.Position.Distance(target), 0f, 0.0001f);

            for (int i = 0; i < 120; i++)
                interpolator.Tick(1f / 60f);

            Assert.Equal(settledCallCount, horseMirror.TeleportToPositionCalls);
        });
    }

    [Fact]
    public void MountedFollow_HeldGuard_SnapsMeasurablePositionDrift()
    {
        using var fixture = new MissionEngineFixture();
        var peer = Clients.First();

        peer.Call(() =>
        {
            var mock = fixture.CreateMission(peer);
            Agent rider = SpawnRider(mock);
            Agent horse = mock.SpawnMount(rider);
            Assert.True(AgentMirror.TryGet(rider, out var riderMirror));
            Assert.True(AgentMirror.TryGet(horse, out var horseMirror));
            riderMirror.MovementFlags =
                Agent.MovementControlFlag.DefendBlock |
                Agent.MovementControlFlag.DefendUp;

            var interpolator = new AgentPositionInterpolator();
            interpolator.SetMountedRiderTarget(
                rider,
                new Vec3(2f, 0f, 0f),
                new Vec2(1f, 0f),
                new Vec2(0f, 1f),
                new Vec3(2f, 0f, 0f));

            interpolator.Tick(1f / 60f);

            Assert.Equal(1, horseMirror.TeleportToPositionCalls);
            Assert.Equal(new Vec3(2f, 0f, 0f), horseMirror.Position);
            Assert.Equal(new Vec2(0f, 1f), horseMirror.MovementDirection);
            Assert.Equal(new Vec2(1f, 0f), riderMirror.MovementDirection);

            horseMirror.Position = new Vec3(3f, 0f, 0f);
            interpolator.Tick(1f / 60f);

            Assert.Equal(1, horseMirror.TeleportToPositionCalls);
            Assert.Equal(new Vec3(3f, 0f, 0f), horseMirror.Position);

            interpolator.SetMountedRiderTarget(
                rider,
                new Vec3(4f, 0f, 0f),
                new Vec2(1f, 0f),
                new Vec2(0f, 1f),
                new Vec3(4f, 0f, 0f));
            interpolator.Tick(1f / 60f);

            Assert.Equal(2, horseMirror.TeleportToPositionCalls);
            Assert.Equal(new Vec3(4f, 0f, 0f), horseMirror.Position);

            riderMirror.MovementFlags = Agent.MovementControlFlag.None;
            interpolator.Tick(1f / 60f);

            Assert.Equal(2, horseMirror.TeleportToPositionCalls);
        });
    }

    [Fact]
    public void MountedFollow_HeldGuard_DoesNotCorrectSubToleranceDrift()
    {
        using var fixture = new MissionEngineFixture();
        var peer = Clients.First();

        peer.Call(() =>
        {
            var mock = fixture.CreateMission(peer);
            Agent rider = SpawnRider(mock);
            Agent horse = mock.SpawnMount(rider);
            Assert.True(AgentMirror.TryGet(rider, out var riderMirror));
            Assert.True(AgentMirror.TryGet(horse, out var horseMirror));
            riderMirror.MovementFlags =
                Agent.MovementControlFlag.DefendBlock |
                Agent.MovementControlFlag.DefendRight;

            var interpolator = new AgentPositionInterpolator();
            interpolator.SetMountedRiderTarget(
                rider,
                new Vec3(0.1f, 0f, 0f),
                new Vec2(1f, 0f),
                new Vec2(0f, 1f),
                new Vec3(0.1f, 0f, 0f));

            interpolator.Tick(1f / 60f);

            Assert.Equal(0, horseMirror.TeleportToPositionCalls);
            Assert.Equal(new Vec2(0f, 1f), horseMirror.MovementDirection);
            Assert.Equal(new Vec2(1f, 0f), riderMirror.MovementDirection);

            horseMirror.Position = new Vec3(1f, 0f, 0f);
            interpolator.Tick(1f / 60f);

            Assert.Equal(0, horseMirror.TeleportToPositionCalls);
            Assert.Equal(new Vec3(1f, 0f, 0f), horseMirror.Position);
        });
    }

    [Fact]
    public void MountedFollow_GuardReaction_SnapsMeasurablePositionDrift()
    {
        using var fixture = new MissionEngineFixture();
        var peer = Clients.First();

        peer.Call(() =>
        {
            var mock = fixture.CreateMission(peer);
            Agent rider = SpawnRider(mock);
            Agent horse = mock.SpawnMount(rider);
            Assert.True(AgentMirror.TryGet(rider, out var riderMirror));
            Assert.True(AgentMirror.TryGet(horse, out var horseMirror));
            riderMirror.Action1CodeType =
                Agent.ActionCodeType.BlockedMelee;

            var interpolator = new AgentPositionInterpolator();
            interpolator.SetMountedRiderTarget(
                rider,
                new Vec3(2f, 0f, 0f),
                Vec2.Forward,
                Vec2.Forward,
                new Vec3(2f, 0f, 0f));

            interpolator.Tick(1f / 60f);

            Assert.Equal(1, horseMirror.TeleportToPositionCalls);
            Assert.Equal(new Vec3(2f, 0f, 0f), horseMirror.Position);
        });
    }

    [Fact]
    public void MountedFollow_HeldGuardStillSnapsALargePositionGap()
    {
        using var fixture = new MissionEngineFixture();
        var peer = Clients.First();

        peer.Call(() =>
        {
            var mock = fixture.CreateMission(peer);
            Agent rider = SpawnRider(mock);
            Agent horse = mock.SpawnMount(rider);
            Assert.True(AgentMirror.TryGet(rider, out var riderMirror));
            Assert.True(AgentMirror.TryGet(horse, out var horseMirror));
            riderMirror.MovementFlags =
                Agent.MovementControlFlag.DefendBlock |
                Agent.MovementControlFlag.DefendUp;

            var target = new Vec3(13f, 0f, 0f);
            var interpolator = new AgentPositionInterpolator();
            interpolator.SetMountedRiderTarget(
                rider,
                target,
                Vec2.Forward,
                Vec2.Forward,
                target);

            interpolator.Tick(1f / 60f);

            Assert.Equal(1, horseMirror.TeleportToPositionCalls);
            Assert.Equal(target, horseMirror.Position);
        });
    }

    [Fact]
    public void RemoteDismount_RestoresALocallyAuthoritativeHorseController()
    {
        using var fixture = new MissionEngineFixture();
        var peer = Clients.First();
        SetControllerId(peer, "peer");

        peer.Call(() =>
        {
            var mock = fixture.CreateMission(peer);
            var registry = peer.Resolve<INetworkAgentRegistry>();
            var component = peer.Resolve<ICoopMissionComponent>();
            var riderId = Guid.NewGuid();
            var horseId = Guid.NewGuid();

            Agent rider = SpawnRider(mock);
            Agent horse = mock.SpawnMount(rider);
            Assert.True(AgentMirror.TryGet(horse, out var horseMirror));
            horseMirror.Controller = AgentControllerType.None;
            horseMirror.MaximumSpeedLimit = 0f;
            Assert.True(registry.TryRegisterAgent("owner", riderId, rider));
            Assert.True(registry.TryRegisterAgent("peer", horseId, horse));

            AgentData data = CreateAgentData(
                riderPosition: Vec3.Zero,
                riderDirection: Vec2.Forward,
                ownerSpeed: 0f,
                mountData: null);
            component.AgentMovementHandler.HandlePacket(
                null,
                new MovementPacket(new[] { riderId }, new[] { data }));

            Assert.Null(rider.MountAgent);
            Assert.Null(horse.RiderAgent);
            Assert.Equal(AgentControllerType.AI, horseMirror.Controller);
            Assert.Equal(-1f, horseMirror.MaximumSpeedLimit);
            Assert.Equal(1, horseMirror.SetMaximumSpeedLimitCalls);
        });
    }

    [Fact]
    public void RemoteDismount_RestoresAnUnregisteredHorseController()
    {
        using var fixture = new MissionEngineFixture();
        var peer = Clients.First();
        SetControllerId(peer, "peer");

        peer.Call(() =>
        {
            var mock = fixture.CreateMission(peer);
            var registry = peer.Resolve<INetworkAgentRegistry>();
            var component = peer.Resolve<ICoopMissionComponent>();
            var riderId = Guid.NewGuid();

            Agent rider = SpawnRider(mock);
            Agent horse = mock.SpawnMount(rider);
            Assert.True(AgentMirror.TryGet(horse, out var horseMirror));
            horseMirror.Controller = AgentControllerType.None;
            horseMirror.MaximumSpeedLimit = 0f;
            Assert.True(registry.TryRegisterAgent("owner", riderId, rider));

            AgentData data = CreateAgentData(
                riderPosition: Vec3.Zero,
                riderDirection: Vec2.Forward,
                ownerSpeed: 0f,
                mountData: null);
            component.AgentMovementHandler.HandlePacket(
                null,
                new MovementPacket(new[] { riderId }, new[] { data }));

            Assert.Null(rider.MountAgent);
            Assert.Null(horse.RiderAgent);
            Assert.Equal(AgentControllerType.AI, horseMirror.Controller);
            Assert.Equal(-1f, horseMirror.MaximumSpeedLimit);
            Assert.Equal(1, horseMirror.SetMaximumSpeedLimitCalls);
        });
    }

    [Fact]
    public void RemoteHorseSwitch_RestoresTheOldLocalHorse_AndPuppetsTheNewHorse()
    {
        using var fixture = new MissionEngineFixture();
        var peer = Clients.First();
        SetControllerId(peer, "peer");

        peer.Call(() =>
        {
            var mock = fixture.CreateMission(peer);
            var registry = peer.Resolve<INetworkAgentRegistry>();
            var component = peer.Resolve<ICoopMissionComponent>();
            var riderId = Guid.NewGuid();
            var oldHorseId = Guid.NewGuid();
            var newHorseId = Guid.NewGuid();

            Agent rider = SpawnRider(mock);
            Agent oldHorse = mock.SpawnMount(rider);
            Agent newHorse = mock.SpawnMount();
            Assert.True(AgentMirror.TryGet(oldHorse, out var oldHorseMirror));
            Assert.True(AgentMirror.TryGet(newHorse, out var newHorseMirror));
            oldHorseMirror.Controller = AgentControllerType.None;
            oldHorseMirror.MaximumSpeedLimit = 0f;
            newHorseMirror.Controller = AgentControllerType.AI;
            Assert.True(registry.TryRegisterAgent("owner", riderId, rider));
            Assert.True(registry.TryRegisterAgent("peer", oldHorseId, oldHorse));
            Assert.True(registry.TryRegisterAgent("owner", newHorseId, newHorse));

            AgentData data = CreateAgentData(
                riderPosition: Vec3.Zero,
                riderDirection: new Vec2(1f, 0f),
                ownerSpeed: 0f,
                mountData: new AgentMountData(newHorse, newHorseId));
            component.AgentMovementHandler.HandlePacket(
                null,
                new MovementPacket(new[] { riderId }, new[] { data }));

            Assert.Same(newHorse, rider.MountAgent);
            Assert.Null(oldHorse.RiderAgent);
            Assert.Equal(AgentControllerType.AI, oldHorseMirror.Controller);
            Assert.Equal(-1f, oldHorseMirror.MaximumSpeedLimit);
            Assert.Equal(1, oldHorseMirror.SetMaximumSpeedLimitCalls);
            Assert.Equal(AgentControllerType.None, newHorseMirror.Controller);
        });
    }

    [Fact]
    public void MovementPolling_RestoresAiOnlyWhenTheLocalHorseIsLocallyDriven()
    {
        using var fixture = new MissionEngineFixture();
        var peer = Clients.First();
        SetControllerId(peer, "peer");

        peer.Call(() =>
        {
            var mock = fixture.CreateMission(peer);
            var registry = peer.Resolve<INetworkAgentRegistry>();
            var component = peer.Resolve<ICoopMissionComponent>();

            Agent remoteRider = SpawnRider(mock);
            Agent localHorse = mock.SpawnMount(remoteRider);
            Assert.True(AgentMirror.TryGet(remoteRider, out var remoteRiderMirror));
            Assert.True(AgentMirror.TryGet(localHorse, out var localHorseMirror));
            localHorseMirror.Controller = AgentControllerType.None;
            localHorseMirror.MaximumSpeedLimit = 0f;
            Assert.True(registry.TryRegisterAgent("owner", Guid.NewGuid(), remoteRider));
            Assert.True(registry.TryRegisterAgent("peer", Guid.NewGuid(), localHorse));

            component.AgentMovementHandler.PollMovement(0.05f);
            Assert.Equal(AgentControllerType.None, localHorseMirror.Controller);
            Assert.Equal(0f, localHorseMirror.MaximumSpeedLimit);
            Assert.Equal(0, localHorseMirror.SetMaximumSpeedLimitCalls);

            remoteRiderMirror.IsActive = false;
            component.AgentMovementHandler.PollMovement(0.05f);
            Assert.Equal(AgentControllerType.AI, localHorseMirror.Controller);
            Assert.Equal(-1f, localHorseMirror.MaximumSpeedLimit);
            Assert.Equal(1, localHorseMirror.SetMaximumSpeedLimitCalls);

            remoteRiderMirror.IsActive = true;
            localHorseMirror.Controller = AgentControllerType.None;
            localHorseMirror.MaximumSpeedLimit = 0f;
            localHorseMirror.SetMaximumSpeedLimitCalls = 0;
            remoteRider.MountAgent = null;
            component.AgentMovementHandler.PollMovement(0.05f);
            Assert.Equal(AgentControllerType.AI, localHorseMirror.Controller);
            Assert.Equal(-1f, localHorseMirror.MaximumSpeedLimit);
            Assert.Equal(1, localHorseMirror.SetMaximumSpeedLimitCalls);

            Agent localRider = SpawnRider(mock);
            Agent remoteHorse = mock.SpawnMount(localRider);
            Assert.True(AgentMirror.TryGet(remoteHorse, out var remoteHorseMirror));
            remoteHorseMirror.Controller = AgentControllerType.None;
            remoteHorseMirror.MaximumSpeedLimit = 0f;
            Assert.True(registry.TryRegisterAgent("peer", Guid.NewGuid(), localRider));
            Assert.True(registry.TryRegisterAgent("owner", Guid.NewGuid(), remoteHorse));

            component.AgentMovementHandler.PollMovement(0.05f);
            Assert.Equal(AgentControllerType.AI, remoteHorseMirror.Controller);
            Assert.Equal(-1f, remoteHorseMirror.MaximumSpeedLimit);
            Assert.Equal(1, remoteHorseMirror.SetMaximumSpeedLimitCalls);

            remoteHorseMirror.Controller = AgentControllerType.None;
            remoteHorseMirror.MaximumSpeedLimit = 0f;
            remoteHorseMirror.SetMaximumSpeedLimitCalls = 0;
            remoteHorseMirror.IsActive = false;
            component.AgentMovementHandler.PollMovement(0.05f);
            Assert.Equal(AgentControllerType.None, remoteHorseMirror.Controller);
            Assert.Equal(0f, remoteHorseMirror.MaximumSpeedLimit);
            Assert.Equal(0, remoteHorseMirror.SetMaximumSpeedLimitCalls);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void StaleMasterlessPacket_DoesNotAddADirectTargetAfterRemount(bool masterlessPacketArrivesLast)
    {
        using var fixture = new MissionEngineFixture();
        var peer = Clients.First();
        SetControllerId(peer, "peer");

        peer.Call(() =>
        {
            var mock = fixture.CreateMission(peer);
            var registry = peer.Resolve<INetworkAgentRegistry>();
            var component = peer.Resolve<ICoopMissionComponent>();
            var riderId = Guid.NewGuid();
            var horseId = Guid.NewGuid();

            Agent rider = SpawnRider(mock);
            Agent horse = mock.SpawnMount();
            Assert.True(AgentMirror.TryGet(horse, out var horseMirror));
            Assert.True(registry.TryRegisterAgent("owner", riderId, rider));
            Assert.True(registry.TryRegisterAgent("owner", horseId, horse));

            Agent sourceHorse = mock.SpawnMount();
            Assert.True(AgentMirror.TryGet(sourceHorse, out var sourceHorseMirror));
            sourceHorseMirror.Position = new Vec3(1f, 0f, 0f);
            sourceHorseMirror.MovementDirection = new Vec2(0f, 1f);
            var mountData = new AgentMountData(sourceHorse, horseId);
            var mountPacket = new MountMovementPacket(new[] { horseId }, new[] { mountData });
            var riderData = CreateAgentData(
                riderPosition: new Vec3(1f, 0f, 1f),
                riderDirection: new Vec2(1f, 0f),
                ownerSpeed: 2f,
                mountData: mountData);
            var riderPacket = new MovementPacket(new[] { riderId }, new[] { riderData });

            if (!masterlessPacketArrivesLast)
                component.AgentMovementHandler.MountMovementApplier.HandlePacket(null, mountPacket);
            component.AgentMovementHandler.HandlePacket(null, riderPacket);
            if (masterlessPacketArrivesLast)
                component.AgentMovementHandler.MountMovementApplier.HandlePacket(null, mountPacket);

            Assert.Same(horse, rider.MountAgent);
            component.AgentMovementHandler.Interpolator.Tick(1f / 60f);

            Assert.Equal(0, horseMirror.SetTargetPositionAndDirectionCalls);
            Assert.Equal(1, horseMirror.TeleportToPositionCalls);
        });
    }

    [Fact]
    public void MasterlessPacket_AppliesTheOwnersMovingSpeedLimit()
    {
        using var fixture = new MissionEngineFixture();
        var peer = Clients.First();
        SetControllerId(peer, "peer");

        peer.Call(() =>
        {
            var mock = fixture.CreateMission(peer);
            var registry = peer.Resolve<INetworkAgentRegistry>();
            var component = peer.Resolve<ICoopMissionComponent>();
            var horseId = Guid.NewGuid();

            Agent puppetHorse = mock.SpawnMount();
            Assert.True(AgentMirror.TryGet(puppetHorse, out var puppetHorseMirror));
            puppetHorseMirror.Controller = AgentControllerType.AI;
            Assert.True(registry.TryRegisterAgent("owner", horseId, puppetHorse));

            Agent sourceHorse = mock.SpawnMount();
            Assert.True(AgentMirror.TryGet(sourceHorse, out var sourceHorseMirror));
            sourceHorseMirror.RealGlobalVelocity = new Vec3(0.6f, 0.8f, 5f);
            var packet = new MountMovementPacket(
                new[] { horseId },
                new[] { new AgentMountData(sourceHorse, horseId) });
            component.AgentMovementHandler.MountMovementApplier.HandlePacket(null, packet);

            Assert.Equal(AgentControllerType.None, puppetHorseMirror.Controller);
            Assert.Equal(1f, puppetHorseMirror.MaximumSpeedLimit);
            Assert.False(puppetHorseMirror.LastMaximumSpeedLimitIsMultiplier);
            Assert.Equal(1, puppetHorseMirror.SetMaximumSpeedLimitCalls);
            Assert.NotNull(puppetHorse.CommonAIComponent);

            puppetHorse.CommonAIComponent.OnMountReserved(47);
            component.AgentMovementHandler.MountMovementApplier.HandlePacket(null, packet);
            Assert.Equal(47, puppetHorse.CommonAIComponent.ReservedRiderAgentIndex);
        });
    }

    [Fact]
    public void MasterlessPacket_DoesNotChangeALocallyControlledHorse()
    {
        using var fixture = new MissionEngineFixture();
        var peer = Clients.First();
        SetControllerId(peer, "peer");

        peer.Call(() =>
        {
            var mock = fixture.CreateMission(peer);
            var registry = peer.Resolve<INetworkAgentRegistry>();
            var component = peer.Resolve<ICoopMissionComponent>();
            var horseId = Guid.NewGuid();

            Agent horse = mock.SpawnMount();
            Assert.True(AgentMirror.TryGet(horse, out var horseMirror));
            horseMirror.Controller = AgentControllerType.AI;
            horseMirror.MovementDirection = Vec2.Forward;
            Assert.True(registry.TryRegisterAgent("peer", horseId, horse));

            Agent sourceHorse = mock.SpawnMount();
            Assert.True(AgentMirror.TryGet(sourceHorse, out var sourceHorseMirror));
            sourceHorseMirror.MovementDirection = new Vec2(1f, 0f);
            component.AgentMovementHandler.MountMovementApplier.HandlePacket(
                null,
                new MountMovementPacket(
                    new[] { horseId },
                    new[] { new AgentMountData(sourceHorse, horseId) }));
            component.AgentMovementHandler.Interpolator.Tick(1f / 60f);

            Assert.Equal(AgentControllerType.AI, horseMirror.Controller);
            Assert.Equal(-1f, horseMirror.MaximumSpeedLimit);
            Assert.Equal(0, horseMirror.SetMaximumSpeedLimitCalls);
            Assert.Equal(Vec2.Forward, horseMirror.MovementDirection);
            Assert.Equal(0, horseMirror.SetTargetPositionAndDirectionCalls);
            Assert.Equal(0, horseMirror.TeleportToPositionCalls);
        });
    }

    private static Agent SpawnRider(MockMission mock)
    {
        return mock.SpawnAgent(
            new AgentBuildData(Game.Current.PlayerTroop)
                .Controller(AgentControllerType.None));
    }

    private static AgentData CreateMountedData(
        Vec3 riderPosition,
        Vec2 riderDirection,
        float ownerSpeed,
        AgentMountData mountData,
        Vec3? riderLookDirection = null)
    {
        return CreateAgentData(riderPosition, riderDirection, ownerSpeed, mountData, riderLookDirection);
    }

    private static AgentData CreateAgentData(
        Vec3 riderPosition,
        Vec2 riderDirection,
        float ownerSpeed,
        AgentMountData mountData,
        Vec3? riderLookDirection = null)
    {
        object boxed = default(AgentData);
        SetBackingField(boxed, nameof(AgentData.Position), riderPosition);
        SetBackingField(boxed, nameof(AgentData.MovementDirection), riderDirection);
        SetBackingField(
            boxed,
            nameof(AgentData.LookDirection),
            riderLookDirection ?? new Vec3(riderDirection.X, riderDirection.Y, 0f));
        SetBackingField(boxed, nameof(AgentData.InputVector), riderDirection);
        SetBackingField(boxed, nameof(AgentData.MountData), mountData);
        SetBackingField(boxed, nameof(AgentData.Speed), ownerSpeed);
        return (AgentData)boxed;
    }

    private static void SetBackingField(object boxed, string propertyName, object value)
    {
        FieldInfo field = typeof(AgentData).GetField(
            $"<{propertyName}>k__BackingField",
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (field == null) throw new MissingFieldException(typeof(AgentData).FullName, propertyName);
        field.SetValue(boxed, value);
    }
}
