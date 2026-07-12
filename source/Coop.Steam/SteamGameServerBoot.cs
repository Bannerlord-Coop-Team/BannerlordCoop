using Common;
using Common.Logging;
using Serilog;
using Steamworks;
using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Coop.Steam;

/// <summary>
/// Adds an anonymous game-server Steam session beside the engine's user session so a standalone
/// process can listen for P2P without a playing owner. <see cref="RunCallbacks"/> must be pumped.
/// </summary>
public static class SteamGameServerBoot
{
    private static readonly ILogger Logger = LogManager.GetLogger(typeof(SteamGameServerBoot));

    // Nominal ports: discovery is via Steam lobbies and the tunnel is Steam P2P (virtual port 0),
    // so these are only game-server metadata, not a reachable bind the owner has to forward.
    private const ushort GamePort = 27315;
    private const ushort QueryPort = 27316;
    private const string AppId = "261550";
    private const string ModDir = "bannerlordcoop";

    private static bool started;
    private static int shutDown;

    // Strong roots: the game-server callback dispatcher holds these weakly.
    private static Callback<SteamServersConnected_t> connectedCallback;
    private static Callback<SteamServerConnectFailure_t> connectFailureCallback;

    private static volatile bool isLoggedOn;

    /// <summary>True once the anonymous logon has completed and the identity is known.</summary>
    public static bool IsLoggedOn => isLoggedOn;

    /// <summary>The game server's Steam identity; the connect target joiners tunnel to. 0 until logged on.</summary>
    public static ulong GameServerSteamId { get; private set; }

    /// <summary>
    /// The server's public IP as Steam sees it, or empty when unknown. Seeds the advertised
    /// address as an editable default; a detected IP is not a promise it is reachable.
    /// </summary>
    public static string PublicIp { get; private set; } = string.Empty;

    /// <summary>Raised on the callback pump thread once the logon completes.</summary>
    public static event Action LoggedOn;

    public static bool TryStart()
    {
        if (started) return true;

        try
        {
            return Boot();
        }
        catch (Exception ex)
        {
            // Steamworks.NET.dll absent (non-Steam install) or the Steam client is not running.
            Logger.Information("Game-server Steam login unavailable: {Reason}", ex.Message);
            return false;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool Boot()
    {
        if (!SteamAPI.IsSteamRunning())
        {
            Logger.Information("Steam is not running; the server will not listen on Steam");
            return false;
        }

        if (!GameServer.Init(0, GamePort, QueryPort, EServerMode.eServerModeNoAuthentication,
            ModInformation.Version.ToString()))
        {
            Logger.Error("GameServer.Init failed; the server will not listen on Steam");
            return false;
        }

        started = true;

        SteamGameServer.SetProduct(AppId);
        SteamGameServer.SetGameDescription("BannerlordCoop");
        SteamGameServer.SetModDir(ModDir);
        SteamGameServer.SetDedicatedServer(true);

        connectedCallback = Callback<SteamServersConnected_t>.CreateGameServer(OnLoggedOn);
        connectFailureCallback = Callback<SteamServerConnectFailure_t>.CreateGameServer(OnLogonFailed);

        // The only teardown hook that reaches this assembly and fires for both a standalone server
        // (window closed) and a managed server (Utilities.QuitGame); ServerShutdown lives in
        // GameInterface, which cannot reference Coop.Steam.
        AppDomain.CurrentDomain.ProcessExit += (_, _) => Shutdown();

        SteamGameServer.LogOnAnonymous();
        Logger.Information("Game-server anonymous logon requested");
        return true;
    }

    /// <summary>Dispatches the game-server callbacks; called every tick by the pump.</summary>
    public static void RunCallbacks()
    {
        if (started) GameServer.RunCallbacks();
    }

    private static void OnLoggedOn(SteamServersConnected_t _)
    {
        try
        {
            GameServerSteamId = SteamGameServer.GetSteamID().m_SteamID;
            PublicIp = FormatPublicIp(SteamGameServer.GetPublicIP());
            SteamGameServerNetworkingUtils.InitRelayNetworkAccess();
            isLoggedOn = true;

            Logger.Information("Game server logged on: id={Id} publicIp={PublicIp}",
                GameServerSteamId.ToString(), PublicIp);
            LoggedOn?.Invoke();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Game-server logon completion failed");
        }
    }

    private static void OnLogonFailed(SteamServerConnectFailure_t failure)
    {
        Logger.Error("Game-server logon failed: {Result} stillRetrying={Retrying}",
            failure.m_eResult, failure.m_bStillRetrying);
    }

    private static string FormatPublicIp(SteamIPAddress_t ip)
    {
        // Only IPv4 is advertised as the direct-connect fallback; 0 means Steam has no public IP yet.
        if (!ip.IsSet() || ip.GetIPType() != ESteamIPType.k_ESteamIPTypeIPv4) return string.Empty;
        return ip.ToIPAddress().ToString();
    }

    public static void Shutdown()
    {
        if (!started) return;
        if (Interlocked.Exchange(ref shutDown, 1) != 0) return;

        try
        {
            SteamGameServer.LogOff();
            GameServer.Shutdown();
            Logger.Information("Game server shut down");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Game-server shutdown failed");
        }
    }
}
