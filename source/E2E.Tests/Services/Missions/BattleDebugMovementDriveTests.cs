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
    public void OwnedAgentMovementDrive_AppliesNativeTargetAndRestoresState()
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
            Controller = AgentControllerType.Player,
            IsAiPaused = true,
            MaximumSpeedLimit = 0f,
        };
        AgentMirror.Bind(agent, mirror);

        Agent.MovementControlFlag originalLocomotion =
            mirror.MovementFlags & Agent.MovementControlFlag.MoveMask;
        Vec2 originalInput = mirror.InputVector;
        AgentControllerType originalController = mirror.Controller;
        bool originalIsAiPaused = mirror.IsAiPaused;
        float originalMaximumSpeedLimit = mirror.MaximumSpeedLimit;

        BattleDebugCommands.ApplyOwnedAgentMovementDrive(agent);

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
        Assert.Equal(-1f, mirror.MaximumSpeedLimit);
        Assert.Equal(1, mirror.SetTargetPositionAndDirectionCalls);
        Assert.InRange(mirror.LastTargetPosition.X, 15.99f, 16.01f);
        Assert.InRange(mirror.LastTargetPosition.Y, 20.99f, 21.01f);

        mirror.MovementFlags |= Agent.MovementControlFlag.DefendRight;
        BattleDebugCommands.RestoreOwnedAgentMovementDrive(
            agent,
            originalLocomotion,
            originalInput,
            originalController,
            originalIsAiPaused,
            originalMaximumSpeedLimit);

        Assert.Equal(
            Agent.MovementControlFlag.Backward |
            Agent.MovementControlFlag.TurnLeft |
            Agent.MovementControlFlag.DefendBlock |
            Agent.MovementControlFlag.DefendRight,
            mirror.MovementFlags);
        Assert.Equal(originalInput.X, mirror.InputVector.X);
        Assert.Equal(originalInput.Y, mirror.InputVector.Y);
        Assert.Equal(originalController, mirror.Controller);
        Assert.Equal(originalIsAiPaused, mirror.IsAiPaused);
        Assert.Equal(originalMaximumSpeedLimit, mirror.MaximumSpeedLimit);
    }
}
#endif
