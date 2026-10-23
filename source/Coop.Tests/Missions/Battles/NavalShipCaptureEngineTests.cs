#if DEBUG
using Missions.Naval;
using Xunit;

namespace Coop.Tests.Missions.Battles;

/// <summary>The DLC side of a capture: what a throw inside vanilla's swap is logged as.</summary>
public class NavalShipCaptureEngineTests
{
    // R46 review: a throw inside vanilla's swap is logged with the step it came from, read from its stack.
    [Theory]
    [InlineData("at NavalDLC.Missions.MissionLogics.NavalShipsLogic.RefreshTeamAIBehaviorShipReferences(Team team)", "team AI refresh")]
    [InlineData("at NavalDLC.Missions.MissionLogics.NavalAgentsLogic.OnShipCaptured(MissionShip ship)", "agent rebinding (ShipCapturedEvent)")]
    [InlineData("at NavalDLC.Missions.AI.TeamAI.NavalQuerySystem.ForceExpireAll()", "query expiry")]
    [InlineData("at NavalDLC.Missions.ShipOrder.OnShipCaptured(MissionShip ship1, MissionShip ship2)", "ship orders")]
    [InlineData("at NavalDLC.Missions.Objects.MissionShip.SetFormation(Formation formation)", "assignment and formation swap")]
    public void SwapStage_NamesTheStepOfVanillasSwapAThrowCameFrom(string frame, string expected)
    {
        Assert.Equal(expected, NavalShipCaptureEngine.SwapStage(frame + "\n   at NavalDLC.Missions.MissionLogics.NavalShipsLogic.OnShipCaptured(MissionShip ship, Formation targetFormation)"));
    }

    // R48 L6: swimmers left in the prize's former formation, which lost its hull, keep its naval behaviours ticking on a null hull.
    [Theory]
    [InlineData(9, false, true)]
    [InlineData(0, false, false)]
    [InlineData(9, true, false)]
    [InlineData(0, true, false)]
    public void KeepsUnitsWithoutShip_OnlyForUnitsLeftWithNoHull(int units, bool hasShip, bool expected)
    {
        Assert.Equal(expected, NavalShipCaptureEngine.KeepsUnitsWithoutShip(units, hasShip));
    }
}
#endif
