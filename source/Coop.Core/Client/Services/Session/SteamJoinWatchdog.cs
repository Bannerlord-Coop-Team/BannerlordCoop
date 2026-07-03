using Common;
using Common.Logging;
using Common.Messaging;
using Coop.Core.Client.Messages;
using Coop.Core.Common.Services.Connection.Messages;
using GameInterface.Services.GameDebug.Messages;
using Serilog;
using System;
using System.Threading;

namespace Coop.Core.Client.Services.Session;

/// <summary>
/// Bounds a discovery-initiated join: the connect retry loop otherwise redials forever, so
/// if no connection comes up within the timeout, the player gets a popup naming the
/// endpoint and the session attempt is ended.
/// </summary>
public class SteamJoinWatchdog : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<SteamJoinWatchdog>();

    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    private readonly IMessageBroker messageBroker;

    private Timer timer;
    private volatile bool connected;
    private volatile bool disposed;
    private string address;
    private int port;
    private bool tunneled;

    public SteamJoinWatchdog(IMessageBroker messageBroker)
    {
        this.messageBroker = messageBroker;

        messageBroker.Subscribe<NetworkConnected>(Handle_NetworkConnected);
    }

    public void Dispose()
    {
        disposed = true;
        messageBroker.Unsubscribe<NetworkConnected>(Handle_NetworkConnected);
        timer?.Dispose();
        timer = null;
    }

    public void Arm(string address, int port, bool tunneled = false)
    {
        if (connected) return;

        this.address = address;
        this.port = port;
        this.tunneled = tunneled;
        timer = new Timer(_ => OnTimeout(), null, Timeout, System.Threading.Timeout.InfiniteTimeSpan);

        // The connect can complete while the timer is being created; drop it then.
        if (connected)
        {
            timer?.Dispose();
            timer = null;
        }
    }

    internal void Handle_NetworkConnected(MessagePayload<NetworkConnected> obj)
    {
        connected = true;
        timer?.Dispose();
        timer = null;
    }

    private void OnTimeout()
    {
        // Decide on the game thread (where every other EndCoopMode publish runs), rechecking
        // connected at execution time so a connect racing the deadline is never torn down.
        GameThread.RunSafe(() =>
        {
            // Also skip when disposed: a stale queued action must never end a newer session.
            if (connected || disposed) return;

            Logger.Warning("Steam-initiated join to {Address}:{Port} (tunneled={Tunneled}) timed out", address, port, tunneled);

            // A tunneled join dials a local pump, so port-forwarding advice would be wrong there.
            string popupText = tunneled
                ? "Could not reach the co-op host through Steam. Make sure the host is still in their session, then try the invite again."
                : $"Could not reach the co-op host at {address}:{port}. " +
                  $"The host must port-forward UDP {port} and set their public address on the co-op screen.";

            messageBroker.Publish(this, new SendPopupMessage(popupText));
            messageBroker.Publish(this, new EndCoopMode());
        }, context: "SteamJoinTimeout");
    }
}
