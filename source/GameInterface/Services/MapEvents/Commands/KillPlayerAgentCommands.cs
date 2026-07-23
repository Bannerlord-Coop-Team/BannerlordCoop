using Common.Logging;
using GameInterface.Utils.Commands;
using Serilog;
using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.MapEvents.Commands;

internal class KillPlayerAgentCommands
{
    public static readonly ILogger Logger = LogManager.GetLogger<KillPlayerAgentCommands>();

    private const string KillPlayerAgentUsage =
@"Usage:
  coop.debug.mapevent.kms

Kills the main agent (the player) in the current battle mission.
Useful for testing player captivity without waiting to die.";

    [CommandLineArgumentFunction("kms", "coop.debug.mapevent")]
    public static string KillPlayerAgent(List<string> args)
    {
        var ctx = new CommandContext(
            "kill_player_agent",
            KillPlayerAgentUsage,
            args);

        if (!ctx.RequireArgCount(0, out var error))
            return error;

        if (Mission.Current is null)
            return "Failed to kill player agent: no active mission.";

        var agent = Agent.Main;
        if (agent is null)
            return "Failed to kill player agent: Agent.Main is null (player has no agent in this mission).";

        if (!agent.IsActive())
            return "Failed to kill player agent: main agent is not active (already dead or removed).";

        try
        {
            var blow = CreateFatalBlow(agent);
            agent.Die(blow, Agent.KillInfo.Invalid);
        }
        catch (Exception ex)
        {
            return CommandHelpers.FormatException("Kill player agent", ex);
        }

        return $"Killed player agent: {agent.Name}";
    }

    private static Blow CreateFatalBlow(Agent agent)
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

        return blow;
    }
}
