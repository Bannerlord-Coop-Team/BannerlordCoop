using Common;
using Common.Logging;
using Common.Messaging;
using Coop.Core.Server.Connections.Messages;
using Coop.Core.Server.Services.Session.Messages;
using Serilog;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Timer = System.Timers.Timer;

namespace Coop.Core.Server.Services.Telemetry;

public interface IServerTelemetryReporter : IDisposable
{
}

/// <summary>Reports the server status at a fixed interval while it is listening.</summary>
public class ServerTelemetryReporter : IServerTelemetryReporter
{
    private static readonly ILogger Logger = LogManager.GetLogger<ServerTelemetryReporter>();
    internal static readonly TimeSpan ReportInterval = TimeSpan.FromSeconds(60);

    private readonly IMessageBroker messageBroker;
    private readonly IServerTelemetryUploader uploader;
    private readonly CancellationTokenSource cancellation;
    private readonly Func<DateTime> getUtcNow;
    private readonly Func<string> createSessionId;
    private readonly Func<string> createServerId;
    private readonly object reportGate = new object();
    private readonly Timer reportTimer;

    private string sessionId;
    private string serverId;
    private DateTime startedAt;
    private int playerCount;
    private bool started;
    private bool disposed;

    public ServerTelemetryReporter(
        IMessageBroker messageBroker,
        IServerTelemetryUploader uploader,
        IServerTelemetryIdentity identity,
        CancellationTokenSource sessionCancellation)
        : this(
            messageBroker,
            uploader,
            sessionCancellation,
            ReportInterval,
            () => DateTime.UtcNow,
            () => Guid.NewGuid().ToString("N"),
            () => identity.ServerId)
    {
    }

    internal ServerTelemetryReporter(
        IMessageBroker messageBroker,
        IServerTelemetryUploader uploader,
        CancellationTokenSource sessionCancellation,
        TimeSpan reportInterval,
        Func<DateTime> getUtcNow,
        Func<string> createSessionId)
        : this(
            messageBroker,
            uploader,
            sessionCancellation,
            reportInterval,
            getUtcNow,
            createSessionId,
            createSessionId)
    {
    }

    internal ServerTelemetryReporter(
        IMessageBroker messageBroker,
        IServerTelemetryUploader uploader,
        CancellationTokenSource sessionCancellation,
        TimeSpan reportInterval,
        Func<DateTime> getUtcNow,
        Func<string> createSessionId,
        Func<string> createServerId)
    {
        if (messageBroker == null) throw new ArgumentNullException(nameof(messageBroker));
        if (uploader == null) throw new ArgumentNullException(nameof(uploader));
        if (sessionCancellation == null) throw new ArgumentNullException(nameof(sessionCancellation));
        if (reportInterval <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(reportInterval));
        if (getUtcNow == null) throw new ArgumentNullException(nameof(getUtcNow));
        if (createSessionId == null) throw new ArgumentNullException(nameof(createSessionId));
        if (createServerId == null) throw new ArgumentNullException(nameof(createServerId));

        this.messageBroker = messageBroker;
        this.uploader = uploader;
        this.getUtcNow = getUtcNow;
        this.createSessionId = createSessionId;
        this.createServerId = createServerId;
        cancellation = CancellationTokenSource.CreateLinkedTokenSource(sessionCancellation.Token);

        reportTimer = new Timer(reportInterval.TotalMilliseconds) { AutoReset = false };
        reportTimer.Elapsed += Handle_ReportTimerElapsed;

        messageBroker.Subscribe<ServerListening>(Handle_ServerListening);
        messageBroker.Subscribe<ConnectedPlayersChanged>(Handle_ConnectedPlayersChanged);
    }

    private void Handle_ServerListening(MessagePayload<ServerListening> _)
    {
        lock (reportGate)
        {
            if (disposed || started) return;

            sessionId = createSessionId();
            serverId = createServerId();
            startedAt = getUtcNow().ToUniversalTime();
            started = true;
            reportTimer.Start();
        }
    }

    private void Handle_ConnectedPlayersChanged(MessagePayload<ConnectedPlayersChanged> payload)
    {
        Volatile.Write(ref playerCount, Math.Max(0, payload.What.ConnectedPlayers));
    }

    private void Handle_ReportTimerElapsed(object sender, ElapsedEventArgs e)
    {
        ServerTelemetryStatus status;
        lock (reportGate)
        {
            if (disposed || !started) return;

            status = new ServerTelemetryStatus(
                serverId,
                sessionId,
                ModInformation.Version.ToString(),
                ModInformation.Commit,
                startedAt,
                Volatile.Read(ref playerCount));
        }

        _ = UploadAndScheduleNextAsync(status);
    }

    private async Task UploadAndScheduleNextAsync(ServerTelemetryStatus status)
    {
        try
        {
            var result = await uploader.UploadAsync(status, cancellation.Token).ConfigureAwait(false);
            if (!result.Uploaded && result.EndpointConfigured)
            {
                Logger.Warning("Reporting server telemetry failed: {Details}", result.Details);
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Reporting server telemetry failed");
        }
        finally
        {
            lock (reportGate)
            {
                if (!disposed) reportTimer.Start();
            }
        }
    }

    public void Dispose()
    {
        lock (reportGate)
        {
            if (disposed) return;

            disposed = true;
            cancellation.Cancel();
            reportTimer.Elapsed -= Handle_ReportTimerElapsed;
            reportTimer.Stop();
            reportTimer.Dispose();
        }

        messageBroker.Unsubscribe<ServerListening>(Handle_ServerListening);
        messageBroker.Unsubscribe<ConnectedPlayersChanged>(Handle_ConnectedPlayersChanged);
        cancellation.Dispose();
    }
}
