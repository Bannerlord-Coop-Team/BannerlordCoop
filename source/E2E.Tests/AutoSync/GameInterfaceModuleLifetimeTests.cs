using GameInterface;
using GameInterface.DynamicSync;
using GameInterface.Tests.Bootstrap;
using HarmonyLib;
using System.Runtime.CompilerServices;
using Xunit;

namespace E2E.Tests.AutoSync;

/// <summary>
/// Exercises the teardown -> rebuild ("leave and rejoin") lifecycle of a Coop session's Harmony
/// state, focusing on the AutoSync collector's <b>static</b> bookkeeping.
///
/// <para>
/// Production teardown (<c>CoopartiveMultiplayerExperience.DestroyContainer</c>) only calls
/// <see cref="Harmony.UnpatchAll()"/> and disposes the container. That removes the live Harmony
/// patches but never calls <see cref="IDynamicSyncPatchCollector.UnpatchAll"/>, and the collector's
/// <c>patchedMethods</c> bookkeeping is <c>static</c> — it survives the container.
/// </para>
///
/// <para>
/// On the second container, the new <c>InstancePerLifetimeScope</c> collector re-collects the same
/// patches. Because <see cref="AutoRegistryFactory"/> uses <b>stable</b> patch methods
/// (<c>LifetimePatches&lt;T&gt;.CreatePrefix</c>/<c>DestroyPostfix</c>), the
/// <c>(method, patch, type)</c> key is identical to the first run, so it is still present in the
/// static set. <see cref="IDynamicSyncPatchCollector.PatchAll"/> then logs "already patched" and skips
/// it, silently leaving the patch off for the whole second session.
/// </para>
/// </summary>
public class GameInterfaceModuleLifetimeTests
{
    // Stable target + patch. Static, never-inlined, uniquely named so it touches neither the real
    // game patches nor sibling tests. This mirrors how AutoRegistryFactory queues its lifetime
    // patches: a fixed game ctor patched with a fixed LifetimePatches<T> method, where the
    // resulting key is identical across container rebuilds.
    private static class PatchTarget
    {
        // NoInlining/NoOptimization so Harmony can regenerate the wrapper when the targeted
        // Unpatch runs; an optimized/inlined stub throws InvalidProgramException on rewrap.
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
    public void GameInterfaceModule_RebuiltAfterTeardown_ReappliesCollectorPatches()
    {
        var target = AccessTools.Method(typeof(PatchTarget), nameof(PatchTarget.Method));
        var patch = AccessTools.Method(typeof(PatchTarget), nameof(PatchTarget.Prefix));

        // Dedicated id so this test is isolated from the shared static GameInterfaceModule harmony
        // the other E2E tests run against. The same instance stands in for the static harmony that
        // production keeps alive across container rebuilds.
        var harmony = new Harmony("Bannerlord.Coop.LifetimeTest");
        try
        {
            // ---- First session: build GameInterface, collect + patch. ----
            var firstCollector = new AutoSyncPatchCollector(harmony);
            IGameInterface firstGameInterface = new GameInterface.GameInterface(harmony, firstCollector, dynamicSyncPatcher: null);

            var firstCollector = firstEnv.Server.Container.Resolve<IDynamicSyncPatchCollector>();
            firstCollector.AddPrefix(target, patch);
            firstCollector.PatchAll();
            Assert.Contains(target, harmony.GetPatchedMethods());

            // ---- Teardown: mirrors CoopartiveMultiplayerExperience.DestroyContainer, which
            //      unpatches through IGameInterface (clearing the collector's static
            //      patchedMethods bookkeeping) and disposes the container. ----
            firstGameInterface.UnpatchAll();
            Assert.DoesNotContain(target, harmony.GetPatchedMethods());

            // ---- Second session: a brand-new collector re-collects and re-patches, exactly like
            //      rebuilding the DI container on reconnect. ----
            var secondCollector = new AutoSyncPatchCollector(harmony);
            IGameInterface secondGameInterface = new GameInterface.GameInterface(harmony, secondCollector, dynamicSyncPatcher: null);

                var secondCollector = secondEnv.Server.Container.Resolve<IDynamicSyncPatchCollector>();
                secondCollector.AddPrefix(target, patch);
                secondCollector.PatchAll();

            // The patch must be live again. Before the fix the static patchedMethods set still
            // believed the method was patched, so PatchAll skipped it.
            Assert.Contains(target, harmony.GetPatchedMethods());
        }
        finally
        {
            // Clean up so this test cannot affect any other test in the process.
            harmony?.UnpatchAll(harmony.Id);
            DynamicSyncPatchCollector.PatchedMethods.Remove((target, patch, HarmonyPatchType.Prefix));
        }
    }
}
