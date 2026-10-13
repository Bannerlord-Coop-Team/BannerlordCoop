using HarmonyLib;
using System.Runtime.CompilerServices;
using TaleWorlds.Engine;
using TaleWorlds.MountAndBlade;

namespace E2E.Tests.Util;

/// <summary>Constructor-skipped mission objects for tests that never reach native code.</summary>
internal static class MissionTestObjects
{
    internal static MissionObject CreateHull() => Uninitialized<TestHull>();

    internal static StandingPoint CreateStandingPoint() => Uninitialized<StandingPoint>();

    // ScriptComponentBehavior's type initializer reads the managed module type catalog the engine normally fills; run
    // first without it, the initializer fails for every later test in the process.
    private static T Uninitialized<T>()
    {
        var managed = typeof(ScriptComponentBehavior).BaseType!.Assembly.GetType("TaleWorlds.DotNet.Managed")!;
        var field = AccessTools.Field(managed, "_moduleTypes");
        var previous = field.GetValue(null);
        try
        {
            if (previous == null) field.SetValue(null, new Dictionary<string, Type>());
            return (T)RuntimeHelpers.GetUninitializedObject(typeof(T));
        }
        finally
        {
            field.SetValue(null, previous);
        }
    }

    private sealed class TestHull : MissionObject
    {
    }
}
