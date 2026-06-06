using GameInterface;
using GameInterface.AutoSync;
using HarmonyLib;
using System.Runtime.CompilerServices;
using Xunit;

namespace E2E.Tests.AutoSync;

/// <summary>
/// Tearing a Coop session down (<c>CoopartiveMultiplayerExperience.DestroyContainer</c> ->
/// <see cref="IGameInterface.UnpatchAll"/>) must only remove <b>Coop's own</b> Harmony patches.
///
/// <para>
/// <see cref="GameInterface.GameInterface.UnpatchAll"/> used to call the parameterless
/// <see cref="Harmony.UnpatchAll()"/>, which is HarmonyLib's <i>global</i> unpatch: it strips every
/// patch in the process regardless of which Harmony id owns it — the base game's patches, every
/// other mod's patches, and the test bootstrap's patches included. In the live game that nukes
/// patches the engine and other mods depend on the moment a player leaves a coop session, which
/// crashes the game. This test pins the teardown to Coop's own id.
/// </para>
///
/// <para>
/// The test drives the real <see cref="GameInterface.GameInterface.UnpatchAll"/> but builds it
/// around dedicated Harmony ids and its own patch targets, so it neither depends on nor pollutes
/// the process-wide static Harmony that the other E2E tests share.
/// </para>
/// </summary>
public class HarmonyUnpatchAllLifetimeTests
{
    // Stands in for a method Coop itself patches. Static, never-inlined and uniquely named so it
    // collides with neither the real game patches nor sibling tests.
    private static class CoopPatchTarget
    {
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public static int Method()
        {
            var sum = 0;
            for (int i = 0; i < 4; i++) sum += i;
            return sum;
        }

        public static void Prefix() { }
    }

    // Stands in for a method the base game / another mod has patched under its own Harmony id.
    private static class ForeignPatchTarget
    {
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public static int Method()
        {
            var sum = 0;
            for (int i = 0; i < 4; i++) sum += i;
            return sum;
        }

        public static void Prefix() { }
    }

    [Fact]
    public void GameInterfaceUnpatchAll_LeavesForeignHarmonyPatchesIntact()
    {
        var coopTarget = AccessTools.Method(typeof(CoopPatchTarget), nameof(CoopPatchTarget.Method));
        var coopPatch = AccessTools.Method(typeof(CoopPatchTarget), nameof(CoopPatchTarget.Prefix));
        var foreignTarget = AccessTools.Method(typeof(ForeignPatchTarget), nameof(ForeignPatchTarget.Method));
        var foreignPatch = AccessTools.Method(typeof(ForeignPatchTarget), nameof(ForeignPatchTarget.Prefix));

        // Dedicated Harmony ids so this test is isolated from the shared static
        // GameInterfaceModule harmony the other E2E tests run against.
        var coopHarmony = new Harmony("Bannerlord.Coop.UnpatchAllTest");
        var foreignHarmony = new Harmony("ThirdParty.Mod.UnpatchAllTest");
        try
        {
            // Real GameInterface wired to our isolated harmony. The DynamicSyncPatcher is never
            // touched by UnpatchAll, so it is omitted here.
            var collector = new AutoSyncPatchCollector(coopHarmony);
            IGameInterface gameInterface = new GameInterface.GameInterface(coopHarmony, collector, dynamicSyncPatcher: null);

            // Coop applies a patch under its own id, exactly like joining a session.
            coopHarmony.Patch(coopTarget, prefix: new HarmonyMethod(coopPatch));
            Assert.True(Harmony.HasAnyPatches(coopHarmony.Id));

            // A foreign mod patches its own method under its own id.
            foreignHarmony.Patch(foreignTarget, prefix: new HarmonyMethod(foreignPatch));
            Assert.Contains(foreignTarget, foreignHarmony.GetPatchedMethods());

            // Leave the session: production DestroyContainer -> IGameInterface.UnpatchAll().
            gameInterface.UnpatchAll();

            // Coop's own patches are gone...
            Assert.False(Harmony.HasAnyPatches(coopHarmony.Id));

            // ...but the foreign patch must remain. With the old global Harmony.UnpatchAll() it was
            // wiped here, which is what crashed the live game on leaving coop.
            Assert.Contains(foreignTarget, foreignHarmony.GetPatchedMethods());
        }
        finally
        {
            // Clean up so this test cannot affect any other test in the process.
            coopHarmony.UnpatchAll(coopHarmony.Id);
            foreignHarmony.UnpatchAll(foreignHarmony.Id);
        }
    }
}
