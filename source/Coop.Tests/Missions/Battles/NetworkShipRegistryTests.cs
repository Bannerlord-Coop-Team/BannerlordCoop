using Missions.Battles;
using System;
using System.Runtime.CompilerServices;
using TaleWorlds.MountAndBlade;
using Xunit;

namespace Coop.Tests.Missions.Battles;

public class NetworkShipRegistryTests
{
    [Fact]
    public void TryRegister_FindsTheHullByIdAndObject()
    {
        var registry = new NetworkShipRegistry();
        var hull = CreateHull();
        var ship = new NetworkShipInfo(Guid.NewGuid(), "local", "MapEventParty_1", false, hull, null);

        Assert.True(registry.TryRegister(ship));

        Assert.True(registry.TryGet(ship.ShipId, out var byId));
        Assert.Same(ship, byId);
        Assert.True(registry.TryGetByHull(hull, out var byHull));
        Assert.Same(ship, byHull);
        Assert.Equal("local", byId.CurrentAuthority);
        Assert.Equal("local", byId.OriginalOwner);
    }

    [Fact]
    public void TryRegister_SameHullTwice_KeepsTheFirstIdentity()
    {
        var registry = new NetworkShipRegistry();
        var hull = CreateHull();
        var first = new NetworkShipInfo(Guid.NewGuid(), "local", null, false, hull, null);

        Assert.True(registry.TryRegister(first));
        Assert.False(registry.TryRegister(new NetworkShipInfo(Guid.NewGuid(), "local", null, false, hull, null)));
        Assert.Same(first, Assert.Single(registry.Ships));
    }

    [Fact]
    public void TryRebindHull_KeepsTheShipIdentityOnTheReplacementHull()
    {
        var registry = new NetworkShipRegistry();
        var copy = CreateHull();
        var fresh = CreateHull();
        var ship = new NetworkShipInfo(Guid.NewGuid(), "host", "MapEventParty_9", true, copy, null);
        registry.TryRegister(ship);
        ship.CurrentAuthority = "successor";

        Assert.True(registry.TryRebindHull(ship.ShipId, fresh));

        Assert.True(registry.TryGetByHull(fresh, out var byFresh));
        Assert.Same(ship, byFresh);
        Assert.False(registry.TryGetByHull(copy, out _));
        Assert.Equal("successor", byFresh.CurrentAuthority);
        Assert.Equal("host", byFresh.OriginalOwner);
        Assert.Equal("MapEventParty_9", byFresh.MapEventPartyId);
        Assert.True(byFresh.IsNpcParty);
    }

    [Fact]
    public void TryRebindHull_RefusesAHullAnotherShipHoldsOrAnUnknownShip()
    {
        var registry = new NetworkShipRegistry();
        var first = new NetworkShipInfo(Guid.NewGuid(), "host", null, true, CreateHull(), null);
        var second = new NetworkShipInfo(Guid.NewGuid(), "host", null, true, CreateHull(), null);
        registry.TryRegister(first);
        registry.TryRegister(second);

        Assert.False(registry.TryRebindHull(first.ShipId, second.Hull));
        Assert.False(registry.TryRebindHull(Guid.NewGuid(), CreateHull()));
        Assert.False(registry.TryRebindHull(first.ShipId, null));
    }

    [Fact]
    public void MarkSpawnFailed_HoldsUntilTheHullRegisters()
    {
        var registry = new NetworkShipRegistry();
        var ship = new NetworkShipInfo(Guid.NewGuid(), "host", "MapEventParty_9", true, CreateHull(), null);

        registry.MarkSpawnFailed(ship.ShipId);
        bool failedBeforeRegister = registry.StateOf(ship.ShipId) == ShipLifecycle.SpawnFailed;
        registry.TryRegister(ship);
        registry.MarkSpawnFailed(ship.ShipId);

        Assert.True(failedBeforeRegister);
        Assert.False(registry.StateOf(ship.ShipId) == ShipLifecycle.SpawnFailed);
    }

    [Fact]
    public void StateOf_FollowsAForeignHullFromItsWaitingRecordToRegisteredAndRemoved()
    {
        var registry = new NetworkShipRegistry();
        var ship = new NetworkShipInfo(Guid.NewGuid(), "peer", "MapEventParty_2", false, CreateHull(), null);
        var unknown = registry.StateOf(ship.ShipId);

        registry.MarkAwaiting(ship.ShipId);
        var awaiting = registry.StateOf(ship.ShipId);
        registry.TryRegister(ship);
        registry.MarkAwaiting(ship.ShipId);
        var registered = registry.StateOf(ship.ShipId);
        registry.Remove(ship.ShipId);

        Assert.Equal(ShipLifecycle.Unknown, unknown);
        Assert.Equal(ShipLifecycle.Awaiting, awaiting);
        Assert.Equal(ShipLifecycle.Registered, registered);
        Assert.Equal(ShipLifecycle.Removed, registry.StateOf(ship.ShipId));
    }

    [Fact]
    public void StateOf_AWaitingRecordGivenUpOnReadsSpawnFailed_AndRemovingAnUnknownShipChangesNothing()
    {
        var registry = new NetworkShipRegistry();
        var shipId = Guid.NewGuid();
        registry.MarkAwaiting(shipId);

        registry.MarkSpawnFailed(shipId);

        Assert.Equal(ShipLifecycle.SpawnFailed, registry.StateOf(shipId));
        Assert.False(registry.Remove(shipId));
        Assert.Equal(ShipLifecycle.SpawnFailed, registry.StateOf(shipId));
    }

    // M8a: a hull that left with its owner stays gone; a late record or a retry must not make it wait or spawn again.
    [Fact]
    public void MarkRemoved_SticksOverALaterWaitOrFailure_AndReadsGone()
    {
        var registry = new NetworkShipRegistry();
        var shipId = Guid.NewGuid();
        registry.MarkAwaiting(shipId);
        bool goneWhileAwaiting = registry.IsGone(shipId);

        registry.MarkRemoved(shipId);
        registry.MarkAwaiting(shipId);
        registry.MarkSpawnFailed(shipId);

        Assert.False(goneWhileAwaiting);
        Assert.Equal(ShipLifecycle.Removed, registry.StateOf(shipId));
        Assert.True(registry.IsGone(shipId));
    }

    [Fact]
    public void IsGone_HoldsForAGivenUpOrRemovedShip_NotForAWaitingOrRegisteredOne()
    {
        var registry = new NetworkShipRegistry();
        var ship = new NetworkShipInfo(Guid.NewGuid(), "peer", "MapEventParty_2", false, CreateHull(), null);
        var failed = Guid.NewGuid();
        registry.MarkSpawnFailed(failed);
        registry.TryRegister(ship);
        bool goneWhileRegistered = registry.IsGone(ship.ShipId);

        registry.Remove(ship.ShipId);

        Assert.True(registry.IsGone(failed));
        Assert.False(goneWhileRegistered);
        Assert.True(registry.IsGone(ship.ShipId));
        Assert.False(registry.IsGone(Guid.NewGuid()));
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(BattleShipReplicator.ForeignHullSpawnAttempts - 1, false)]
    [InlineData(BattleShipReplicator.ForeignHullSpawnAttempts, true)]
    public void IsForeignHullSpawnExhausted_StopsRetryingAfterTheBoundedAttempts(int attempts, bool expected)
    {
        Assert.Equal(expected, BattleShipReplicator.IsForeignHullSpawnExhausted(attempts));
    }

    private static MissionObject CreateHull() => ShipTestHulls.Create();
}
