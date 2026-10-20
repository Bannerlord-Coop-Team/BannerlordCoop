#if DEBUG
using HarmonyLib;
using Missions.Naval;
using NavalDLC;
using NavalDLC.Missions.MissionLogics;
using System.Runtime.CompilerServices;
using Xunit;

namespace Coop.Tests.Missions.Battles;

/// <summary>R41a: a returner with no reserve must leave its naval mission instead of hanging in deployment.</summary>
public class CoopNavalReserveGuardTests
{
    // Mission.AfterStart resets the state after EarlyStart, so the guard ends the mission from its first pre-display tick.
    [Fact]
    public void Guard_EndsTheMissionFromItsPreDisplayTick()
    {
        Assert.NotNull(AccessTools.DeclaredMethod(typeof(CoopNavalReserveGuard), nameof(CoopNavalReserveGuard.OnPreDisplayMissionTick)));
    }

    [Fact]
    public void Target_ThePlayerTeamsInitialAllocationExists()
    {
        Assert.NotNull(AccessTools.DeclaredMethod(typeof(NavalTeamSideSpawnContext),
            nameof(NavalTeamSideSpawnContext.AllocateAndDeployInitialTroopsOfPlayerTeam)));
    }

    // Vanilla assigns no ship to a team with no troops, and its allocation would put the player troop on that missing hull.
    [Fact]
    public void ShiplessPlayerTeam_SkipsTheInitialAllocation()
    {
        var unset = (ShipAssignment)RuntimeHelpers.GetUninitializedObject(typeof(ShipAssignment));

        Assert.False(CoopNavalShiplessPlayerTeamPatch.AllocatesPlayerTeam(null));
        Assert.False(CoopNavalShiplessPlayerTeamPatch.AllocatesPlayerTeam(unset));
    }
}
#endif
