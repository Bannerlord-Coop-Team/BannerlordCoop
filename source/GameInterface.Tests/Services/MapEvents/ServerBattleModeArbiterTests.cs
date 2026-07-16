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
}
