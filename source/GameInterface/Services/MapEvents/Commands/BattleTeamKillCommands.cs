using Common.Logging;
using GameInterface.Utils.Commands;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.MapEvents.Commands;

/// <summary>
/// Battle-outcome test commands: kill one enemy, the whole enemy team, or the local player team in the current
/// battle mission. Run the direct kill commands on the battle-authority client because it owns the AI/enemy
/// agents, so each kill goes through the coop death path: <c>Agent.Die</c>, the mission death callback,
/// the death broadcast, and the server-roster casualty, exactly like <c>coop.debug.mapevent.kms</c>. Use
/// <c>kill_enemy_puppet</c> from a client that sees the enemy as a puppet to test routed ally kills.
/// </summary>
internal class BattleTeamKillCommands
{
    public static readonly ILogger Logger = LogManager.GetLogger<BattleTeamKillCommands>();

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

    private const string KillEnemyPuppetUsage =
@"Usage:
  coop.debug.mapevent.kill_enemy_puppet

Routes a fatal blow from a locally controlled ally to one remote-owned enemy puppet.";

    [CommandLineArgumentFunction("kill_enemy_puppet", "coop.debug.mapevent")]
    public static string KillEnemyPuppet(List<string> args)
    {
        var ctx = new CommandContext("kill_enemy_puppet", KillEnemyPuppetUsage, args);
        if (!ctx.RequireArgCount(0, out var error))
            return error;

        var mission = Mission.Current;
        if (mission is null)
            return "Failed: no active mission.";
        if (mission.PlayerTeam is null)
            return "Failed: no player team in this mission.";

        var attacker = mission.PlayerTeam.ActiveAgents
            .FirstOrDefault(agent => agent != null && agent.IsActive() && agent.Controller == AgentControllerType.AI)
            ?? Agent.Main;
        if (attacker == null || !attacker.IsActive())
            return "Failed: no locally controlled allied agent.";

        var victim = mission.Agents.FirstOrDefault(agent =>
            agent != null
            && agent.IsActive()
            && agent.IsHuman
            && agent.Controller == AgentControllerType.None
            && agent.Team != null
            && agent.Team.IsEnemyOf(mission.PlayerTeam));
        if (victim == null)
            return "Failed: no remote-owned enemy puppet.";

        try
        {
            var blow = CreateFatalBlow(victim, attacker.Index);
            victim.RegisterBlow(blow, default);
        }
        catch (Exception ex)
        {
            return CommandHelpers.FormatException("Kill enemy puppet", ex);
        }

        return $"Routed fatal blow from {attacker.Name} to enemy puppet {victim.Name}.";
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
        var blow = CreateFatalBlow(agent, agent.Index);
        agent.Die(blow, Agent.KillInfo.Invalid);
    }

    private static Blow CreateFatalBlow(Agent victim, int ownerId)
    {
        return new Blow(ownerId)
        {
            DamageType = DamageTypes.Pierce,
            BaseMagnitude = 100000f,
            InflictedDamage = 100000,
            DamagedPercentage = 1f,
            DamageCalculated = true,
            GlobalPosition = victim.Position,
            VictimBodyPart = BoneBodyPartType.Head,
        };
    }
}
