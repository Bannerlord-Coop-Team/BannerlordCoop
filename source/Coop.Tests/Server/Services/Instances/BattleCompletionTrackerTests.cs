using Coop.Core.Server.Services.Instances;
using TaleWorlds.Core;
using Xunit;

namespace Coop.Tests.Server.Services.Instances;

/// <summary>Tests authoritative reconciliation of per-member battle result reports.</summary>
public class BattleCompletionTrackerTests
{
    [Fact]
    public void SoleMissionMemberReport_ConcludesBattle()
    {
        var tracker = new BattleCompletionTracker();

        var concluded = tracker.TryRecordResult(
            "battle",
            "host",
            BattleState.DefenderVictory,
            1,
            new[] { "host" },
            "host",
            1,
            out var state);

        Assert.True(concluded);
        Assert.Equal(BattleState.DefenderVictory, state);
    }

    [Fact]
    public void HostReport_WaitsForEveryCurrentMember()
    {
        var tracker = new BattleCompletionTracker();
        var members = new[] { "host", "successor" };

        Assert.False(tracker.TryRecordResult(
            "battle", "host", BattleState.AttackerVictory, 1, members, "host", 1, out _));

        Assert.True(tracker.TryRecordResult(
            "battle", "successor", BattleState.AttackerVictory, 1, members, "host", 1, out var state));
        Assert.Equal(BattleState.AttackerVictory, state);
    }

    [Fact]
    public void ConflictingMemberReports_DoNotConcludeBattle()
    {
        var tracker = new BattleCompletionTracker();
        var members = new[] { "host", "successor" };

        Assert.False(tracker.TryRecordResult(
            "battle", "host", BattleState.AttackerVictory, 1, members, "host", 1, out _));
        Assert.False(tracker.TryRecordResult(
            "battle", "successor", BattleState.DefenderVictory, 1, members, "host", 1, out _));
    }

    [Fact]
    public void DepartedMemberReport_DoesNotCountAfterReentry()
    {
        var tracker = new BattleCompletionTracker();
        var members = new[] { "host", "successor" };

        Assert.False(tracker.TryRecordResult(
            "battle", "successor", BattleState.AttackerVictory, 1, members, "host", 1, out _));
        tracker.ResetMember("battle", "successor", isFirstMember: false);
        Assert.False(tracker.TryRecordResult(
            "battle", "host", BattleState.AttackerVictory, 1, members, "host", 1, out _));
    }

    [Fact]
    public void WaitingMemberDeparts_RemainingHostReportConcludesBattle()
    {
        var tracker = new BattleCompletionTracker();
        var members = new[] { "host", "loading" };

        Assert.False(tracker.TryRecordResult(
            "battle", "host", BattleState.DefenderVictory, 1, members, "host", 1, out _));

        Assert.True(tracker.TryReconcile(
            "battle", new[] { "host" }, "host", 1, out var state));
        Assert.Equal(BattleState.DefenderVictory, state);
    }

    [Fact]
    public void HostDeparts_PromotedHostUsesRetainedCurrentMissionReport()
    {
        var tracker = new BattleCompletionTracker();
        var members = new[] { "host", "successor" };

        Assert.False(tracker.TryRecordResult(
            "battle", "successor", BattleState.AttackerVictory, 1, members, "host", 1, out _));

        tracker.HostAssigned("battle", "successor", 2);
        Assert.True(tracker.TryReconcile(
            "battle", new[] { "successor" }, "successor", 2, out var state));
        Assert.Equal(BattleState.AttackerVictory, state);
    }

    [Fact]
    public void ReportsBeforeHostElection_AreRetainedForReconciliation()
    {
        var tracker = new BattleCompletionTracker();

        Assert.False(tracker.TryRecordResult(
            "battle", "host", BattleState.AttackerVictory, 0, new[] { "host" }, null, 0, out _));
        tracker.HostAssigned("battle", "host", 1);
        Assert.True(tracker.TryReconcile(
            "battle", new[] { "host" }, "host", 1, out var state));

        Assert.Equal(BattleState.AttackerVictory, state);
    }

    [Fact]
    public void PreElectionReport_ConcludesAfterEmptyInstanceHostAssignmentArrives()
    {
        var tracker = new BattleCompletionTracker();

        Assert.False(tracker.TryRecordResult(
            "battle", "host", BattleState.DefenderVictory, 0, new[] { "host" }, null, 0, out _,
            canConclude: false));

        tracker.HostAssigned("battle", "host", 1);

        Assert.True(tracker.TryConcludeAbandoned(
            "battle", out var state, out var hostEpoch, out _));
        Assert.Equal(BattleState.DefenderVictory, state);
        Assert.Equal(1, hostEpoch);
    }

    [Fact]
    public void EmptyResolvedInstance_ConcludesFromAllParticipantReports()
    {
        var tracker = new BattleCompletionTracker();
        var members = new[] { "host", "successor" };

        Assert.False(tracker.TryRecordResult(
            "battle", "host", BattleState.DefenderVictory, 1, members, "host", 1, out _, canConclude: false));
        Assert.False(tracker.TryRecordResult(
            "battle", "successor", BattleState.DefenderVictory, 1, members, "host", 1, out _, canConclude: false));

        Assert.True(tracker.TryConcludeAbandoned(
            "battle", out var state, out var hostEpoch, out var memberCount));
        Assert.Equal(BattleState.DefenderVictory, state);
        Assert.Equal(1, hostEpoch);
        Assert.Equal(2, memberCount);
    }

    [Fact]
    public void EmptyInstance_MissingParticipantReportRemainsUnresolved()
    {
        var tracker = new BattleCompletionTracker();

        Assert.False(tracker.TryRecordResult(
            "battle",
            "host",
            BattleState.AttackerVictory,
            1,
            new[] { "host", "successor" },
            "host",
            1,
            out _,
            canConclude: false));

        Assert.False(tracker.TryConcludeAbandoned("battle", out _, out _, out _));
    }

    [Fact]
    public void DepartedMemberWithoutAReport_DoesNotBlockAbandonedConclusion()
    {
        var tracker = new BattleCompletionTracker();

        Assert.False(tracker.TryRecordResult(
            "battle",
            "host",
            BattleState.AttackerVictory,
            1,
            new[] { "host", "departed" },
            "host",
            1,
            out _,
            canConclude: false));

        tracker.MemberDeparted("battle", "departed");

        Assert.True(tracker.TryConcludeAbandoned(
            "battle", out var state, out _, out var memberCount));
        Assert.Equal(BattleState.AttackerVictory, state);
        Assert.Equal(1, memberCount);
    }

    [Fact]
    public void DepartedMemberWithAReport_RemainsInAbandonedConsensus()
    {
        var tracker = new BattleCompletionTracker();
        var members = new[] { "host", "departed" };

        Assert.False(tracker.TryRecordResult(
            "battle", "host", BattleState.DefenderVictory, 1, members, "host", 1, out _, canConclude: false));
        Assert.False(tracker.TryRecordResult(
            "battle", "departed", BattleState.DefenderVictory, 1, members, "host", 1, out _, canConclude: false));

        tracker.MemberDeparted("battle", "departed");

        Assert.True(tracker.TryConcludeAbandoned(
            "battle", out var state, out _, out var memberCount));
        Assert.Equal(BattleState.DefenderVictory, state);
        Assert.Equal(2, memberCount);
    }
}
