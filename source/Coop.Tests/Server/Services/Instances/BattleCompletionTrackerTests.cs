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
            new[] { "host" },
            "host",
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
            "battle", "host", BattleState.AttackerVictory, members, "host", out _));

        Assert.True(tracker.TryRecordResult(
            "battle", "successor", BattleState.AttackerVictory, members, "host", out var state));
        Assert.Equal(BattleState.AttackerVictory, state);
    }

    [Fact]
    public void ConflictingMemberReports_DoNotConcludeBattle()
    {
        var tracker = new BattleCompletionTracker();
        var members = new[] { "host", "successor" };

        Assert.False(tracker.TryRecordResult(
            "battle", "host", BattleState.AttackerVictory, members, "host", out _));
        Assert.False(tracker.TryRecordResult(
            "battle", "successor", BattleState.DefenderVictory, members, "host", out _));
    }

    [Fact]
    public void DepartedMemberReport_DoesNotCountAfterReentry()
    {
        var tracker = new BattleCompletionTracker();
        var members = new[] { "host", "successor" };

        Assert.False(tracker.TryRecordResult(
            "battle", "successor", BattleState.AttackerVictory, members, "host", out _));
        tracker.RemoveMember("battle", "successor", isInstanceEmpty: false);
        Assert.False(tracker.TryRecordResult(
            "battle", "host", BattleState.AttackerVictory, members, "host", out _));
    }

    [Fact]
    public void WaitingMemberDeparts_RemainingHostReportConcludesBattle()
    {
        var tracker = new BattleCompletionTracker();
        var members = new[] { "host", "loading" };

        Assert.False(tracker.TryRecordResult(
            "battle", "host", BattleState.DefenderVictory, members, "host", out _));

        tracker.RemoveMember("battle", "loading", isInstanceEmpty: false);

        Assert.True(tracker.TryReconcile(
            "battle", new[] { "host" }, "host", out var state));
        Assert.Equal(BattleState.DefenderVictory, state);
    }

    [Fact]
    public void HostDeparts_PromotedMembersExistingReportConcludesBattle()
    {
        var tracker = new BattleCompletionTracker();
        var members = new[] { "host", "successor" };

        Assert.False(tracker.TryRecordResult(
            "battle", "successor", BattleState.AttackerVictory, members, "host", out _));

        tracker.RemoveMember("battle", "host", isInstanceEmpty: false);

        Assert.True(tracker.TryReconcile(
            "battle", new[] { "successor" }, "successor", out var state));
        Assert.Equal(BattleState.AttackerVictory, state);
    }
}
