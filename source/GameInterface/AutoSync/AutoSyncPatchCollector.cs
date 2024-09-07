using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace GameInterface.AutoSync;

public interface IAutoSyncPatchCollector : IDisposable
{
    void AddTranspiler(MethodInfo patchMethod, MethodInfo patch);
    void PatchAll();
    void UnpatchAll();
}

class AutoSyncPatchCollector : IAutoSyncPatchCollector
{
    private readonly Harmony harmony;

    private readonly List<(MethodInfo, MethodInfo)> transpilers = new List<(MethodInfo, MethodInfo)>();

    private static bool IsPatched = false;

    public AutoSyncPatchCollector(Harmony harmony)
    {
        this.harmony = harmony;
    }

    public void AddTranspiler(MethodInfo patchMethod, MethodInfo patch)
    {
        transpilers.Add((patchMethod, patch));
    }

    public void PatchAll()
    {
        if (IsPatched) UnpatchAll();

        IsPatched = true;

        foreach (var (method, patch) in transpilers) {
            harmony.Patch(method, transpiler: new HarmonyMethod(patch));
        }
    }

    public void UnpatchAll()
    {
        IsPatched = false;

        foreach (var (method, patch) in transpilers)
        {
            harmony.Unpatch(method, patch);
        }
    }

    public void Dispose()
    {
        UnpatchAll();
    }
}
