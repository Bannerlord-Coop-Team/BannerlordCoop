using GameInterface.AutoSync;
using GameInterface.Utils;
using HarmonyLib;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace GameInterface;

public interface IGameInterface : IDisposable
{
    void PatchAll();
    void PatchGameStarted();
    void UnpatchAll();
}

public class GameInterface : IGameInterface
{
    public const string HARMONY_STATIC_FIXES_CATEGORY = "HarmonyStaticFixes";
    public const string HARMONY_UI_LOADING_CATEGORY = "UILoadingPatches";

    // Applied at boot by CoopMod so it is active before native first-time minor-faction initialization
    public const string HARMONY_CONFIGURED_MINOR_FACTION_CATEGORY = "ConfiguredMinorFactionPatches";

    public const string HARMONY_GAME_STARTED_CATEGORY = "GameStartedPatches";

    private const string PatchingFailedEarlierMessage =
        "Patching failed earlier in this session. Restart Bannerlord before joining or hosting again.";

    private static bool gameStartedPatchesApplied;

    // Patches outlive the container, so a later attempt would otherwise take the reconnect path over a partial install.
    private static readonly ConcurrentDictionary<string, Exception> failedPatchAttempts =
        new ConcurrentDictionary<string, Exception>();

    private readonly Harmony harmony;
    private readonly IAutoSyncPatchCollector patchCollector;
    private readonly AutoSyncPatcher AutoSyncPatcher;
    private readonly IEnumerable<HarmonyPatchCategoryRegistration> patchCategories;

    public GameInterface(
        Harmony harmony,
        IAutoSyncPatchCollector patchCollector,
        AutoSyncPatcher AutoSyncPatcher,
        IEnumerable<HarmonyPatchCategoryRegistration> patchCategories)
    {
        this.harmony = harmony;
        this.patchCollector = patchCollector;
        this.AutoSyncPatcher = AutoSyncPatcher;
        this.patchCategories = patchCategories;
    }

    public void Dispose()
    {
    }

    public void PatchAll()
    {
        if (failedPatchAttempts.TryGetValue(harmony.Id, out Exception earlierFailure))
        {
            throw new InvalidOperationException(PatchingFailedEarlierMessage, earlierFailure);
        }

        if (Harmony.HasAnyPatches(harmony.Id))
        {
            // Reconnect skips the install below, so handlers torn down on disconnect must be rebound here.
            AutoSyncPatcher.RebindHandlers();
            return;
        }

        try
        {
            PatchAllCore();
        }
        catch (Exception e)
        {
            // UnpatchAll is disabled, so whatever was applied before the failure stays for the whole process.
            if (Harmony.HasAnyPatches(harmony.Id))
            {
                failedPatchAttempts.TryAdd(harmony.Id, e);
            }

            throw;
        }
    }

    private void PatchAllCore()
    {
        var assembly = typeof(GameInterface).Assembly;

        // Must run before any other detour below, or a fragile no-op method's detour can corrupt its inline
        // x64 unwind info and deadlock the GC.
        FragileDetourGuard.Apply(harmony);

        harmony.PatchCategory(assembly, HARMONY_STATIC_FIXES_CATEGORY);
        harmony.PatchAllUncategorized(assembly);

        foreach (HarmonyPatchCategoryRegistration patchCategory in patchCategories)
        {
            patchCategory.Apply(harmony);
        }

        AutoSyncPatcher.PatchAll();
    }

    public void PatchGameStarted()
    {
        if (gameStartedPatchesApplied) return;

        harmony.PatchCategory(typeof(GameInterface).Assembly, HARMONY_GAME_STARTED_CATEGORY);
        if (AutoSyncPatcher.Assembly != null)
            harmony.PatchCategory(AutoSyncPatcher.Assembly, HARMONY_GAME_STARTED_CATEGORY);
        gameStartedPatchesApplied = true;
    }

    public void UnpatchAll()
    {
        // Disabled: container disposal relies on patches staying live through teardown.
        return;

        patchCollector.UnpatchAll();
        harmony.UnpatchAll();
    }
}
