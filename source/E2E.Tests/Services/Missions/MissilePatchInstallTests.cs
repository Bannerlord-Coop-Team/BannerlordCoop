using System.Linq;
using HarmonyLib;
using Missions.Missiles;
using Missions.Missiles.Patches;
using Xunit;

namespace E2E.Tests.Services.Missions;

/// <summary>
/// Verifies that the explicit Harmony category installs all agent projectile-sync patches from the Missions
/// assembly, which the client's default GameInterface-only patch pass does not discover.
/// </summary>
public class MissilePatchInstallTests
{
    [Fact]
    public void MissilePatches_AreInstalledByCategory_AndHookTheirTargets()
    {
        const string id = "e2e.missilepatch.patchtest";
        var harmony = new Harmony(id);
        try
        {
            // The exact call MissilePatchInstaller makes on CampaignReady.
            harmony.PatchCategory(typeof(AddMissileAuxPatch).Assembly, MissilePatchInstaller.MissilePatchCategory);

            // Scope to this harmony id: an empty/missing entry means the class carried no matching category (or a
            // bad [HarmonyPatch] target) and would never install at runtime.
            var patched = Harmony.GetAllPatchedMethods()
                .Where(m => Harmony.GetPatchInfo(m)?.Owners.Contains(id) == true)
                .Select(m => m.Name)
                .ToList();

            Assert.Contains("AddMissileAux", patched);
            Assert.Contains("AddMissileSingleUsageAux", patched);
            Assert.Contains("OnAgentShootMissile", patched);
        }
        finally
        {
            harmony.UnpatchAll(id);
        }
    }
}
