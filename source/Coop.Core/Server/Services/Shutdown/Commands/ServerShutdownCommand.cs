using Common;
using Common.Commands;
using System;
using System.Globalization;

namespace Coop.Core.Server.Services.Shutdown.Commands;

/// <summary>Operator command for a graceful restart, compiled into Release and DEBUG builds.</summary>
public class ServerShutdownCommand
{
    private const string Usage = "Usage: coop.server.shutdown <seconds|cancel|status> [save_name]";

    private static CoopCommandResult Succeeded(string output) =>
        new CoopCommandResult(true, output);

    private static CoopCommandResult Failed(string output) =>
        new CoopCommandResult(false, output, "command_failed");

    // coop.server.shutdown <seconds|cancel|status> [save_name]
    public sealed class ShutdownCoopCommand : ICoopCommand
    {
        private readonly IServerShutdownCoordinator coordinator;

        public ShutdownCoopCommand(IServerShutdownCoordinator coordinator)
        {
            this.coordinator = coordinator;
        }

        public string Prefix => "coop.server";

        public string Name => "shutdown";

        public string Description => "Warns players, closes joins, disconnects everyone, then saves.";

        public CoopCommandSide Side => CoopCommandSide.Server;

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("seconds", "Seconds from 0 through 3600 before the restart, or cancel, or status.", isRequired: true),
            new ExpectedArgs("save_name", "The save to write, 1 through 64 letters, digits, underscores, or hyphens. Defaults to the loaded save.", isRequired: false),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (!ModInformation.IsServer)
                return Failed("Command can only be run on the server.");

            string action = args[0];
            if (string.Equals(action, "status", StringComparison.OrdinalIgnoreCase))
                return args.Count == 1 ? Succeeded(coordinator.DescribeStatus()) : Failed(Usage);

            if (string.Equals(action, "cancel", StringComparison.OrdinalIgnoreCase))
            {
                if (args.Count != 1) return Failed(Usage);
                return coordinator.TryCancel(out string cancelResult) ? Succeeded(cancelResult) : Failed(cancelResult);
            }

            if (!int.TryParse(action, NumberStyles.None, CultureInfo.InvariantCulture, out int seconds) ||
                seconds > ServerShutdownCoordinator.MaxDelaySeconds)
            {
                return Failed($"Seconds must be a whole number from 0 through {ServerShutdownCoordinator.MaxDelaySeconds}. {Usage}");
            }

            string saveName = args.Count > 1 ? args[1] : null;
            return coordinator.TrySchedule(seconds, saveName, out string result) ? Succeeded(result) : Failed(result);
        }
    }
}
