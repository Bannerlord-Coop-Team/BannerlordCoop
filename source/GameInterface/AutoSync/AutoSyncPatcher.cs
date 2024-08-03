using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace GameInterface.AutoSync;

public interface IAutoSyncPatcher
{
    void AddPostfix(MethodBase original, MethodInfo patch);
    void AddPrefix(MethodBase original, MethodInfo patch);
    void AddTranspiler(MethodBase original, MethodInfo patch);
    void PatchAll();
    void UnpatchAll();
}

internal class AutoSyncPatcher : IAutoSyncPatcher
{
    private const string HarmonyId = nameof(AutoSyncPatcher);

    private readonly Harmony harmony = new Harmony(HarmonyId);

    private bool Patched = false;

    List<Patch> patches = new List<Patch>();

    public void AddPrefix(MethodBase original, MethodInfo patchMethod)
    {
        var patch = new Patch(PatchType.Prefix, original, patchMethod);

        patches.Add(patch);

        if (Patched)
        {
            SinglePatch(patch);
        }
    }

    public void AddPostfix(MethodBase original, MethodInfo patchMethod)
    {
        var patch = new Patch(PatchType.Postfix, original, patchMethod);

        patches.Add(patch);

        if (Patched)
        {
            SinglePatch(patch);
        }
    }

    public void AddTranspiler(MethodBase original, MethodInfo patchMethod)
    {
        var patch = new Patch(PatchType.Transpiler, original, patchMethod);

        patches.Add(patch);

        if (Patched)
        {
            SinglePatch(patch);
        }
    }

    public void PatchAll()
    {
        if (Patched) return;

        Patched = true;

        foreach (var patch in patches)
        {
            SinglePatch(patch);
        }
    }

    private MethodInfo SinglePatch(Patch patch)
    {
        var original = patch.TargetMethod;
        var patchedMethod = new HarmonyMethod(patch.PatchMethod);

        return patch.Type switch
        {
            PatchType.Prefix => harmony.Patch(original, prefix: patchedMethod),
            PatchType.Postfix => harmony.Patch(original, postfix: patchedMethod),
            PatchType.Transpiler => harmony.Patch(original, transpiler: patchedMethod),
            _ => throw new ArgumentException()
        };
    }

    public void UnpatchAll()
    {
        if (Patched == false) return;

        Patched = false;

        foreach (var patch in patches)
        {
            harmony.Unpatch(patch.TargetMethod, HarmonyPatchType.All, HarmonyId);
        }
    }

    enum PatchType
    {
        Prefix,
        Postfix,
        Transpiler
    }

    struct Patch
    {
        public PatchType Type { get; }
        public MethodBase TargetMethod { get; }
        public MethodInfo PatchMethod { get; }

        public Patch(PatchType type, MethodBase targetMethod, MethodInfo patchMethod)
        {
            Type = type;
            PatchMethod = patchMethod;
            TargetMethod = targetMethod;
        }
    }
}
