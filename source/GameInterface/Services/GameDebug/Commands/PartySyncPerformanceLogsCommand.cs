using Common;
using GameInterface.Services.GameDebug.Metrics;
using System;
using System.Collections.Generic;
using System.Globalization;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.GameDebug.Commands;

public class PartySyncPerformanceLogsCommand
{
    private const string Usage = "Usage: coop.debug.metrics.party_sync_performance_logs on <seconds> <filename> | off | status";

    [CommandLineArgumentFunction("party_sync_performance_logs", "coop.debug.metrics")]
    public static string PartySyncPerformanceLogs(List<string> args)
    {
        if (ModInformation.IsServer)
        {
            return "party_sync_performance_logs can only be called by a client";
        }

        if (ContainerProvider.TryResolve<IPartySyncPerformanceLogger>(out var logger) == false)
        {
            return $"Unable to get {nameof(IPartySyncPerformanceLogger)}";
        }

        if (args.Count == 0)
        {
            return Usage;
        }

        var mode = args[0].ToLowerInvariant();

        switch (mode)
        {
            case "on":
                return Enable(logger, args);
            case "off":
                return logger.Disable();
            case "status":
                return logger.Status();
            default:
                return Usage;
        }
    }

    private static string Enable(IPartySyncPerformanceLogger logger, List<string> args)
    {
        if (args.Count != 3)
        {
            return Usage;
        }

        if (!double.TryParse(args[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) ||
            seconds <= 0d)
        {
            return "Seconds must be a positive number";
        }

        return logger.Enable(TimeSpan.FromSeconds(seconds), args[2]);
    }
}
