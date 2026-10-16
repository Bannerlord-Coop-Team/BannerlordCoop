using Missions;
using Missions.Battles;
using Moq;
using System;
using Xunit;

namespace Coop.Tests.Missions.Battles;

public class NavalHullAuthorityTests
{
    private const string Own = "local";
    private const string Peer = "peer";

    // M10 capture hands a player's hull to a new authority through the same swap an AI hull takes on a host change.
    [Fact]
    public void Transfer_APlayerHullToThisClient_QueuesItsReplacement_AndAwayReleasesIt()
    {
        var (authority, registry, engine) = Create();
        var hull = ShipTestHulls.Create();
        var ship = new NetworkShipInfo(Guid.NewGuid(), Peer, "MapEventParty_2", false, hull, null);
        registry.TryRegister(ship);

        bool takenOver = authority.Transfer(ship, Own);
        bool replacing = authority.IsReplacing(ship.ShipId);
        bool handedAway = authority.Transfer(ship, Peer);

        Assert.True(takenOver);
        Assert.True(replacing);
        Assert.False(handedAway);
        Assert.False(authority.IsReplacing(ship.ShipId));
        Assert.Equal(Peer, ship.CurrentAuthority);
        engine.Verify(e => e.ReleaseNpcHull(hull), Times.Once);
    }

    [Fact]
    public void SuppressSpawnRegistration_HoldsUntilEveryScopeCloses()
    {
        var (authority, _, _) = Create();

        var outer = authority.SuppressSpawnRegistration();
        var inner = authority.SuppressSpawnRegistration();
        inner.Dispose();
        inner.Dispose();
        bool afterInner = authority.IsSpawnRegistrationSuppressed;
        outer.Dispose();

        Assert.True(afterInner);
        Assert.False(authority.IsSpawnRegistrationSuppressed);
    }

    private static (NavalHullAuthority Authority, NetworkShipRegistry Registry, Mock<INavalShipEngine> Engine) Create()
    {
        var registry = new NetworkShipRegistry();
        var component = new Mock<ICoopMissionComponent>();
        component.SetupGet(c => c.ShipRegistry).Returns(registry);
        var session = new Mock<IBattleSession>();
        session.SetupGet(s => s.OwnControllerId).Returns(Own);
        var engine = new Mock<INavalShipEngine>();
        return (new NavalHullAuthority(session.Object, component.Object, engine.Object), registry, engine);
    }
}
