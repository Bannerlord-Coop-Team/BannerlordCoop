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

    private static MissionObject CreateHull() => ShipTestHulls.Create();
}
