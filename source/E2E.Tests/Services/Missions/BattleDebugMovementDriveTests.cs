#if DEBUG
using Common.Util;
using GameInterface.Services.Entity;
using Missions;
using Moq;
using Newtonsoft.Json.Linq;
using System;
using E2E.Tests.Environment.MockEngine;
using Missions.Battles;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using Xunit;

namespace E2E.Tests.Services.Missions;

public class BattleDebugMovementDriveTests
{
    [Fact]
    public void CanDriveOwnedAgent_DoesNotReadNativeStateAfterMissionTeardown()
    {
        Agent clearedAgent = ObjectHelper.SkipConstructor<Agent>();
        Mission mission = ObjectHelper.SkipConstructor<Mission>();

        Assert.False(BattleDebugCommands.CanDriveOwnedAgent(clearedAgent, mission));
    }

    [Fact]
    public void OwnedAgentMovementDrive_AppliesNativeTargetAndRestoresUnlockedState()
    {
        using var fixture = new MissionEngineFixture();
        Agent agent = ObjectHelper.SkipConstructor<Agent>();
        var mirror = new MirrorAgent
        {
            MovementFlags =
                Agent.MovementControlFlag.Backward |
                Agent.MovementControlFlag.TurnLeft |
                Agent.MovementControlFlag.DefendBlock,
            InputVector = new Vec2(0.25f, -0.5f),
            LookDirection = new Vec3(3f, 4f, 2f),
            Position = new Vec3(4f, 5f, 0f),
            Controller = AgentControllerType.AI,
            IsAiPaused = true,
            MaximumSpeedLimit = 0.75f,
            LastMaximumSpeedLimitIsMultiplier = true,
        };
        AgentMirror.Bind(agent, mirror);

        Agent.MovementControlFlag originalLocomotion =
            mirror.MovementFlags & Agent.MovementControlFlag.MoveMask;
        Vec2 originalInput = mirror.InputVector;
        bool originalIsAiPaused = mirror.IsAiPaused;
        AgentMovementLockedState originalMovementLockedState = mirror.MovementLockedState;

        BattleDebugCommands.ApplyOwnedAgentMovementDrive(agent, applyAiDrive: true);

        Assert.Equal(
            Agent.MovementControlFlag.Forward |
            Agent.MovementControlFlag.DefendBlock,
            mirror.MovementFlags);
        Assert.Equal(Vec2.Forward.X, mirror.InputVector.X);
        Assert.Equal(Vec2.Forward.Y, mirror.InputVector.Y);
        Assert.Equal(1, mirror.AddAccelerationCalls);
        Assert.InRange(mirror.LastAcceleration.X, 2.39f, 2.41f);
        Assert.InRange(mirror.LastAcceleration.Y, 3.19f, 3.21f);
        Assert.Equal(0f, mirror.LastAcceleration.Z);
        Assert.Equal(AgentControllerType.AI, mirror.Controller);
        Assert.False(mirror.IsAiPaused);
        Assert.Equal(0.75f, mirror.MaximumSpeedLimit);
        Assert.True(mirror.LastMaximumSpeedLimitIsMultiplier);
        Assert.Equal(0, mirror.SetMaximumSpeedLimitCalls);
        Assert.Equal(1, mirror.SetTargetPositionAndDirectionCalls);
        Assert.Equal(AgentMovementLockedState.FrameLocked, mirror.MovementLockedState);
        Assert.InRange(mirror.LastTargetPosition.X, 15.99f, 16.01f);
        Assert.InRange(mirror.LastTargetPosition.Y, 20.99f, 21.01f);

        mirror.MovementFlags |= Agent.MovementControlFlag.DefendRight;
        BattleDebugCommands.RestoreOwnedAgentMovementDrive(
            agent,
            originalLocomotion,
            originalInput,
            true,
            originalIsAiPaused,
            originalMovementLockedState,
            default,
            default);

        Assert.Equal(
            Agent.MovementControlFlag.Backward |
            Agent.MovementControlFlag.TurnLeft |
            Agent.MovementControlFlag.DefendBlock |
            Agent.MovementControlFlag.DefendRight,
            mirror.MovementFlags);
        Assert.Equal(originalInput.X, mirror.InputVector.X);
        Assert.Equal(originalInput.Y, mirror.InputVector.Y);
        Assert.Equal(AgentControllerType.AI, mirror.Controller);
        Assert.Equal(originalIsAiPaused, mirror.IsAiPaused);
        Assert.Equal(0.75f, mirror.MaximumSpeedLimit);
        Assert.True(mirror.LastMaximumSpeedLimitIsMultiplier);
        Assert.Equal(0, mirror.SetMaximumSpeedLimitCalls);
        Assert.Equal(1, mirror.ClearTargetFrameCalls);
        Assert.Equal(AgentMovementLockedState.None, mirror.MovementLockedState);
    }

