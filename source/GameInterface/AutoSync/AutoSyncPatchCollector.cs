using Common.Logging;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace GameInterface.AutoSync;

public interface IAutoSyncPatchCollector : IDisposable
{
    void AddPrefix(MethodBase patchMethod, MethodInfo patch);
    void AddPostfix(MethodBase patchMethod, MethodInfo patch);
    void AddTranspiler(MethodBase patchMethod, MethodInfo patch);
    void PatchAll();
    void UnpatchAll();
}

class AutoSyncPatchCollector : IAutoSyncPatchCollector
{
    private static readonly ILogger Logger = LogManager.GetLogger<AutoSyncPatchCollector>();

    public static readonly HashSet<(MethodBase, MethodInfo, HarmonyPatchType)> PatchedMethods = new();

    private readonly Harmony harmony;

    private readonly List<(MethodBase, MethodInfo)> transpilers = new();
    private readonly List<(MethodBase, MethodInfo)> prefixes = new();
    private readonly List<(MethodBase, MethodInfo)> postfixes = new();
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
    public void AddPostfix(MethodBase patchMethod, MethodInfo patch)
    {
        if (patchMethod == null) throw new ArgumentNullException(nameof(patchMethod));
        if (patch == null) throw new ArgumentNullException(nameof(patch));

        postfixes.Add((patchMethod, patch));
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
            var key = (method, patch, HarmonyPatchType.Transpiler);
            if (PatchedMethods.Contains(key))
            {
                Logger.Error("Method '{MethodName}' was already patched with '{PatchName}'", method.Name, patch.Name);
                continue;
            }

            try
            { 
                harmony.Patch(method, transpiler: new HarmonyMethod(patch));
                PatchedMethods.Add(key);
            } 
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to patch {DeclaringType}.{MethodName} with patch {PatchDeclaringType}.{PatchName}",
                    method.DeclaringType, method.Name, patch.DeclaringType, patch.Name);
            }
        }

        foreach (var (method, patch) in prefixes)
        {
            var key = (method, patch, HarmonyPatchType.Prefix);
            if (PatchedMethods.Contains(key))
            {
                Logger.Error("Method '{DeclaringType}.{MethodName}' was already patched with '{PatchName}'", method.DeclaringType, method.Name, patch.Name);
                continue;
            }

            try
            {
                harmony.Patch(method, prefix: new HarmonyMethod(patch));
                PatchedMethods.Add(key);
            } catch (Exception ex)
            {
                Logger.Error(ex, "Failed to patch {DeclaringType}.{MethodName} with patch {PatchDeclaringType}.{PatchName}",
                    method.DeclaringType, method.Name, patch.DeclaringType, patch.Name);
            }
        }

        foreach (var (method, patch) in postfixes)
        {
            var key = (method, patch, HarmonyPatchType.Postfix);
            if (PatchedMethods.Contains(key))
            {
                Logger.Error("Method '{MethodName}' was already patched with '{PatchName}'", method.Name, patch.Name);
                continue;
            }

            try
            {
                harmony.Patch(method, postfix: new HarmonyMethod(patch));
                PatchedMethods.Add(key);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to patch {DeclaringType}.{MethodName} with patch {PatchDeclaringType}.{PatchName}",
                    method.DeclaringType, method.Name, patch.DeclaringType, patch.Name);
            }
        }
    }

    public void UnpatchAll()
    {
        UnpatchAll(transpilers, HarmonyPatchType.Transpiler);
        UnpatchAll(prefixes, HarmonyPatchType.Prefix);
        UnpatchAll(postfixes, HarmonyPatchType.Postfix);
    }

    private void UnpatchAll(List<(MethodBase, MethodInfo)> patches, HarmonyPatchType patchType)
    {
        foreach (var (method, patch) in patches.ToArray())
        {
            // Always clear the bookkeeping, even if the live unpatch fails: the static set must not
            // outlive the patches, otherwise a rebuilt container's PatchAll skips re-patching them.
            // The live patch is also removed in bulk by the caller's Harmony.UnpatchAll().
            try
            {
                harmony.Unpatch(method, patch);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to unpatch {DeclaringType}.{MethodName} from patch {PatchDeclaringType}.{PatchName}",
                    method.DeclaringType, method.Name, patch.DeclaringType, patch.Name);
            }

            PatchedMethods.Remove((method, patch, patchType));
            patches.Remove((method, patch));
        }
    }

    public void Dispose()
    {
    }
}
