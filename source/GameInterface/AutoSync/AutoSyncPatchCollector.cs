using Common.Logging;
using HarmonyLib;
using SandBox.BoardGames.Pawns;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace GameInterface.AutoSync;

public interface IAutoSyncPatchCollector : IDisposable
{
    void AddPrefix(MethodBase patchMethod, MethodInfo patch);
    void AddTranspiler(MethodBase patchMethod, MethodInfo patch);
    void PatchAll();
    void UnpatchAll();
}

class AutoSyncPatchCollector : IAutoSyncPatchCollector
{
    private static readonly ILogger Logger = LogManager.GetLogger<AutoSyncPatchCollector>();
    private static readonly HashSet<(MethodBase, MethodInfo)> patchedMethods = new();

    private readonly Harmony harmony;

    private readonly List<(MethodBase, MethodInfo)> transpilers = new();
    private readonly List<(MethodBase, MethodInfo)> prefixes = new();

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
            var key = (method, patch);
            if (patchedMethods.Contains(key))
            {
                Logger.Error("Method '{MethodName}' was already patched with '{PatchName}'", method.Name, patch.Name);
                continue;
            }

            try
            { 
                harmony.Patch(method, transpiler: new HarmonyMethod(patch));
                patchedMethods.Add(key);
            } 
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to patch {DeclaringType}.{MethodName} with patch {PatchDeclaringType}.{PatchName}",
                    method.DeclaringType, method.Name, patch.DeclaringType, patch.Name);
            }
    }

        foreach (var (method, patch) in prefixes)
        {
            var key = (method, patch);
            if (patchedMethods.Contains(key))
            {
                Logger.Error("Method '{DeclaringType}.{MethodName}' was already patched with '{PatchName}'", method.DeclaringType, method.Name, patch.Name);
                return;
            }

            try
            {
                harmony.Patch(method, prefix: new HarmonyMethod(patch));
                patchedMethods.Add(key);
            } catch (Exception ex)
            {
                Logger.Error(ex, "Failed to patch {DeclaringType}.{MethodName} with patch {PatchDeclaringType}.{PatchName}",
                    method.DeclaringType, method.Name, patch.DeclaringType, patch.Name);
            }
        }
    }

    public void UnpatchAll()
    {
        foreach (var (method, patch) in transpilers)
        {
            harmony.Unpatch(method, HarmonyPatchType.Transpiler, harmony.Id);

            var key = (method, patch);
            patchedMethods.Remove(key);
            transpilers.Remove(key);
        }

        foreach (var (method, patch) in prefixes)
        {
            harmony.Unpatch(method, HarmonyPatchType.Prefix, harmony.Id);
            var key = (method, patch);
            patchedMethods.Remove(key);
            prefixes.Remove(key);
        }
    }

    public void Dispose()
    {
    }
}
