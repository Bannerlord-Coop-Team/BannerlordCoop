using Common.Logging;
using HarmonyLib;
using Serilog;
using System;
using System.Diagnostics;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.MapEvents.Patches;

/// <summary>
/// Lets deployment finish when the local player has no agent on the field.
/// </summary>
/// <remarks>
/// Native <c>SetupTeams</c> runs in this order:
///
///   1. Mission.DisableDying = true; SetFallAvoidSystemActive(true)
///   2. OnSetupTeamsOfSide(EnemySide)        - enemy spawns
///   3. SetupAIOfEnemySide / HideAgentsOfSide
///   4. OnSetupTeamsOfSide(PlayerSide)       - player side spawns
///   5. Mission.InitialPlayerAgent.Controller = ...   &lt;-- dereferenced unconditionally
///   6. CanPlayerSideDeployWithOrderOfBattle()        &lt;-- where coop handles "no leader on field"
///
/// Coop can legitimately reach step 5 with no player agent, and the codebase already knows it: see
/// <see cref="CoopAllowPlayerDeploymentPatch"/>, which handles a hero downed and rejoining, or a wounded hero
/// starting a fresh battle. The problem is purely one of ORDER - that handling is consulted at step 6, while
/// the unguarded dereference is at step 5. So whenever the hero has no agent, SetupTeams throws before the
/// coop path is ever reached, deployment never finishes, and EVERY player in the battle is left on the
/// deployment screen with nothing logged to explain it.
///
/// A finalizer rather than a prefix or transpiler: the method must still do all its real work (both sides
/// spawn in steps 1-4), so it has to run and be allowed to fail at step 5. What is missing afterwards is the
/// tail - the deployment never being finished - and that is what is completed here.
/// </remarks>
[HarmonyPatch(typeof(DeploymentMissionController), "SetupTeams")]
internal class CoopDeploymentPlayerAgentGuardPatch
{
    private static readonly ILogger Logger = LogManager.GetLogger<CoopDeploymentPlayerAgentGuardPatch>();

    [HarmonyFinalizer]
    private static Exception Finalizer(DeploymentMissionController __instance, Exception __exception)
    {
        if (__exception == null) return null;
        if (!BattleSpawnGate.IsCoopBattleActive) return __exception;
        if (!ThrewOnMissingPlayerAgent(__exception)) return __exception;

        var mission = __instance.Mission ?? Mission.Current;
        if (mission == null) return __exception;

        Logger.Warning(
            "[Deployment] SetupTeams could not complete without a local player agent; finishing deployment so the battle is playable ({Error})",
            __exception.GetType().Name);

        Invoke(__instance, "OnSetupTeamsFinished");

        try
        {
            FinishDeploymentWithoutPlayerAgent(__instance, mission);
        }
        catch (Exception e)
        {
            Logger.Error(e, "[Deployment] Could not finish deployment after the missing player agent");
            return __exception;
        }

        Logger.Warning("[Deployment] Deployment finished without a player agent; the battle runs leaderless");
        return null;
    }

    /// <summary>
    /// Whether this exception is the missing-player-agent dereference at step 5, and not some earlier failure.
    /// </summary>
    /// <remarks>
    /// <c>InitialPlayerAgent</c> is ALSO null during steps 1-4, so "it is null now" on its own would swallow an
    /// unrelated failure in enemy-side setup or AI setup and then run the deployment tail against half-built
    /// teams - trading a visible stall for silent corruption. Three things have to agree instead:
    ///
    ///   - the exception is a NullReferenceException,
    ///   - it was raised in SetupTeams ITSELF rather than in anything it called (the dereference is inline;
    ///     a failure inside OnSetupTeamsOfSide or SetupAIOfEnemySide leaves that frame on top),
    ///   - agents exist, so the spawning steps did run.
    ///
    /// Harmony renames the original when it patches it, hence the prefix match rather than an equality check.
    /// </remarks>
    private static bool ThrewOnMissingPlayerAgent(Exception exception)
    {
        if (!(exception is NullReferenceException)) return false;

        var mission = Mission.Current;
        if (mission == null || mission.InitialPlayerAgent != null) return false;
        if ((mission.Agents?.Count ?? 0) == 0) return false;

        var frames = new StackTrace(exception, false).GetFrames();
        if (frames == null || frames.Length == 0) return false;

        var method = frames[0].GetMethod();
        if (method == null) return false;
        if (!method.Name.StartsWith("SetupTeams", StringComparison.Ordinal)) return false;

        return method.DeclaringType == null
            || typeof(DeploymentMissionController).IsAssignableFrom(method.DeclaringType);
    }

