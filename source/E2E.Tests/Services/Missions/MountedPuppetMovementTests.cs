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
            sourceHorseMirror.InputVector = new Vec2(0.4f, 0.6f);
            puppetHorseMirror.Action0Index = ActionIndexCache.act_none.Index;

            new AgentMountData(
                sourceHorse,
                mountAction0Speed: 0.8f,
                mountAction0IsLocomotion: true,
                mountAction0TurnDirection: AgentMountData.TurnRight).ApplyMount(puppetHorse);

            Assert.Equal(sourceHorseMirror.InputVector, puppetHorseMirror.InputVector);
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
            Assert.Equal(turnActionIndex, sourceHorseMirror.Action0Index);
            Assert.Equal(1, sourceHorseMirror.SetActionChannelCalls);

            AgentMountData sentMount = Assert.Single(
                Assert.Single(network.NetworkSentPackets.GetPackets<MountMovementPacket>())
                    .Mounts);
            Assert.Equal(AgentMountData.TurnLeft, sentMount.MountAction0TurnDirection);
            Assert.Equal(turnActionIndex, sentMount.MountAction0TurnActionIndex);

            sourceHorseMirror.MovementDirection = new Vec2(-0.2f, 0.98f);
            component.AgentMovementHandler.PollMovement(0.025f);
            Assert.Equal(1, sourceHorseMirror.SetActionChannelCalls);
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
            Assert.Equal(turnActionIndex, sourceHorseMirror.Action0Index);
            Assert.Equal(1, sourceHorseMirror.SetActionChannelCalls);

            AgentMountData sentMount = Assert.Single(
                Assert.Single(network.NetworkSentPackets.GetPackets<MountMovementPacket>())
                    .Mounts);
            Assert.Equal(AgentMountData.TurnLeft, sentMount.MountAction0TurnDirection);
            Assert.Equal(turnActionIndex, sentMount.MountAction0TurnActionIndex);
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
            ActionIndexCache.act_none.Index,
            AgentMountData.ResolveAction0Index(
                actionIndex: 101,
                speed: 0f,
                isLocomotion: true,
                turnDirection: AgentMountData.NoTurn,
                turnActionIndex: 902));
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
                    new[] { new AgentMountData(
                        sourceHorse,
                        horseId,
                        0.7f,
                        true,
                        AgentMountData.TurnLeft,
                        902) }));
            var result = Assert.IsType<MountMovementPacket>(serializer.Deserialize<IPacket>(wire));

            Assert.Equal(5f, Assert.Single(result.Mounts).MountSpeed);
            Assert.Equal(0.7f, Assert.Single(result.Mounts).MountAction0Speed);
            Assert.True(Assert.Single(result.Mounts).MountAction0IsLocomotion);
            Assert.Equal(
                AgentMountData.TurnLeft,
                Assert.Single(result.Mounts).MountAction0TurnDirection);
            Assert.Equal(902, Assert.Single(result.Mounts).MountAction0TurnActionIndex);
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
            component.AgentMovementHandler.MountMovementApplier.HandlePacket(
                null,
                new MountMovementPacket(
                    new[] { horseId },
                    new[] { new AgentMountData(sourceHorse, horseId) }));

            Assert.Equal(AgentControllerType.None, puppetHorseMirror.Controller);
            Assert.Equal(1f, puppetHorseMirror.MaximumSpeedLimit);
            Assert.False(puppetHorseMirror.LastMaximumSpeedLimitIsMultiplier);
            Assert.Equal(1, puppetHorseMirror.SetMaximumSpeedLimitCalls);
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
