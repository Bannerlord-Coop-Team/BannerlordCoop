#if DEBUG
using Missions.Battles;
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
