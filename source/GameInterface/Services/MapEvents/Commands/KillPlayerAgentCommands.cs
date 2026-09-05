using Common.Commands;
using Common.Logging;
using GameInterface.Services.MapEvents.Patches;
using GameInterface.Utils.Commands;
using Serilog;
using System;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.MapEvents.Commands;

internal class KillPlayerAgentCommands
{
    private static CoopCommandResult Succeeded(string output) =>
        new CoopCommandResult(true, output);

    private static CoopCommandResult Failed(string output) =>
        new CoopCommandResult(false, output, "command_failed");

    public static readonly ILogger Logger = LogManager.GetLogger<KillPlayerAgentCommands>();

    public sealed class KmsCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.map_event";

        public string Name => "kms";

        public string Description => "Removes the main agent using the current survival roll, or forces death during an active co-op battle.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("force", "Use force to guarantee a killed result during an active co-op battle.", false),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            bool forceDeath = args.Count == 1 &&
                              string.Equals(args[0], "force", StringComparison.OrdinalIgnoreCase);
            if (args.Count == 1 && !forceDeath)
                return Failed("The optional argument must be 'force'.");

            if (Mission.Current is null)
                return Failed("Failed to kill player agent: no active mission.");

            var agent = Agent.Main;
            if (agent is null)
                return Failed("Failed to kill player agent: Agent.Main is null (player has no agent in this mission).");

            if (!agent.IsActive())
                return Failed("Failed to kill player agent: main agent is not active (already dead or removed).");

            if (forceDeath && (!BattleSpawnConfig.Enabled || !BattleSpawnGate.IsCoopBattleActive))
                return Failed("Failed to force-kill player agent: no active co-op battle.");

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
                return Failed(CommandHelpers.FormatException("Kill player agent", ex));
            }

            return forceDeath
                ? Succeeded($"Force-killed player agent: {agent.Name}")
                : Succeeded($"Removed player agent using the current survival chance: {agent.Name}");
        }
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
