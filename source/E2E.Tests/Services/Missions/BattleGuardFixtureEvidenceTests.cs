#if DEBUG
using GameInterface.Services.Battles.Messages;
using Missions.Battles;
using ProtoBuf;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using Xunit;

namespace E2E.Tests.Services.Missions;

public class BattleGuardFixtureEvidenceTests
{
    [Fact]
    public void Reaction_ReturningToGuardBeforeTerminalProgress_IsInterrupted()
    {
        var evidence = new BattleGuardReactionEvidence();

        evidence.Observe(
            receivedReactionActive: true,
            exactVisual: true,
            returnedToExactGuard: false,
            channel: 1,
            actionIndex: 101,
            animationIndex: 201,
            visualProgress: 0.5f,
            speed: 7.5f,
            dt: 0.02f);
        evidence.Observe(
            receivedReactionActive: false,
            exactVisual: false,
            returnedToExactGuard: true,
            channel: -1,
            actionIndex: -1,
            animationIndex: -1,
            visualProgress: -1f,
            speed: 7.5f,
            dt: 0.02f);

        Assert.True(evidence.HasStarted);
        Assert.True(evidence.Interrupted);
        Assert.False(evidence.Completed);
        Assert.False(evidence.Active);
        Assert.Equal(0.5f, evidence.MaxVisualProgress);
        Assert.Equal(7.5f, evidence.OnsetSpeed);
    }

    [Fact]
    public void Reaction_ReturningToGuardAfterVisibleReaction_Completes()
    {
        var evidence = new BattleGuardReactionEvidence();

        evidence.Observe(
            receivedReactionActive: true,
            exactVisual: true,
            returnedToExactGuard: false,
            channel: 1,
            actionIndex: 101,
            animationIndex: 201,
            visualProgress: 0.2f,
            speed: 6f,
            dt: 0.02f);
        evidence.Observe(
            receivedReactionActive: false,
            exactVisual: true,
            returnedToExactGuard: false,
            channel: -1,
            actionIndex: -1,
            animationIndex: -1,
            visualProgress: 0.70f,
            speed: 6f,
            dt: 0.02f);
        evidence.Observe(
            receivedReactionActive: false,
            exactVisual: false,
            returnedToExactGuard: true,
            channel: -1,
            actionIndex: -1,
            animationIndex: -1,
            visualProgress: -1f,
            speed: 6f,
            dt: 0.02f);

        Assert.True(evidence.Completed);
        Assert.False(evidence.Interrupted);
        Assert.False(evidence.Active);
        Assert.Equal(0.70f, evidence.MaxVisualProgress);
        Assert.Equal(0.04f, evidence.VisualDurationSeconds, 3);
    }

    [Fact]
    public void Reaction_SameSemanticActionWithVisualVariant_IsNotInterrupted()
    {
        var evidence = new BattleGuardReactionEvidence();

        evidence.Observe(
            receivedReactionActive: true,
            exactVisual: false,
            returnedToExactGuard: false,
            channel: 1,
            actionIndex: 101,
            animationIndex: 201,
            visualProgress: -1f,
            speed: 0f,
            dt: 0.02f);
        evidence.Observe(
            receivedReactionActive: true,
            exactVisual: true,
            returnedToExactGuard: false,
            channel: 1,
            actionIndex: 101,
            animationIndex: 202,
            visualProgress: 0.75f,
            speed: 0f,
            dt: 0.02f);

        Assert.True(evidence.Active);
        Assert.False(evidence.Interrupted);
        Assert.Equal(202, evidence.AnimationIndex);
        Assert.Equal(0.75f, evidence.MaxVisualProgress);
    }

