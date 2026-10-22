#if DEBUG
using HarmonyLib;
using NavalDLC.Missions.MissionLogics;
using NavalDLC.Missions.Objects;
using NavalDLC.Missions.Objects.UsableMachines;
using TaleWorlds.MountAndBlade;
using Xunit;

namespace Coop.Tests.Missions.Battles;

/// <summary>In a co-op battle vanilla's elapsed capture timer goes to the capture replicator instead of swapping the hulls.</summary>
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
    }
}
#endif
