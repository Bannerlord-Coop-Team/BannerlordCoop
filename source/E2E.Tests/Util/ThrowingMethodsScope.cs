using HarmonyLib;
using System.Reflection;

namespace E2E.Tests.Util;

/// <summary>
/// Makes each given method throw until disposed. Open it inside a server call, whose game lock keeps other tests out.
/// </summary>
internal sealed class ThrowingMethodsScope : IDisposable
{
    private readonly Harmony harmony = new($"throwing-methods.{Guid.NewGuid():N}");

    public ThrowingMethodsScope(params MethodBase[] methods)
    {
        foreach (var method in methods)
            harmony.Patch(method, prefix: new HarmonyMethod(typeof(ThrowingMethodsScope), nameof(Throw)));
    }

    // Also thrown from listeners a test adds, so every stubbed failure is the same exception
    public static bool Throw() => throw new InvalidOperationException("Stubbed failure");

    public void Dispose() => harmony.UnpatchAll(harmony.Id);
}