    [Fact]
    public void Replay_RecordsInstalledAnimationAndProgressRewind()
    {
        var evidence = new BattleGuardReplayEvidence();

        evidence.CapturePre(
            Frame(0, 10, 0.8f, 1f),
            Frame(1, 20, 0.5f, 1f));
        evidence.ObservePost(
            Frame(0, 11, 0.1f, 1f),
            Frame(1, 20, 0.4f, 1.5f));

        Assert.Equal(1, evidence.PairedFrames);
        Assert.Equal(1, evidence.AnimationChanges);
        Assert.Equal(1, evidence.ProgressRewinds);
        Assert.Equal(0.1f, evidence.MaxProgressDelta, 3);
        Assert.Equal(0.5f, evidence.MaxSpeedDelta, 3);
    }

    [Fact]
    public void AnimationTrace_RecordsRunsProgressAndSpeed()
    {
        var evidence = new BattleGuardAnimationEvidence();

        evidence.ObserveFrame(
            0.02f,
            Frame(0, 10, 0.2f, 1f),
            Frame(1, -1, -1f, -1f));
        evidence.ObserveFrame(
            0.02f,
            Frame(0, 10, 0.4f, 2f),
            Frame(1, -1, -1f, -1f));
        evidence.ObserveFrame(
            0.02f,
            Frame(0, 10, 0.4f, 0.5f),
            Frame(1, -1, -1f, -1f));
        evidence.ObserveFrame(
            0.02f,
            Frame(0, 10, 0.1f, 1f),
            Frame(1, -1, -1f, -1f));
        evidence.ObserveFrame(
            0.02f,
            Frame(0, 20, 0.1f, 1f),
            Frame(1, -1, -1f, -1f));
        evidence.ObserveFrame(
            0.02f,
            Frame(0, 10, 0.3f, 1.5f),
            Frame(1, -1, -1f, -1f));

        Assert.True(evidence.TryGetTrace(0, 10, out BattleGuardAnimationTrace trace));
        Assert.Equal(5, trace.Samples);
        Assert.Equal(2, trace.RunStarts);
        Assert.Equal(4, trace.MaxRunSamples);
        Assert.Equal(0.1f, trace.DurationSeconds, 3);
        Assert.Equal(0.08f, trace.MaxRunSeconds, 3);
        Assert.Equal(0.1f, trace.ProgressMin);
        Assert.Equal(0.4f, trace.ProgressMax);
        Assert.Equal(0.3f, trace.ProgressSpan, 3);
        Assert.Equal(1, trace.ProgressAdvances);
        Assert.Equal(1, trace.ProgressStalls);
        Assert.Equal(1, trace.NonCyclicProgressResets);
        Assert.Equal(0.25f, trace.MaxNormalizedProgressStep, 3);
        Assert.Equal(1.5f, trace.CurrentSpeed);
        Assert.Equal(0.5f, trace.SpeedMin);
        Assert.Equal(2f, trace.SpeedMax);
        Assert.Equal(1.2f, trace.SpeedMean, 3);
    }

    [Fact]
    public void AnimationTrace_CyclicWrap_AdvancesWithoutReset()
    {
        var evidence = new BattleGuardAnimationEvidence();

        evidence.ObserveFrame(
            0.02f,
            Frame(0, 10, 0.9f, 1f, isCyclic: true),
            Frame(1, -1, -1f, -1f));
        evidence.ObserveFrame(
            0.02f,
            Frame(0, 10, 0.1f, 1f, isCyclic: true),
            Frame(1, -1, -1f, -1f));

        Assert.True(evidence.TryGetTrace(0, 10, out BattleGuardAnimationTrace trace));
        Assert.Equal(1, trace.ProgressAdvances);
        Assert.Equal(0, trace.NonCyclicProgressResets);
    }

    [Fact]
    public void AnimationTrace_NonCyclicRewind_RecordsReset()
    {
        var evidence = new BattleGuardAnimationEvidence();

        evidence.ObserveFrame(
            0.02f,
            Frame(0, 10, 0.9f, 1f),
            Frame(1, -1, -1f, -1f));
        evidence.ObserveFrame(
            0.02f,
            Frame(0, 10, 0.1f, 1f),
            Frame(1, -1, -1f, -1f));

        Assert.True(evidence.TryGetTrace(0, 10, out BattleGuardAnimationTrace trace));
        Assert.Equal(0, trace.ProgressAdvances);
        Assert.Equal(1, trace.NonCyclicProgressResets);
    }

