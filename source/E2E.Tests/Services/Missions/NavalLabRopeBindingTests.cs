#if DEBUG
using System.Reflection;
using HarmonyLib;

namespace E2E.Tests.Services.Missions;

public sealed class NavalLabRopeBindingTests
{
    [Fact]
    public void InstalledRopeAndPlankPatchClassesBindWithoutExecutingNativeMethods()
    {
        var patches = typeof(global::Missions.Naval.NavalMissionAdapter).Assembly
            .GetType("Missions.Naval.NavalLabRopePatches", throwOnError: true)!;
        var classes = patches.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic)
            .Where(type => type.IsDefined(typeof(HarmonyPatch), false)).ToArray();
        Assert.Equal(14, classes.Length);
        var harmony = new Harmony("coop.e2e.naval.rope-plank-binding");
        try
        {
            foreach (var patch in classes)
            {
                var bound = harmony.CreateClassProcessor(patch).Patch();
                Assert.NotNull(bound);
                Assert.Equal(patch.Name == "OwnerEndpointWrites" ? 5 : patch.Name == "PlankCosmetics" ? 3 : 1, bound.Count);
            }
            var methods = harmony.GetPatchedMethods().ToArray();
            Assert.Equal(19, methods.Length);
            Assert.All(methods, method => Assert.Contains(harmony.Id, Harmony.GetPatchInfo(method).Owners));
        }
        finally { harmony.UnpatchAll(harmony.Id); }
        Assert.Empty(harmony.GetPatchedMethods());
    }
}
#endif
