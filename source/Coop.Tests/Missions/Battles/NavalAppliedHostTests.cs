using Common.Messaging;
using Missions.Battles;
using Missions.Messages;
using Moq;
using Xunit;

namespace Coop.Tests.Missions.Battles;

/// <summary>The applied host and epoch: the join epoch, then only the epochs of applied hull moves.</summary>
public class NavalAppliedHostTests
{
    private const string Instance = "MapEvent_1";

    private readonly MessageBroker broker = new MessageBroker();
    private readonly Mock<IBattleSession> session = new Mock<IBattleSession>();
    private int sessionEpoch;

    public NavalAppliedHostTests()
    {
        session.SetupGet(s => s.InstanceId).Returns(Instance);
        session.SetupGet(s => s.HostEpoch).Returns(() => sessionEpoch);
    }

    [Fact]
    public void Starts_AtTheEpochTheSessionHolds_WithNoHostMoveApplied()
    {
        sessionEpoch = 3;

        using var applied = new NavalAppliedHost(broker, session.Object);

        Assert.Equal(3, applied.AppliedHostEpoch);
        Assert.Null(applied.Host);
    }

    // A client that opened the battle before any assignment joins at the first one; a later assignment is a host change,
    // whose epoch applies only with its hull move.
    [Fact]
    public void FirstAssignment_IsTheJoinEpoch_AndALaterOneWaitsForItsMove()
    {
        using var applied = new NavalAppliedHost(broker, session.Object);

        Assign(Instance, 1);
        Assign(Instance, 2);
        int beforeMove = applied.AppliedHostEpoch;
        applied.Apply("successor", 2);

        Assert.Equal(1, beforeMove);
        Assert.Equal(2, applied.AppliedHostEpoch);
        Assert.Equal("successor", applied.Host);
    }

    [Fact]
    public void Assignment_OfAnotherBattle_IsIgnored()
    {
        using var applied = new NavalAppliedHost(broker, session.Object);

        Assign("MapEvent_2", 4);

        Assert.Equal(0, applied.AppliedHostEpoch);
    }

    [Fact]
    public void Dispose_StopsTakingAssignments()
    {
        var applied = new NavalAppliedHost(broker, session.Object);

        applied.Dispose();
        Assign(Instance, 1);

        Assert.Equal(0, applied.AppliedHostEpoch);
    }

    // Publishes what BattleHostHandler publishes once it stored an assignment.
    private void Assign(string instanceId, int epoch) =>
        broker.Publish(this, new BattleHostAssignmentApplied(new NetworkBattleHostAssigned(instanceId, "host", null, epoch)));
}