    [Fact]
    public void GuardContinuity_ExactGuardDisappears_RecordsInterruption()
    {
        var evidence = new BattleGuardContinuityEvidence();

        evidence.Observe(exact: true, dt: 0.05f);
        evidence.Observe(exact: false, dt: 0.05f);

        Assert.Equal(2, evidence.Samples);
        Assert.Equal(1, evidence.ExactSamples);
        Assert.Equal(1, evidence.Interruptions);
        Assert.Equal(0.05f, evidence.MaxExactRunSeconds, 3);
    }

    [Fact]
    public void AnimationEvidence_MissingFrames_AreNotTraced()
    {
        var evidence = new BattleGuardAnimationEvidence();

        evidence.ObserveFrame(
            0.02f,
            Frame(0, -1, float.NaN, float.NaN),
            Frame(1, -1, -1f, -1f));

        Assert.Empty(evidence.Traces);
    }

    [Fact]
    public void SpeedEvidence_StableRollingWindow_ReachesPlateau()
    {
        var evidence = new BattleGuardSpeedEvidence();

        for (int index = 0; index < 40; index++)
            evidence.Observe(5f, 0.05f);

        Assert.True(evidence.PlateauReady);
        Assert.Equal(40, evidence.Samples);
        Assert.Equal(40, evidence.RecentSamples);
        Assert.Equal(5f, evidence.RecentMedian);
        Assert.Equal(0f, evidence.RecentSpread);
        Assert.Equal(0f, evidence.RecentSlope);
    }

    [Fact]
    public void SpeedEvidence_EndpointGaitNoise_DoesNotCreateFalseSlope()
    {
        var evidence = new BattleGuardSpeedEvidence();

        for (int index = 0; index < 40; index++)
        {
            float speed = index switch
            {
                0 => 8.4f,
                39 => 7.6f,
                _ => 8f
            };
            evidence.Observe(speed, 0.05f);
        }

        Assert.True(evidence.PlateauReady);
        Assert.InRange(evidence.RecentSlope, -0.1f, 0.1f);
    }

    [Fact]
    public void SpeedEvidence_SustainedTrend_DoesNotReachPlateau()
    {
        var evidence = new BattleGuardSpeedEvidence();

        for (int index = 0; index < 40; index++)
            evidence.Observe(8.5f - (index / 39f), 0.05f);

        Assert.False(evidence.PlateauReady);
        Assert.True(evidence.RecentSlope < -0.35f);
    }

    [Fact]
    public void MountedRoute_StraightSegment_IsStrikeReady()
    {
        var route = new BattleGuardMountedRoute(
            new Vec3(0f, 0f, 0f),
            new Vec3(0f, 1f, 0f),
            40f);

        BattleGuardMountedRouteInput input = route.Update(
            new Vec3(0f, 10f, 0f),
            new Vec3(0f, 1f, 0f));

        Assert.Equal("Forward", route.State);
        Assert.True(route.CanStageStrike);
        Assert.Equal(30f, route.RemainingDistance);
        Assert.Equal(
            Agent.MovementControlFlag.Forward,
            input.TranslationFlag);
        Assert.Equal(Agent.MovementControlFlag.None, input.TurnFlag);
        Assert.Equal(0f, input.Movement.x);
        Assert.Equal(1f, input.Movement.y);
    }

    [Fact]
    public void MountedRoute_MovementDirection_WinsOverStaleLookDirection()
    {
        var route = new BattleGuardMountedRoute(
            new Vec3(0f, 0f, 0f),
            new Vec3(0f, 1f, 0f),
            40f);

        route.Update(
            new Vec3(0f, 30f, 0f),
            new Vec2(0f, -1f),
            new Vec3(0f, 1f, 0f),
            8f);

        Assert.Equal("Return", route.State);
        Assert.True(route.CanStageStrike);
    }

