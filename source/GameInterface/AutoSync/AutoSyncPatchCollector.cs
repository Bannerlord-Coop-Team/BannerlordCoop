﻿using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace GameInterface.AutoSync;

public interface IAutoSyncPatchCollector : IDisposable
{
    void AddPrefix(MethodInfo patchMethod, MethodInfo patch);
    void AddTranspiler(MethodInfo patchMethod, MethodInfo patch);
    void PatchAll();
    void UnpatchAll();
}

class AutoSyncPatchCollector : IAutoSyncPatchCollector
{
    private readonly Harmony harmony;

    private readonly List<(MethodInfo, MethodInfo)> transpilers = new List<(MethodInfo, MethodInfo)>();
    private readonly List<(MethodInfo, MethodInfo)> prefixes = new List<(MethodInfo, MethodInfo)>();

    private static bool IsPatched = false;

    public AutoSyncPatchCollector(Harmony harmony)
    {
        this.harmony = harmony;
    }

    public void AddPrefix(MethodInfo patchMethod, MethodInfo patch)
    {
        if (patchMethod == null) throw new ArgumentNullException(nameof(patchMethod));
        if (patch == null) throw new ArgumentNullException(nameof(patch));

        prefixes.Add((patchMethod, patch));
    }
    public void AddTranspiler(MethodInfo patchMethod, MethodInfo patch)
    {
        if (patchMethod == null) throw new ArgumentNullException(nameof(patchMethod));
        if (patch == null) throw new ArgumentNullException(nameof(patch));

        transpilers.Add((patchMethod, patch));
    }

    public void PatchAll()
    {
        if (IsPatched) UnpatchAll();

        IsPatched = true;

        foreach (var (method, patch) in transpilers) {
            harmony.Patch(method, transpiler: new HarmonyMethod(patch));
        }

        foreach (var (method, patch) in prefixes)
        {
            harmony.Patch(method, prefix: new HarmonyMethod(patch));
        }
    }

    public void UnpatchAll()
    {
        IsPatched = false;

        foreach (var (method, patch) in transpilers)
        {
            harmony.Unpatch(method, patch);
        }

        foreach (var (method, patch) in prefixes)
        {
            harmony.Unpatch(method, patch);
        }
    }

    public void Dispose()
    {
        UnpatchAll();

        transpilers.Clear();
        prefixes.Clear();
    }
}