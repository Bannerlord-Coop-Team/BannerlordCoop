using Common.Logging;
using Serilog;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Coop.Core.Server.Services.Telemetry;

/// <summary>Keeps DEBUG servers off the production server statistics.</summary>
public sealed class DisabledServerTelemetryUploader : IServerTelemetryUploader, IBattlesFoughtUploader
{
    private const string Reason = "Server statistics are off in this DEBUG build.";

    private static readonly Task<ServerTelemetryUploadResult> DisabledResult = Task.FromResult(
        new ServerTelemetryUploadResult(false, false, Reason));

    private readonly ILogger logger;
    private int heartbeatSkipLogged;
    private int battleSkipLogged;

    public bool IsConfigured => false;

    public DisabledServerTelemetryUploader() : this(LogManager.GetLogger<DisabledServerTelemetryUploader>())
    {
    }

    internal DisabledServerTelemetryUploader(ILogger logger)
    {
        if (logger == null) throw new ArgumentNullException(nameof(logger));

        this.logger = logger;
        logger.Information("Server statistics are off in this DEBUG build; no heartbeat or battle count is sent");
    }

    public Task<ServerTelemetryUploadResult> UploadAsync(
        ServerTelemetryStatus status,
        CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref heartbeatSkipLogged, 1) == 0)
        {
            logger.Debug(
                "Skipped the first server statistics heartbeat in this DEBUG build; later ones are skipped without a log line");
        }

        return DisabledResult;
    }

    public Task<ServerTelemetryUploadResult> RecordBattleStartedAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref battleSkipLogged, 1) == 0)
        {
            logger.Debug(
                "Skipped the first battles fought count in this DEBUG build; later ones are skipped without a log line");
        }

        return DisabledResult;
    }
}