    [Fact]
    public void MountedRoute_StoppedMount_UsesLookDespiteStaleMovementDirection()
    {
        var route = new BattleGuardMountedRoute(
            new Vec3(0f, 0f, 0f),
            new Vec3(0f, 1f, 0f),
            40f);

        route.Update(
            new Vec3(0f, 30f, 0f),
            new Vec2(0f, 1f),
            new Vec3(0f, -1f, 0f),
            0f);

        Assert.Equal("Return", route.State);
        Assert.True(route.CanStageStrike);
    }

    [Fact]
    public void MountedRoute_Endpoint_BrakesDuringNativeTurnThenReturns()
    {
        var route = new BattleGuardMountedRoute(
            new Vec3(0f, 0f, 0f),
            new Vec3(0f, 1f, 0f),
            40f);

        BattleGuardMountedRouteInput braking = route.Update(
            new Vec3(0f, 35f, 0f),
            new Vec2(0f, 1f),
            new Vec3(0f, 1f, 0f),
            8f);

        Assert.Equal("BrakingToStart", route.State);
        Assert.False(route.CanStageStrike);
        Assert.Equal(
            Agent.MovementControlFlag.Backward,
            braking.TranslationFlag);
        Assert.Equal(
            Agent.MovementControlFlag.TurnRight,
            braking.TurnFlag);
        Assert.Equal(1f, braking.Movement.x);
        Assert.Equal(-1f, braking.Movement.y);

        BattleGuardMountedRouteInput reverseVelocity = route.Update(
            new Vec3(0f, 35f, 0f),
            new Vec2(0f, -1f),
            new Vec3(0f, 1f, 0f),
            0.5f);

        Assert.Equal("TurningToStart", route.State);
        Assert.False(route.CanStageStrike);
        Assert.Equal(
            Agent.MovementControlFlag.Forward,
            reverseVelocity.TranslationFlag);
        Assert.Equal(
            Agent.MovementControlFlag.TurnRight,
            reverseVelocity.TurnFlag);
        Assert.Equal(0.2f, reverseVelocity.Movement.y);

        BattleGuardMountedRouteInput turning = route.Update(
            new Vec3(0f, 35f, 0f),
            Vec2.Zero,
            new Vec3(0f, 1f, 0f),
            0f);

        Assert.Equal("TurningToStart", route.State);
        Assert.False(route.CanStageStrike);
        Assert.Equal(
            Agent.MovementControlFlag.Forward,
            turning.TranslationFlag);
        Assert.Equal(
            Agent.MovementControlFlag.TurnRight,
            turning.TurnFlag);
        Assert.Equal(0.2f, turning.Movement.y);

        BattleGuardMountedRouteInput rebraking = route.Update(
            new Vec3(0f, 35f, 0f),
            new Vec2(0.1f, -0.99f),
            new Vec3(0.1f, 0.99f, 0f),
            1.5f);

        Assert.Equal("BrakingToStart", route.State);
        Assert.False(route.CanStageStrike);
        Assert.Equal(
            Agent.MovementControlFlag.Backward,
            rebraking.TranslationFlag);
        Assert.Equal(
            Agent.MovementControlFlag.TurnRight,
            rebraking.TurnFlag);
        Assert.Equal(1f, rebraking.Movement.x);
        Assert.Equal(-1f, rebraking.Movement.y);

        BattleGuardMountedRouteInput resumedTurn = route.Update(
            new Vec3(0f, 35f, 0f),
            Vec2.Zero,
            new Vec3(0f, 1f, 0f),
            1f);

        Assert.Equal("TurningToStart", route.State);
        Assert.False(route.CanStageStrike);
        Assert.Equal(
            Agent.MovementControlFlag.Forward,
            resumedTurn.TranslationFlag);
        Assert.Equal(
            Agent.MovementControlFlag.TurnRight,
            resumedTurn.TurnFlag);
        Assert.Equal(0.2f, resumedTurn.Movement.y);

        BattleGuardMountedRouteInput returning = route.Update(
            new Vec3(0f, 35f, 0f),
            Vec2.Zero,
            new Vec3(0f, -1f, 0f),
            0f);

        Assert.Equal("Return", route.State);
        Assert.True(route.CanStageStrike);
        Assert.Equal(1, route.CompletedTurns);
        Assert.Equal(
            Agent.MovementControlFlag.Forward,
            returning.TranslationFlag);
        Assert.Equal(
            Agent.MovementControlFlag.None,
            returning.TurnFlag);
    }

