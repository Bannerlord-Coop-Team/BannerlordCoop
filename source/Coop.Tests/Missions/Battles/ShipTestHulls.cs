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
    internal static MissionObject Create()
    {
        var managed = typeof(ScriptComponentBehavior).BaseType!.Assembly.GetType("TaleWorlds.DotNet.Managed")!;
        var field = AccessTools.Field(managed, "_moduleTypes");
        var previous = field.GetValue(null);
        try
        {
            if (previous == null) field.SetValue(null, new Dictionary<string, Type>());
            return (MissionObject)RuntimeHelpers.GetUninitializedObject(typeof(TestHull));
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
