using Common.Logging;
using HarmonyLib;
using Serilog;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.MapEvents.Patches;

/// <summary>
/// Lets deployment finish when the local player has no agent on the field.
/// </summary>
/// <remarks>
/// Native <c>SetupTeams</c> runs in this order:
///
///   1. OnSetupTeamsOfSide(EnemySide)        - enemy spawns
///   2. SetupAIOfEnemySide / HideAgentsOfSide
///   3. OnSetupTeamsOfSide(PlayerSide)       - player side spawns
///   4. Mission.InitialPlayerAgent.Controller = ...   &lt;-- dereferenced unconditionally
///   5. CanPlayerSideDeployWithOrderOfBattle()        &lt;-- where coop handles "no leader on field"
///
/// Coop can legitimately reach step 4 with no player agent, and the codebase already knows it: see
/// <see cref="CoopAllowPlayerDeploymentPatch"/>, which handles a hero downed and rejoining, or a wounded hero
/// starting a fresh battle. The problem is purely one of ORDER - that handling is consulted at step 5, while
/// the unguarded dereference is at step 4. So whenever the hero has no agent, SetupTeams throws before the
/// coop path is ever reached, deployment never finishes, and EVERY player in the battle is left on the
/// deployment screen with nothing logged to explain it. Observed live: both clients frozen, one
/// NullReferenceException each, no other error.
///
/// A finalizer rather than a prefix or transpiler: the method must still do all its real work (both sides
/// spawn in steps 1-3), so it has to run and be allowed to fail at step 4. What is missing afterwards is the
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

        // Only the missing-player-agent case is swallowed. Anything else is a real failure and must keep
        // propagating rather than being hidden behind a half-finished deployment.
        if (Mission.Current?.InitialPlayerAgent != null) return __exception;

        Logger.Warning(
            "[Deployment] SetupTeams could not complete without a local player agent; finishing deployment so the battle is playable ({Error})",
            __exception.GetType().Name);

        Invoke(__instance, "OnSetupTeamsFinished");

        // FinishDeployment dereferences the SAME null InitialPlayerAgent, so it cannot be relied on to
        // complete - it throws partway and skips everything after. Its work is therefore driven directly,
        // in native order, with the two player-agent lines omitted.
        //
        // The last two matter most: without OnAfterDeploymentFinished the mission never leaves deployment,
        // and without removing this behaviour OnMissionTick keeps re-entering SetupTeams and throwing the
        // same exception every frame - which is what left the screen frozen even after the exception here
        // was already being caught.
        var mission = __instance.Mission ?? Mission.Current;

        Invoke(__instance, "BeforeDeploymentFinished");
        Invoke(__instance, "UnhideAgentsOfSide", __instance.IsPlayerAttacker
            ? BattleSideEnum.Defender
            : BattleSideEnum.Attacker);

        try
        {
            mission?.OnDeploymentFinished();
            mission?.SetFallAvoidSystemActive(true);
            mission?.OnAfterDeploymentFinished();
            Invoke(__instance, "AfterDeploymentFinished");
            mission?.RemoveMissionBehavior(__instance);

            Logger.Warning("[Deployment] Deployment finished without a player agent; the battle runs leaderless");
        }
        catch (Exception e)
        {
            Logger.Error(e, "[Deployment] Could not finish deployment after the missing player agent");
            return __exception;
        }

        return null;
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
