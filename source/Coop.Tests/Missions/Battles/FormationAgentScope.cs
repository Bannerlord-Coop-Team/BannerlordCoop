using HarmonyLib;
using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Coop.Tests.Missions.Battles;

/// <summary>Provides an identity-only active agent; only static animation lookup is stubbed during initialization.</summary>
[HarmonyPatch(typeof(MBAnimation), nameof(MBAnimation.GetActionCodeWithName))]
internal sealed class FormationAgentScope : IDisposable
{
    private readonly IntPtr state = Marshal.AllocHGlobal(sizeof(int));
    public Agent Agent { get; }

    public FormationAgentScope()
    {
        var harmony = new Harmony("coop.tests.formation-agent");
        try
        {
            harmony.CreateClassProcessor(typeof(FormationAgentScope)).Patch();
            RuntimeHelpers.RunClassConstructor(typeof(Agent).TypeHandle);
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
        }

        Agent = (Agent)FormatterServices.GetUninitializedObject(typeof(Agent));
        Marshal.WriteInt32(state, (int)AgentState.Active);
        typeof(Agent).GetField("_statePointer", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(Agent, new UIntPtr(unchecked((ulong)state.ToInt64())));
    }

    // Static action caches need an index, but these tests never play an animation.
    [HarmonyPrefix]
    private static bool AnimationLookup(ref int __result)
    {
        __result = 0;
        return false;
    }

    public void Dispose() => Marshal.FreeHGlobal(state);
}
