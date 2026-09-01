using Common.Logging;
using GameInterface.Services.MapEvents.Patches;
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
  coop.debug.mapevent.kms [force]

Removes the main agent using the current killed/unconscious roll.
Pass force during an active co-op battle to guarantee a killed result.";

    [CommandLineArgumentFunction("kms", "coop.debug.mapevent")]
    public static string KillPlayerAgent(List<string> args)
    {
        var ctx = new CommandContext(
            "kill_player_agent",
            KillPlayerAgentUsage,
            args);

        bool forceDeath = ctx.Args.Count == 1 && string.Equals(ctx.Args[0], "force", StringComparison.OrdinalIgnoreCase);
        if (ctx.Args.Count > 1 || (ctx.Args.Count == 1 && !forceDeath)) return KillPlayerAgentUsage;

        if (Mission.Current is null)
            return "Failed to kill player agent: no active mission.";

        var agent = Agent.Main;
        if (agent is null)
            return "Failed to kill player agent: Agent.Main is null (player has no agent in this mission).";

        if (!agent.IsActive())
            return "Failed to kill player agent: main agent is not active (already dead or removed).";

        if (forceDeath && (!BattleSpawnConfig.Enabled || !BattleSpawnGate.IsCoopBattleActive))
            return "Failed to force-kill player agent: no active co-op battle.";

        try
        {
            var blow = CreateFatalBlow(agent);
            if (forceDeath)
                ForceCommandDeathPatch.RunWithForcedDeath(agent, () => agent.Die(blow, Agent.KillInfo.Invalid));
            else
                agent.Die(blow, Agent.KillInfo.Invalid);
        }
        catch (Exception ex)
        {
            return CommandHelpers.FormatException("Kill player agent", ex);
        }

        return forceDeath
            ? $"Force-killed player agent: {agent.Name}"
            : $"Removed player agent using the current survival chance: {agent.Name}";
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
