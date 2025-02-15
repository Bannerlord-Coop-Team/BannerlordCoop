using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace GameInterface.AutoSync;

public interface IAutoSyncPatchCollector : IDisposable
{
    void AddPrefix(MethodBase patchMethod, MethodInfo patch);
    void AddTranspiler(MethodBase patchMethod, MethodInfo patch);
    void PatchAll();
}

class AutoSyncPatchCollector : IAutoSyncPatchCollector
{
    const string HarmonyID = "CoopAutoSyncPatchCollector";
    private readonly Harmony harmony = new Harmony(HarmonyID);

    private readonly List<(MethodBase, MethodInfo)> transpilers = new List<(MethodBase, MethodInfo)>();
    private readonly List<(MethodBase, MethodInfo)> prefixes = new List<(MethodBase, MethodInfo)>();

    public AutoSyncPatchCollector(Harmony harmony)
    {
        this.harmony = harmony;
    }

    public void AddPrefix(MethodBase patchMethod, MethodInfo patch)
    {
        if (patchMethod == null) throw new ArgumentNullException(nameof(patchMethod));
        if (patch == null) throw new ArgumentNullException(nameof(patch));

        prefixes.Add((patchMethod, patch));
    }
    public void AddTranspiler(MethodBase patchMethod, MethodInfo patch)
    {
        if (patchMethod == null) throw new ArgumentNullException(nameof(patchMethod));
        if (patch == null) throw new ArgumentNullException(nameof(patch));

        transpilers.Add((patchMethod, patch));
    }

    public void PatchAll()
    {
        if (Harmony.HasAnyPatches(HarmonyID)) return;

        foreach (var (method, patch) in transpilers)
        {
            harmony.Patch(method, transpiler: new HarmonyMethod(patch));
        }

        foreach (var (method, patch) in prefixes)
        {
            harmony.Patch(method, prefix: new HarmonyMethod(patch));
        }
    }

    public void Dispose()
    {
        transpilers.Clear();
        prefixes.Clear();
    }
}