    /// <summary>
    /// Native <c>FinishDeployment</c>, minus the two lines that need a player agent.
    /// </summary>
    /// <remarks>
    /// FinishDeployment cannot simply be called: it dereferences the SAME null <c>InitialPlayerAgent</c>
    /// partway through, throws, and skips everything after it. Its work is therefore driven directly, in
    /// native order, omitting only <c>SetDetachableFromFormation(true)</c> and <c>Controller = Player</c> on
    /// the agent that does not exist.
    ///
    /// The three mission flags matter as much as the callbacks. SetupTeams sets <c>DisableDying = true</c> and
    /// fall avoidance ON for the duration of deployment, and FinishDeployment is what turns both back off -
    /// so a tail that skips them leaves a battle whose agents cannot die. Equally, the per-unit pass is what
    /// un-pauses every AI agent: without it the troops that just spawned stay frozen, which in a leaderless
    /// battle means nothing on the field moves at all.
    /// </remarks>
    private static void FinishDeploymentWithoutPlayerAgent(DeploymentMissionController controller, Mission mission)
    {
        Invoke(controller, "BeforeDeploymentFinished");

        // Native unhides only the defender side, and only when the player is the attacker.
        if (controller.IsPlayerAttacker)
            Invoke(controller, "UnhideAgentsOfSide", BattleSideEnum.Defender);

        mission.OnDeploymentFinished();
        WakeDeployedAgents(mission);

        mission.AllowAiTicking = true;
        mission.DisableDying = false;
        mission.SetFallAvoidSystemActive(false);

        mission.OnAfterDeploymentFinished();
        Invoke(controller, "AfterDeploymentFinished");
        mission.RemoveMissionBehavior(controller);
    }

    /// <summary>Native's per-unit deployment-end pass: alarm, un-pause, refresh caches, re-sync behaviour.</summary>
    /// <remarks>
    /// The same sequence as <c>Missions.Battles.AgentAiWaker</c>, which cannot be reused from here because
    /// Missions references GameInterface and not the other way round.
    /// </remarks>
    private static void WakeDeployedAgents(Mission mission)
    {
        foreach (var team in mission.Teams)
        {
            foreach (var formation in team.FormationsIncludingSpecialAndEmpty)
            {
                if (formation.CountOfUnits <= 0) continue;

                formation.ApplyActionOnEachUnit(agent =>
                {
                    if (!agent.IsAIControlled) return;

                    agent.SetAlarmState(Agent.AIStateFlag.Alarmed);
                    agent.SetIsAIPaused(false);
                    if (agent.GetAgentFlags().HasFlag(AgentFlag.CanWieldWeapon))
                        agent.ResetEnemyCaches();
                    agent.HumanAIComponent?.SyncBehaviorParamsIfNecessary();
                }, null);
            }
        }
    }

    /// <summary>Calls one of the controller's non-public deployment steps, ignoring a missing method.</summary>
    private static void Invoke(DeploymentMissionController controller, string name, object argument = null)
    {
        try
        {
            var method = AccessTools.Method(typeof(DeploymentMissionController), name);
            if (method == null)
            {
                Logger.Warning("[Deployment] {Method} not found; skipping that step", name);
                return;
            }

            method.Invoke(controller, argument == null ? null : new[] { argument });
        }
        catch (Exception e)
        {
            // Best effort: one missing step must not stop the rest of the deployment from being finished.
            Logger.Warning(e, "[Deployment] {Method} failed; continuing", name);
        }
    }
}
