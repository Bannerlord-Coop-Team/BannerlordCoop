using Common.Logging;
using Serilog;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Missions.Battles;

/// <summary>
/// Coop <see cref="BattleDeploymentMissionController"/> that defers the native one-time team/command setup
/// (<c>SetupTeams</c>, which grants player command via <c>OnTeamDeployed</c>) until the spawn handler has sized the
/// sides. Native runs it on the first tick regardless of troops; in coop a late reserve would run it against empty
/// teams with a null <c>Agent.Main</c> and latch the player commanding nothing. Gating on
/// <see cref="CoopBattleMissionSpawnHandler.IsSized"/> holds it until the troops and hero have spawned; the on-time
/// path is unchanged (IsSized is already true on the first tick).
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
        // Hold SetupTeams until the sides are sized. Gate on the handler's game-thread IsSized, not the suppliers'
        // network-thread IsPopulated (which could read true mid-frame before Init has sized).
        if (_spawnHandler != null && !_spawnHandler.IsSized) return;

        bool setupWasOver = TeamSetupOver;
        base.OnMissionTick(dt);

        // Re-pause every agent when the deferred SetupTeams first completes. The joint re-Init enters SetupTeams
        // with both sides spawn-active, so the first side's spawn puts the other side on the field before its own
        // pause pass, and the native per-formation freeze can miss agents, which then drift once AI ticks. Skip if
        // SetupTeams released the field outright (no Order of Battle).
        if (!setupWasOver && TeamSetupOver && !base.Mission.AllowAiTicking)
            FreezeDeployedAgents();
    }

    // The native deployment freeze applied over every agent, not just the ones each per-side pass covered.
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
