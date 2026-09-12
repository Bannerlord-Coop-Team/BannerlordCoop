using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network.Session;
using Coop.Steam;
using Serilog;
using System;
using System.Runtime.CompilerServices;

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
        TryStartWithCallbackPump(isServerProcess, commandLine);
    }

    /// <summary>Starts Steam integration and returns the standalone server's callback pump, if any.</summary>
    public static IUpdateable TryStartWithCallbackPump(bool isServerProcess, string commandLine)
    {
        if (started) return null;
        started = true;

        // The dedicated server logs into Steam as a game server so friends can join it directly.
        if (isServerProcess)
        {
            try
            {
                return TryStartServer();
            }
            catch (Exception ex)
            {
                // Also catches type-load/JIT failures before the non-inlined helper can enter its own guard.
                Logger.Warning(ex, "Server Steam integration unavailable");
                return null;
            }
        }

        try
        {
            SessionDiscovery.SteamAvailable = SteamBoot.TryStart(MessageBroker.Instance, commandLine);
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Steam integration unavailable");
            return null;
        }

        if (SessionDiscovery.SteamAvailable)
        {
            Logger.Information("Steam integration active");
        }
        else
        {
            Logger.Information("Steam integration inactive; see SteamBoot diagnostics above");
        }

        return null;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static IUpdateable TryStartServer()
    {
        try
        {
            SessionDiscovery.SteamAvailable = SteamGameServerBoot.TryStart();
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Server Steam integration unavailable");
            return null;
        }

        if (!SessionDiscovery.SteamAvailable)
        {
            Logger.Information(
                "Server Steam integration inactive; initialization did not reach anonymous logon. See SteamGameServerBoot diagnostics above");
            return null;
        }

        Logger.Information("Server Steam game-server initialized; anonymous logon pending");
        return new GameServerCallbackPump();
    }
}
