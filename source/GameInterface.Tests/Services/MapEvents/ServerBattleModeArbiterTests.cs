using GameInterface.Services.MapEvents;
using Xunit;

namespace GameInterface.Tests.Services.MapEvents;

/// <summary>Tests conditional release of server-authoritative battle mode claims.</summary>
public class ServerBattleModeArbiterTests
{
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

    [Fact]
    public void ClaimMission_FirstThenSameMode_ReportsNewThenAlreadyClaimed()
    {
        // A mid-battle joiner's start request against an already-claimed mission event (requirement R1): the
        // dispatcher must be able to tell a fresh claim apart from a same-mode re-claim so the mission starter can
        // still run its full body (rebroadcast) for the joiner.
        const string mapEventId = "same-mode-reclaim";

        try
        {
            Assert.Equal(BattleClaimResult.NewClaim, ServerBattleModeArbiter.ClaimMission(mapEventId));
            Assert.Equal(BattleClaimResult.AlreadyClaimedSameMode, ServerBattleModeArbiter.ClaimMission(mapEventId));
        }
        finally
        {
            ServerBattleModeArbiter.Release(mapEventId);
        }
    }

    [Fact]
    public void ClaimSimulation_AfterMissionClaim_IsRefused()
    {
        const string mapEventId = "cross-mode-refused";

        try
        {
            Assert.Equal(BattleClaimResult.NewClaim, ServerBattleModeArbiter.ClaimMission(mapEventId));
            Assert.Equal(BattleClaimResult.Refused, ServerBattleModeArbiter.ClaimSimulation(mapEventId));
        }
        finally
        {
            ServerBattleModeArbiter.Release(mapEventId);
        }
    }
}
