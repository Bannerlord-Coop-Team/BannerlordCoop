#if COOP_NAVAL
using Common.Commands;
using Missions.Naval;
using Missions.Messages;
using Moq;
using NavalDLC.Missions.Objects;
using NavalDLC.Missions.Objects.UsableMachines;
using Xunit;

namespace Coop.Tests.Missions.Battles;

/// <summary>Checks invalid cut requests cannot mutate a rope or another authority's hull.</summary>
[Collection("Mission.Current")]
public class NavalRopeCutCoopCommandTests
{
    /// <summary>Rejects malformed station indices before reading mission state.</summary>
    [Theory]
    [InlineData(0, null)]
    [InlineData(2, "0")]
    [InlineData(1, "-1")]
    [InlineData(1, "+1")]
    [InlineData(1, "1.5")]
    [InlineData(1, "2147483648")]
    [InlineData(1, "cut")]
    public void ProcessCommand_RejectsMalformedIndices(int count, string? index)
    {
        var hulls = new Mock<INavalDebugHulls>(MockBehavior.Strict);
        var result = new NavalRopeCutCoopCommand(hulls.Object).ProcessCommand(Args(count, index));

        Assert.False(result.Succeeded);
        Assert.Equal("command_failed", result.ErrorCode);
        hulls.VerifyNoOtherCalls();
    }

    /// <summary>Stops when the local client cannot select an authoritative hull.</summary>
    [Fact]
    public void ProcessCommand_ReportsOwnHullRefusal()
    {
        var hulls = new Mock<INavalDebugHulls>(MockBehavior.Strict);
        MissionShip hull = null!;
        hulls.Setup(value => value.TryGetOwnHull(out hull)).Returns("This client owns no registered hull.");

        var result = new NavalRopeCutCoopCommand(hulls.Object).ProcessCommand(Args(1, "0"));

        Assert.False(result.Succeeded);
        Assert.Equal("This client owns no registered hull.", result.Output);
    }

    /// <summary>Rejects missing stations and stations without a target before entering vanilla's disconnect.</summary>
    [Theory]
    [InlineData("1")]
    [InlineData("0")]
    public void ProcessCommand_RejectsMissingConnections(string index)
    {
        var hull = ShipTestHulls.Uninitialized<MissionShip>();
        var station = ShipTestHulls.Uninitialized<ShipAttachmentMachine>();
        var hulls = new Mock<INavalDebugHulls>(MockBehavior.Strict);
        hulls.Setup(value => value.TryGetOwnHull(out hull)).Returns((string)null!);
        hulls.Setup(value => value.RopeStations(hull)).Returns(new[] { station });

        var result = new NavalRopeCutCoopCommand(hulls.Object).ProcessCommand(Args(1, index));

        Assert.False(result.Succeeded);
        Assert.Equal("command_failed", result.ErrorCode);
    }

    /// <summary>Refuses in-flight, already broken and targetless ropes before vanilla dereferences their connection.</summary>
    [Theory]
    [InlineData(ShipAttachmentMachine.ShipAttachment.ShipAttachmentState.RopeThrown, true)]
    [InlineData(ShipAttachmentMachine.ShipAttachment.ShipAttachmentState.BrokenAndWaitingForRemoval, true)]
    [InlineData(ShipAttachmentMachine.ShipAttachment.ShipAttachmentState.RopesPulling, false)]
    public void ProcessCommand_RejectsRopesWithoutALiveConnection(ShipAttachmentMachine.ShipAttachment.ShipAttachmentState state, bool hasTarget)
    {
        var hull = ShipTestHulls.Uninitialized<MissionShip>();
        var station = ShipTestHulls.Uninitialized<ShipAttachmentMachine>();
        var rope = ShipTestHulls.Uninitialized<ShipAttachmentMachine.ShipAttachment>();
        rope._state = state;
        rope.AttachmentTarget = hasTarget ? ShipTestHulls.Uninitialized<ShipAttachmentPointMachine>() : null;
        station.CurrentAttachment = rope;
        var hulls = new Mock<INavalDebugHulls>(MockBehavior.Strict);
        hulls.Setup(value => value.TryGetOwnHull(out hull)).Returns((string)null!);
        hulls.Setup(value => value.RopeStations(hull)).Returns(new[] { station });

        var result = new NavalRopeCutCoopCommand(hulls.Object).ProcessCommand(Args(1, "0"));

        Assert.False(result.Succeeded);
        Assert.Equal("The station needs a live attached rope or plank to cut.", result.Output);
        Assert.Equal(state, rope.State);
    }
    /// <summary>Releasing a held removal works after the rope itself has already disappeared.</summary>
    [Fact]
    public void ProcessCommand_ReleasesRemovalWithoutALiveAttachment()
    {
        var hull = ShipTestHulls.Uninitialized<MissionShip>();
        var station = ShipTestHulls.Uninitialized<ShipAttachmentMachine>();
        var hulls = new Mock<INavalDebugHulls>(MockBehavior.Strict);
        hulls.Setup(value => value.TryGetOwnHull(out hull)).Returns((string)null!);
        hulls.Setup(value => value.RopeStations(hull)).Returns(new[] { station });
        var records = NavalRopes.records;
        NavalRopes.Clear();
        try
        {
            records[station] = new NavalRopes.RopeRecord { Generation = 1, Last = BattleRopeStateTests.Rope(1, BattleRopeState.RopesPulling) };
            Assert.True(NavalRopes.HoldRemoval(station));
            var args = new Mock<ICoopCommandArgs>();
            args.SetupGet(value => value.Count).Returns(2);
            args.SetupGet(value => value[0]).Returns("0");
            args.SetupGet(value => value[1]).Returns("release_removal");

            var result = new NavalRopeCutCoopCommand(hulls.Object).ProcessCommand(args.Object);

            Assert.True(result.Succeeded);
            Assert.Equal("NAVAL_ROPE_REMOVAL_RELEASED sourceIndex=0", result.Output);
            Assert.Null(records[station].HeldRemovalLiveState);
            Assert.NotNull(records[station].RemovalHoldReleasedUtc);
        }
        finally
        {
            NavalRopes.Clear();
        }
    }

    /// <summary>Supplies command arguments without starting a game mission.</summary>
    private static ICoopCommandArgs Args(int count, string? index)
    {
        var args = new Mock<ICoopCommandArgs>();
        args.SetupGet(value => value.Count).Returns(count);
        args.SetupGet(value => value[0]).Returns(index!);
        return args.Object;
    }
}
#endif
