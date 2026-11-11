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

    internal static RangedSiegeWeapon CreateShipWeapon() => Uninitialized<Ballista>();

    private static T Uninitialized<T>() => (T)Uninitialized(typeof(T));

    // A constructor-skipped instance of a script type, also one only known at runtime such as a NavalDLC machine.
    // ScriptComponentBehavior's type initializer reads the managed module type catalog the engine normally fills; run
    // first without it, the initializer fails for every later test in the process.
    internal static object Uninitialized(Type type)
    {
        var managed = typeof(ScriptComponentBehavior).BaseType!.Assembly.GetType("TaleWorlds.DotNet.Managed")!;
        var field = AccessTools.Field(managed, "_moduleTypes");
        var previous = field.GetValue(null);
        try
        {
            if (previous == null) field.SetValue(null, new Dictionary<string, Type>());
            return RuntimeHelpers.GetUninitializedObject(type);
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
