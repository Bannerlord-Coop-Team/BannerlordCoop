using Common.Logging;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Utils.Commands;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.InputSystem;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.GauntletUI.Mission.Singleplayer;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.MapEvents.Commands;

/// <summary>
/// Battle fixture commands for deployment, scoreboard inspection, mission exit, and combat outcomes. Run the
/// direct kill commands on the battle-authority client because it owns the AI/enemy
/// agents, so each kill goes through the coop death path: <c>Agent.Die</c>, the mission death callback,
/// the death broadcast, and the server-roster casualty, exactly like <c>coop.debug.mapevent.kms</c>.
/// </summary>
internal class BattleTeamKillCommands
{
    public static readonly ILogger Logger = LogManager.GetLogger<BattleTeamKillCommands>();

    private const string FinishDeploymentUsage =
@"Usage:
  coop.debug.mapevent.finish_deployment

Finishes the current battle deployment through the native deployment handler.";

    [CommandLineArgumentFunction("finish_deployment", "coop.debug.mapevent")]
    public static string FinishDeployment(List<string> args)
    {
        var ctx = new CommandContext("finish_deployment", FinishDeploymentUsage, args);
        if (!ctx.RequireArgCount(0, out var error))
            return error;

        var mission = Mission.Current;
        if (mission is null)
            return "Failed: no active mission.";

        var deploymentController = mission.GetMissionBehavior<DeploymentMissionController>();
        if (deploymentController == null)
            return "No active deployment.";
        if (!deploymentController.TeamSetupOver)
            return "Failed: deployment team setup is not complete.";

        var deploymentHandler = mission.GetMissionBehavior<DeploymentHandler>();
        if (deploymentHandler == null)
            return "Failed: no deployment handler.";

        deploymentHandler.FinishDeployment();
        return "Finished the current deployment.";
    }

    private const string ToggleScoreboardUsage =
@"Usage:
  coop.debug.mapevent.toggle_scoreboard

Holds or releases the configured scoreboard binding through the native input path.";

    [CommandLineArgumentFunction("toggle_scoreboard", "coop.debug.mapevent")]
    public static string ToggleScoreboard(List<string> args)
    {
        var ctx = new CommandContext("toggle_scoreboard", ToggleScoreboardUsage, args);
        if (!ctx.RequireArgCount(0, out var error))
            return error;

        var mission = Mission.Current;
        if (mission is null)
            return "Failed: no active mission.";

        var scoreboard = mission.GetMissionBehavior<MissionGauntletBattleScore>();
        if (scoreboard?.DataSource == null)
            return "Failed: no battle scoreboard UI.";

        var scoreboardHold = mission.GetMissionBehavior<ScoreboardHoldBehavior>();
        if (scoreboardHold == null)
        {
            var holdShow = HotKeyManager.GetCategory(ScoreboardHotKeyCategory.CategoryId)
                ?.GetHotKey(ScoreboardHotKeyCategory.HoldShow);
            var key = holdShow?.Keys.FirstOrDefault(candidate => candidate.IsKeyboardInput || candidate.IsMouseButtonInput);
            if (key == null)
                return "Failed: the scoreboard has no configured keyboard or mouse binding.";

            mission.AddMissionBehavior(new ScoreboardHoldBehavior(key.InputKey));
            Input.PressKey(key.InputKey);
            return $"Holding the configured scoreboard binding ({key.InputKey}).";
        }

        mission.RemoveMissionBehavior(scoreboardHold);
        return "Released the configured scoreboard binding.";
    }

    private const string ScoreboardStateUsage =
@"Usage:
  coop.debug.mapevent.scoreboard_state

Lists the player parties and party rows currently loaded by the battle scoreboard.";

    [CommandLineArgumentFunction("scoreboard_state", "coop.debug.mapevent")]
    public static string ScoreboardState(List<string> args)
    {
        var ctx = new CommandContext("scoreboard_state", ScoreboardStateUsage, args);
        if (!ctx.RequireArgCount(0, out var error))
            return error;

        var mission = Mission.Current;
        if (mission is null)
            return "Failed: no active mission.";

        var scoreboard = mission.GetMissionBehavior<MissionGauntletBattleScore>();
        var dataSource = scoreboard?.DataSource;
        if (dataSource == null)
            return "Failed: no battle scoreboard UI.";

        var mapEvent = MobileParty.MainParty?.MapEvent;
        if (mapEvent == null)
            return "Failed: the main party has no current map event.";

        var expectedParties = mapEvent.InvolvedParties
            .Where(party => party != null)
            .Distinct()
            .ToArray();
        var expectedPlayerParties = expectedParties
            .Where(party => party.MobileParty?.IsPlayerParty() == true)
            .ToArray();
        if (expectedPlayerParties.Length == 0)
            return "Failed: the current map event has no registered player parties.";
        var scoreboardParties = dataSource.Attackers.Parties
            .Concat(dataSource.Defenders.Parties)
            .Select(party => party.BattleCombatant)
            .OfType<PartyBase>()
            .Distinct()
            .ToArray();
        var missingPlayerParties = expectedPlayerParties.Except(scoreboardParties).ToArray();

        return $"Visible: {dataSource.ShowScoreboard}; " +
               $"Expected player parties ({expectedPlayerParties.Length}): {FormatPartyNames(expectedPlayerParties)}; " +
               $"Scoreboard parties ({scoreboardParties.Length}): {FormatPartyNames(scoreboardParties)}; " +
               $"Missing player parties ({missingPlayerParties.Length}): {FormatPartyNames(missingPlayerParties)}";
    }