    [Fact]
    public void MountedRoute_ReturnEndpoint_ReentersForwardLane()
    {
        var route = new BattleGuardMountedRoute(
            new Vec3(0f, 0f, 0f),
            new Vec3(0f, 1f, 0f),
            40f);

        route.Update(
            new Vec3(0f, 35f, 0f),
            new Vec3(0f, 1f, 0f));
        route.Update(
            new Vec3(0f, 35f, 0f),
            new Vec3(0f, -1f, 0f));
        BattleGuardMountedRouteInput turning = route.Update(
            new Vec3(0f, 5f, 0f),
            new Vec3(0f, -1f, 0f));

        Assert.Equal("TurningToEnd", route.State);
        Assert.Equal(
            Agent.MovementControlFlag.TurnRight,
            turning.TurnFlag);

        route.Update(
            new Vec3(0f, 5f, 0f),
            new Vec3(0f, 1f, 0f));

        Assert.Equal("Forward", route.State);
        Assert.Equal(2, route.CompletedTurns);
    }

    [Fact]
    public void MountedRoute_LateralDrift_SteersBackToTarget()
    {
        var route = new BattleGuardMountedRoute(
            new Vec3(0f, 0f, 0f),
            new Vec3(0f, 1f, 0f),
            40f);

        BattleGuardMountedRouteInput input = route.Update(
            new Vec3(2f, 10f, 0f),
            new Vec3(0f, 1f, 0f));

        Assert.Equal(
            Agent.MovementControlFlag.TurnLeft,
            input.TurnFlag);
        Assert.True(input.Movement.x < 0f);
    }

    [Fact]
    public void MountedRoute_OutsideClearedLane_IsNotStrikeReady()
    {
        var route = new BattleGuardMountedRoute(
            new Vec3(0f, 0f, 0f),
            new Vec3(0f, 1f, 0f),
            40f);

        route.Update(
            new Vec3(4f, 10f, 0f),
            new Vec3(0f, 1f, 0f));

        Assert.False(route.CanStageStrike);
    }

    [Fact]
    public void MountedRoute_FirstRemoteSample_InfersReturnDirection()
    {
        var route = new BattleGuardMountedRoute(
            new Vec3(0f, 0f, 0f),
            new Vec3(0f, 1f, 0f),
            40f);

        route.Update(
            new Vec3(0f, 30f, 0f),
            new Vec3(0f, -1f, 0f));

        Assert.Equal("Return", route.State);
        Assert.True(route.CanStageStrike);
        Assert.Equal(30f, route.RemainingDistance);
    }

    [Fact]
    public void MountedRoute_PrePositionSample_DoesNotCompleteTurn()
    {
        var route = new BattleGuardMountedRoute(
            new Vec3(0f, 25f, 0f),
            new Vec3(0f, 1f, 0f),
            40f);

        BattleGuardMountedRouteInput pending = route.Update(
            Vec3.Zero,
            new Vec3(0f, -1f, 0f));

        Assert.Equal("Pending", route.State);
        Assert.Equal(0, route.CompletedTurns);
        Assert.Equal(Vec2.Zero, pending.Movement);
        Assert.Equal(
            Agent.MovementControlFlag.None,
            pending.TranslationFlag);
        Assert.Equal(
            Agent.MovementControlFlag.None,
            pending.TurnFlag);

        route.Update(
            new Vec3(0f, 25f, 0f),
            new Vec3(0f, 1f, 0f));

        Assert.Equal("Forward", route.State);
        Assert.Equal(0, route.CompletedTurns);
    }

