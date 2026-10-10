using System;
#if DEBUG
using System.Collections.Generic;
using Common;
using Common.Commands;
using Newtonsoft.Json;
#endif
using HarmonyLib;
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
    // True while the engine is building a team's deployment plan — see the class remarks for why the override must
    // stand down here. Game-thread only; ThreadStatic is belt-and-suspenders.
    [ThreadStatic] private static bool _inMakeTeamPlans;

#if DEBUG
    private static readonly List<WeakReference> observedTeams = new List<WeakReference>();
    private static readonly List<WeakReference> observedMissions = new List<WeakReference>();
    private static int observedTeamCount;
    private static int observedMissionCount;
    private static long overrideCalls;
    private static long emptyTeamOverrideCalls;
    private static string lastOverrideSide;
    private static int lastOverrideActiveAgents;
#endif

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
#if DEBUG
            TrackOverride(team);
#endif
        }
    }

#if DEBUG
    /// <summary>Reports non-owning deployment observations without changing campaign state.</summary>
    public sealed class DeploymentRetentionStateCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.map_event";
        public string Name => "deployment_retention_state";
        public string Description => "Reports deployment overrides and weak-reference survival; collect/reset require mission exit.";
        public CoopCommandSide Side => CoopCommandSide.Both;
        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("action", "status (default), collect, or reset", false),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            string action = args.Count == 0 ? "status" : args[0];
            if (args.Count > 1 ||
                (!string.Equals(action, "status", StringComparison.OrdinalIgnoreCase) &&
                 !string.Equals(action, "collect", StringComparison.OrdinalIgnoreCase) &&
                 !string.Equals(action, "reset", StringComparison.OrdinalIgnoreCase)))
            {
                return new CoopCommandResult(false,
                    "Usage: coop.debug.map_event.deployment_retention_state [status|collect|reset]",
                    "invalid_arguments");
            }

            bool collect = string.Equals(action, "collect", StringComparison.OrdinalIgnoreCase);
            bool reset = string.Equals(action, "reset", StringComparison.OrdinalIgnoreCase);
            if ((collect || reset) && (Mission.Current != null || BattleSpawnGate.IsCoopBattleActive))
                return new CoopCommandResult(false, "Leave the mission and co-op battle before collecting or resetting observations.", "battle_active");

            if (collect)
            {
                // DEBUG probe only: collection tests reachability; it is never a teardown mechanism.
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }

            RemoveCollectedReferences(observedTeams);
            RemoveCollectedReferences(observedMissions);
            if (reset)
            {
                if (observedTeams.Count != 0 || observedMissions.Count != 0)
                    return new CoopCommandResult(false, "Observed teams or missions are still alive; capture status and roots before resetting.", "observations_alive");

                observedTeamCount = 0;
                observedMissionCount = 0;
                overrideCalls = 0;
                emptyTeamOverrideCalls = 0;
                lastOverrideSide = null;
                lastOverrideActiveAgents = 0;
            }

            return new CoopCommandResult(true, "LIVE_TEST_JSON=" + JsonConvert.SerializeObject(new
            {
                schemaVersion = 1,
                collectionRequested = collect,
                resetPerformed = reset,
                sourceModuleVersionId = typeof(CoopEmptyTeamDeploymentPatch).Assembly.ManifestModule.ModuleVersionId,
                role = ModInformation.IsServer ? "server" : "client",
                totalObservedTeams = observedTeamCount,
                aliveTeams = observedTeams.Count,
                totalObservedMissions = observedMissionCount,
                aliveMissions = observedMissions.Count,
                currentMissionActive = Mission.Current != null,
                coopBattleActive = BattleSpawnGate.IsCoopBattleActive,
                overrideCalls,
                emptyTeamOverrideCalls,
                lastOverrideSide,
                lastOverrideActiveAgents,
            }));
        }
    }

    private static void TrackOverride(Team team)
    {
        overrideCalls++;
        lastOverrideSide = team.Side.ToString();
        lastOverrideActiveAgents = team.ActiveAgents.Count;
        if (lastOverrideActiveAgents == 0)
            emptyTeamOverrideCalls++;
        RemoveCollectedReferences(observedTeams);
        RemoveCollectedReferences(observedMissions);

        if (!ContainsTarget(observedTeams, team))
        {
            observedTeams.Add(new WeakReference(team));
            observedTeamCount++;
        }

        var mission = team.Mission;
        if (mission != null && !ContainsTarget(observedMissions, mission))
        {
            observedMissions.Add(new WeakReference(mission));
            observedMissionCount++;
        }
    }

    private static bool ContainsTarget(List<WeakReference> references, object target)
    {
        foreach (var reference in references)
        {
            if (ReferenceEquals(reference.Target, target))
                return true;
        }

        return false;
    }

    private static void RemoveCollectedReferences(List<WeakReference> references)
    {
        for (int index = references.Count - 1; index >= 0; index--)
        {
            if (!references[index].IsAlive)
                references.RemoveAt(index);
        }
    }
#endif

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