    [Fact]
    public void OwnedAgentMovementDrive_RestoresPositionLockedTarget()
    {
        using var fixture = new MissionEngineFixture();
        Agent agent = ObjectHelper.SkipConstructor<Agent>();
        var originalTargetPosition = new Vec2(8f, 13f);
        var mirror = new MirrorAgent
        {
            Controller = AgentControllerType.AI,
            LookDirection = new Vec3(1f, 0f, 0f),
            Position = new Vec3(2f, 3f, 0f),
            MovementLockedState = AgentMovementLockedState.PositionLocked,
            LastTargetPosition = originalTargetPosition,
            MaximumSpeedLimit = 0.5f,
            LastMaximumSpeedLimitIsMultiplier = true,
        };
        AgentMirror.Bind(agent, mirror);

        BattleDebugCommands.ApplyOwnedAgentMovementDrive(agent, applyAiDrive: true);

        Assert.Equal(AgentMovementLockedState.FrameLocked, mirror.MovementLockedState);

        BattleDebugCommands.RestoreOwnedAgentMovementDrive(
            agent,
            Agent.MovementControlFlag.None,
            Vec2.Zero,
            true,
            false,
            AgentMovementLockedState.PositionLocked,
            originalTargetPosition,
            default);

        Assert.Equal(AgentMovementLockedState.PositionLocked, mirror.MovementLockedState);
        Assert.Equal(1, mirror.SetTargetPositionCalls);
        Assert.Equal(1, mirror.SetTargetPositionAndDirectionCalls);
        Assert.Equal(0, mirror.ClearTargetFrameCalls);
        Assert.Equal(originalTargetPosition.X, mirror.LastTargetPosition.X);
        Assert.Equal(originalTargetPosition.Y, mirror.LastTargetPosition.Y);
        Assert.Equal(0.5f, mirror.MaximumSpeedLimit);
        Assert.True(mirror.LastMaximumSpeedLimitIsMultiplier);
        Assert.Equal(0, mirror.SetMaximumSpeedLimitCalls);
    }

    [Fact]
    public void OwnedAgentMovementDrive_PlayerDriveLeavesControllerTargetAndSpeedLimitUntouched()
    {
        using var fixture = new MissionEngineFixture();
        Agent agent = ObjectHelper.SkipConstructor<Agent>();
        var originalTargetPosition = new Vec2(5f, 8f);
        var originalTargetDirection = new Vec3(0f, 1f, 0f);
        var mirror = new MirrorAgent
        {
            Controller = AgentControllerType.Player,
            IsAiPaused = true,
            LookDirection = new Vec3(1f, 0f, 0f),
            MovementLockedState = AgentMovementLockedState.FrameLocked,
            LastTargetPosition = originalTargetPosition,
            LastTargetDirection = originalTargetDirection,
            MaximumSpeedLimit = 0.5f,
            LastMaximumSpeedLimitIsMultiplier = true,
        };
        AgentMirror.Bind(agent, mirror);

        BattleDebugCommands.ApplyOwnedAgentMovementDrive(agent, applyAiDrive: false);

        Assert.Equal(1, mirror.AddAccelerationCalls);
        Assert.Equal(AgentControllerType.Player, mirror.Controller);
        Assert.True(mirror.IsAiPaused);
        Assert.Equal(AgentMovementLockedState.FrameLocked, mirror.MovementLockedState);
        Assert.Equal(0, mirror.SetTargetPositionCalls);
        Assert.Equal(0, mirror.SetTargetPositionAndDirectionCalls);
        Assert.Equal(0, mirror.ClearTargetFrameCalls);
        Assert.Equal(originalTargetPosition.X, mirror.LastTargetPosition.X);
        Assert.Equal(originalTargetPosition.Y, mirror.LastTargetPosition.Y);
        Assert.Equal(originalTargetDirection.X, mirror.LastTargetDirection.X);
        Assert.Equal(originalTargetDirection.Y, mirror.LastTargetDirection.Y);
        Assert.Equal(0.5f, mirror.MaximumSpeedLimit);
        Assert.True(mirror.LastMaximumSpeedLimitIsMultiplier);
        Assert.Equal(0, mirror.SetMaximumSpeedLimitCalls);
    }

