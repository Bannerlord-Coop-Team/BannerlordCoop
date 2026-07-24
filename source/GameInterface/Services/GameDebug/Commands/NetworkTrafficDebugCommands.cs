#if DEBUG
using Common;
using Common.Network;
using System;
using System.Collections.Generic;
using System.Globalization;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.GameDebug.Commands;

/// <summary>Drives a bounded client-side network blackhole for automated timeout tests.</summary>
internal static class NetworkTrafficDebugCommands
{
    [CommandLineArgumentFunction("traffic", "coop.debug.network")]
    public static string Traffic(List<string> args)
    {
        if (!ModInformation.IsClient)
            return "Run this command on a client.";

        if (!ContainerProvider.TryResolve<IDebugNetworkTrafficControl>(out var traffic))
            return "DEBUG network traffic control is unavailable.";

        if (args.Count == 1 && args[0].Equals("status", StringComparison.OrdinalIgnoreCase))
            return FormatStatus(traffic);

        if (args.Count == 1 && args[0].Equals("resume", StringComparison.OrdinalIgnoreCase))
        {
            traffic.ResumeTraffic();
            return FormatStatus(traffic);
        }

        if (args.Count == 2 &&
            args[0].Equals("pause", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(args[1], NumberStyles.None, CultureInfo.InvariantCulture, out var seconds) &&
            seconds >= 1 &&
            seconds <= 120)
        {
            traffic.PauseTraffic(TimeSpan.FromSeconds(seconds));
            return FormatStatus(traffic);
        }

        return "Usage: coop.debug.network.traffic <status|resume|pause 1-120>";
    }

    private static string FormatStatus(IDebugNetworkTrafficControl traffic)
    {
        var until = traffic.TrafficPausedUntilUtc;
        return until.HasValue
            ? $"Network traffic paused until {until.Value:O}."
            : "Network traffic active.";
    }
}
#endif
