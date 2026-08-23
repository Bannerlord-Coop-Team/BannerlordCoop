using GameInterface.Services.SiegeEvents.Commands;
using Xunit;

namespace GameInterface.Tests.Services.SiegeEvents;

public class DefenderSiegeFixtureContractTests
{
    [Fact]
    public void HasExactControllerIds_RequiresTheTwoExpectedConnectedControllers()
    {
        Assert.True(DefenderSiegeFixtureContract.HasExactControllerIds(
            new[] { "testclient2", "testclient" },
            new[] { "testclient", "testclient2" }));
        Assert.False(DefenderSiegeFixtureContract.HasExactControllerIds(
            new[] { "testclient", "testclient" },
            new[] { "testclient", "testclient2" }));
        Assert.False(DefenderSiegeFixtureContract.HasExactControllerIds(
            new[] { "testclient", "testclient2", "other" },
            new[] { "testclient", "testclient2" }));
    }

    [Fact]
    public void IsCleanForCapture_RejectsAnyAmbiguousPartyState()
    {
        Assert.True(DefenderSiegeFixtureContract.IsCleanForCapture(CreateState()));
        Assert.False(DefenderSiegeFixtureContract.IsCleanForCapture(CreateState(hasMapEvent: true)));
        Assert.False(DefenderSiegeFixtureContract.IsCleanForCapture(CreateState(hasBesiegerCamp: true)));
        Assert.False(DefenderSiegeFixtureContract.IsCleanForCapture(CreateState(isAtSea: true)));
        Assert.False(DefenderSiegeFixtureContract.IsCleanForCapture(CreateState(isHolding: false)));
        Assert.False(DefenderSiegeFixtureContract.IsCleanForCapture(CreateState(isTransitionInProgress: true)));
        Assert.False(DefenderSiegeFixtureContract.IsCleanForCapture(CreateState(hasArmy: true)));
        Assert.False(DefenderSiegeFixtureContract.IsCleanForCapture(CreateState(hasAttachedTo: true)));
        Assert.False(DefenderSiegeFixtureContract.IsCleanForCapture(CreateState(hasAttachedParties: true)));
    }

    [Fact]
    public void IsStaged_RequiresTheDefenderPartyInsideDanusticaWithoutSiegeState()
    {
        Assert.True(DefenderSiegeFixtureContract.IsStaged(
            CreateState(currentSettlementId: "town_ES1"), "town_ES1"));
        Assert.False(DefenderSiegeFixtureContract.IsStaged(
            CreateState(currentSettlementId: "town_ES1", hasBesiegerCamp: true), "town_ES1"));
        Assert.False(DefenderSiegeFixtureContract.IsStaged(
            CreateState(currentSettlementId: "town_ES2"), "town_ES1"));
    }

    [Fact]
    public void IsRestored_RequiresTheCapturedPartyStateExactly()
    {
        DefenderFixturePartyState captured = CreateState(
            currentSettlementId: "town_ES2",
            lastVisitedSettlementId: "town_ES2",
            positionX: 12f,
            bearingY: 4f);

        Assert.True(DefenderSiegeFixtureContract.IsRestored(captured, captured));
        Assert.False(DefenderSiegeFixtureContract.IsRestored(captured,
            CreateState(
                currentSettlementId: "town_ES2",
                lastVisitedSettlementId: "town_ES2",
                positionX: 12f,
                bearingY: 5f)));
    }

    [Fact]
    public void IsPreAssaultReady_RequiresTopologyAndTheRoleLocalParty()
    {
        Assert.True(DefenderSiegeFixtureContract.IsPreAssaultReady(
            fixtureStaged: true,
            connectionReady: true,
            expectedPlayersResolved: true,
            noMapEvent: true,
            noBesiegerCamp: true,
            insideSettlement: true,
            localPartyReady: true));
        Assert.False(DefenderSiegeFixtureContract.IsPreAssaultReady(
            fixtureStaged: true,
            connectionReady: true,
            expectedPlayersResolved: true,
            noMapEvent: true,
            noBesiegerCamp: true,
            insideSettlement: true,
            localPartyReady: false));
    }

    [Fact]
    public void CanEnterSettlementWithoutOwnerVisit_RejectsAnOwnerLeader()
    {
        Assert.True(DefenderSiegeFixtureContract.CanEnterSettlementWithoutOwnerVisit(false));
        Assert.False(DefenderSiegeFixtureContract.CanEnterSettlementWithoutOwnerVisit(true));
    }

    [Fact]
    public void IsConnectionReadinessSatisfied_RequiresServerIdentityButOnlyAnAuthoritativeCountOnClients()
    {
        Assert.True(DefenderSiegeFixtureContract.IsConnectionReadinessSatisfied(
            isServer: true,
            connectedPlayerCount: 2,
            connectedControllersExact: true));
        Assert.False(DefenderSiegeFixtureContract.IsConnectionReadinessSatisfied(
            isServer: true,
            connectedPlayerCount: 2,
            connectedControllersExact: false));
        Assert.True(DefenderSiegeFixtureContract.IsConnectionReadinessSatisfied(
            isServer: false,
            connectedPlayerCount: 2,
            connectedControllersExact: false));
        Assert.False(DefenderSiegeFixtureContract.IsConnectionReadinessSatisfied(
            isServer: false,
            connectedPlayerCount: 1,
            connectedControllersExact: false));
    }

    private static DefenderFixturePartyState CreateState(
        string currentSettlementId = null,
        string lastVisitedSettlementId = null,
        float positionX = 1f,
        float bearingY = 2f,
        bool hasMapEvent = false,
        bool hasBesiegerCamp = false,
        bool isAtSea = false,
        bool isHolding = true,
        bool isTransitionInProgress = false,
        bool hasArmy = false,
        bool hasAttachedTo = false,
        bool hasAttachedParties = false) =>
        new DefenderFixturePartyState(
            partyId: "party-1",
            isActive: true,
            isAtSea: isAtSea,
            isHolding: isHolding,
            currentSettlementId: currentSettlementId,
            lastVisitedSettlementId: lastVisitedSettlementId,
            positionX: positionX,
            positionY: 3f,
            positionIsOnLand: true,
            bearingX: 0f,
            bearingY: bearingY,
            hasMapEvent: hasMapEvent,
            hasBesiegerCamp: hasBesiegerCamp,
            isTransitionInProgress: isTransitionInProgress,
            hasArmy: hasArmy,
            hasAttachedTo: hasAttachedTo,
            hasAttachedParties: hasAttachedParties);
}
