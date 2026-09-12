using HarmonyLib;
using System;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.MapEvents.Patches;

/// <summary>
/// Used by coop.debug.map_event.kms to force the player's agent to die
/// Agent death normally uses survival chance but this overrides that result
/// </summary>
[HarmonyPatch(typeof(Mission))]
internal static class ForceCommandDeathPatch
{
    [ThreadStatic]
    private static Agent forcedDeathAgent;

    internal static void RunWithForcedDeath(Agent agent, Action applyDeath)
    {
        var previousForcedDeathAgent = forcedDeathAgent;
        forcedDeathAgent = agent;
        try
        {
            applyDeath();
        }
        finally
        {
            forcedDeathAgent = previousForcedDeathAgent;
        }
    }

    [HarmonyPatch(nameof(Mission.GetAgentState))]
    [HarmonyPatch(new[] { typeof(Agent), typeof(Agent), typeof(DamageTypes), typeof(WeaponFlags)})]  
    [HarmonyPostfix]
    private static void ForceCommandDeathState(Agent agent, ref AgentState __result)
    {
        if (!ReferenceEquals(agent, forcedDeathAgent)) return;

        __result = AgentState.Killed;
    }
}
