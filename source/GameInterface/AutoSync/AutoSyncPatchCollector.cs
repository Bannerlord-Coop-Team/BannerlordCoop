using HarmonyLib;
using SandBox.BoardGames.Pawns;
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
    private readonly Harmony harmony;

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
        foreach (var (method, patch) in transpilers)
        {
            harmony.Patch(method, transpiler: new HarmonyMethod(patch));
        }

        foreach (var (method, patch) in prefixes)
        {
            harmony.Patch(method, prefix: new HarmonyMethod(patch));
        }
    }

    private void UnpatchAll()
    {
        foreach (var (method, patch) in transpilers)
        {
            harmony.Unpatch(method, HarmonyPatchType.Transpiler, harmony.Id);
        }

        foreach (var (method, patch) in prefixes)
        {
            harmony.Unpatch(method, HarmonyPatchType.Prefix, harmony.Id);
        }
    }

    public void Dispose()
    {
        UnpatchAll();

        transpilers.Clear();
        prefixes.Clear();
    }
}
