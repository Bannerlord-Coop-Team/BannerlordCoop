using Common.Logging;
using Serilog;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Missions.Battles;

/// <summary>
/// Coop <see cref="BattleDeploymentMissionController"/> that holds the one-time team + player-command setup until
/// <see cref="CoopBattleMissionSpawnHandler"/> has sized the sides. The native <see cref="DeploymentMissionController"/>
/// runs <c>SetupTeams</c> on the first tick where the scene exists, with no troop-count gate. <c>SetupTeams</c> drives
/// <c>OnSetupTeamsOfSide -&gt; DefaultBattleMissionAgentSpawnLogic.OnSideDeploymentOver -&gt; Mission.OnTeamDeployed -&gt;
/// AssignPlayerRoleInTeamMissionController.OnTeamDeployed</c>, the sole grant of player command during deployment: the
/// spawn logic's own re-fire is dead while <c>Mission.Mode == Deployment</c> because its <c>IsDeploymentOver</c> guard
/// is always false there. In coop the owned reserve can land after that first tick, so the setup would run against
/// empty teams with a null <c>Agent.Main</c> and latch the player commanding nothing.
/// <para>
/// Gating the tick on the spawn handler's <see cref="CoopBattleMissionSpawnHandler.IsSized"/> defers <c>SetupTeams</c>
/// to the tick the handler jointly sized the sides. On that same tick the handler (earlier in the behavior list) has
/// already re-run Init, so <c>SetupTeams -&gt; OnSetupTeamsOfSide(PlayerSide)</c> spawns our troops and hero — the hero
/// becomes <c>Agent.Main</c> inside that synchronous <c>SetSpawnTroops(enforceSpawning)</c> call — before
/// <c>OnTeamDeployed</c> grants command over the now-populated player formations. The on-time path is unchanged: the
/// handler sizes in <c>AfterStart</c>, so the gate is already open on the first tick.
/// </para>
/// </summary>
public class CoopBattleDeploymentMissionController : BattleDeploymentMissionController
{
    private static readonly ILogger Logger = LogManager.GetLogger<CoopBattleDeploymentMissionController>();

    private CoopBattleMissionSpawnHandler _spawnHandler;

    public CoopBattleDeploymentMissionController(bool isPlayerAttacker)
        : base(isPlayerAttacker)
    {
    }

    public override void OnBehaviorInitialize()
    {
        base.OnBehaviorInitialize();
        _spawnHandler = base.Mission.GetMissionBehavior<CoopBattleMissionSpawnHandler>();
    }

    public override void OnMissionTick(float dt)
    {
        // Hold the native team/command setup until the handler has sized the sides. Gating on the handler's IsSized
        // (set on the game thread after Init) rather than the suppliers' IsPopulated keeps setup strictly after
        // sizing: IsPopulated flips on the network thread and could read true between the handler's tick and ours in
        // the same frame, running SetupTeams against still-empty phases and latching the player onto no units.
        if (_spawnHandler != null && !_spawnHandler.IsSized) return;

        bool setupWasOver = TeamSetupOver;
        base.OnMissionTick(dt);

        // On the tick the deferred SetupTeams completes, re-assert the deployment AI-pause over every agent. Native
        // freezes each side once, in SetupAgentAIStatesForSide, and only the agents already counted in a formation
        // when its per-side pass runs. On the joint deferred re-Init path both sides' phases are enabled together
        // (Init sets TroopSpawnActive for both), so the first side's enforced spawn puts the OTHER side's troops on
        // the field before that side's own pause pass runs, and any agent a per-side pass doesn't cover keeps the
        // isAlarmed state it spawned with and drifts the moment AI ticks. Pausing over Mission.Agents covers every
        // deployed agent regardless of formation, so nothing is left un-paused until Start Battle. Skip when
        // SetupTeams finished deployment outright (no Order of Battle) — that already released the field
        // (AllowAiTicking is back on) and re-freezing would fight it. FinishDeployment un-pauses per agent later, so
        // the Start-Battle release is unaffected; the hero is Controller.None (not IsAIControlled) so command holds.
        if (!setupWasOver && TeamSetupOver && !base.Mission.AllowAiTicking)
            FreezeDeployedAgents();
    }

    // Mirror of the native deployment freeze, applied to every agent rather than only the ones each per-side
    // SetupAgentAIStatesForSide pass covered. One-shot on the setup-complete edge above.
    private void FreezeDeployedAgents()
    {
        int def = 0, atk = 0;
        foreach (Agent agent in base.Mission.Agents)
        {
            if (!agent.IsHuman || !agent.IsActive() || !agent.IsAIControlled) continue;
            agent.SetAlarmState(Agent.AIStateFlag.None);
            agent.SetIsAIPaused(isPaused: true);
            if (agent.Team?.Side == BattleSideEnum.Defender) def++;
            else if (agent.Team?.Side == BattleSideEnum.Attacker) atk++;
        }
        Logger.Information("[BattleSync] Deferred deployment freeze: re-paused Defender={Def}, Attacker={Atk} AI agents", def, atk);
    }
}
