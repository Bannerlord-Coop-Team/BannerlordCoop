using System;
using System.Linq;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace E2E.Tests.Environment.MockEngine;

/// <summary>
/// Lets the NPC release (<c>BattleDeploymentCoordinator.ActivateNpcAi</c>) run to its end on mocked teams: it walks
/// the enemy formations, wakes their units and resets the team tactic. Every prefix falls through for real objects.
/// </summary>
public static class NpcReleaseMockPatches
{
    private static readonly object Gate = new();
    private static bool applied;

    // Applied once per test process and never unpatched: the prefixes only answer for mock shells, and
    // unpatching would rewrite the methods under tests running in parallel.
    public static void Apply()
    {
        lock (Gate)
        {
            if (applied) return;
            var harmony = new Harmony("E2E.Tests.NpcReleaseMockPatches");
            Prefix(harmony, AccessTools.PropertyGetter(typeof(Team), nameof(Team.FormationsIncludingSpecialAndEmpty)), nameof(Team_get_FormationsIncludingSpecialAndEmpty));
            Prefix(harmony, AccessTools.PropertyGetter(typeof(Team), nameof(Team.QuerySystem)), nameof(Team_get_QuerySystem));
            Prefix(harmony, AccessTools.Method(typeof(TeamQuerySystem), nameof(TeamQuerySystem.Expire)), nameof(TeamQuerySystem_Expire));
            Prefix(harmony, AccessTools.Method(typeof(Team), nameof(Team.ResetTactic)), nameof(Team_ResetTactic));
            Prefix(harmony, AccessTools.PropertyGetter(typeof(Formation), nameof(Formation.CountOfUnits)), nameof(Formation_get_CountOfUnits));
            Prefix(harmony, AccessTools.Method(typeof(Formation), nameof(Formation.ApplyActionOnEachUnit), new[] { typeof(Action<Agent>), typeof(Agent) }),
                nameof(Formation_ApplyActionOnEachUnit));
            Prefix(harmony, AccessTools.Method(typeof(Agent), nameof(Agent.GetAgentFlags)), nameof(Agent_GetAgentFlags));
            applied = true;
        }
    }

    private static void Prefix(Harmony harmony, System.Reflection.MethodBase target, string patch)
    {
        if (target == null) throw new MissingMethodException(patch);
        harmony.Patch(target, prefix: new HarmonyMethod(AccessTools.Method(typeof(NpcReleaseMockPatches), patch)));
    }

    private static bool Team_get_FormationsIncludingSpecialAndEmpty(Team __instance, ref MBList<Formation> __result)
    {
        if (!MockTeam.ForShell(__instance, out var team)) return true;
        __result = team.Formations;
        return false;
    }

    private static bool Team_get_QuerySystem(Team __instance, ref TeamQuerySystem __result)
    {
        if (!MockTeam.ForShell(__instance, out var team)) return true;
        __result = team.QuerySystem;
        return false;
    }

    private static bool TeamQuerySystem_Expire(TeamQuerySystem __instance) => !MockTeam.OwnsQuerySystem(__instance);

    private static bool Team_ResetTactic(Team __instance)
    {
        if (!MockTeam.ForShell(__instance, out var team)) return true;
        team.ResetTacticCalls++;
        return false;
    }

    private static bool Formation_get_CountOfUnits(Formation __instance, ref int __result)
    {
        if (!MockFormation.ForShell(__instance, out var f)) return true;
        __result = f.CountOfUnits;
        return false;
    }

    private static bool Formation_ApplyActionOnEachUnit(Formation __instance, Action<Agent> __0)
    {
        if (!MockFormation.ForShell(__instance, out var f)) return true;
        foreach (var agent in f.Units.ToList()) __0(agent);
        return false;
    }

    private static bool Agent_GetAgentFlags(Agent __instance, ref AgentFlag __result)
    {
        if (!AgentMirror.TryGet(__instance, out _)) return true;
        __result = AgentFlag.CanWieldWeapon;
        return false;
    }
}
