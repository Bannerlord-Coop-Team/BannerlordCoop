using Common;
using Common.Logging;
using Galaxy.Api;
using Serilog;
using System;
using System.Threading;

namespace Coop.GOG;

/// <summary>Optional Galaxy game-server boot using only operator-supplied credentials.</summary>
internal static class GalaxyGameServerBoot
{
    private const string ClientIdVariable = "BANNERLORDCOOP_GOG_CLIENT_ID";
    private const string ClientSecretVariable = "BANNERLORDCOOP_GOG_CLIENT_SECRET";
    private const string ServerKeyVariable = "BANNERLORDCOOP_GOG_SERVER_KEY";

    private static readonly Serilog.ILogger Logger = LogManager.GetLogger(typeof(GalaxyGameServerBoot));
    private static AuthListener authListener;
    private static volatile bool started;
    private static volatile bool isReady;
    private static int shutDown;

    public static bool HasConfiguredCredentials =>
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ClientIdVariable)) &&
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ClientSecretVariable)) &&
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ServerKeyVariable));

    public static bool IsReady => isReady;
    public static event Action Ready;

    public static bool TryStart()
    {
        if (started) return true;
        if (!HasConfiguredCredentials)
        {
            Logger.Information(
                "GOG game-server integration inactive; operator Galaxy credentials are not configured");
            return false;
        }

        bool initialized = false;
        AuthListener listener = null;
        try
        {
            var initParams = new InitParams(
                Environment.GetEnvironmentVariable(ClientIdVariable),
                Environment.GetEnvironmentVariable(ClientSecretVariable));
            GalaxyInstance.InitGameServer(initParams);
            initialized = true;
            listener = new AuthListener();
            authListener = listener;
            GalaxyInstance.GameServerUser().SignInServerKey(
                Environment.GetEnvironmentVariable(ServerKeyVariable),
                listener);
            isReady = false;
            Interlocked.Exchange(ref shutDown, 0);
            started = true;
            AppDomain.CurrentDomain.ProcessExit += HandleProcessExit;
            Logger.Information("Galaxy game-server sign-in requested");
            return true;
        }
        catch (Exception ex)
        {
            listener?.Dispose();
            authListener = null;
            isReady = false;
            started = false;
            if (initialized)
            {
                try
                {
                    GalaxyInstance.ShutdownGameServer();
                }
                catch (Exception cleanupFailure)
                {
                    Logger.Warning(cleanupFailure, "Galaxy game-server initialization cleanup failed");
                }
            }

            Logger.Warning(ex, "Galaxy game-server initialization failed");
            return false;
        }
    }

    public static void ProcessData()
    {
        if (started) GalaxyInstance.ProcessGameServerData();
    }

    public static void Shutdown()
    {
        if (!started || Interlocked.Exchange(ref shutDown, 1) != 0) return;

        started = false;
        isReady = false;
        AppDomain.CurrentDomain.ProcessExit -= HandleProcessExit;
        try
        {
            GalaxyInstance.ShutdownGameServer();
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Galaxy game-server shutdown failed");
        }
        finally
        {
            authListener?.Dispose();
            authListener = null;
            Ready = null;
        }
    }

    private static void HandleProcessExit(object sender, EventArgs args) => Shutdown();

    private sealed class AuthListener : IAuthListener
    {
        public override void OnAuthSuccess()
        {
            if (!started) return;

            isReady = true;
            Logger.Information(
                "Galaxy game server authenticated as {GalaxyId}",
                GalaxyInstance.GameServerUser().GetGalaxyID().ToUint64().ToString());
            Ready?.Invoke();
        }

        public override void OnAuthFailure(FailureReason failureReason)
        {
            isReady = false;
            Logger.Error("Galaxy game-server authentication failed: {Reason}", failureReason);
        }

        public override void OnAuthLost()
        {
            isReady = false;
            Logger.Warning("Galaxy game-server authentication was lost");
        }
    }
}

internal sealed class GalaxyGameServerCallbackPump : IUpdateable
{
    public int Priority => UpdatePriority.MainLoop.PlatformCallbacks;
    public void Update(TimeSpan frameTime) => GalaxyGameServerBoot.ProcessData();
}