    [Fact]
    public void MountedRoute_AuthoritativeDefinition_RoundTrips()
    {
        Guid commandId = Guid.NewGuid();
        var original = new NetworkBattleGuardFixtureRoute(
            "battle",
            commandId,
            Guid.NewGuid(),
            "guard-owner",
            12f,
            34f,
            2f,
            0.6f,
            0.8f,
            40f,
            BattleGuardFixturePhase.Attack);
        using var stream = new MemoryStream();

        Serializer.Serialize(stream, original);
        stream.Position = 0;
        NetworkBattleGuardFixtureRoute received =
            Serializer.Deserialize<NetworkBattleGuardFixtureRoute>(stream);
        var route = new BattleGuardMountedRoute(
            new Vec3(received.StartX, received.StartY, received.StartZ),
            new Vec3(received.DirectionX, received.DirectionY, 0f),
            received.Length);

        Assert.Equal(original.BattleInstanceId, received.BattleInstanceId);
        Assert.Equal(commandId, received.CommandId);
        Assert.Equal(original.GuardAgentId, received.GuardAgentId);
        Assert.Equal(original.GuardAuthority, received.GuardAuthority);
        Assert.Equal(BattleGuardFixturePhase.Attack, received.Phase);
        Assert.Equal(new Vec3(12f, 34f, 2f), route.Start);
        Assert.Equal(new Vec3(0.6f, 0.8f, 0f), route.Direction);
        Assert.Equal(40f, route.Length);
    }

    [Fact]
    public void MountedRouteArrival_ClearsRouteWaitError()
    {
        Assert.Null(
            BattleGuardFixture.ClearMountedRouteWaitError(
                "waiting for mounted guard route"));
    }

    [Fact]
    public void MountedRouteArrival_PreservesSetupError()
    {
        const string Error = "fixture weapon is unavailable";

        Assert.Equal(
            Error,
            BattleGuardFixture.ClearMountedRouteWaitError(Error));
    }

    [Fact]
    public void FixtureWieldState_RequiresModeUsageAndNoOffhand()
    {
        Assert.True(
            BattleGuardFixture.IsFixtureWieldState(
                BattleGuardFixtureMode.Foot,
                EquipmentIndex.Weapon0,
                EquipmentIndex.None,
                1,
                "empire_lance_1_t3_blunt"));
        Assert.False(
            BattleGuardFixture.IsFixtureWieldState(
                BattleGuardFixtureMode.Foot,
                EquipmentIndex.Weapon0,
                EquipmentIndex.None,
                0,
                "empire_lance_1_t3_blunt"));
        Assert.True(
            BattleGuardFixture.IsFixtureWieldState(
                BattleGuardFixtureMode.Mounted,
                EquipmentIndex.Weapon0,
                EquipmentIndex.None,
                0,
                "empire_lance_1_t3_blunt"));
        Assert.False(
            BattleGuardFixture.IsFixtureWieldState(
                BattleGuardFixtureMode.Mounted,
                EquipmentIndex.Weapon0,
                EquipmentIndex.None,
                1,
                "empire_lance_1_t3_blunt"));
        Assert.False(
            BattleGuardFixture.IsFixtureWieldState(
                BattleGuardFixtureMode.Mounted,
                EquipmentIndex.Weapon0,
                EquipmentIndex.Weapon1,
                0,
                "empire_lance_1_t3_blunt"));
    }

    private static BattleGuardAnimationFrame Frame(
        int channel,
        int animationIndex,
        float progress,
        float speed,
        bool isCyclic = false)
    {
        return new BattleGuardAnimationFrame(
            channel,
            animationIndex,
            progress,
            speed,
            isCyclic);
    }
}
#endif
