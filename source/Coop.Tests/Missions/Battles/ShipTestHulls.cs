using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using TaleWorlds.Engine;
using TaleWorlds.MountAndBlade;

namespace Coop.Tests.Missions.Battles;

/// <summary>Constructor-skipped hull objects for registry tests that never reach native code.</summary>
internal static class ShipTestHulls
{
    // ScriptComponentBehavior's type initializer reads the managed module type catalog the engine normally fills.
    internal static MissionObject Create() => Uninitialized<TestHull>();

    internal static UsableMissionObject CreatePoint() => Uninitialized<StandingPoint>();

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
