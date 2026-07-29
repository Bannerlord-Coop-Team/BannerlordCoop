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
        Assert.Equal(new Vec3(0f, 1f, 0f), input.LookDirection);
    }

    [Fact]
    public void MountedStrikeStraightInput_HoldsTravelAndGuardDirections()
    {
        BattleGuardMountedRouteInput input =
            BattleGuardMountedRoute.CreateStraightInput(
                new Vec2(0f, 1f),
                new Vec3(-1f, 0f, 0f),
                7.5f,
                new Vec3(0f, 2f, 1f),
                new Vec3(2f, 0f, 1f));

        Assert.Equal(
            Agent.MovementControlFlag.Forward,
            input.TranslationFlag);
        Assert.Equal(
            Agent.MovementControlFlag.None,
            input.TurnFlag);
        Assert.Equal(0f, input.Movement.x);
        Assert.Equal(1f, input.Movement.y);
        Assert.Equal(
            new Vec3(1f, 0f, 0f),
            input.LookDirection);
    }

    [Theory]
    [InlineData(0.2f, 0.98f, Agent.MovementControlFlag.TurnLeft)]
    [InlineData(-0.2f, 0.98f, Agent.MovementControlFlag.TurnRight)]
    public void MountedStrikeStraightInput_CorrectsTravelHeading(
        float movementX,
        float movementY,
        Agent.MovementControlFlag expectedTurn)
    {
        BattleGuardMountedRouteInput input =
            BattleGuardMountedRoute.CreateStraightInput(
                new Vec2(movementX, movementY),
                new Vec3(0f, 1f, 0f),
                7.5f,
                new Vec3(0f, 1f, 0f),
                new Vec3(1f, 0f, 0f));

        Assert.Equal(expectedTurn, input.TurnFlag);
        Assert.Equal(
            Math.Sign(movementX) * -1,
            Math.Sign(input.Movement.x));
        Assert.Equal(1f, input.Movement.y);
        Assert.Equal(
            new Vec3(1f, 0f, 0f),
            input.LookDirection);
    }

    [Theory]
    [InlineData(92.5f, true)]
    [InlineData(92.51f, false)]
    public void MountedStrikeRunway_AccountsForLeadBeforeRouteTurn(
        float routeProgress,
        bool expected)
    {
        var route = new BattleGuardMountedRoute(
            new Vec3(0f, 0f, 0f),
            new Vec3(0f, 1f, 0f),
            120f);

        route.Update(
            new Vec3(0f, routeProgress, 0f),
            new Vec3(0f, 1f, 0f));

        Assert.True(route.CanStageStrike);
        Assert.Equal(
            expected,
            BattleGuardFixture.HasMountedStrikeRunway(route));
    }

    [Fact]
    public void MountedStrikeRunway_ReturnLegCanStageStraightStrike()
    {
        var route = new BattleGuardMountedRoute(
            new Vec3(0f, 0f, 0f),
            new Vec3(0f, 1f, 0f),
            120f);

        route.Update(
            new Vec3(0f, 60f, 0f),
            new Vec3(0f, -1f, 0f));

        Assert.Equal("Return", route.State);
        Assert.True(route.CanStageStrike);
        Assert.True(
            BattleGuardFixture.HasMountedStrikeRunway(route));
    }

    [Fact]
    public void MountedStrikeRunway_RemoteStrikerTrustsOwnerGate()
    {
        Assert.False(
            BattleGuardFixture.HasMountedStrikeStagingRunway(
                route: null,
                guardLocallyDriven: true));
        Assert.True(
            BattleGuardFixture.HasMountedStrikeStagingRunway(
                route: null,
                guardLocallyDriven: false));
    }

    [Theory]
    [InlineData(3f, true)]
    [InlineData(3.01f, false)]
    [InlineData(-3.01f, false)]
    public void MountedStrikeRunway_UsesRouteStraightLaneBound(
        float lateralOffset,
        bool expected)
    {
        var route = new BattleGuardMountedRoute(
            new Vec3(0f, 0f, 0f),
            new Vec3(0f, 1f, 0f),
            120f);

        route.Update(
            new Vec3(lateralOffset, 60f, 0f),
            new Vec3(0f, 1f, 0f));

        Assert.Equal(expected, route.CanStageStrike);
        Assert.Equal(
            expected,
            BattleGuardFixture.HasMountedStrikeRunway(route));
    }

    [Theory]
    [InlineData(0f, 1f, 0f, 2f, true)]
    [InlineData(0.1f, 0.995f, 0f, 1f, true)]
    [InlineData(0.2f, 0.98f, 0f, 1f, false)]
    [InlineData(0f, 0f, 0f, 1f, false)]
    public void MountedStrikeTravelAlignment_WaitsForSettledGuardFacing(
        float travelX,
        float travelY,
        float lookX,
        float lookY,
        bool expected)
    {
        Assert.Equal(
            expected,
            BattleGuardFixture.HasMountedStrikeTravelAlignment(
                new Vec3(travelX, travelY, 0f),
                new Vec3(lookX, lookY, 0f)));
    }

    [Theory]
    [InlineData(0.15f, 0.989f, true, false)]
    [InlineData(0.15f, 0.989f, false, true)]
    [InlineData(0.33f, 0.944f, false, false)]
    public void MountedStrikeTravelAlignment_RemoteStrikerUsesContactOracle(
        float lookX,
        float lookY,
        bool guardLocallyDriven,
        bool expected)
    {
        Assert.Equal(
            expected,
            BattleGuardFixture.HasMountedStrikeStagingAlignment(
                new Vec3(0f, 1f, 0f),
                new Vec3(lookX, lookY, 0f),
                guardLocallyDriven));
    }

    [Theory]
    [InlineData(true, false, BattleGuardFixtureDirection.Left, BattleGuardFixtureDirection.Left, true)]
    [InlineData(true, true, BattleGuardFixtureDirection.Left, BattleGuardFixtureDirection.Left, false)]
    [InlineData(true, true, BattleGuardFixtureDirection.Right, BattleGuardFixtureDirection.Left, true)]
    [InlineData(false, true, BattleGuardFixtureDirection.Right, BattleGuardFixtureDirection.Left, true)]
    [InlineData(false, false, BattleGuardFixtureDirection.Right, BattleGuardFixtureDirection.Left, false)]
    public void MountedGuardState_CommandsNativeStateOnlyOnTransition(
        bool guarding,
        bool guardCommandActive,
        BattleGuardFixtureDirection direction,
        BattleGuardFixtureDirection guardCommandDirection,
        bool expected)
    {
        Assert.Equal(
            expected,
            BattleGuardFixture.ShouldCommandMountedGuardState(
                guarding,
                guardCommandActive,
                direction,
                guardCommandDirection));
    }

    [Theory]
    [InlineData(BattleGuardFixtureMode.Mounted, false, true)]
    [InlineData(BattleGuardFixtureMode.Mounted, true, false)]
    [InlineData(BattleGuardFixtureMode.Foot, false, false)]
    [InlineData(BattleGuardFixtureMode.Foot, true, false)]
    public void MovementFlagGuardInput_BypassesExplicitPresentationReplay(
        BattleGuardFixtureMode mode,
        bool useMovementFlagGuardInput,
        bool expected)
    {
        Assert.Equal(
            expected,
            BattleGuardFixture.ShouldApplyExplicitMountedGuardInput(
                mode,
                useMovementFlagGuardInput));
    }

    [Theory]
    [InlineData(BattleGuardFixtureMode.Mounted, true)]
    [InlineData(BattleGuardFixtureMode.Foot, false)]
    public void MountedGuardInput_UsesOneShotNativeCommand(
        BattleGuardFixtureMode mode,
        bool expected)
    {
        Assert.Equal(
            expected,
            BattleGuardFixture.ShouldApplyMountedGuardCommand(mode));
    }

    [Theory]
    [InlineData(true, false, false, true)]
    [InlineData(true, true, false, true)]
    [InlineData(false, false, true, false)]
    [InlineData(false, true, false, false)]
    [InlineData(false, true, true, true)]
    public void MountedGuardPresentation_ReplaysMovementFlagTransitionOnceAfterAgentTick(
        bool explicitPresentation,
        bool postAgentTick,
        bool transitionPending,
        bool expected)
    {
        Assert.Equal(
            expected,
            BattleGuardFixture.ShouldApplyMountedGuardPresentation(
                explicitPresentation,
                postAgentTick,
                transitionPending));
    }

    [Theory]
    [InlineData(true, false, BattleGuardFixtureDirection.Right, BattleGuardFixtureDirection.Left, false)]
    [InlineData(true, true, BattleGuardFixtureDirection.Left, BattleGuardFixtureDirection.Left, false)]
    [InlineData(true, true, BattleGuardFixtureDirection.Right, BattleGuardFixtureDirection.Left, true)]
    [InlineData(false, true, BattleGuardFixtureDirection.Right, BattleGuardFixtureDirection.Left, false)]
    [InlineData(false, false, BattleGuardFixtureDirection.Right, BattleGuardFixtureDirection.Left, false)]
    public void MountedGuardDirection_ResetsOnlyWhileChangingHeldDirection(
        bool guarding,
        bool guardCommandActive,
        BattleGuardFixtureDirection direction,
        BattleGuardFixtureDirection guardCommandDirection,
        bool expected)
    {
        Assert.Equal(
            expected,
            BattleGuardFixture.ShouldResetMountedGuardDirection(
                guarding,
                guardCommandActive,
                direction,
                guardCommandDirection));
    }

    [Theory]
    [InlineData(
        BattleGuardFixtureDirection.Left,
        "act_defend_left_1h_passive")]
    [InlineData(
        BattleGuardFixtureDirection.Right,
        "act_defend_right_1h_passive")]
    public void MountedGuardPresentationAction_MapsHorizontalTransitions(
        BattleGuardFixtureDirection direction,
        string expected)
    {
        Assert.Equal(
            expected,
            BattleGuardFixture.GetMountedGuardPresentationActionName(
                direction));
    }

    [Theory]
    [InlineData(BattleGuardFixtureDirection.Up)]
    [InlineData(BattleGuardFixtureDirection.Down)]
    public void MountedGuardPresentationAction_DoesNotMapVerticalDirections(
        BattleGuardFixtureDirection direction)
    {
        Assert.Null(
            BattleGuardFixture.GetMountedGuardPresentationActionName(
                direction));
    }

    [Theory]
    [InlineData(
        BattleGuardFixturePhase.Guard,
        BattleGuardFixtureDirection.Left,
        Agent.GuardMode.Left,
        true)]
    [InlineData(
        BattleGuardFixturePhase.Guard,
        BattleGuardFixtureDirection.Right,
        Agent.GuardMode.Left,
        false)]
    [InlineData(
        BattleGuardFixturePhase.Guard,
        BattleGuardFixtureDirection.Right,
        Agent.GuardMode.Right,
        true)]
    [InlineData(
        BattleGuardFixturePhase.Attack,
        BattleGuardFixtureDirection.Right,
        Agent.GuardMode.Left,
        true)]
    public void GuardPresentation_DefersOnlyMismatchedGuardPhaseDirection(
        BattleGuardFixturePhase phase,
        BattleGuardFixtureDirection expectedDirection,
        Agent.GuardMode observedGuardMode,
        bool expected)
    {
        Assert.Equal(
            expected,
            BattleGuardFixture.ShouldLatchGuardPresentation(
                phase,
                expectedDirection,
                observedGuardMode));
    }

    [Theory]
    [InlineData(true, BattleGuardFixtureDirection.Left, true)]
    [InlineData(true, BattleGuardFixtureDirection.Right, true)]
    [InlineData(true, BattleGuardFixtureDirection.Up, false)]
    [InlineData(true, BattleGuardFixtureDirection.Down, false)]
    [InlineData(false, BattleGuardFixtureDirection.Left, false)]
    [InlineData(false, BattleGuardFixtureDirection.Right, false)]
    public void MountedGuardPresentationAction_QueuesOnlyHeldHorizontalGuard(
        bool guarding,
        BattleGuardFixtureDirection direction,
        bool expected)
    {
        Assert.Equal(
            expected,
            BattleGuardFixture.ShouldQueueMountedGuardPresentation(
                guarding,
                direction));
    }

    [Theory]
    [InlineData(
        BattleGuardFixtureMode.Mounted,
        BattleGuardFixturePhase.Guard,
        BattleGuardFixtureDirection.Left,
        false,
        true)]
    [InlineData(
        BattleGuardFixtureMode.Mounted,
        BattleGuardFixturePhase.Guard,
        BattleGuardFixtureDirection.Right,
        false,
        true)]
    [InlineData(
        BattleGuardFixtureMode.Mounted,
        BattleGuardFixturePhase.Calibration,
        BattleGuardFixtureDirection.Right,
        false,
        false)]
    [InlineData(
        BattleGuardFixtureMode.Mounted,
        BattleGuardFixturePhase.Attack,
        BattleGuardFixtureDirection.Right,
        false,
        true)]
    [InlineData(
        BattleGuardFixtureMode.Mounted,
        BattleGuardFixturePhase.Attack,
        BattleGuardFixtureDirection.Right,
        true,
        false)]
    [InlineData(
        BattleGuardFixtureMode.Foot,
        BattleGuardFixturePhase.Guard,
        BattleGuardFixtureDirection.Right,
        false,
        false)]
    [InlineData(
        BattleGuardFixtureMode.Mounted,
        BattleGuardFixturePhase.Guard,
        BattleGuardFixtureDirection.Up,
        false,
        false)]
    public void MountedGuardPresentationAction_MaintainsUntilAttackReaction(
        BattleGuardFixtureMode mode,
        BattleGuardFixturePhase phase,
        BattleGuardFixtureDirection direction,
        bool reactionActive,
        bool expected)
    {
        Assert.Equal(
            expected,
            BattleGuardFixture.ShouldMaintainMountedGuardPresentation(
                mode,
                phase,
                direction,
                reactionActive));
    }

    [Theory]
    [InlineData(true, 0.5f, 0f)]
    [InlineData(false, -1f, 0f)]
    [InlineData(false, 1.1f, 0f)]
    [InlineData(false, 0f, 0f)]
    [InlineData(false, 0.42f, 0.42f)]
    public void MountedGuardPresentationAction_UsesValidStartProgress(
        bool transitionPending,
        float currentProgress,
        float expected)
    {
        Assert.Equal(
            expected,
            BattleGuardFixture.GetMountedGuardPresentationStartProgress(
                transitionPending,
                currentProgress));
    }

    [Fact]
    public void MountedGuardPresentationAction_ClampsNonFiniteStartProgress()
    {
        Assert.Equal(
            0f,
            BattleGuardFixture.GetMountedGuardPresentationStartProgress(
                false,
                float.NaN));
        Assert.Equal(
            0f,
            BattleGuardFixture.GetMountedGuardPresentationStartProgress(
                false,
                float.PositiveInfinity));
    }

    [Fact]
    public void MountedStrikerPosition_RecreatesSideInterceptionGeometry()
    {
        var guardPosition = new Vec3(2f, 3f, 4f);
        var route = new Vec3(0f, 1f, 0f);
        var lateral = new Vec3(1f, 0f, 0f);
        Vec3 guardedLook =
            BattleGuardFixture.GetMountedStrikeGuardedLookDirection(
                route);

        Vec3 contactPoint =
            BattleGuardFixture.GetMountedStrikeContactPoint(
                guardPosition,
                new Vec3(0f, 2f, 1f),
                8f);
        Vec3 strikeTarget =
            BattleGuardFixture.GetMountedStrikeTargetPoint(
                contactPoint,
                route);
        Vec3 position = BattleGuardFixture.GetMountedStrikerPosition(
            strikeTarget,
            route);
        Vec3 targetOffset = strikeTarget - contactPoint;
        Vec3 forwardOffset = position - strikeTarget;
        Vec3 strikerOffset = position - contactPoint;
        Vec3 strikerDirection = strikerOffset;
        strikerDirection.Normalize();
        Vec3 attackDirection = strikeTarget - position;
        attackDirection.Normalize();
        Vec3 contactOffset = contactPoint - guardPosition;
        float forwardAlignment =
            Vec3.DotProduct(strikerDirection, route);
        float lateralAlignment =
            Vec3.DotProduct(strikerDirection, lateral);

        Assert.Equal(
            8f,
            Vec3.DotProduct(contactOffset, route),
            precision: 3);
        Assert.Equal(
            0f,
            Vec3.DotProduct(contactOffset, lateral),
            precision: 3);
        Assert.Equal(1.5f, targetOffset.Length, precision: 3);
        Assert.Equal(
            0f,
            Vec3.DotProduct(targetOffset, route),
            precision: 3);
        Assert.Equal(
            1.5f,
            Vec3.DotProduct(targetOffset, lateral),
            precision: 3);
        Assert.Equal(1.5f, forwardOffset.Length, precision: 3);
        Assert.Equal(
            1.5f,
            Vec3.DotProduct(forwardOffset, route),
            precision: 3);
        Assert.Equal(
            0f,
            Vec3.DotProduct(forwardOffset, lateral),
            precision: 3);
        Assert.Equal(2.12132025f, strikerOffset.Length, precision: 3);
        Assert.Equal(
            1f,
            Vec3.DotProduct(strikerDirection, guardedLook),
            precision: 3);
        Assert.Equal(0.70710677f, forwardAlignment, precision: 3);
        Assert.Equal(0.70710677f, lateralAlignment, precision: 3);
        Assert.Equal(
            forwardAlignment,
            lateralAlignment,
            precision: 3);
        Assert.Equal(
            -1f,
            Vec3.DotProduct(attackDirection, route),
            precision: 3);
        Assert.Equal(
            0f,
            Vec3.DotProduct(attackDirection, lateral),
            precision: 3);
        Assert.Equal(guardPosition.z, contactPoint.z, precision: 3);
        Assert.Equal(contactPoint.z, strikeTarget.z, precision: 3);
        Assert.Equal(contactPoint.z, position.z, precision: 3);
    }

    [Fact]
    public void MountedStrikeTrackedLook_FacesCurrentStrikerPosition()
    {
        Vec3 direction =
            BattleGuardFixture.GetMountedStrikeTrackedLookDirection(
                new Vec3(2f, 3f, 4f),
                new Vec3(5f, 7f, 9f),
                new Vec3(0f, 1f, 0f));

        Assert.Equal(0.6f, direction.x, precision: 3);
        Assert.Equal(0.8f, direction.y, precision: 3);
        Assert.Equal(0f, direction.z);
    }

    [Fact]
    public void MountedStrikeTrackedLook_UsesFallbackAtZeroStandoff()
    {
        Vec3 direction =
            BattleGuardFixture.GetMountedStrikeTrackedLookDirection(
                new Vec3(2f, 3f, 4f),
                new Vec3(2f, 3f, 9f),
                new Vec3(0f, 2f, 1f));

        Assert.Equal(new Vec3(0f, 1f, 0f), direction);
    }

    [Theory]
    [InlineData(
        true,
        BattleGuardFixtureMode.Mounted,
        BattleGuardFixturePhase.Attack,
        true)]
    [InlineData(
        false,
        BattleGuardFixtureMode.Mounted,
        BattleGuardFixturePhase.Attack,
        false)]
    [InlineData(
        true,
        BattleGuardFixtureMode.Mounted,
        BattleGuardFixturePhase.Guard,
        false)]
    [InlineData(
        true,
        BattleGuardFixtureMode.Foot,
        BattleGuardFixturePhase.Attack,
        false)]
    public void MountedStrikeLook_IsWrittenOnlyByOwningMountedGuard(
        bool guardLocallyDriven,
        BattleGuardFixtureMode mode,
        BattleGuardFixturePhase phase,
        bool expected)
    {
        Assert.Equal(
            expected,
            BattleGuardFixture.ShouldApplyOwnedMountedStrikeLook(
                guardLocallyDriven,
                mode,
                phase));
    }

    [Theory]
    [InlineData(1f, true)]
    [InlineData(0.95f, true)]
    [InlineData(0.949f, false)]
    [InlineData(-0.278f, false)]
    public void MountedStrikeCharge_WaitsForReplicatedContactLook(
        float alignment,
        bool expected)
    {
        Assert.Equal(
            expected,
            BattleGuardFixture.HasMountedStrikeContactAlignment(
                alignment));
    }

    [Theory]
    [InlineData(true, false, 7, 7, -1f, true)]
    [InlineData(false, true, 7, 8, 1f, true)]
    [InlineData(false, true, 7, 8, 0.999f, true)]
    [InlineData(false, true, 7, 7, 1f, false)]
    [InlineData(false, true, 7, 8, 0.998f, false)]
    [InlineData(false, true, 7, 8, 0.707f, false)]
    [InlineData(false, true, 7, 8, 0.949f, false)]
    [InlineData(false, false, 7, 8, 1f, false)]
    public void MountedStrikeCharge_RequiresPostStageOwnerLookEvidence(
        bool guardLocallyDriven,
        bool hasReplicatedLook,
        long stagedUpdateSequence,
        long currentUpdateSequence,
        float replicatedLookAlignment,
        bool expected)
    {
        Assert.Equal(
            expected,
            BattleGuardFixture.HasObservedReplicatedMountedStrikeLook(
                guardLocallyDriven,
                hasReplicatedLook,
                stagedUpdateSequence,
                currentUpdateSequence,
                replicatedLookAlignment));
    }

    [Theory]
    [InlineData(2.499f, false)]
    [InlineData(2.5f, true)]
    [InlineData(3f, true)]
    public void MountedStrikeCharge_PreservesTotalAttemptTimeout(
        float chargeSeconds,
        bool expected)
    {
        Assert.Equal(
            expected,
            BattleGuardFixture.HasMountedStrikeChargeTimedOut(
                chargeSeconds));
    }

    [Theory]
    [InlineData(1f, 0.35f, true)]
    [InlineData(1f, 0.349f, false)]
    [InlineData(0.949f, 1f, false)]
    public void MountedStrikeCharge_DeadlineRequiresCompletedAlignedPress(
        float alignment,
        float alignedSeconds,
        bool expected)
    {
        Assert.Equal(
            expected,
            BattleGuardFixture.ShouldReleaseTimedOutMountedStrike(
                alignment,
                alignedSeconds));
    }

    [Theory]
    [InlineData(2f, 8f, 0.5f, true)]
    [InlineData(2.9f, 8f, 0.5f, false)]
    [InlineData(100f, 8f, 2.5f, true)]
    public void MountedStrikeRelease_LeadsContactOrEndsCharge(
        float longitudinalDistance,
        float speed,
        float chargeSeconds,
        bool expected)
    {
        Assert.Equal(
            expected,
            BattleGuardFixture.ShouldReleaseMountedStrike(
                longitudinalDistance,
                speed,
                chargeSeconds,
                BattleGuardFixture.GetMountedStrikeReleaseLeadSeconds(1)));
    }

    [Theory]
    [InlineData(7.5f, 7.5f, true)]
    [InlineData(7.125f, 7.5f, true)]
    [InlineData(7.124f, 7.5f, false)]
    [InlineData(7.5f, -1f, false)]
    public void MountedStrikeSpeed_RequiresCalibratedPlateauRetention(
        float speed,
        float calibratedPlateauSpeed,
        bool expected)
    {
        Assert.Equal(
            expected,
            BattleGuardFixture.HasMountedStrikeSpeed(
                speed,
                calibratedPlateauSpeed));
    }

    [Theory]
    [InlineData(-1f, true, -1f)]
    [InlineData(-1f, false, 7.5f)]
    [InlineData(7.4f, false, 7.4f)]
    public void MountedStrikeSpeed_RemoteStrikerUsesFixtureSpeedLimit(
        float calibratedPlateauSpeed,
        bool guardLocallyDriven,
        float expected)
    {
        Assert.Equal(
            expected,
            BattleGuardFixture.GetMountedStrikeSpeedBaseline(
                calibratedPlateauSpeed,
                guardLocallyDriven),
            precision: 3);
    }

    [Theory]
    [InlineData(0, 0.35f)]
    [InlineData(1, 0.35f)]
    [InlineData(2, 0.25f)]
    [InlineData(3, 0.45f)]
    [InlineData(5, 0.65f)]
    [InlineData(6, 0.65f)]
    public void MountedStrikeRelease_ProfilesBracketNativeImpactTiming(
        int attempt,
        float expected)
    {
        Assert.Equal(
            expected,
            BattleGuardFixture.GetMountedStrikeReleaseLeadSeconds(attempt),
            precision: 3);
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, false)]
    public void StrikeCompletion_RequiresExactBlockedPairEvidence(
        bool isBlocked,
        bool isExactGuard,
        bool expected)
    {
        Assert.Equal(
            expected,
            BattleGuardFixture.ShouldCompleteStrikeFromScoreHit(
                isBlocked,
                isExactGuard));
    }

    [Theory]
    [InlineData(Agent.ActionStage.AttackReady, true)]
    [InlineData(Agent.ActionStage.AttackQuickReady, true)]
    [InlineData(Agent.ActionStage.AttackRelease, false)]
    [InlineData(Agent.ActionStage.None, false)]
    public void MountedStrikeRelease_RequiresNativeReadyStage(
        Agent.ActionStage actionStage,
        bool expected)
    {
        Assert.Equal(
            expected,
            BattleGuardFixture.IsNativeAttackReady(actionStage));
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, false)]
    public void RemountState_RequiresBothAgentLinks(
        bool riderReferencesMount,
        bool mountReferencesRider,
        bool expected)
    {
        Assert.Equal(
            expected,
            BattleGuardFixture.IsRemountStateReconciled(
                riderReferencesMount,
                mountReferencesRider));
    }

    [Theory]
    [InlineData(false, false, false, false)]
    [InlineData(false, true, true, false)]
    [InlineData(true, true, true, false)]
    [InlineData(true, true, false, true)]
    [InlineData(true, false, true, true)]
    [InlineData(true, false, false, true)]
    public void MountRestore_RequiresReciprocalLinksForActiveMount(
        bool originalMountActive,
        bool riderReferencesMount,
        bool mountReferencesRider,
        bool expected)
    {
        Assert.Equal(
            expected,
            BattleGuardFixture.NeedsMountRestore(
                originalMountActive,
                riderReferencesMount,
                mountReferencesRider));
    }

    [Fact]
    public void MountedRoute_MovementDirection_WinsOverStaleLookDirection()
    {
        var route = new BattleGuardMountedRoute(
            new Vec3(0f, 0f, 0f),
            new Vec3(0f, 1f, 0f),
            40f);

        BattleGuardMountedRouteInput input = route.Update(
            new Vec3(0f, 30f, 0f),
            new Vec2(0f, -1f),
            new Vec3(0f, 1f, 0f),
            new Vec3(0f, 1f, 0f),
            8f);

        Assert.Equal("Return", route.State);
        Assert.True(route.CanStageStrike);
        Assert.Equal(new Vec3(0f, -1f, 0f), input.LookDirection);
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
            new Vec3(0f, 1f, 0f),
            0f);

        Assert.Equal("Return", route.State);
        Assert.True(route.CanStageStrike);
    }

    [Fact]
    public void MountedRoute_Endpoint_UsesPhysicalFacingToCompleteTurn()
    {
        var route = new BattleGuardMountedRoute(
            new Vec3(0f, 0f, 0f),
            new Vec3(0f, 1f, 0f),
            40f);

        BattleGuardMountedRouteInput braking = route.Update(
            new Vec3(0f, 35f, 0f),
            new Vec2(0f, 1f),
            new Vec3(0f, 1f, 0f),
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
            new Vec3(0f, 1f, 0f),
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
            new Vec3(0f, 1f, 0f),
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
        Assert.Equal(
            new Vec3(0f, -1f, 0f),
            returning.LookDirection);
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

    [Theory]
    [InlineData(4.9f, 60f, true)]
    [InlineData(5.1f, 60f, false)]
    [InlineData(0f, 124f, true)]
    [InlineData(0f, 126f, false)]
    public void MountedRouteClearance_IncludesLaneAndEndpointRadius(
        float x,
        float y,
        bool expected)
    {
        Assert.Equal(
            expected,
            BattleGuardFixture.IsInsideMountedRouteClearance(
                new Vec3(x, y, 0f),
                Vec3.Zero,
                new Vec3(0f, 1f, 0f),
                120f,
                5f));
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

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void MountedStrikeLifecycle_AuthoritativeDefinition_RoundTrips(
        bool active)
    {
        Guid commandId = Guid.NewGuid();
        Guid guardAgentId = Guid.NewGuid();
        Guid strikerAgentId = Guid.NewGuid();
        var original = new NetworkBattleGuardFixtureStrike(
            "battle",
            commandId,
            guardAgentId,
            "guard-owner",
            strikerAgentId,
            "striker-owner",
            active,
            active ? 0.6f : 0f,
            active ? 0.8f : 0f,
            active ? 0.7f : 0f,
            active ? 0.7f : 0f,
            active ? 12f : 0f,
            active ? 34f : 0f);
        using var stream = new MemoryStream();

        Serializer.Serialize(stream, original);
        stream.Position = 0;
        NetworkBattleGuardFixtureStrike received =
            Serializer.Deserialize<NetworkBattleGuardFixtureStrike>(stream);

        Assert.Equal(original.BattleInstanceId, received.BattleInstanceId);
        Assert.Equal(commandId, received.CommandId);
        Assert.Equal(guardAgentId, received.GuardAgentId);
        Assert.Equal("guard-owner", received.GuardAuthority);
        Assert.Equal(strikerAgentId, received.StrikerAgentId);
        Assert.Equal("striker-owner", received.StrikerAuthority);
        Assert.Equal(active, received.Active);
        Assert.Equal(original.TravelDirectionX, received.TravelDirectionX);
        Assert.Equal(original.TravelDirectionY, received.TravelDirectionY);
        Assert.Equal(original.GuardLookDirectionX, received.GuardLookDirectionX);
        Assert.Equal(original.GuardLookDirectionY, received.GuardLookDirectionY);
        Assert.Equal(original.TargetX, received.TargetX);
        Assert.Equal(original.TargetY, received.TargetY);
        Assert.True(BattleGuardFixture.IsValidMountedStrike(received));
    }

    [Fact]
    public void MountedStrikeLifecycle_ActiveUpdateRequiresFiniteGeometry()
    {
        var invalid = new NetworkBattleGuardFixtureStrike(
            "battle",
            Guid.NewGuid(),
            Guid.NewGuid(),
            "guard-owner",
            Guid.NewGuid(),
            "striker-owner",
            active: true,
            travelDirectionX: 0f,
            travelDirectionY: 1f,
            guardLookDirectionX: 1f,
            guardLookDirectionY: 0f,
            targetX: float.NaN,
            targetY: 34f);

        Assert.False(BattleGuardFixture.IsValidMountedStrike(invalid));
    }

    [Theory]
    [InlineData(
        BattleGuardFixtureDirection.Up,
        Agent.MovementControlFlag.DefendUp,
        Agent.MovementControlFlag.AttackUp,
        Agent.GuardMode.Up)]
    [InlineData(
        BattleGuardFixtureDirection.Down,
        Agent.MovementControlFlag.DefendDown,
        Agent.MovementControlFlag.AttackDown,
        Agent.GuardMode.Down)]
    [InlineData(
        BattleGuardFixtureDirection.Left,
        Agent.MovementControlFlag.DefendLeft,
        Agent.MovementControlFlag.AttackRight,
        Agent.GuardMode.Left)]
    [InlineData(
        BattleGuardFixtureDirection.Right,
        Agent.MovementControlFlag.DefendRight,
        Agent.MovementControlFlag.AttackLeft,
        Agent.GuardMode.Right)]
    public void GuardDirection_MapsToExactNativeInput(
        BattleGuardFixtureDirection direction,
        Agent.MovementControlFlag directionFlag,
        Agent.MovementControlFlag attackFlag,
        Agent.GuardMode guardMode)
    {
        Assert.Equal(
            Agent.MovementControlFlag.DefendBlock | directionFlag,
            BattleGuardFixture.GetDefendFlags(direction));
        Assert.Equal(
            attackFlag,
            BattleGuardFixture.GetAttackFlagForGuard(direction));
        Assert.Equal(
            guardMode,
            BattleGuardFixture.GetGuardMode(direction));
    }

    [Fact]
    public void GuardCommand_DirectionRoundTrips()
    {
        var original = new NetworkBattleGuardFixtureCommand(
            "battle",
            Guid.NewGuid(),
            Guid.NewGuid(),
            "guard-owner",
            Guid.NewGuid(),
            "striker-owner",
            BattleGuardFixtureMode.Mounted,
            BattleGuardFixturePhase.Guard,
            BattleGuardFixtureDirection.Right,
            useMovementFlagGuardInput: true);
        using var stream = new MemoryStream();

        Serializer.Serialize(stream, original);
        stream.Position = 0;
        NetworkBattleGuardFixtureCommand received =
            Serializer.Deserialize<NetworkBattleGuardFixtureCommand>(stream);

        Assert.Equal(
            BattleGuardFixtureDirection.Right,
            received.Direction);
        Assert.True(received.UseMovementFlagGuardInput);
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

    [Fact]
    public void FixtureStrikerWieldState_RequiresExactModeWeaponUsageAndNoOffhand()
    {
        Assert.True(
            BattleGuardFixture.IsFixtureStrikerWieldState(
                BattleGuardFixtureMode.Mounted,
                EquipmentIndex.Weapon0,
                EquipmentIndex.None,
                1,
                "empire_menavlion_1_t3_blunt"));
        Assert.True(
            BattleGuardFixture.IsFixtureStrikerWieldState(
                BattleGuardFixtureMode.Foot,
                EquipmentIndex.Weapon0,
                EquipmentIndex.None,
                0,
                "empire_sword_1_t2_blunt"));
        Assert.False(
            BattleGuardFixture.IsFixtureStrikerWieldState(
                BattleGuardFixtureMode.Mounted,
                EquipmentIndex.Weapon0,
                EquipmentIndex.None,
                1,
                "empire_sword_1_t2_blunt"));
        Assert.False(
            BattleGuardFixture.IsFixtureStrikerWieldState(
                BattleGuardFixtureMode.Mounted,
                EquipmentIndex.Weapon0,
                EquipmentIndex.Weapon1,
                1,
                "empire_menavlion_1_t3_blunt"));
        Assert.False(
            BattleGuardFixture.IsFixtureStrikerWieldState(
                BattleGuardFixtureMode.Mounted,
                EquipmentIndex.None,
                EquipmentIndex.None,
                1,
                "empire_menavlion_1_t3_blunt"));
        Assert.False(
            BattleGuardFixture.IsFixtureStrikerWieldState(
                BattleGuardFixtureMode.Mounted,
                EquipmentIndex.Weapon0,
                EquipmentIndex.None,
                0,
                "empire_menavlion_1_t3_blunt"));
    }

    [Theory]
    [InlineData(false, false, -1, 0, false, false)]
    [InlineData(true, false, -1, 0, false, true)]
    [InlineData(true, true, 0, 0, true, false)]
    [InlineData(true, false, 0, 0, false, false)]
    [InlineData(true, false, 0, 0, true, true)]
    [InlineData(true, true, 0, 1, false, true)]
    public void FixtureStrikerWieldRequest_RetriesOnCadenceOrNewAttempt(
        bool weaponAvailable,
        bool weaponReady,
        int lastRequestAttempt,
        int attempt,
        bool retryDue,
        bool expected)
    {
        Assert.Equal(
            expected,
            BattleGuardFixture.ShouldRequestFixtureStrikerWield(
                weaponAvailable,
                weaponReady,
                lastRequestAttempt,
                attempt,
                retryDue));
    }

    [Theory]
    [InlineData(false, EquipmentIndex.Weapon2, false)]
    [InlineData(true, EquipmentIndex.None, false)]
    [InlineData(true, EquipmentIndex.Weapon2, true)]
    public void FixtureStrikerOffHandRepair_OnlySheathesAvailableWeapon(
        bool weaponAvailable,
        EquipmentIndex offHandIndex,
        bool expected)
    {
        Assert.Equal(
            expected,
            BattleGuardFixture.ShouldSheathFixtureStrikerOffHand(
                weaponAvailable,
                offHandIndex));
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
