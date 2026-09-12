using System;

namespace GameInterface.Registry.Auto;

/// <summary>Suppresses managed-object synchronization for one intentional transient Equipment construction.</summary>
public sealed class TransientEquipmentSyncScope : IDisposable
{
    [ThreadStatic]
    private static int depth;

    public static bool IsActive => depth > 0;

    public TransientEquipmentSyncScope()
    {
        depth++;
    }

    public void Dispose()
    {
        depth--;
    }
}
