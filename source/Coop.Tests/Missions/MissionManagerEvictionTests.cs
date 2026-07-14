using System.Linq;
using Common.Util;
using Coop.Core.Server.Services.Instances;
using Coop.Tests.Extensions;
using LiteNetLib;
using Xunit;

namespace Coop.Tests.Missions;

/// <summary>
/// Unit tests for <see cref="MissionManager"/>'s one-instance-per-controller enforcement in
/// <see cref="MissionManager.EnterMission"/>: a controller whose prior leave never reached the server (mission
/// teardown died before the <c>MissionLeft</c>) is evicted from its old relay table on entry, and that missed
/// departure is surfaced as a <see cref="StaleDeparture"/> — carrying the members still present — so the caller
/// can notify them. The peers here are only reference-equality routing keys, so uninitialized stand-ins suffice.
/// </summary>
public class MissionManagerEvictionTests
{
    // NetPeer keys the relay table by its endpoint, so each stand-in needs a distinct, initialized endpoint.
    private static NetPeer NewPeer(int id)
    {
        var peer = ObjectHelper.SkipConstructor<NetPeer>();
        peer.Setup(id, $"127.0.0.{id}");
        return peer;
    }

    [Fact]
    public void EnterMission_WithNoPriorInstance_ReportsNoStaleDepartures()
    {
        var manager = new MissionManager();

        manager.EnterMission(NewPeer(1), "A", "instance1", out var stale);

        Assert.Empty(stale);
    }

    [Fact]
    public void EnterMission_EvictsFromPriorInstance_AndReportsTheMembersStillThere()
    {
        var manager = new MissionManager();
        var peerX = NewPeer(1);
        var peerY = NewPeer(2);

        // X and Y share instance1.
        manager.EnterMission(peerX, "X", "instance1", out _);
        manager.EnterMission(peerY, "Y", "instance1", out _);

        // X enters instance2 WITHOUT leaving instance1 first (its leave never arrived).
        manager.EnterMission(peerX, "X", "instance2", out var stale);

        // The missed departure is reported, carrying the member still present in the old instance so the caller
        // can tell it X is gone.
        var departure = Assert.Single(stale);
        Assert.Equal("instance1", departure.InstanceId);
        var remaining = Assert.Single(departure.Remaining);
        Assert.Equal("Y", remaining.controllerId);
        Assert.Same(peerY, remaining.peer);

        // The server routing table reflects the move: the old instance no longer resolves X, the new one does.
        Assert.True(manager.TryGetControllers("instance1", out var oldControllers));
        Assert.Equal(new[] { "Y" }, oldControllers.ToArray());
        Assert.False(manager.TryGetRelayTarget("instance1", "X", out _));
        Assert.True(manager.TryGetRelayTarget("instance2", "X", out _));
    }

    [Fact]
    public void EnterMission_EvictingTheLastMember_ReportsAnEmptyRemainder()
    {
        var manager = new MissionManager();
        var peerX = NewPeer(1);

        manager.EnterMission(peerX, "X", "instance1", out _);

        // X was alone in instance1; entering instance2 evicts it, leaving nobody to notify.
        manager.EnterMission(peerX, "X", "instance2", out var stale);

        var departure = Assert.Single(stale);
        Assert.Equal("instance1", departure.InstanceId);
        Assert.Empty(departure.Remaining);
    }

    [Fact]
    public void EnterMission_AfterAGracefulLeave_ReportsNoStaleDeparture()
    {
        var manager = new MissionManager();
        var peerX = NewPeer(1);

        manager.EnterMission(peerX, "X", "instance1", out _);
        manager.LeaveMission(peerX, "X", "instance1"); // the leave DID arrive, so nothing lingers

        manager.EnterMission(peerX, "X", "instance2", out var stale);

        Assert.Empty(stale);
    }
}
