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

    private static MissionObject CreateHull() => ShipTestHulls.Create();
}
