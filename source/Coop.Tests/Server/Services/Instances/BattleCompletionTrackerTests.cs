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
    public void SiegeAmbushCompletion_ReconcilesWithoutVictoryState()
    {
        var tracker = new BattleCompletionTracker();

        var concluded = tracker.TryRecordResult(
            "ambush",
            "host",
            BattleState.DefenderPullBack,
            1,
            new[] { "host" },
            "host",
            1,
            out var state);

        Assert.True(concluded);
        Assert.Equal(BattleState.DefenderPullBack, state);
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
    public void MemberEnteringResolvedBattle_MustReportResult()
    {
        var tracker = new BattleCompletionTracker();

        Assert.False(tracker.TryRecordResult(
            "battle",
            "host",
            BattleState.DefenderVictory,
            1,
            new[] { "host" },
            "host",
            1,
            out _,
            canConclude: false));

        Assert.False(tracker.TryReconcile(
            "battle", new[] { "host", "joiner" }, "host", 1, out _));

        Assert.True(tracker.TryRecordResult(
            "battle", "joiner", BattleState.DefenderVictory, 1,
            new[] { "host", "joiner" }, "host", 1, out var state));
        Assert.Equal(BattleState.DefenderVictory, state);
    }

    [Fact]
    public void HostDeparts_PromotedHostMustReportAtNewEpoch()
    {
        var tracker = new BattleCompletionTracker();
        var members = new[] { "host", "successor" };

        Assert.False(tracker.TryRecordResult(
            "battle", "successor", BattleState.AttackerVictory, 1, members, "host", 1, out _));

        tracker.HostAssigned("battle", "successor", 2);
        Assert.False(tracker.TryReconcile(
            "battle", new[] { "successor" }, "successor", 2, out _));
        Assert.True(tracker.TryRecordResult(
            "battle", "successor", BattleState.AttackerVictory, 2,
            new[] { "successor" }, "successor", 2, out var state));
        Assert.Equal(BattleState.AttackerVictory, state);
    }

    [Fact]
    public void HostLeavesMidEnd_PeerReportAtTheOldEpochWaitsForThePromotedHost()
    {
        var tracker = new BattleCompletionTracker();

        Assert.False(tracker.TryRecordResult(
            "battle", "host", BattleState.AttackerVictory, 1, new[] { "host", "peer" }, "host", 1, out _));
        tracker.MemberDeparted("battle", "host");
        tracker.HostAssigned("battle", "peer", 2);

        // The peer's report of the old host's snapshot is stamped with epoch 1.
        Assert.False(tracker.TryRecordResult(
            "battle", "peer", BattleState.AttackerVictory, 1, new[] { "peer" }, "peer", 2, out _));
        Assert.True(tracker.TryRecordResult(
            "battle", "peer", BattleState.AttackerVictory, 2, new[] { "peer" }, "peer", 2, out var state));
        Assert.Equal(BattleState.AttackerVictory, state);
    }

    // R41a D: a naval host stays host after it reported and left, so a remaining member's matching report concludes.
    [Fact]
    public void NavalHostReportsAndLeaves_ARemainingMembersMatchingReportConcludes()
    {
        var tracker = new BattleCompletionTracker();

        Assert.False(tracker.TryRecordResult(
            "battle", "host", BattleState.DefenderVictory, 1, new[] { "host", "peer" }, "host", 1, out _,
            keepsDepartedHostReport: true));
        tracker.MemberDeparted("battle", "host");

        Assert.False(tracker.TryReconcile("battle", new[] { "peer" }, "host", 1, out _, keepsDepartedHostReport: false));
        Assert.True(tracker.TryRecordResult(
            "battle", "peer", BattleState.DefenderVictory, 1, new[] { "peer" }, "host", 1, out var state,
            keepsDepartedHostReport: true));
        Assert.Equal(BattleState.DefenderVictory, state);
    }

    // R41a D: the naval host reported and left, a member never reported and leaves too; the empty instance concludes with
    // the host's result, even across a host change the silent member got.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NavalHostReportsAndLeaves_ASilentMembersDepartureConcludesTheEmptyInstance(bool hostChangedFirst)
    {
        var tracker = new BattleCompletionTracker();

        Assert.False(tracker.TryRecordResult(
            "battle", "host", BattleState.DefenderVictory, 1, new[] { "host", "silent" }, "host", 1, out _,
            keepsDepartedHostReport: true));
        tracker.MemberDeparted("battle", "host");
        Assert.False(tracker.TryReconcile("battle", new[] { "silent" }, "host", 1, out _, keepsDepartedHostReport: true));
        if (hostChangedFirst) tracker.HostAssigned("battle", "silent", 2, keepsDepartedHostReport: true);
        tracker.MemberDeparted("battle", "silent");

        Assert.True(tracker.TryConcludeAbandoned("battle", out var state, out var hostEpoch, out var memberCount));
        Assert.Equal(BattleState.DefenderVictory, state);
        Assert.Equal(1, hostEpoch);
        Assert.Equal(1, memberCount);
    }

    // Land keeps its rule: a new host must report its own final state, so the old host's report is dropped.
    [Fact]
    public void LandHostChange_DropsTheDepartedHostsReport_ForAnEmptiedInstance()
    {
        var tracker = new BattleCompletionTracker();

        Assert.False(tracker.TryRecordResult(
            "battle", "host", BattleState.DefenderVictory, 1, new[] { "host", "silent" }, "host", 1, out _));
        tracker.MemberDeparted("battle", "host");
        tracker.HostAssigned("battle", "silent", 2);
        tracker.MemberDeparted("battle", "silent");

        Assert.False(tracker.TryConcludeAbandoned("battle", out _, out _, out _));
    }

    [Fact]
    public void ReportsBeforeHostElection_RequireCurrentEpochHostReport()
    {
        var tracker = new BattleCompletionTracker();

        Assert.False(tracker.TryRecordResult(
            "battle", "host", BattleState.AttackerVictory, 0, new[] { "host" }, null, 0, out _));
        tracker.HostAssigned("battle", "host", 1);
        Assert.False(tracker.TryReconcile(
            "battle", new[] { "host" }, "host", 1, out _));
        Assert.True(tracker.TryRecordResult(
            "battle", "host", BattleState.AttackerVictory, 1,
            new[] { "host" }, "host", 1, out var state));

        Assert.Equal(BattleState.AttackerVictory, state);
    }

    [Fact]
    public void PreElectionReport_DoesNotAuthorizeAbandonedConclusion()
    {
        var tracker = new BattleCompletionTracker();

        Assert.False(tracker.TryRecordResult(
            "battle", "host", BattleState.DefenderVictory, 0, new[] { "host" }, null, 0, out _,
            canConclude: false));

        tracker.HostAssigned("battle", "host", 1);

        Assert.False(tracker.TryConcludeAbandoned(
            "battle", out _, out _, out _));
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
