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
        engine.Verify(e => e.ReleaseHull(hull), Times.Once);
    }

    // Capture 2.3 step 4: the captor takes control of its own copy and the old authority turns its hull into a copy; no swap.
    [Fact]
    public void Transfer_InPlace_AdoptsTheCopyOrReleasesTheHull_WithoutQueuingASwap()
    {
        var (authority, registry, engine, _) = Create();
        var hull = ShipTestHulls.Create();
        var ship = new NetworkShipInfo(Guid.NewGuid(), Peer, "MapEventParty_2", true, hull, null);
        registry.TryRegister(ship);

        bool adopted = authority.Transfer(ship, Own, inPlace: true);
        bool queuedASwap = authority.IsReplacing(ship.ShipId);
        bool handedAway = authority.Transfer(ship, Peer, inPlace: true);

        Assert.True(adopted);
        Assert.False(queuedASwap);
        Assert.False(handedAway);
        engine.Verify(e => e.AdoptHull(hull), Times.Once);
        engine.Verify(e => e.ReleaseHull(hull), Times.Once);
        engine.Verify(e => e.ReplaceHull(It.IsAny<MissionObject>()), Times.Never);
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

    // M8c r1: an owner still in the battle (a per-ship retreat) keeps the crew its removal left alive and
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

    // R41a fix-loop C3: only a swap that parked its agents is about to replace its hull. One parking through the deployment,
    // or one boarding its fresh hull, leaves a hull agents may move onto.
    [Fact]
    public void HullsBeingReplaced_HoldsOnlyTheHullOfASwapAboutToReplaceIt()
    {
        var (authority, registry, engine, _) = Create();
        var hull = ShipTestHulls.Create();
        var fresh = ShipTestHulls.Create();
        var ship = new NetworkShipInfo(Guid.NewGuid(), Own, "MapEventParty_2", true, hull, null);
        registry.TryRegister(ship);
        engine.Setup(e => e.ParkHullAgents(hull)).Returns(Array.Empty<HullSwapAgent>());
        engine.Setup(e => e.ReplaceHull(hull)).Returns(fresh);
        engine.SetupGet(e => e.IsDeploymentMode).Returns(true);
        authority.Replace(ship.ShipId);

        authority.Tick();
        var whileDeploying = authority.HullsBeingReplaced;
        engine.SetupGet(e => e.IsDeploymentMode).Returns(false);
        authority.Tick();
        var whileParked = authority.HullsBeingReplaced;
        authority.Tick();
        var whileBoarding = authority.HullsBeingReplaced;

        Assert.Empty(whileDeploying);
        Assert.Equal(new[] { hull }, whileParked);
        Assert.True(authority.IsReplacing(ship.ShipId));
        Assert.Empty(whileBoarding);
    }

    // R41a-2: a departed owner's party withdraws only after its hulls are gone, so the hull removal takes the crew first.
    [Fact]
    public void IsSettledFor_WaitsForTheOwnersRemovalToFinish_AndNotForAnotherOwnersRemoval()
    {
        var (authority, _, engine, _) = Create();
        var departing = new NetworkShipInfo(Guid.NewGuid(), Peer, "MapEventParty_2", false, ShipTestHulls.Create(), null);
        var other = new NetworkShipInfo(Guid.NewGuid(), "third", "MapEventParty_3", false, ShipTestHulls.Create(), null);
        engine.Setup(e => e.ParkHullAgents(It.IsAny<MissionObject>())).Returns(Array.Empty<HullSwapAgent>());

        authority.Remove(new[] { departing }, DepartingCrew.All);
        authority.Remove(new[] { other }, DepartingCrew.All);
        bool whileQueued = authority.IsSettledFor(Peer);
        authority.Tick();
        bool whileParked = authority.IsSettledFor(Peer);
        bool nobodyWhileParked = authority.IsSettledFor("nobody");
        authority.Tick();

        Assert.False(whileQueued);
        Assert.False(whileParked);
        Assert.True(nobodyWhileParked);
        engine.Verify(e => e.RemoveHull(departing.Hull), Times.Once);
        Assert.True(authority.IsSettledFor(Peer));
    }

    // The withdrawal fades agents, which must never happen to one a swap holds parked with physics off (R33, R40).
    [Fact]
    public void IsSettledFor_WaitsWhileASwapHoldsAgentsParked()
    {
        var (authority, registry, engine, _) = Create();
        var hull = ShipTestHulls.Create();
        var ship = new NetworkShipInfo(Guid.NewGuid(), Own, "MapEventParty_2", true, hull, null);
        registry.TryRegister(ship);
        engine.Setup(e => e.ParkHullAgents(hull)).Returns(new[]
        {
            new HullSwapAgent(CreateAgent(), isCrew: true, isParked: true, Vec3.Zero, Vec3.Zero),
        });
        engine.Setup(e => e.ReplaceHull(hull)).Returns(ShipTestHulls.Create());
        authority.Replace(ship.ShipId);
        authority.Tick();

        bool whileReplacing = authority.IsSettledFor(Peer);
        authority.Tick();
        bool whileBoarding = authority.IsSettledFor(Peer);
        authority.Tick();

        Assert.False(whileReplacing);
        Assert.False(whileBoarding);
        Assert.True(authority.IsSettledFor(Peer));
    }

    // R50 F1: vanilla's off-ship check can move an adopted agent in the very tick it turns AI, so it is enlisted at once.
    [Fact]
    public void EnlistCrew_EnlistsOnTheCrewHullAtOnce_AndOnlyOnce()
    {
        var (authority, _, engine, _) = Create();
        var agent = CreateAgent();
        var hull = ShipTestHulls.Create();
        engine.Setup(e => e.IsAlive(agent)).Returns(true);
        engine.Setup(e => e.CrewHullOf(agent)).Returns(hull);

        authority.EnlistCrew(agent);
        authority.Tick();

        engine.Verify(e => e.EnlistCrew(agent, hull), Times.Once);
    }

    // AddAgentToShip makes room on a full hull during deployment by parking someone else, so enlisting waits it out.
    [Fact]
    public void EnlistCrew_WaitsOutDeployment()
    {
        var (authority, _, engine, _) = Create();
        var agent = CreateAgent();
        var hull = ShipTestHulls.Create();
        engine.Setup(e => e.IsAlive(agent)).Returns(true);
        engine.Setup(e => e.CrewHullOf(agent)).Returns(hull);
        engine.SetupGet(e => e.IsDeploymentMode).Returns(true);

        authority.EnlistCrew(agent);
        authority.Tick();
        engine.Verify(e => e.EnlistCrew(It.IsAny<Agent>(), It.IsAny<MissionObject>()), Times.Never);
        engine.SetupGet(e => e.IsDeploymentMode).Returns(false);
        authority.Tick();

        engine.Verify(e => e.EnlistCrew(agent, hull), Times.Once);
    }

    // Vanilla's removal fades the crew it tracks on the hull, so an agent whose hull a swap replaces is enlisted on the fresh one.
    [Fact]
    public void EnlistCrew_WaitsWhileItsHullIsReplaced_ThenEnlistsOnTheFreshHull()
    {
        var (authority, registry, engine, _) = Create();
        var agent = CreateAgent();
        var hull = ShipTestHulls.Create();
        var fresh = ShipTestHulls.Create();
        var crewHull = hull;
        var ship = new NetworkShipInfo(Guid.NewGuid(), Own, "MapEventParty_2", true, hull, null);
        registry.TryRegister(ship);
        engine.Setup(e => e.IsAlive(agent)).Returns(true);
        engine.Setup(e => e.CrewHullOf(agent)).Returns(() => crewHull);
        engine.Setup(e => e.ParkHullAgents(hull)).Returns(Array.Empty<HullSwapAgent>());
        engine.Setup(e => e.ReplaceHull(hull)).Callback(() => crewHull = fresh).Returns(fresh);
        authority.Replace(ship.ShipId);
        authority.Tick();

        authority.EnlistCrew(agent);
        engine.Verify(e => e.EnlistCrew(It.IsAny<Agent>(), It.IsAny<MissionObject>()), Times.Never);
        authority.Tick();

        engine.Verify(e => e.EnlistCrew(agent, fresh), Times.Once);
        engine.Verify(e => e.EnlistCrew(agent, hull), Times.Never);
    }

    // A departing hull takes its crew out with it; one that died or has no crew hull left is dropped, not retried.
    [Fact]
    public void EnlistCrew_NeverEnlistsOnADepartingHull_AndDropsAnAgentWithNoCrewHullLeft()
    {
        var (authority, _, engine, _) = Create();
        var agent = CreateAgent();
        var departing = new NetworkShipInfo(Guid.NewGuid(), Peer, "MapEventParty_2", false, ShipTestHulls.Create(), null);
        MissionObject? crewHull = departing.Hull;
        engine.Setup(e => e.IsAlive(agent)).Returns(true);
        engine.Setup(e => e.CrewHullOf(agent)).Returns(() => crewHull!);
        engine.Setup(e => e.ParkHullAgents(departing.Hull)).Returns(Array.Empty<HullSwapAgent>());
        engine.Setup(e => e.RemoveHull(departing.Hull)).Callback(() => crewHull = null);
        authority.Remove(new[] { departing }, DepartingCrew.None);

        authority.EnlistCrew(agent);
        authority.Tick();
        authority.Tick();
        crewHull = ShipTestHulls.Create();
        authority.Tick();

        engine.Verify(e => e.RemoveHull(departing.Hull), Times.Once);
        engine.Verify(e => e.EnlistCrew(It.IsAny<Agent>(), It.IsAny<MissionObject>()), Times.Never);
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
