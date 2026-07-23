using Common.Logging;
using Common.Messaging;
using Serilog;
using Steamworks;
using System;
using System.Runtime.CompilerServices;

namespace Coop.Steam;

/// <summary>
/// Probes the game-initialized Steam runtime and starts the process-lifetime Steam services.
/// The probe and service creation are separate non-inlined methods so a non-Steam install
/// (no Steamworks.NET.dll in the game bin) fails with a catchable load exception here
/// instead of poisoning the caller.
/// </summary>
public static class SteamBoot
{
    private static readonly ILogger Logger = LogManager.GetLogger(typeof(SteamBoot));

    // Strong root: MessageBroker subscriptions are weak references, so the listener must be
    // reachable for the process lifetime or its subscriptions silently die.
    public static SteamJoinListener JoinListener { get; private set; }

    // Strong root for the main-menu browser exposed through SessionDiscovery.
    public static SteamLobbyBrowser LobbyBrowser { get; private set; }

    // Created before any session container exists, so it lives here rather than in DI.
    public static SteamTunnelJoinEndpointPreparer TunnelPreparer { get; private set; }

    public static bool TryStart(IMessageBroker messageBroker, string commandLine)
    {
        if (JoinListener != null) return true;

        bool available;
        try
        {
            available = ProbeSteam();
        }
        catch (Exception ex)
        {
            // Steamworks.NET.dll absent (non-Steam install) or SteamAPI not initialized.
            Logger.Warning(ex, "Steam client bootstrap failed during the runtime probe");
            available = false;
        }

        if (!available) return false;

        CreateServices(messageBroker, commandLine);
        return true;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool ProbeSteam()
    {
        bool steamRunning = SteamAPI.IsSteamRunning();
        if (!steamRunning)
        {
            Logger.Warning("Steam client unavailable: SteamAPI.IsSteamRunning returned false");
            return false;
        }

        CSteamID steamId = SteamUser.GetSteamID();
        bool validSteamId = steamId.IsValid();
        if (!validSteamId)
        {
            Logger.Warning("Steam client unavailable: SteamUser.GetSteamID returned an invalid identity");
        }
        return validSteamId;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void CreateServices(IMessageBroker messageBroker, string commandLine)
    {
        var lobbyApi = new SteamLobbyApi();
        JoinListener = new SteamJoinListener(messageBroker, lobbyApi);
        LobbyBrowser = new SteamLobbyBrowser(lobbyApi);
        Common.Network.Session.SessionDiscovery.SteamLobbyBrowser = LobbyBrowser;
        TunnelPreparer = new SteamTunnelJoinEndpointPreparer();
        JoinListener.ProcessLaunchArguments(commandLine);
    }
}
