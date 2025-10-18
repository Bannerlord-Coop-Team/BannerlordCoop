using HarmonyLib;
using SandBox.BoardGames.Pawns;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;

namespace GameInterface.AutoSync;

public interface IAutoSyncPatchCollector : IDisposable
{
    void AddPrefix(MethodBase patchMethod, MethodInfo patch);
    void AddPostfix(MethodBase patchMethod, MethodInfo patch);
    void AddTranspiler(MethodBase patchMethod, MethodInfo patch);
    void PatchAll();
}

class AutoSyncPatchCollector : IAutoSyncPatchCollector
{
    private readonly Harmony harmony;

    private readonly HashSet<(MethodBase, MethodInfo)> prefixes = new HashSet<(MethodBase, MethodInfo)>();
    private readonly HashSet<(MethodBase, MethodInfo)> postfixes = new HashSet<(MethodBase, MethodInfo)>();
    private readonly HashSet<(MethodBase, MethodInfo)> transpilers = new HashSet<(MethodBase, MethodInfo)>();
    

    public AutoSyncPatchCollector(Harmony harmony)
    {
        this.harmony = harmony;
    }

    public void AddPrefix(MethodBase patchMethod, MethodInfo patch) => AddGeneric(patchMethod, patch, prefixes);

    public void AddPostfix(MethodBase patchMethod, MethodInfo patch) => AddGeneric(patchMethod, patch, postfixes);

    public void AddTranspiler(MethodBase patchMethod, MethodInfo patch)
    {
        if (patchMethod == null) throw new ArgumentNullException(nameof(patchMethod));
        if (patch == null) throw new ArgumentNullException(nameof(patch));

        transpilers.Add((patchMethod, patch));
    }

    private void AddGeneric(MethodBase patchMethod, MethodInfo patch, HashSet<(MethodBase, MethodInfo)> set)
    {
        if (patchMethod == null) throw new ArgumentNullException(nameof(patchMethod));
        if (patch == null) throw new ArgumentNullException(nameof(patch));

        var tuple = (patchMethod, patch);

        if (set.Contains(tuple)) throw new ArgumentException("This patch already exists");

        set.Add((patchMethod, patch));
    }

    public void PatchAll()
    {
        foreach (var (method, patch) in prefixes)
        {
            harmony.Patch(method, prefix: new HarmonyMethod(patch));
        }

        foreach (var (method, patch) in postfixes)
        {
            harmony.Patch(method, postfix: new HarmonyMethod(patch));
        }

        foreach (var (method, patch) in transpilers)
        {
            harmony.Patch(method, transpiler: new HarmonyMethod(patch));
        }
    }

    public void Dispose()
    {
        transpilers.Clear();
        prefixes.Clear();
    }
}
