using System;
using System.Collections.Generic;
using Common.Logging;
using HarmonyLib;
using Serilog;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.MapEvents.Patches;

/// <summary>
/// In a coop field battle a client only fields the troops it OWNS, so a team it does not field — e.g. a non-host's
/// AI-ally team, whose troops arrive as host-driven puppets over the mesh — is EMPTY at deployment.
/// <c>DefaultBattleMissionAgentSpawnLogic.CheckDeployment</c> only spawns a side once EVERY team on it reports
/// <c>IsPlanMade</c>, and an empty team never gets a plan (there is nothing to deploy) → the client never spawns its
/// own troops (a spectator).
/// <para>
/// Treat an empty team as already-planned so the spawn gate proceeds — BUT only when <c>CheckDeployment</c> reads it
/// as the spawn gate, NOT while <c>MakeTeamPlans</c> is running. <c>MakeTeamPlans</c> uses the SAME
/// <c>IsPlanMade(team)</c> as its "already planned?" guard, and at plan time EVERY team is still empty (troops spawn
/// later), so forcing it true there would skip making the real plans — the troops then spawn into an unplanned team
/// and crash (the host's <c>SpawnAgent</c> "Nullable object must have a value"). The <see cref="_inMakeTeamPlans"/>
/// flag scopes the override out of that path. <c>IsReinforcementPlanMade</c> is deliberately NOT overridden: leaving
/// it false for an empty team keeps <c>CheckDeployment</c>'s plan-making loop alive (it gates the SKIP on
/// <c>IsPlanMade &amp;&amp; IsReinforcementPlanMade</c>) so the fillable teams still get real plans. Scoped to coop.
/// </para>
/// </summary>
[HarmonyPatch] // bare class-level marker so PatchAll discovers this multi-target (MakeTeamPlans + IsPlanMade) class
internal class CoopEmptyTeamDeploymentPatch
{
    private static readonly ILogger Logger = LogManager.GetLogger<CoopEmptyTeamDeploymentPatch>();

    // True while the engine is building a team's deployment plan — see the class remarks for why the override must
    // stand down here. Game-thread only; ThreadStatic is belt-and-suspenders.
    [ThreadStatic] private static bool _inMakeTeamPlans;

    // TEMP diagnostic: log each distinct team we treat as planned, once, so a live run confirms this build is active
    // and the override is firing on the foreign (puppet) team. Remove once the non-host spawn is confirmed solid.
    private static readonly HashSet<Team> _loggedOverrides = new HashSet<Team>();

    [HarmonyPatch(typeof(DefaultBattleMissionAgentSpawnLogic), "MakeTeamPlans")]
    [HarmonyPrefix]
    private static void MakeTeamPlans_Prefix() => _inMakeTeamPlans = true;

    // Finalizer (not postfix) so the flag is cleared even if MakeTeamPlans throws.
    [HarmonyPatch(typeof(DefaultBattleMissionAgentSpawnLogic), "MakeTeamPlans")]
    [HarmonyFinalizer]
    private static void MakeTeamPlans_Finalizer() => _inMakeTeamPlans = false;

    [HarmonyPatch(typeof(DefaultMissionDeploymentPlan), nameof(DefaultMissionDeploymentPlan.IsPlanMade), new[] { typeof(Team) })]
    [HarmonyPostfix]
    private static void IsPlanMade_Postfix(Team team, ref bool __result)
    {
        if (!__result && !_inMakeTeamPlans
            && BattleSpawnConfig.Enabled && BattleSpawnGate.IsCoopBattleActive
            && IsForeignTeam(team))
        {
            __result = true;
            if (_loggedOverrides.Add(team))
                Logger.Information("[BattleDiag] Treating foreign team side={Side} (activeAgents={Count}) as deployment-planned so it doesn't stall the local spawn gate",
                    team.Side, team.ActiveAgents.Count);
        }
    }

    // A team the LOCAL client does not field — anything but its own PlayerTeam. The local client only spawns its
    // OWN party (into PlayerTeam, which gets a real plan via MakeTeamPlans); every OTHER team on its side is filled
    // by puppets replicated from their owner, which are NOT deployment-spawned and so never get a deployment plan.
    // Such a team must not stall the spawn gate (an empty-only check fails once puppets populate the ally team).
    // Only fires when there's no real plan (the `!__result` guard), so a foreign team the host DOES field — which
    // gets a real plan in MakeTeamPlans — is unaffected.
    private static bool IsForeignTeam(Team team)
    {
        var mission = Mission.Current;
        return team != null && mission != null && team != mission.PlayerTeam;
    }
}