    private static string FormatPartyNames(IEnumerable<PartyBase> parties)
    {
        var names = parties.Select(party => party.Name?.ToString() ?? "<unnamed>").ToArray();
        return names.Length == 0 ? "<none>" : string.Join(", ", names);
    }

    private sealed class ScoreboardHoldBehavior : MissionLogic
    {
        private readonly InputKey inputKey;

        public ScoreboardHoldBehavior(InputKey inputKey)
        {
            this.inputKey = inputKey;
        }

        public override void OnMissionTick(float dt)
        {
            base.OnMissionTick(dt);
            Input.PressKey(inputKey);
        }
    }

    private const string LeaveBattleUsage =
@"Usage:
  coop.debug.mapevent.leave_battle

Leaves the current battle through the native mission lifecycle.";

    [CommandLineArgumentFunction("leave_battle", "coop.debug.mapevent")]
    public static string LeaveBattle(List<string> args)
    {
        var ctx = new CommandContext("leave_battle", LeaveBattleUsage, args);
        if (!ctx.RequireArgCount(0, out var error))
            return error;

        var mission = Mission.Current;
        if (mission is null)
            return "Failed: no active mission.";

        mission.EndMission();
        return "Left the current battle mission.";
    }

    private const string KillEnemyUsage =
@"Usage:
  coop.debug.mapevent.kill_enemy

Kills one live enemy-team agent in the current battle (battle-authority side).";

    [CommandLineArgumentFunction("kill_enemy", "coop.debug.mapevent")]
    public static string KillOneEnemy(List<string> args)
    {
        var ctx = new CommandContext("kill_one_enemy", KillEnemyUsage, args);
        if (!ctx.RequireArgCount(0, out var error))
            return error;

        if (!TryGetEnemyAgents(out var agents, out var failure))
            return failure;

        var agent = agents.FirstOrDefault();
        if (agent is null)
            return "No live enemy agents to kill.";

        try
        {
            Kill(agent);
        }
        catch (Exception ex)
        {
            return CommandHelpers.FormatException("Kill enemy", ex);
        }

        return $"Killed enemy agent: {agent.Name}";
    }

    private const string KillEnemyTeamUsage =
@"Usage:
  coop.debug.mapevent.kill_enemy_team

Kills every live enemy-team agent in the current battle (battle-authority side). Useful for testing a coop battle WIN.";

    [CommandLineArgumentFunction("kill_enemy_team", "coop.debug.mapevent")]
    public static string KillEnemyTeam(List<string> args)
    {
        var ctx = new CommandContext("kill_enemy_team", KillEnemyTeamUsage, args);
        if (!ctx.RequireArgCount(0, out var error))
            return error;

        if (!TryGetEnemyAgents(out var agents, out var failure))
            return failure;

        var killed = KillAll(agents, out var ex);
        if (ex != null)
            return CommandHelpers.FormatException("Kill enemy team", ex);

        return $"Killed {killed} enemy agent(s).";
    }

    private const string KillOwnTeamUsage =
@"Usage:
  coop.debug.mapevent.kill_own_team

Kills every live agent on the local player team in the current battle (battle-authority side). Useful for testing a
coop battle LOSS.";

    [CommandLineArgumentFunction("kill_own_team", "coop.debug.mapevent")]
    public static string KillOwnTeam(List<string> args)
    {
        var ctx = new CommandContext("kill_own_team", KillOwnTeamUsage, args);
        if (!ctx.RequireArgCount(0, out var error))
            return error;

        var mission = Mission.Current;
        if (mission is null)
            return "Failed: no active mission.";
        if (mission.PlayerTeam is null)
            return "Failed: no player team in this mission.";

        var agents = mission.PlayerTeam.ActiveAgents.ToList();
        var killed = KillAll(agents, out var ex);
        if (ex != null)
            return CommandHelpers.FormatException("Kill own team", ex);

        return $"Killed {killed} agent(s) on the local player team.";
    }

    /// <summary>Live agents on any team hostile to the player (host) team.</summary>
    private static bool TryGetEnemyAgents(out List<Agent> agents, out string failure)
    {
        agents = null;
        failure = null;

        var mission = Mission.Current;
        if (mission is null) { failure = "Failed: no active mission."; return false; }
        if (mission.PlayerTeam is null) { failure = "Failed: no player team in this mission."; return false; }

        agents = mission.Agents
            .Where(a => a != null && a.IsActive() && a.Team != null && a.Team.IsEnemyOf(mission.PlayerTeam))
            .ToList();
        return true;
    }

    private static int KillAll(List<Agent> agents, out Exception error)
    {
        error = null;
        var killed = 0;
        foreach (var agent in agents)
        {
            if (agent is null || !agent.IsActive())
                continue;
            try
            {
                Kill(agent);
                killed++;
            }
            catch (Exception ex)
            {
                error = ex;
                break;
            }
        }
        return killed;
    }

    private static void Kill(Agent agent)
    {
        var blow = new Blow(agent.Index)
        {
            DamageType = DamageTypes.Pierce,
            BaseMagnitude = 100000f,
            InflictedDamage = 100000,
            DamagedPercentage = 1f,
            DamageCalculated = true,
            GlobalPosition = agent.Position,
            VictimBodyPart = BoneBodyPartType.Head,
        };
        agent.Die(blow, Agent.KillInfo.Invalid);
    }
}
