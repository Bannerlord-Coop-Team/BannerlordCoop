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
        var tracker = CreateTracker();

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
        var tracker = CreateTracker();

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
        var tracker = CreateTracker();
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
        var tracker = CreateTracker();
        var members = new[] { "host", "successor" };

        Assert.False(tracker.TryRecordResult(
            "battle", "host", BattleState.AttackerVictory, 1, members, "host", 1, out _));
        Assert.False(tracker.TryRecordResult(
            "battle", "successor", BattleState.DefenderVictory, 1, members, "host", 1, out _));
    }

    [Fact]
    public void DepartedMemberReport_DoesNotCountAfterReentry()
    {
        var tracker = CreateTracker();
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
        var tracker = CreateTracker();
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
        var tracker = CreateTracker();

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
        var tracker = CreateTracker();
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
        var tracker = CreateTracker();

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

    // R41a D: a host kept after it reported and left still concludes with a remaining member's matching report; a host that
    // left without being kept concludes nothing.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void HostReportsAndLeaves_ARemainingMembersMatchingReportConcludesOnlyForAKeptHost(bool hostKept)
    {
        var tracker = CreateTracker();

        Assert.False(tracker.TryRecordResult(
            "battle", "host", BattleState.DefenderVictory, 1, new[] { "host", "peer" }, "host", 1, out _));
        tracker.MemberDeparted("battle", "host");

        bool concluded = tracker.TryRecordResult(
            "battle", "peer", BattleState.DefenderVictory, 1, new[] { "peer" }, "host", 1, out var state, hostKept: hostKept);
        Assert.Equal(hostKept, concluded);
        Assert.Equal(hostKept ? BattleState.DefenderVictory : BattleState.None, state);
    }

    // R41a D: the kept host reported and left, a member never reported and leaves too; the empty instance concludes with
    // the host's result.
    [Fact]
    public void KeptHostReportsAndLeaves_ASilentMembersDepartureConcludesTheEmptyInstance()
    {
        var tracker = CreateTracker();

        Assert.False(tracker.TryRecordResult(
            "battle", "host", BattleState.DefenderVictory, 1, new[] { "host", "silent" }, "host", 1, out _));
        tracker.MemberDeparted("battle", "host");
        Assert.False(tracker.TryReconcile("battle", new[] { "silent" }, "host", 1, out _, hostKept: true));
        tracker.MemberDeparted("battle", "silent");

        Assert.True(tracker.TryConcludeAbandoned("battle", out var state, out var hostEpoch, out var memberCount));
        Assert.Equal(BattleState.DefenderVictory, state);
        Assert.Equal(1, hostEpoch);
        Assert.Equal(1, memberCount);
    }

    // Q7: once the wait for the members expired, the kept host's report concludes over a silent and a disagreeing member.
    [Fact]
    public void StopWaitingForMembers_TheKeptHostsReportConcludesOverUnmatchedMembers()
    {
        var tracker = CreateTracker();
        var members = new[] { "host", "silent", "other" };
        Assert.False(tracker.TryRecordResult(
            "battle", "host", BattleState.DefenderVictory, 1, members, "host", 1, out _));
        Assert.False(tracker.TryRecordResult(
            "battle", "other", BattleState.AttackerVictory, 1, members, "host", 1, out _));
        tracker.MemberDeparted("battle", "host");
        var remaining = new[] { "silent", "other" };
        Assert.False(tracker.TryReconcile("battle", remaining, "host", 1, out _, hostKept: true));

        tracker.StopWaitingForMembers("battle");

        Assert.True(tracker.TryReconcile("battle", remaining, "host", 1, out var state, hostKept: true));
        Assert.Equal(BattleState.DefenderVictory, state);
    }

    // Q7: an expired wait still needs the current host's report at its epoch, and an emptied instance concludes over the
    // disagreeing member that stayed a participant.
    [Fact]
    public void StopWaitingForMembers_StillNeedsTheHostsReport_AndConcludesAnEmptiedInstance()
    {
        var tracker = CreateTracker();
        var members = new[] { "host", "other" };
        Assert.False(tracker.TryRecordResult(
            "battle", "other", BattleState.AttackerVictory, 1, members, "host", 1, out _));
        tracker.StopWaitingForMembers("battle");
        Assert.False(tracker.TryReconcile("battle", members, "host", 1, out _));
        Assert.False(tracker.TryConcludeAbandoned("battle", out _, out _, out _));

        Assert.False(tracker.TryRecordResult(
            "battle", "host", BattleState.DefenderVictory, 1, members, "host", 1, out _, canConclude: false));
        tracker.MemberDeparted("battle", "host");
        tracker.MemberDeparted("battle", "other");

        Assert.True(tracker.TryConcludeAbandoned("battle", out var state, out _, out var memberCount));
        Assert.Equal(BattleState.DefenderVictory, state);
        Assert.Equal(2, memberCount);
    }

    // Q7: a battle cleared after its wait expired waits for its members again when it is entered anew.
    [Fact]
    public void Clear_ForgetsTheExpiredMemberWait()
    {
        var tracker = CreateTracker();
        tracker.StopWaitingForMembers("battle");
        tracker.Clear("battle");
        var members = new[] { "host", "silent" };

        Assert.False(tracker.TryRecordResult(
            "battle", "host", BattleState.DefenderVictory, 1, members, "host", 1, out _));
    }

    // Land keeps its rule: a new host must report its own final state, so the old host's report is dropped.
    [Fact]
    public void LandHostChange_DropsTheDepartedHostsReport_ForAnEmptiedInstance()
    {
        var tracker = CreateTracker();

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
        var tracker = CreateTracker();

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
        var tracker = CreateTracker();

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
        var tracker = CreateTracker();
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
        var tracker = CreateTracker();

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
        var tracker = CreateTracker();

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
        var tracker = CreateTracker();
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

    // A tracker with no reports yet.
    private static BattleCompletionTracker CreateTracker() => new BattleCompletionTracker();
}
