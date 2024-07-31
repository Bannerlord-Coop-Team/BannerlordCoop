using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;

namespace GameInterface.AutoSync;

public interface IPatchCollection
{
    ReadOnlyCollection<Patch> Patches { get; }

    void AddPrefix(MethodBase target, MethodInfo patch);

    void AddPostfix(MethodBase target, MethodInfo patch);

    void Clear();
}

internal class PatchCollection : IPatchCollection
{
    public ReadOnlyCollection<Patch> Patches => _patches.AsReadOnly();
    private readonly List<Patch> _patches = new List<Patch>();

    public void AddPrefix(MethodBase target, MethodInfo patch)
    {
        _patches.Add(new Patch(PatchType.Prefix, target, patch));
    }

    public void AddPostfix(MethodBase target, MethodInfo patch)
    {
        _patches.Add(new Patch(PatchType.Prefix, target, patch));
    }

    public void Clear() => _patches.Clear();
}

public enum PatchType
{
    Prefix,
    Postfix
}

public struct Patch
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