using Common.Commands;
using Common.Logging;
using GameInterface.Utils.Commands;
using Serilog;
using System;
using System.Collections.Generic;
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

        public string Description => "Runs the kms debug operation.";

        public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (Mission.Current is null)
                return Failed("Failed to kill player agent: no active mission.");

            var agent = Agent.Main;
            if (agent is null)
                return Failed("Failed to kill player agent: Agent.Main is null (player has no agent in this mission).");

            if (!agent.IsActive())
                return Failed("Failed to kill player agent: main agent is not active (already dead or removed).");

            try
            {
                var blow = CreateFatalBlow(agent);
                agent.Die(blow, Agent.KillInfo.Invalid);
            }
            catch (Exception ex)
            {
                return Failed(CommandHelpers.FormatException("Kill player agent", ex));
            }

            return Succeeded($"Killed player agent: {agent.Name}");
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
