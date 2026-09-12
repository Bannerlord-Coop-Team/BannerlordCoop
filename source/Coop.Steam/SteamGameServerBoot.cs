using Common;
using Common.Logging;
using Serilog;
using Steamworks;
using System;
using System.Linq;
using System.Net.NetworkInformation;
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

    // Steam receives these during native game-server initialization. Player traffic still uses
    // the P2P tunnel (virtual port 0), so neither port replaces the direct-IP gameplay port.
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
            Logger.Warning(ex,
                "Game-server Steam startup threw before completion; the server will not listen on Steam");
            return false;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool Boot()
    {
        bool steamRunning = SteamAPI.IsSteamRunning();
        if (!steamRunning)
        {
            Logger.Warning("Steam game-server unavailable: SteamAPI.IsSteamRunning returned false");
            return false;
        }

        string runtimeAppId = GetClientRuntimeAppId();
        string version = ModInformation.Version.ToString();
        var steamworksAssembly = typeof(SteamAPI).Assembly;
        Logger.Information(
            "Calling GameServer.Init: expectedAppId={ExpectedAppId} runtimeAppId={RuntimeAppId} SteamAppId={SteamAppId} gamePort={GamePort} queryPort={QueryPort} udpListeners={UdpListeners} version={Version} cwd={WorkingDirectory} wrapperVersion={WrapperVersion} wrapperPath={WrapperPath}",
            AppId, runtimeAppId, Environment.GetEnvironmentVariable("SteamAppId") ?? "<unset>",
            GamePort, QueryPort, GetUdpListenerState(), version, Environment.CurrentDirectory,
            steamworksAssembly.GetName().Version, steamworksAssembly.Location);
        if (!GameServer.Init(0, GamePort, QueryPort, EServerMode.eServerModeNoAuthentication,
            version))
        {
            Logger.Error(
                "GameServer.Init returned false; anonymous logon was not attempted. Check the preceding App ID, port, and wrapper context");
            return false;
        }

        started = true;

        SteamGameServer.SetProduct(AppId);
        SteamGameServer.SetGameDescription("Bannerlord Coop");
        SteamGameServer.SetModDir(ModDir);
        SteamGameServer.SetDedicatedServer(true);

        connectedCallback = Callback<SteamServersConnected_t>.CreateGameServer(OnLoggedOn);
        connectFailureCallback = Callback<SteamServerConnectFailure_t>.CreateGameServer(OnLogonFailed);

        // The teardown hook that reaches this assembly when the user closes either kind of
        // standalone server process. GameInterface cannot reference Coop.Steam directly.
        AppDomain.CurrentDomain.ProcessExit += (_, _) => Shutdown();

        SteamGameServer.LogOnAnonymous();
        Logger.Information("Game-server anonymous logon requested");
        return true;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static string GetClientRuntimeAppId()
    {
        try
        {
            AppId_t appId = SteamUtils.GetAppID();
            return appId.m_AppId.ToString();
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Steam runtime App ID could not be read by the server process");
            return "<unavailable>";
        }
    }

    private static string GetUdpListenerState()
    {
        try
        {
            var listeners = IPGlobalProperties.GetIPGlobalProperties().GetActiveUdpListeners();
            return $"{GamePort}={listeners.Any(x => x.Port == GamePort)}, "
                + $"{QueryPort}={listeners.Any(x => x.Port == QueryPort)}";
        }
        catch (Exception ex)
        {
            return $"unavailable ({ex.GetType().Name})";
        }
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
