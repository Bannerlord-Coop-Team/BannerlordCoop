using Missions;
using Missions.Battles;
using Moq;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
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
        var (authority, registry, engine, _) = Create();
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

    // M8a r1, R33, M8c: vanilla removes no hull its agents stand on, so everyone aboard departing hulls is parked a tick before
    // they go. Then their crew, including one departing hull's crew on another's deck, withdraw before the hulls are removed,
    // as the owner's removal took them out of the battle; anyone else goes back.
    [Fact]
    public void Remove_ParksEveryoneAboardOneTick_ThenWithdrawsTheCrewBeforeRemovingTheHulls_AndPutsBackTheRest()
    {
        var (authority, _, engine, withdrawal) = Create();
        var first = new NetworkShipInfo(Guid.NewGuid(), Peer, "MapEventParty_2", false, ShipTestHulls.Create(), null);
        var second = new NetworkShipInfo(Guid.NewGuid(), Peer, "MapEventParty_2", false, ShipTestHulls.Create(), null);
        var crew = CreateAgent();
        var otherHullsCrew = CreateAgent();
        var stray = CreateAgent();
        var strayParked = new HullSwapAgent(stray, isCrew: false, isParked: true, Vec3.Zero, Vec3.Zero);
        engine.Setup(e => e.ParkHullAgents(first.Hull)).Returns(new[]
        {
            new HullSwapAgent(crew, isCrew: true, isParked: true, Vec3.Zero, Vec3.Zero),
            new HullSwapAgent(otherHullsCrew, isCrew: false, isParked: true, Vec3.Zero, Vec3.Zero),
            strayParked,
        });
        engine.Setup(e => e.ParkHullAgents(second.Hull)).Returns(new[]
        {
            new HullSwapAgent(otherHullsCrew, isCrew: true, isParked: false, Vec3.Zero, Vec3.Zero),
        });
        var steps = new List<string>();
        withdrawal.Setup(w => w.Deregister(It.IsAny<Agent>())).Callback<Agent>(agent => steps.Add(Name(agent) + " deregister"));
        engine.Setup(e => e.WithdrawAgent(It.IsAny<Agent>())).Callback<Agent>(agent => steps.Add(Name(agent) + " withdraw"));
        engine.Setup(e => e.RemoveHull(It.IsAny<MissionObject>())).Callback(() => steps.Add("remove"));
        IReadOnlyList<HullSwapAgent>? unparked = null;
        engine.Setup(e => e.BoardHullAgents(null!, It.IsAny<IReadOnlyList<HullSwapAgent>>()))
            .Callback<MissionObject, IReadOnlyList<HullSwapAgent>>((_, agents) => unparked = agents);

        authority.Remove(new[] { first, second }, DepartingCrew.All);
        authority.Tick();
        engine.Verify(e => e.ParkHullAgents(first.Hull), Times.Once);
        engine.Verify(e => e.ParkHullAgents(second.Hull), Times.Once);
        Assert.Empty(steps);
        authority.Tick();
        authority.Tick();

        engine.Verify(e => e.RemoveHull(first.Hull), Times.Once);
        engine.Verify(e => e.RemoveHull(second.Hull), Times.Once);
        Assert.Equal(new[]
        {
            "crew deregister", "crew withdraw", "otherHullsCrew deregister", "otherHullsCrew withdraw", "remove", "remove",
        }, steps);
        Assert.Equal(new[] { strayParked }, unparked);

        string Name(Agent agent) => agent == crew ? "crew" : agent == otherHullsCrew ? "otherHullsCrew" : "stray";
    }

    // M8c: a departed owner's crew goes whether or not it stood aboard, but this client's main agent never withdraws.
    [Fact]
    public void Remove_HullsLeavingWithTheirOwner_WithdrawCrewNotAboard_ButNeverTheMainAgent()
    {
        var (authority, _, engine, withdrawal) = Create();
        var departing = new NetworkShipInfo(Guid.NewGuid(), Peer, "MapEventParty_2", false, ShipTestHulls.Create(), null);
        var crewInTheWater = CreateAgent();
        var hero = CreateAgent();
        var heroMoved = new HullSwapAgent(hero, isCrew: true, isParked: true, Vec3.Zero, Vec3.Zero);
        engine.Setup(e => e.IsMainAgent(hero)).Returns(true);
        engine.Setup(e => e.ParkHullAgents(departing.Hull)).Returns(new[]
        {
            new HullSwapAgent(crewInTheWater, isCrew: true, isParked: false, Vec3.Zero, Vec3.Zero),
            heroMoved,
        });
        IReadOnlyList<HullSwapAgent>? unparked = null;
        engine.Setup(e => e.BoardHullAgents(null!, It.IsAny<IReadOnlyList<HullSwapAgent>>()))
            .Callback<MissionObject, IReadOnlyList<HullSwapAgent>>((_, agents) => unparked = agents);

        authority.Remove(new[] { departing }, DepartingCrew.All);
        authority.Tick();
        authority.Tick();

        withdrawal.Verify(w => w.Deregister(crewInTheWater), Times.Once);
        engine.Verify(e => e.WithdrawAgent(crewInTheWater), Times.Once);
        withdrawal.Verify(w => w.Deregister(hero), Times.Never);
        engine.Verify(e => e.WithdrawAgent(hero), Times.Never);
        Assert.Equal(new[] { heroMoved }, unparked);
    }

    // M8c r1: an owner still in the battle (a per-ship retreat, a reclaimed duplicate) keeps the crew its removal left alive and
    // sends the ones it fades as routs, so nobody withdraws here and everyone parked goes back once the hull is removed.
    [Fact]
    public void Remove_HullsWhoseOwnerStays_WithdrawNobody_AndPutEveryoneParkedBack()
    {
        var (authority, _, engine, withdrawal) = Create();
        var departing = new NetworkShipInfo(Guid.NewGuid(), Peer, "MapEventParty_2", false, ShipTestHulls.Create(), null);
        var crewAboard = CreateAgent();
        var crewInTheWater = CreateAgent();
        var crewAboardMoved = new HullSwapAgent(crewAboard, isCrew: true, isParked: true, Vec3.Zero, Vec3.Zero);
        engine.Setup(e => e.ParkHullAgents(departing.Hull)).Returns(new[]
        {
            crewAboardMoved,
            new HullSwapAgent(crewInTheWater, isCrew: true, isParked: false, Vec3.Zero, Vec3.Zero),
        });
        var steps = new List<string>();
        engine.Setup(e => e.RemoveHull(It.IsAny<MissionObject>())).Callback(() => steps.Add("remove"));
        engine.Setup(e => e.BoardHullAgents(null!, It.IsAny<IReadOnlyList<HullSwapAgent>>()))
            .Callback<MissionObject, IReadOnlyList<HullSwapAgent>>((_, agents) => steps.Add($"unpark {agents.Count}"));

        authority.Remove(new[] { departing }, DepartingCrew.None);
        authority.Tick();
        authority.Tick();

        withdrawal.Verify(w => w.Deregister(It.IsAny<Agent>()), Times.Never);
        engine.Verify(e => e.WithdrawAgent(It.IsAny<Agent>()), Times.Never);
        Assert.Equal(new[] { "remove", "unpark 1" }, steps);
        engine.Verify(e => e.BoardHullAgents(null!, It.Is<IReadOnlyList<HullSwapAgent>>(agents => agents[0] == crewAboardMoved)),
            Times.Once);
    }

    // M8b: an own duplicate's crew leaves the battle with the hull, the way vanilla's removal withdraws a hull's crew. Own
    // agents stay registered, so their own removal reaches the peers.
    [Fact]
    public void Remove_AnOwnDuplicate_WithdrawsOnlyTheParkedCrewWithTheHull()
    {
        var (authority, _, engine, withdrawal) = Create();
        var duplicate = new NetworkShipInfo(Guid.NewGuid(), Own, "MapEventParty_1", false, ShipTestHulls.Create(), null);
        var crew = CreateAgent();
        var stray = CreateAgent();
        engine.Setup(e => e.ParkHullAgents(duplicate.Hull)).Returns(new[]
        {
            new HullSwapAgent(crew, isCrew: true, isParked: true, Vec3.Zero, Vec3.Zero),
            new HullSwapAgent(stray, isCrew: false, isParked: true, Vec3.Zero, Vec3.Zero),
        });

        authority.Remove(new[] { duplicate }, DepartingCrew.ParkedOwn);
        authority.Tick();
        engine.Verify(e => e.WithdrawAgent(It.IsAny<Agent>()), Times.Never);
        authority.Tick();

        engine.Verify(e => e.RemoveHull(duplicate.Hull), Times.Once);
        engine.Verify(e => e.WithdrawAgent(crew), Times.Once);
        engine.Verify(e => e.WithdrawAgent(stray), Times.Never);
        withdrawal.Verify(w => w.Deregister(It.IsAny<Agent>()), Times.Never);
    }

    // M8b r1: crew is a formation match, so the returned hero in the duplicate's formation, or crew off its deck, stay.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Remove_AnOwnDuplicate_KeepsTheHeroInItsFormation_AndCrewNotAboard(bool heroAboard)
    {
        var (authority, _, engine, _) = Create();
        var duplicate = new NetworkShipInfo(Guid.NewGuid(), Own, "MapEventParty_1", false, ShipTestHulls.Create(), null);
        var crew = CreateAgent();
        var crewInTheWater = CreateAgent();
        var hero = CreateAgent();
        var heroMoved = new HullSwapAgent(hero, isCrew: true, isParked: heroAboard, Vec3.Zero, Vec3.Zero);
        engine.Setup(e => e.IsMainAgent(hero)).Returns(true);
        engine.Setup(e => e.ParkHullAgents(duplicate.Hull)).Returns(new[]
        {
            new HullSwapAgent(crew, isCrew: true, isParked: true, Vec3.Zero, Vec3.Zero),
            new HullSwapAgent(crewInTheWater, isCrew: true, isParked: false, Vec3.Zero, Vec3.Zero),
            heroMoved,
        });
        IReadOnlyList<HullSwapAgent>? unparked = null;
        engine.Setup(e => e.BoardHullAgents(null!, It.IsAny<IReadOnlyList<HullSwapAgent>>()))
            .Callback<MissionObject, IReadOnlyList<HullSwapAgent>>((_, agents) => unparked = agents);

        authority.Remove(new[] { duplicate }, DepartingCrew.ParkedOwn);
        authority.Tick();
        authority.Tick();

        engine.Verify(e => e.WithdrawAgent(crew), Times.Once);
        engine.Verify(e => e.WithdrawAgent(crewInTheWater), Times.Never);
        engine.Verify(e => e.WithdrawAgent(hero), Times.Never);
        Assert.Equal(heroAboard ? new[] { heroMoved } : Array.Empty<HullSwapAgent>(), unparked ?? Array.Empty<HullSwapAgent>());
    }

    [Fact]
    public void SuppressSpawnRegistration_HoldsUntilEveryScopeCloses()
    {
        var (authority, _, _, _) = Create();

        var outer = authority.SuppressSpawnRegistration();
        var inner = authority.SuppressSpawnRegistration();
        inner.Dispose();
        inner.Dispose();
        bool afterInner = authority.IsSpawnRegistrationSuppressed;
        outer.Dispose();

        Assert.True(afterInner);
        Assert.False(authority.IsSpawnRegistrationSuppressed);
    }

    private static Agent CreateAgent() => (Agent)RuntimeHelpers.GetUninitializedObject(typeof(Agent));

    private static (NavalHullAuthority Authority, NetworkShipRegistry Registry, Mock<INavalShipEngine> Engine,
        Mock<IBattleTroopWithdrawal> Withdrawal) Create()
    {
        var registry = new NetworkShipRegistry();
        var component = new Mock<ICoopMissionComponent>();
        component.SetupGet(c => c.ShipRegistry).Returns(registry);
        var session = new Mock<IBattleSession>();
        session.SetupGet(s => s.OwnControllerId).Returns(Own);
        var engine = new Mock<INavalShipEngine>();
        var withdrawal = new Mock<IBattleTroopWithdrawal>();
        return (new NavalHullAuthority(session.Object, component.Object, engine.Object, withdrawal.Object), registry, engine,
            withdrawal);
    }
}
