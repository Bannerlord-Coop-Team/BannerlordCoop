#if COOP_NAVAL && DEBUG
using HarmonyLib;
using Missions.Naval;
using Xunit;

namespace Coop.Tests.Missions.Battles;

public class NavalScenarioShipLimitPatchTests
{
    [Fact]
    public void ShipLimitPatch_BindsToTheInstalledDeploymentModel()
    {
        var harmony = new Harmony("coop.tests.naval.scenario_ship_limit");
        try
        {
            Assert.NotEmpty(harmony.CreateClassProcessor(typeof(NavalScenarioShipLimitPatch)).Patch());
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
        }
    }
}
#endif
