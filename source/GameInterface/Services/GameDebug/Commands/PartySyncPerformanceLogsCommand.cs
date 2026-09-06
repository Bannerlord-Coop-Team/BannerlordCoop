using Common.Commands;
using Common;
using GameInterface.Services.GameDebug.Metrics;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.GameDebug.Commands;

public class PartySyncPerformanceLogsCommand
{
    private static CoopCommandResult Succeeded(string output) =>
        new CoopCommandResult(true, output);

    private static CoopCommandResult Failed(string output) =>
        new CoopCommandResult(false, output, "command_failed");

    public sealed class MetricsPartySyncPerformanceLogsCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.metrics";

        public string Name => "party_sync_performance_logs";

        public string Description => "Runs the party sync performance logs debug operation.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("mode", "on, off, or status.", isRequired: true),
            new ExpectedArgs("seconds", "The logging duration in seconds when enabling.", isRequired: false),
            new ExpectedArgs("file_name", "The output file name when enabling.", isRequired: false),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsServer)
            {
                return Failed("party_sync_performance_logs can only be called by a client");
            }

            if (ContainerProvider.TryResolve<IPartySyncPerformanceLogger>(out var logger) == false)
            {
                return Failed($"Unable to get {nameof(IPartySyncPerformanceLogger)}");
            }

            var mode = args[0].ToLowerInvariant();

            switch (mode)
            {
                case "on":
                    return Enable(logger, args);
                case "off":
                    return Succeeded(logger.Disable());
                case "status":
                    return Succeeded(logger.Status());
                default:
                    return Failed($"Invalid mode '{args[0]}'. Expected on, off, or status.");
            }
        }
    }

    private static CoopCommandResult Enable(IPartySyncPerformanceLogger logger, IReadOnlyList<string> args)
    {
        if (args.Count != 3)
            return Failed("Enabling performance logs requires seconds and a file name.");

        if (!double.TryParse(args[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) ||
            seconds <= 0d)
        {
            return Failed("Seconds must be a positive number");
        }

        string fileName = args[2];
        if (string.IsNullOrWhiteSpace(fileName))
            return Failed("File name must not be empty.");
        if (Path.GetFileName(fileName) != fileName || fileName.Contains(".."))
            return Failed("File name must not include a path.");
        if (fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            return Failed("File name contains invalid characters.");

        return Succeeded(logger.Enable(TimeSpan.FromSeconds(seconds), fileName));
    }
}
