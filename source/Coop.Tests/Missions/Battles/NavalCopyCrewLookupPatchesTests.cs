#if COOP_NAVAL
using HarmonyLib;
using Missions.Naval;
using Xunit;

namespace Coop.Tests.Missions.Battles;

public class NavalCopyCrewLookupPatchesTests
{
    // The postfixes bind their arguments and the publicized ships logic by name, so only patching the real methods settles them.
    [Fact]
    public void CopySkippingPatches_BindToTheTeamAiShipLookups()
    {
        var harmony = new Harmony("coop.tests.naval.copy_crew_lookup");
        try
        {
            Assert.NotEmpty(harmony.CreateClassProcessor(typeof(CopySkippingNearestShipPatch)).Patch());
            Assert.NotEmpty(harmony.CreateClassProcessor(typeof(CopySkippingConnectedShipPatch)).Patch());
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
        }
    }
}
#endif
