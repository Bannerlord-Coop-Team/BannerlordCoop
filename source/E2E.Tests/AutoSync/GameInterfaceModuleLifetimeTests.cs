using Autofac;
using Common;
using E2E.Tests.Environment;
using GameInterface;
using GameInterface.DynamicSync;
using GameInterface.Tests.Bootstrap;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.AutoSync;

/// <summary>
/// Exercises building a real <see cref="GameInterface.GameInterfaceModule"/> container, tearing it
/// down the way production does, and building a second one — the reconnect / "leave and rejoin"
/// lifecycle.
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
    private readonly ITestOutputHelper output;

    public GameInterfaceModuleLifetimeTests(ITestOutputHelper output)
    {
        this.output = output;
    }

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
        GameLoopRunner.Instance.SetGameLoopThread();
        GameBootStrap.Initialize();

        var target = AccessTools.Method(typeof(PatchTarget), nameof(PatchTarget.Method));
        var patch = AccessTools.Method(typeof(PatchTarget), nameof(PatchTarget.Prefix));

        Harmony harmony = null;
        try
        {
            // ---- First container lifecycle: build module, collect + patch. ----
            var firstEnv = new TestEnvironment(output, numClients: 0, registerGameInterface: true);
            harmony = firstEnv.Server.Container.Resolve<Harmony>();

            var firstCollector = firstEnv.Server.Container.Resolve<IDynamicSyncPatchCollector>();
            firstCollector.AddPrefix(target, patch);
            firstCollector.PatchAll();
            Assert.Contains(target, harmony.GetPatchedMethods());

            // ---- Teardown: mirrors CoopartiveMultiplayerExperience.DestroyContainer, which
            //      unpatches through IGameInterface (clearing the collector's static
            //      patchedMethods bookkeeping) and disposes the container. ----
            firstEnv.Server.Container.Resolve<IGameInterface>().UnpatchAll();
            firstEnv.Server.Dispose();
            Assert.DoesNotContain(target, harmony.GetPatchedMethods());

            // Production's Harmony.UnpatchAll() is global, so it also strips GameBootStrap's
            // test-only path patches. Re-establish them before rebuilding (no-op in production,
            // where those patches don't exist).
            GameBootStrap.Initialize();

            // ---- Second container lifecycle: a brand-new module + collector re-collect and
            //      re-patch, exactly like rebuilding the DI container on reconnect. ----
            var secondEnv = new TestEnvironment(output, numClients: 0, registerGameInterface: true);
            try
            {
                // Same static Harmony instance is shared across containers.
                var harmony2 = secondEnv.Server.Container.Resolve<Harmony>();
                Assert.Same(harmony, harmony2);

                var secondCollector = secondEnv.Server.Container.Resolve<IDynamicSyncPatchCollector>();
                secondCollector.AddPrefix(target, patch);
                secondCollector.PatchAll();

                // The patch must be live again. Today it is not: the static patchedMethods set
                // still believes the method is patched, so PatchAll skips it.
                Assert.Contains(target, harmony2.GetPatchedMethods());
            }
            finally
            {
                secondEnv.Server.Dispose();
            }
        }
        finally
        {
            // Clean up so this test cannot affect any other test in the process.
            harmony?.UnpatchAll(harmony.Id);
            DynamicSyncPatchCollector.PatchedMethods.Remove((target, patch, HarmonyPatchType.Prefix));
        }
    }
}
