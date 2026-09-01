using Coop.Core.Server.Services.Telemetry;
using System.Threading;
using System.Threading.Tasks;

namespace Coop.Tests.Mocks;

/// <summary>Prevents server test containers from sending external telemetry.</summary>
public sealed class MockServerTelemetryUploader :
    IServerTelemetryUploader,
    IBattlesFoughtUploader
{
    private static readonly Task<ServerTelemetryUploadResult> DisabledResult = Task.FromResult(
        new ServerTelemetryUploadResult(false, false, "Disabled in tests."));

    public bool IsConfigured => false;

    public Task<ServerTelemetryUploadResult> UploadAsync(
        ServerTelemetryStatus status,
        CancellationToken cancellationToken) => DisabledResult;

    public Task<ServerTelemetryUploadResult> RecordBattleStartedAsync(
        CancellationToken cancellationToken) => DisabledResult;
}
