using System;

namespace GameInterface.Registry.Auto;

/// <summary>Suppresses auto-registry handling for one intentional transient Equipment construction.</summary>
public sealed class TransientEquipmentLifetimeScope : IDisposable
{
    [ThreadStatic]
    private static int depth;

    internal static bool IsActive => depth > 0;

    public TransientEquipmentLifetimeScope()
    {
        depth++;
    }

    public void Dispose()
    {
        depth--;
    }
}
