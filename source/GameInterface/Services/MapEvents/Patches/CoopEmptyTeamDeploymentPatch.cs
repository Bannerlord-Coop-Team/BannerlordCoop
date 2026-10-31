using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
#if DEBUG
using Common;
using Common.Commands;
using Newtonsoft.Json;
#endif
using Common.Logging;
using HarmonyLib;
using Serilog;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.MapEvents.Patches;

/// <summary>
/// Lets <c>DefaultBattleMissionAgentSpawnLogic.CheckDeployment</c> spawn a side whose other teams this client never
/// fields (e.g. an empty AI-ally team filled by puppets) by treating those unplanned teams as planned at its call sites.
/// </summary>
/// <remarks>
/// The override rewrites <c>CheckDeployment</c> itself instead of postfixing <c>IsPlanMade</c>: the JIT inlines that
/// small getter into a <c>CheckDeployment</c> compiled before coop patches exist (any battle earlier in the process),
/// which silently skipped the postfix and left the player's side unspawned. Patching the caller rebuilds it from IL.
/// </remarks>
[HarmonyPatch(typeof(DefaultBattleMissionAgentSpawnLogic), "CheckDeployment")]
internal class CoopEmptyTeamDeploymentPatch
{
    private static readonly ILogger Logger = LogManager.GetLogger<CoopEmptyTeamDeploymentPatch>();

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

    // Routes CheckDeployment's IsPlanMade(team) reads through the spawn-gate override. The other callers keep the native
    // answer: MakeTeamPlans must still plan the teams this client fields, and PhaseTick must only clear real plans.
    [HarmonyTranspiler]
    internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original)
    {
        var isPlanMade = AccessTools.Method(typeof(DefaultMissionDeploymentPlan),
            nameof(DefaultMissionDeploymentPlan.IsPlanMade), new[] { typeof(Team) });
        var replacement = AccessTools.Method(typeof(CoopEmptyTeamDeploymentPatch), nameof(IsPlanMadeForSpawnGate));

        var replacements = 0;
        foreach (var code in instructions)
        {
            if ((code.opcode == OpCodes.Call || code.opcode == OpCodes.Callvirt)
                && code.operand is MethodInfo method && method == isPlanMade)
            {
                code.opcode = OpCodes.Call;
                code.operand = replacement;
                replacements++;
            }
            yield return code;
        }

        if (replacements == 0)
            Logger.Warning("Redirected no IsPlanMade calls in {Method}; a game update may have changed the method",
                original?.Name);
    }

    // CheckDeployment's view of a team's plan: the native answer, or planned for a team only puppets fill.
    internal static bool IsPlanMadeForSpawnGate(DefaultMissionDeploymentPlan plan, Team team)
        => ApplySpawnGateOverride(team, plan.IsPlanMade(team));

    // Answers planned for an unplanned foreign team inside a coop battle, otherwise keeps the native answer.
    internal static bool ApplySpawnGateOverride(Team team, bool isPlanMade)
    {
        if (isPlanMade || !BattleSpawnConfig.Enabled || !IsForeignTeam(team))
            return isPlanMade;

        if (!ContainerProvider.TryResolve<ICoopBattleMissionRegistry>(out var missionRegistry)
            || !missionRegistry.IsCoopBattleActive)
            return isPlanMade;

#if DEBUG
        TrackOverride(team);
#endif
        return true;
    }

#if DEBUG
    /// <summary>Reports non-owning deployment observations without changing campaign state.</summary>
    public sealed class DeploymentRetentionStateCoopCommand : ICoopCommand
    {
        private readonly ICoopBattleMissionRegistry missionRegistry;

        // Takes the registry whose battle window blocks collect/reset while a coop battle is open.
        public DeploymentRetentionStateCoopCommand(ICoopBattleMissionRegistry missionRegistry)
        {
            this.missionRegistry = missionRegistry;
        }

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
            if ((collect || reset) && (Mission.Current != null || missionRegistry.IsCoopBattleActive))
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
                coopBattleActive = missionRegistry.IsCoopBattleActive,
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
    // Only consulted when there's no real plan, so a foreign team the host DOES field — which gets a real plan in
    // MakeTeamPlans — is unaffected.
    private static bool IsForeignTeam(Team team)
    {
        var mission = Mission.Current;
        return team != null && mission != null && team != mission.PlayerTeam;
    }
}
