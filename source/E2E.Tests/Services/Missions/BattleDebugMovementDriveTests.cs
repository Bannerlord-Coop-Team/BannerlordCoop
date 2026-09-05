#if DEBUG
using Common.Util;
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
}
#endif
