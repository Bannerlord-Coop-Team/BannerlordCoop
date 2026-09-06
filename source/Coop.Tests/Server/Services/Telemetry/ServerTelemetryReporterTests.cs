using Common;
using Common.Tests.Utils;
using Coop.Core.Server.Connections.Messages;
using Coop.Core.Server.Services.Session.Messages;
using Coop.Core.Server.Services.Telemetry;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Coop.Tests.Server.Services.Telemetry;

public class ServerTelemetryReporterTests
{
    [Fact]
    public async Task ServerListening_StartsSixtySecondStatusReportsWithStableSessionData()
    {
        var messageBroker = new TestMessageBroker();
        var uploader = new RecordingTelemetryUploader(expectedReports: 2);
        using var sessionCancellation = new CancellationTokenSource();
        var startedAt = new DateTime(2026, 8, 29, 22, 15, 0, DateTimeKind.Utc);
        using var reporter = new ServerTelemetryReporter(
            messageBroker,
            uploader,
            sessionCancellation,
            TimeSpan.FromMilliseconds(20),
            () => startedAt,
            () => "41fa0000000000000000000000000000");

        messageBroker.Publish(this, new ConnectedPlayersChanged(5));
        await Task.Delay(60);
        Assert.Empty(uploader.Statuses);

        messageBroker.Publish(this, new ServerListening());
        await uploader.ExpectedReportsReceived.Task.WaitAsync(TimeSpan.FromSeconds(2));

        var statuses = uploader.Statuses.Take(2).ToArray();
        Assert.Equal(TimeSpan.FromSeconds(60), ServerTelemetryReporter.ReportInterval);
        Assert.Equal(2, statuses.Length);
        Assert.All(statuses, status =>
        {
            Assert.Equal("41fa0000000000000000000000000000", status.SessionId);
            Assert.Equal(ModInformation.Version.ToString(), status.ModVersion);
            Assert.Equal(ModInformation.Commit, status.Commit);
            Assert.Equal(startedAt, status.StartedAt);
            Assert.Equal(5, status.PlayerCount);
        });
    }

    private sealed class RecordingTelemetryUploader : IServerTelemetryUploader
    {
        private readonly int expectedReports;
        private int reportCount;

        public bool IsConfigured => true;
        public ConcurrentQueue<ServerTelemetryStatus> Statuses { get; } = new();
        public TaskCompletionSource<bool> ExpectedReportsReceived { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public RecordingTelemetryUploader(int expectedReports)
        {
            this.expectedReports = expectedReports;
        }

        public Task<ServerTelemetryUploadResult> UploadAsync(
            ServerTelemetryStatus status,
            CancellationToken cancellationToken)
        {
            Statuses.Enqueue(status);
            if (Interlocked.Increment(ref reportCount) >= expectedReports)
                ExpectedReportsReceived.TrySetResult(true);

            return Task.FromResult(new ServerTelemetryUploadResult(true, true, "accepted"));
        }
    }
}
