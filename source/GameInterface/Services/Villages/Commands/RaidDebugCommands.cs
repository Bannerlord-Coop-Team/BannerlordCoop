using Common.Commands;
using Autofac;
using Common;
using Common.Network;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Handlers;
using GameInterface.Services.MapEvents.Messages;
using System.Collections.Generic;
namespace GameInterface.Services.Villages.Commands;

public class RaidDebugCommands
{
    private static CoopCommandResult Succeeded(string output) =>
        new CoopCommandResult(true, output);

    private static CoopCommandResult Failed(string output) =>
        new CoopCommandResult(false, output, "command_failed");

    public sealed class AllowRaidAiInterventionCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.mapevent";

        public string Name => "allow_raid_ai_intervention";

        public string Description => "Controls raid ai intervention for co-op debugging.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("mode", "The mode."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {

            var value = args[0].ToLowerInvariant();
            switch (value)
            {
                case "on":
                case "true":
                case "1":
                    return Succeeded(ApplyRaidAiInterventionConfig(true));
                case "off":
                case "false":
                case "0":
                    return Succeeded(ApplyRaidAiInterventionConfig(false));
                case "toggle":
                    return Succeeded(ApplyRaidAiInterventionConfig(!MapEventConfig.AllowRaidAiIntervention));
                case "status":
                    return Succeeded(RaidAiInterventionConfigHandler.StatusText);
                default:
                    return Failed("Invalid action. Use on, off, toggle, or status.");
            }

        }
    }

    private static string ApplyRaidAiInterventionConfig(bool allow)
    {
        MapEventConfig.AllowRaidAiIntervention = allow;

        if (ModInformation.IsServer)
        {
            if (ContainerProvider.TryResolve<RaidAiInterventionConfigHandler>(out var handler))
                handler.SetAndBroadcast(allow);

            return RaidAiInterventionConfigHandler.StatusText;
        }

        if (ContainerProvider.TryResolve<INetwork>(out var network))
            network.SendAll(new NetworkRequestRaidAiInterventionConfigChange(allow));

        return RaidAiInterventionConfigHandler.StatusText + " (server update requested)";
    }
}
