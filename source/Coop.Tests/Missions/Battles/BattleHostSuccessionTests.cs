using Common.Messaging;
using Common.Network;
using GameInterface.Services.Entity;
using GameInterface.Services.MapEvents;
using Missions.Battles;
using Missions.Messages;
using Moq;
using TaleWorlds.Core;
using Xunit;

namespace Coop.Tests.Missions.Battles;

public class BattleHostSuccessionTests
{
    private const string Battle = "battle";

    private readonly BattleHostRegistry registry = new BattleHostRegistry(Mock.Of<IControllerIdProvider>());
    private readonly Mock<INetwork> network = new Mock<INetwork>();
    private readonly Mock<IMessageBroker> broker = new Mock<IMessageBroker>();

    // Q4 regression guard: the server is the only place that keeps a naval host. A host that reported (or whose map event
    // applied a result) and left stays host, is marked kept, and no assignment goes out, so no client sees a host change.
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void NavalHostWithAResult_Departs_IsKeptWithoutAnAssignmentBroadcast(bool reported, bool concluded)
    {
        var succession = Create(naval: true);
        succession.Elect(Battle, "A", isNaval: true);
        Assert.True(succession.TryAppendSuccessor(Battle, "B", out _));
        if (reported) succession.RecordReportedResult(Battle, BattleState.DefenderVictory);
        if (concluded) succession.MarkConcluded(Battle);
        network.Invocations.Clear();
        broker.Invocations.Clear();

        Assert.Equal(HostDepartureOutcome.Kept, succession.MemberDeparted(Battle, "A"));

        Assert.True(registry.TryGet(Battle, out var assignment));
        Assert.Equal("A", assignment.HostControllerId);
        Assert.Equal(new[] { "B" }, assignment.SuccessorControllerIds);
        Assert.Equal(1, assignment.Epoch);
        Assert.True(assignment.KeptAfterResult);
        network.Verify(n => n.SendAll(It.Is<IMessage>(m => m is NetworkBattleHostAssigned)), Times.Never);
        broker.Verify(b => b.Publish(It.IsAny<object>(), It.Is<BattleHostAssignmentChanged>(e => e.MapEventId == Battle)), Times.Once);
    }

    // A land host with its result, or a naval host without one, is replaced by the first successor in the next generation;
    // the promotion keeps whether the battle is at sea and re-sends a reported result at the new epoch.
    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void HostWithoutAKeepingResult_Departs_PromotesTheFirstSuccessor(bool naval, bool reported)
    {
        var succession = Create(naval);
        succession.Elect(Battle, "A", isNaval: naval);
        succession.TryAppendSuccessor(Battle, "B", out _);
        succession.TryAppendSuccessor(Battle, "C", out _);
        if (reported) succession.RecordReportedResult(Battle, BattleState.AttackerVictory);
        network.Invocations.Clear();

        Assert.Equal(HostDepartureOutcome.Promoted, succession.MemberDeparted(Battle, "A"));

        Assert.True(registry.TryGet(Battle, out var assignment));
        Assert.Equal("B", assignment.HostControllerId);
        Assert.Equal(new[] { "C" }, assignment.SuccessorControllerIds);
        Assert.Equal(2, assignment.Epoch);
        Assert.Equal(naval, assignment.IsNaval);
        Assert.False(assignment.KeptAfterResult);
        network.Verify(n => n.SendAll(It.Is<IMessage>(m => m is NetworkBattleHostAssigned)), Times.Once);
        network.Verify(n => n.SendAll(It.Is<IMessage>(m => m is NetworkBattleResultSnapshot
            && ((NetworkBattleResultSnapshot)m).HostEpoch == 2)), reported ? Times.Once() : Times.Never());
    }

    [Fact]
    public void HostWithoutASuccessor_Departs_LeavesTheBattleHostless()
    {
        var succession = Create(naval: false);
        succession.Elect(Battle, "A", isNaval: false);

        Assert.Equal(HostDepartureOutcome.Hostless, succession.MemberDeparted(Battle, "A"));

        Assert.False(registry.TryGet(Battle, out _));
    }

    [Fact]
    public void SuccessorDeparts_LeavesTheLineAtTheSameEpoch_AndANonMemberChangesNothing()
    {
        var succession = Create(naval: false);
        succession.Elect(Battle, "A", isNaval: false);
        succession.TryAppendSuccessor(Battle, "B", out _);

        Assert.Equal(HostDepartureOutcome.SuccessorDropped, succession.MemberDeparted(Battle, "B"));
        Assert.Equal(HostDepartureOutcome.Unchanged, succession.MemberDeparted(Battle, "X"));

        Assert.True(registry.TryGet(Battle, out var assignment));
        Assert.Equal("A", assignment.HostControllerId);
        Assert.Empty(assignment.SuccessorControllerIds);
        Assert.Equal(1, assignment.Epoch);
    }

    [Fact]
    public void Departure_WithoutAnElection_ReportsNoHost()
    {
        Assert.Equal(HostDepartureOutcome.NoHost, Create(naval: false).MemberDeparted(Battle, "A"));
    }

    // BR-102: an abandoned battle entered again elects at a higher epoch than any it issued before.
    [Fact]
    public void ReElection_AfterTheAssignmentWasRemoved_IssuesAHigherEpoch()
    {
        var succession = Create(naval: false);
        succession.Elect(Battle, "A", isNaval: false);
        registry.Remove(Battle);

        Assert.Equal(2, succession.Elect(Battle, "B", isNaval: false).Epoch);
    }

    [Fact]
    public void TryAppendSuccessor_RefusesTheHostAndARepeat()
    {
        var succession = Create(naval: false);
        succession.Elect(Battle, "A", isNaval: false);

        Assert.False(succession.TryAppendSuccessor(Battle, "A", out _));
        Assert.True(succession.TryAppendSuccessor(Battle, "B", out _));
        Assert.False(succession.TryAppendSuccessor(Battle, "B", out _));
    }

    // A forgotten result no longer keeps a naval host.
    [Fact]
    public void ForgetResult_ANavalHostLeavingAfterwardsIsPromoted()
    {
        var succession = Create(naval: true);
        succession.Elect(Battle, "A", isNaval: true);
        succession.TryAppendSuccessor(Battle, "B", out _);
        succession.RecordReportedResult(Battle, BattleState.DefenderVictory);

        succession.ForgetResult(Battle);

        Assert.False(succession.TryGetReportedResult(Battle, out _));
        Assert.Equal(HostDepartureOutcome.Promoted, succession.MemberDeparted(Battle, "A"));
    }

    // A succession whose policy keeps a host with a result only at sea, like the real one.
    private BattleHostSuccession Create(bool naval)
    {
        var policy = new Mock<IBattleHostRetentionPolicy>();
        policy.Setup(p => p.KeepsHostAfterResult(Battle, It.IsAny<bool>())).Returns<string, bool>((_, hasResult) => naval && hasResult);
        return new BattleHostSuccession(broker.Object, network.Object, registry, policy.Object);
    }
}
