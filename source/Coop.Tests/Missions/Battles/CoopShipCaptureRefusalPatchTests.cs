#if DEBUG
using HarmonyLib;
using NavalDLC.Missions.MissionLogics;
using NavalDLC.Missions.Objects;
using NavalDLC.Missions.Objects.UsableMachines;
using TaleWorlds.MountAndBlade;
using Xunit;

namespace Coop.Tests.Missions.Battles;

/// <summary>In-mission ship capture is refused in a co-op battle until it replicates.</summary>
public class CoopShipCaptureRefusalPatchTests
{
    // The refusal prefix hooks the helm's capture completion, the one call into NavalShipsLogic.OnShipCaptured.
    [Fact]
    public void Target_CaptureCompletionExists()
    {
        Assert.NotNull(AccessTools.DeclaredMethod(typeof(ShipControllerMachine), nameof(ShipControllerMachine.OnShipCapturedByAgent),
            new[] { typeof(Agent) }));
        Assert.NotNull(AccessTools.DeclaredMethod(typeof(NavalShipsLogic), nameof(NavalShipsLogic.OnShipCaptured),
            new[] { typeof(MissionShip), typeof(Formation) }));
    }
}
#endif
