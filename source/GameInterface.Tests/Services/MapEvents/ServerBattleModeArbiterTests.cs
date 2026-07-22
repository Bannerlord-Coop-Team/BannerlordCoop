using GameInterface.Services.MapEvents;
using Xunit;

namespace GameInterface.Tests.Services.MapEvents;

/// <summary>Tests conditional release of server-authoritative battle mode claims.</summary>
public class ServerBattleModeArbiterTests
{
    [Fact]
    public void TryClaimMission_ExistingMission_ReportsOnlyFirstClaimAsNew()
    {
        const string mapEventId = "existing-mission-claim";

        try
        {
            Assert.True(ServerBattleModeArbiter.TryClaimMission(mapEventId, out var firstClaimIsNew));
            Assert.True(firstClaimIsNew);

            Assert.True(ServerBattleModeArbiter.TryClaimMission(mapEventId, out var secondClaimIsNew));
            Assert.False(secondClaimIsNew);
        }
        finally
        {
            ServerBattleModeArbiter.Release(mapEventId);
        }
    }

    [Fact]
    public void ReleaseMission_MissionClaim_AllowsSimulationClaim()
    {
        const string mapEventId = "release-mission-claim";

        try
        {
            Assert.True(ServerBattleModeArbiter.TryClaimMission(mapEventId));

            Assert.True(ServerBattleModeArbiter.ReleaseMission(mapEventId));
            Assert.True(ServerBattleModeArbiter.TryClaimSimulation(mapEventId));
        }
        finally
        {
            ServerBattleModeArbiter.Release(mapEventId);
        }
    }

    [Fact]
    public void ReleaseMission_SimulationClaim_DoesNotReleaseClaim()
    {
        const string mapEventId = "preserve-simulation-claim";

        try
        {
            Assert.True(ServerBattleModeArbiter.TryClaimSimulation(mapEventId));

            Assert.False(ServerBattleModeArbiter.ReleaseMission(mapEventId));
            Assert.False(ServerBattleModeArbiter.TryClaimMission(mapEventId));
        }
        finally
        {
            ServerBattleModeArbiter.Release(mapEventId);
        }
    }

    // The "mission-held blocks simulation" direction: the two resolution modes are mutually exclusive after
    // one has begun (BR-001), and player simulation is unavailable once a battle mission owns the event
    // (BR-003). The existing tests only cover the reverse (simulation-held) direction directly.
    [Fact]
    [Trait("Requirement", "BR-001")]
    [Trait("Requirement", "BR-003")]
    public void MissionClaim_RefusesSimulationClaim_WhileHeld()
    {
        const string mapEventId = "mission-blocks-simulation";

        try
        {
            Assert.True(ServerBattleModeArbiter.TryClaimMission(mapEventId));

            // Player simulation cannot claim the event while the mission owns it...
            Assert.False(ServerBattleModeArbiter.TryClaimSimulation(mapEventId));
            // ...while a re-entrant mission claim (another player opening the same mission) still succeeds.
            Assert.True(ServerBattleModeArbiter.TryClaimMission(mapEventId));
        }
        finally
        {
            ServerBattleModeArbiter.Release(mapEventId);
        }
    }

    // Surrender (and similar side-effectful actions) consult the claim read-only: IsClaimed reports either
    // mode without disturbing it, and reports false before a claim exists and after it releases.
    [Fact]
    public void IsClaimed_ReflectsClaimLifecycle_WithoutDisturbingIt()
    {
        const string mapEventId = "is-claimed-lifecycle";

        try
        {
            Assert.False(ServerBattleModeArbiter.IsClaimed(null));
            Assert.False(ServerBattleModeArbiter.IsClaimed(mapEventId));

            Assert.True(ServerBattleModeArbiter.TryClaimMission(mapEventId));
            Assert.True(ServerBattleModeArbiter.IsClaimed(mapEventId));

            // Reading did not disturb the claim: the opposing mode is still refused.
            Assert.False(ServerBattleModeArbiter.TryClaimSimulation(mapEventId));

            Assert.True(ServerBattleModeArbiter.ReleaseMission(mapEventId));
            Assert.False(ServerBattleModeArbiter.IsClaimed(mapEventId));

            // A simulation claim reports the same way.
            Assert.True(ServerBattleModeArbiter.TryClaimSimulation(mapEventId));
            Assert.True(ServerBattleModeArbiter.IsClaimed(mapEventId));
        }
        finally
        {
            ServerBattleModeArbiter.Release(mapEventId);
        }
    }

    // The mutual exclusion is scoped per map event (BR-001 addresses "a map event"): two distinct events are
    // independent battles and can hold opposite modes at once, each still excluding its own opposite.
    [Fact]
    [Trait("Requirement", "BR-001")]
    public void DistinctMapEvents_ClaimModesIndependently()
    {
        const string missionEvent = "independent-mission-event";
        const string simulationEvent = "independent-simulation-event";

        try
        {
            Assert.True(ServerBattleModeArbiter.TryClaimMission(missionEvent));
            Assert.True(ServerBattleModeArbiter.TryClaimSimulation(simulationEvent));

            // Each event still excludes its opposite mode on its own.
            Assert.False(ServerBattleModeArbiter.TryClaimSimulation(missionEvent));
            Assert.False(ServerBattleModeArbiter.TryClaimMission(simulationEvent));
        }
        finally
        {
            ServerBattleModeArbiter.Release(missionEvent);
            ServerBattleModeArbiter.Release(simulationEvent);
        }
    }
}
