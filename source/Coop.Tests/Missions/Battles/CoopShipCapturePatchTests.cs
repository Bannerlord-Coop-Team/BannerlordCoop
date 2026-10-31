#if COOP_NAVAL
using HarmonyLib;
using Missions.Naval;
using NavalDLC.Missions.MissionLogics;
using NavalDLC.Missions.Objects;
using NavalDLC.Missions.Objects.UsableMachines;
using TaleWorlds.MountAndBlade;
using Xunit;

namespace Coop.Tests.Missions.Battles;

/// <summary>In a co-op battle vanilla's elapsed capture timer goes to the capture replicator instead of swapping the hulls.</summary>
[Collection("Mission.Current")]
public class CoopShipCapturePatchTests
{
    // The prefix hooks the helm's capture completion, the one call into NavalShipsLogic.OnShipCaptured, which the engine replays.
    [Fact]
    public void Target_CaptureCompletionAndSwapExist()
    {
        Assert.NotNull(AccessTools.DeclaredMethod(typeof(ShipControllerMachine), nameof(ShipControllerMachine.OnShipCapturedByAgent),
            new[] { typeof(Agent) }));
        Assert.NotNull(AccessTools.DeclaredMethod(typeof(NavalShipsLogic), nameof(NavalShipsLogic.OnShipCaptured),
            new[] { typeof(MissionShip), typeof(Formation) }));
        Assert.NotNull(AccessTools.DeclaredMethod(typeof(ShipControllerMachine), nameof(ShipControllerMachine.IsAttachedShipVacant)));
        Assert.NotNull(AccessTools.DeclaredMethod(typeof(MissionShip), nameof(MissionShip.AreShipsConnected),
            new[] { typeof(MissionShip), typeof(MissionShip) }));
    }

    // C1: a captured player's formation has no hull left, so vanilla's helm checks ask about a null hull every tick; in a co-op
    // battle that reads "not connected" instead of throwing, and outside one vanilla runs.
    [Fact]
    public void AreShipsConnected_WithAMissingHull_ReadsNotConnected_OnlyInACoopBattle()
    {
        bool wasActive = CoopNavalMissionScope.IsActive;
        try
        {
            CoopNavalMissionScope.IsActive = true;
            bool connected = true;
            bool runsVanilla = CapturedFormationConnectionPatch.Prefix(null!, null!, ref connected);
            CoopNavalMissionScope.IsActive = false;
            bool outsideRunsVanilla = CapturedFormationConnectionPatch.Prefix(null!, null!, ref connected);

            Assert.False(runsVanilla);
            Assert.False(connected);
            Assert.True(outsideRunsVanilla);
        }
        finally
        {
            CoopNavalMissionScope.IsActive = wasActive;
        }
    }
}
#endif