    [Fact]
    public void OwnedAgentMovementDrive_AiToPlayerTransitionClearsFixtureTarget()
    {
        using var fixture = new MissionEngineFixture();
        Agent agent = ObjectHelper.SkipConstructor<Agent>();
        var originalTargetPosition = new Vec2(3f, 5f);
        var originalTargetDirection = new Vec3(0f, 1f, 0f);
        var mirror = new MirrorAgent
        {
            Controller = AgentControllerType.AI,
            LookDirection = new Vec3(1f, 0f, 0f),
            MovementLockedState = AgentMovementLockedState.FrameLocked,
            LastTargetPosition = originalTargetPosition,
            LastTargetDirection = originalTargetDirection,
        };
        AgentMirror.Bind(agent, mirror);

        bool applyAiDrive = BattleDebugCommands.ApplyOwnedAgentMovementDrive(
            agent,
            applyAiDrive: true);
        mirror.Controller = AgentControllerType.Player;

        applyAiDrive = BattleDebugCommands.ApplyOwnedAgentMovementDrive(agent, applyAiDrive);

        Assert.False(applyAiDrive);
        Assert.Equal(AgentMovementLockedState.None, mirror.MovementLockedState);
        Assert.Equal(1, mirror.SetTargetPositionAndDirectionCalls);
        Assert.Equal(1, mirror.ClearTargetFrameCalls);

        BattleDebugCommands.RestoreOwnedAgentMovementDrive(
            agent,
            Agent.MovementControlFlag.None,
            Vec2.Zero,
            applyAiDrive,
            false,
            AgentMovementLockedState.FrameLocked,
            originalTargetPosition,
            originalTargetDirection);

        Assert.Equal(AgentControllerType.Player, mirror.Controller);
        Assert.Equal(AgentMovementLockedState.None, mirror.MovementLockedState);
        Assert.Equal(1, mirror.SetTargetPositionAndDirectionCalls);
        Assert.Equal(1, mirror.ClearTargetFrameCalls);
    }
    [Fact]
    public void MovementDriveLifecycle_RestoresUnchangedAuthorityOnce()
    {
        using var fixture = new MovementDriveFixture();
        fixture.Begin();
        fixture.Behavior.TickOwnedAgentMovementDrive(fixture.Mission.Shell, 1f);
        Assert.Equal(2, fixture.Mirror.AddAccelerationCalls);

        fixture.Behavior.CancelOwnedAgentMovementDrive();
        fixture.Behavior.CancelOwnedAgentMovementDrive();
        fixture.Behavior.TickOwnedAgentMovementDrive(fixture.Mission.Shell, 2f);

        Assert.Equal(2, fixture.Mirror.AddAccelerationCalls);
        Assert.Equal(Agent.MovementControlFlag.Backward, fixture.Mirror.MovementFlags);
        Assert.Equal(-0.5f, fixture.Mirror.InputVector.Y);
        Assert.True(fixture.Mirror.IsAiPaused);
        Assert.Equal(1, fixture.Mirror.ClearTargetFrameCalls);
        Assert.Equal(0, fixture.Behavior.OwnedAgentMovementDriveAgentCount);
        Assert.Equal(1, (int)fixture.Status["restoredAgents"]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MovementDriveLifecycle_HostEpochChangeStopsDriveAndRestoresUnchangedAuthority(bool cancelDirectly)
    {
        using var fixture = new MovementDriveFixture();
        fixture.Mirror.MovementLockedState = AgentMovementLockedState.FrameLocked;
        fixture.Mirror.LastTargetPosition = new Vec2(12f, 18f);
        fixture.Mirror.LastTargetDirection = new Vec3(0f, 1f, 0f);
        fixture.Begin();
        fixture.Session.SetupGet(session => session.HostEpoch).Returns(2);

        if (!cancelDirectly)
            fixture.Behavior.TickOwnedAgentMovementDrive(fixture.Mission.Shell, 1f);
        fixture.Behavior.CancelOwnedAgentMovementDrive();
        fixture.Behavior.TickOwnedAgentMovementDrive(fixture.Mission.Shell, 2f);

        Assert.Equal(1, fixture.Mirror.AddAccelerationCalls);
        Assert.Equal(Agent.MovementControlFlag.Backward, fixture.Mirror.MovementFlags);
        Assert.Equal(-0.5f, fixture.Mirror.InputVector.Y);
        Assert.True(fixture.Mirror.IsAiPaused);
        Assert.Equal(AgentMovementLockedState.FrameLocked, fixture.Mirror.MovementLockedState);
        Assert.Equal(12f, fixture.Mirror.LastTargetPosition.X);
        Assert.Equal(18f, fixture.Mirror.LastTargetPosition.Y);
        Assert.Equal(0f, fixture.Mirror.LastTargetDirection.X);
        Assert.Equal(1f, fixture.Mirror.LastTargetDirection.Y);
        Assert.Equal(2, fixture.Mirror.SetTargetPositionAndDirectionCalls);
        Assert.Equal(0, fixture.Mirror.ClearTargetFrameCalls);
        Assert.Equal(0, fixture.Behavior.OwnedAgentMovementDriveAgentCount);
        Assert.Equal(1, (int)fixture.Status["restoredAgents"]);
    }

    [Fact]
    public void MovementDriveLifecycle_HostEpochAndAuthorityChangeRejectsSuccessorStateRestoration()
    {
        using var fixture = new MovementDriveFixture();
        fixture.Begin();
        fixture.Session.SetupGet(session => session.HostEpoch).Returns(2);
        Assert.True(fixture.Registry.TryTransferAuthority("B", fixture.AgentId));
        fixture.SetSuccessorState();

        fixture.Behavior.TickOwnedAgentMovementDrive(fixture.Mission.Shell, 1f);
        fixture.Behavior.CancelOwnedAgentMovementDrive();

        fixture.AssertSuccessorStateUnchanged();
        Assert.Equal(1, (int)fixture.Status["invalidatedAgents"]);
        Assert.Equal(0, (int)fixture.Status["restoredAgents"]);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void MovementDriveLifecycle_AuthorityTransferRejectsSavedState(
        bool returnsToOriginalAuthority,
        bool cancelDirectly)
    {
        using var fixture = new MovementDriveFixture();
        fixture.Begin();
        Assert.True(fixture.Registry.TryTransferAuthority("B", fixture.AgentId));
        if (returnsToOriginalAuthority)
            Assert.True(fixture.Registry.TryTransferAuthority("A", fixture.AgentId));
        fixture.SetSuccessorState();

        if (!cancelDirectly)
            fixture.Behavior.TickOwnedAgentMovementDrive(fixture.Mission.Shell, 1f);
        fixture.Behavior.CancelOwnedAgentMovementDrive();

        fixture.AssertSuccessorStateUnchanged();
        Assert.Equal(1, (int)fixture.Status["invalidatedAgents"]);
    }

    [Fact]
    public void MovementDriveLifecycle_ReRegisteredIdentityCannotReuseSavedState()
    {
        using var fixture = new MovementDriveFixture();
        fixture.Begin();
        Assert.True(fixture.Registry.RemoveAgent(fixture.AgentId));
        Assert.True(fixture.Registry.TryRegisterAgent("A", fixture.AgentId, fixture.Agent));
        fixture.SetSuccessorState();

        fixture.Behavior.TickOwnedAgentMovementDrive(fixture.Mission.Shell, 1f);
        fixture.Behavior.CancelOwnedAgentMovementDrive();

        fixture.AssertSuccessorStateUnchanged();
    }

    [Fact]
    public void MovementDriveLifecycle_ExpiredAuthoritySnapshotDoesNotResumeAfterAuthorityReturns()
    {
        using var fixture = new MovementDriveFixture();
        fixture.Begin();
        Assert.True(fixture.Registry.TryTransferAuthority("B", fixture.AgentId));
        fixture.Behavior.TickOwnedAgentMovementDrive(fixture.Mission.Shell, 1f);
        Assert.True(fixture.Registry.TryTransferAuthority("A", fixture.AgentId));
        fixture.SetSuccessorState();

        fixture.Behavior.TickOwnedAgentMovementDrive(fixture.Mission.Shell, 2f);
        fixture.Behavior.CancelOwnedAgentMovementDrive();

        fixture.AssertSuccessorStateUnchanged();
    }

    [Fact]
    public void MovementDriveLifecycle_DeadlineRestoresAndRepeatedStartKeepsOriginalState()
    {
        using var fixture = new MovementDriveFixture();
        fixture.Begin();
        fixture.Begin();
        Assert.Equal(2, fixture.Mirror.AddAccelerationCalls);
        Assert.Equal(1, fixture.Mirror.ClearTargetFrameCalls);

        fixture.Behavior.TickOwnedAgentMovementDrive(fixture.Mission.Shell, 3f);
        fixture.Behavior.TickOwnedAgentMovementDrive(fixture.Mission.Shell, 4f);

        Assert.Equal(2, fixture.Mirror.AddAccelerationCalls);
        Assert.Equal(2, fixture.Mirror.ClearTargetFrameCalls);
        Assert.Equal(Agent.MovementControlFlag.Backward, fixture.Mirror.MovementFlags);
        Assert.True(fixture.Mirror.IsAiPaused);
        Assert.Equal(0, fixture.Behavior.OwnedAgentMovementDriveAgentCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MovementDriveLifecycle_MissionChangeOrTeardownDiscardsSavedState(bool removedAgent)
    {
        using var fixture = new MovementDriveFixture();
        fixture.Begin();
        fixture.SetSuccessorState();
        Mission currentMission = new MockMission().Shell;
        if (removedAgent)
        {
            fixture.Mirror.Mission = null;
            currentMission = fixture.Mission.Shell;
        }

        fixture.Behavior.TickOwnedAgentMovementDrive(currentMission, 1f);
        fixture.Behavior.CancelOwnedAgentMovementDrive();

        fixture.AssertSuccessorStateUnchanged();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MovementDriveLifecycle_OneAgentFailureDoesNotStrandOtherAgents(bool failDuringCancel)
    {
        using var fixture = new MovementDriveFixture();
        var secondAgent = ObjectHelper.SkipConstructor<Agent>();
        var secondMirror = new MirrorAgent
        {
            Mission = fixture.Mission.Shell,
            Controller = AgentControllerType.AI,
            LookDirection = new Vec3(1f, 0f, 0f),
            MovementFlags = Agent.MovementControlFlag.Backward,
            IsAiPaused = true,
        };
        AgentMirror.Bind(secondAgent, secondMirror);
        Assert.True(fixture.Registry.TryRegisterAgent("A", Guid.NewGuid(), secondAgent));
        bool failFirstAgent = false;
        var registry = new Mock<INetworkAgentRegistry>();
        registry.Setup(value => value.TryGetAgentInfo(It.IsAny<Guid>(), out It.Ref<CoopAgentInfo>.IsAny))
            .Returns((Guid id, out CoopAgentInfo info) =>
            {
                if (failFirstAgent && id == fixture.AgentId)
                    throw new InvalidOperationException("fixture registry failure");
                return fixture.Registry.TryGetAgentInfo(id, out info);
            });
        fixture.Begin(registry.Object);
        failFirstAgent = true;

        if (!failDuringCancel)
            fixture.Behavior.TickOwnedAgentMovementDrive(fixture.Mission.Shell, 1f);
        fixture.Behavior.CancelOwnedAgentMovementDrive();
        fixture.Behavior.CancelOwnedAgentMovementDrive();

        Assert.Equal(failDuringCancel ? 1 : 2, secondMirror.AddAccelerationCalls);
        Assert.Equal(Agent.MovementControlFlag.Backward, secondMirror.MovementFlags);
        Assert.True(secondMirror.IsAiPaused);
        Assert.Equal(1, secondMirror.ClearTargetFrameCalls);
        Assert.Equal(0, fixture.Behavior.OwnedAgentMovementDriveAgentCount);
        Assert.True((int)fixture.Status["failedAgents"] > 0);
        Assert.Equal(1, (int)fixture.Status["restoredAgents"]);
    }

    private sealed class MovementDriveFixture : IDisposable
    {
        private readonly MissionEngineFixture engine = new MissionEngineFixture();
        public MockMission Mission { get; } = new MockMission();
        public Mock<IBattleSession> Session { get; } = new Mock<IBattleSession>();
        public NetworkAgentRegistry Registry { get; }
        public BattleDebugCommands.BattleDebugTickBehavior Behavior { get; } =
            new BattleDebugCommands.BattleDebugTickBehavior();
        public Agent Agent { get; } = ObjectHelper.SkipConstructor<Agent>();
        public Guid AgentId { get; } = Guid.NewGuid();
        public MirrorAgent Mirror { get; }
        public JObject Status => JObject.FromObject(Behavior.GetOwnedAgentMovementDriveStatus());

        public MovementDriveFixture()
        {
            Session.SetupGet(session => session.HasInstance).Returns(true);
            Session.SetupGet(session => session.InstanceId).Returns("battle-drive");
            Session.SetupGet(session => session.OwnControllerId).Returns("A");
            Session.SetupGet(session => session.HostEpoch).Returns(1);
            var controllerIdProvider = new Mock<IControllerIdProvider>();
            controllerIdProvider.SetupGet(provider => provider.ControllerId).Returns("A");
            Registry = new NetworkAgentRegistry(controllerIdProvider.Object);
            Mirror = new MirrorAgent
            {
                Mission = Mission.Shell,
                Controller = AgentControllerType.AI,
                MovementFlags = Agent.MovementControlFlag.Backward,
                InputVector = new Vec2(0.25f, -0.5f),
                LookDirection = new Vec3(1f, 0f, 0f),
                IsAiPaused = true,
            };
            AgentMirror.Bind(Agent, Mirror);
            Assert.True(Registry.TryRegisterAgent("A", AgentId, Agent));
        }

        public void Begin(INetworkAgentRegistry registry = null)
        {
            Behavior.BeginOwnedAgentMovementDrive(
                Mission.Shell, registry ?? Registry, Session.Object, Registry.GetAgents("A"), 3f);
        }

        public void SetSuccessorState()
        {
            Mirror.MovementFlags = Agent.MovementControlFlag.TurnLeft;
            Mirror.InputVector = new Vec2(-1f, 0f);
            Mirror.IsAiPaused = false;
            Mirror.MovementLockedState = AgentMovementLockedState.PositionLocked;
            Mirror.LastTargetPosition = new Vec2(45f, 60f);
        }

        public void AssertSuccessorStateUnchanged()
        {
            Assert.Equal(1, Mirror.AddAccelerationCalls);
            Assert.Equal(Agent.MovementControlFlag.TurnLeft, Mirror.MovementFlags);
            Assert.Equal(-1f, Mirror.InputVector.X);
            Assert.Equal(0f, Mirror.InputVector.Y);
            Assert.False(Mirror.IsAiPaused);
            Assert.Equal(AgentMovementLockedState.PositionLocked, Mirror.MovementLockedState);
            Assert.Equal(45f, Mirror.LastTargetPosition.X);
            Assert.Equal(60f, Mirror.LastTargetPosition.Y);
            Assert.Equal(1, Mirror.SetTargetPositionAndDirectionCalls);
            Assert.Equal(0, Mirror.ClearTargetFrameCalls);
            Assert.Equal(0, Behavior.OwnedAgentMovementDriveAgentCount);
        }

        public void Dispose()
        {
            Behavior.CancelOwnedAgentMovementDrive();
            Registry.Dispose();
            engine.Dispose();
        }
    }

}
#endif
