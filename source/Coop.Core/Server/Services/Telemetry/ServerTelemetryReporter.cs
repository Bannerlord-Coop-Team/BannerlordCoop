using Common;
using Common.Logging;
using Common.Messaging;
using Coop.Core.Server.Connections.Messages;
using Coop.Core.Server.Services.Session.Messages;
using Serilog;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Coop.Core.Server.Services.Telemetry;

public interface IServerTelemetryReporter : IDisposable
{
}

/// <summary>Reports server status while listening and skips unchanged player counts until a refresh is due.</summary>
public class ServerTelemetryReporter : IServerTelemetryReporter
{
    private static readonly ILogger Logger = LogManager.GetLogger<ServerTelemetryReporter>();
    // TickInterval * UnchangedRefreshTicks (90s) must stay under the backend's two-minute statistics freshness window.
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(30);
    private const int UnchangedRefreshTicks = 3;

    private readonly IMessageBroker messageBroker;
    private readonly IServerTelemetryUploader uploader;
    private readonly CancellationTokenSource cancellation;
    private readonly Func<DateTime> getUtcNow;
    private readonly Func<string> createSessionId;
    private readonly Func<CancellationToken, Task> waitForTick;
    private readonly object reportGate = new object();

    private int playerCount;
    private bool started;
    private bool disposed;

    public ServerTelemetryReporter(
        IMessageBroker messageBroker,
        IServerTelemetryUploader uploader,
        CancellationTokenSource sessionCancellation)
        : this(
            messageBroker,
            uploader,
            sessionCancellation,
            () => DateTime.UtcNow,
            () => Guid.NewGuid().ToString("N"),
            ct => Task.Delay(TickInterval, ct))
    {
    }

    internal ServerTelemetryReporter(
        IMessageBroker messageBroker,
        IServerTelemetryUploader uploader,
        CancellationTokenSource sessionCancellation,
        Func<DateTime> getUtcNow,
        Func<string> createSessionId,
        Func<CancellationToken, Task> waitForTick)
    {
        if (messageBroker == null) throw new ArgumentNullException(nameof(messageBroker));
        if (uploader == null) throw new ArgumentNullException(nameof(uploader));
        if (sessionCancellation == null) throw new ArgumentNullException(nameof(sessionCancellation));
        if (getUtcNow == null) throw new ArgumentNullException(nameof(getUtcNow));
        if (createSessionId == null) throw new ArgumentNullException(nameof(createSessionId));
        this.messageBroker = messageBroker;
        this.uploader = uploader;
        this.getUtcNow = getUtcNow;
        this.createSessionId = createSessionId;
        this.waitForTick = waitForTick;
        cancellation = CancellationTokenSource.CreateLinkedTokenSource(sessionCancellation.Token);

        messageBroker.Subscribe<ServerListening>(Handle_ServerListening);
        messageBroker.Subscribe<ConnectedPlayersChanged>(Handle_ConnectedPlayersChanged);
    }

    private void Handle_ServerListening(MessagePayload<ServerListening> _) => StartReporting();

    private void StartReporting()
    {
        string sessionId;
        DateTime startedAt;
        CancellationToken cancellationToken;
        lock (reportGate)
        {
            if (disposed || started) return;

            sessionId = createSessionId();
            startedAt = getUtcNow().ToUniversalTime();
            cancellationToken = cancellation.Token;
            started = true;
        }

        _ = ReportPeriodicallyAsync(sessionId, startedAt, cancellationToken);
    }

    private void Handle_ConnectedPlayersChanged(MessagePayload<ConnectedPlayersChanged> payload)
    {
        Volatile.Write(ref playerCount, Math.Max(0, payload.What.ConnectedPlayers));
    }

    private async Task ReportPeriodicallyAsync(
        string sessionId,
        DateTime startedAt,
        CancellationToken cancellationToken)
    {
        int? lastUploadedPlayerCount = null;
        var ticksSinceUpload = 0;
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await waitForTick(cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();

                ticksSinceUpload++;
                var currentPlayerCount = Volatile.Read(ref playerCount);
                var refreshDue = ticksSinceUpload >= UnchangedRefreshTicks;
                if (lastUploadedPlayerCount == currentPlayerCount && !refreshDue) continue;

                var status = new ServerTelemetryStatus(
                    sessionId,
                    ModInformation.Version.ToString(),
                    ModInformation.Commit,
                    startedAt,
                    currentPlayerCount);
                if (await TryUploadAsync(status, cancellationToken).ConfigureAwait(false))
                {
                    lastUploadedPlayerCount = status.PlayerCount;
                    ticksSinceUpload = 0;
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private async Task<bool> TryUploadAsync(ServerTelemetryStatus status, CancellationToken cancellationToken)
    {
        try
        {
            var result = await uploader.UploadAsync(status, cancellationToken).ConfigureAwait(false);
            if (result.Uploaded)
            {
                return true;
            }
            else if (result.EndpointConfigured)
            {
                Logger.Warning("Reporting server telemetry failed: {Details}", result.Details);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Reporting server telemetry failed");
        }

        return false;
    }

    public void Dispose()
    {
        lock (reportGate)
        {
            if (disposed) return;

            disposed = true;
            cancellation.Cancel();
        }

        messageBroker.Unsubscribe<ServerListening>(Handle_ServerListening);
        messageBroker.Unsubscribe<ConnectedPlayersChanged>(Handle_ConnectedPlayersChanged);
        cancellation.Dispose();
    }
}
