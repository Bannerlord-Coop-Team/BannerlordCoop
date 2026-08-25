using System;

namespace GameInterface.Registry.Auto;

#if DEBUG
/// <summary>Suppresses auto-registry handling for one intentional debug fixture equipment construction.</summary>
public sealed class DebugEquipmentLifetimeFixtureScope : IDisposable
{
    [ThreadStatic]
    private static int depth;

    internal static bool IsActive => depth > 0;

    public DebugEquipmentLifetimeFixtureScope()
    {
        depth++;
    }

    public void Dispose()
    {
        depth--;
    }
}
#endif
