using Common.Logging;
using Common.Messaging;
using Common.Network.Session;
using Coop.Steam;
using Serilog;
using System;

namespace Coop.Core.Common.Session;

/// <summary>
/// Starts the process-lifetime Steam services once the main menu is active: SteamAPI is
/// initialized well before then, and a launch-argument join needs the menu to proceed.
/// </summary>
public static class SteamIntegrationBoot
{
    private static readonly ILogger Logger = LogManager.GetLogger(typeof(SteamIntegrationBoot));

    private static bool started;

    public static void TryStart(bool isServerProcess, string commandLine)
    {
        if (started) return;
        started = true;

        // The dedicated server process never initiates or receives Steam joins.
        if (isServerProcess) return;

        try
        {
            SessionDiscovery.SteamAvailable = SteamBoot.TryStart(MessageBroker.Instance, commandLine);
        }
        catch (Exception ex)
        {
            Logger.Information("Steam integration unavailable: {Reason}", ex.Message);
            return;
        }

        if (SessionDiscovery.SteamAvailable)
        {
            Logger.Information("Steam integration active");
        }
        else
        {
            Logger.Information("Steam integration inactive (Steam not running or not a Steam install)");
        }
    }
}
