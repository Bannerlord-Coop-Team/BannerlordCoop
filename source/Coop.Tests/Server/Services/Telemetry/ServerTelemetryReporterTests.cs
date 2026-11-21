using Common;
using Common.Tests.Utils;
using Coop.Core.Server.Connections.Messages;
using Coop.Core.Server.Services.Session.Messages;
using Coop.Core.Server.Services.Telemetry;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Xunit;

namespace Coop.Tests.Server.Services.Telemetry;

public class ServerTelemetryReporterTests
{
    private static readonly DateTime StartedAt = new(2026, 8, 29, 22, 15, 0, DateTimeKind.Utc);
    private const string SessionId = "41fa0000000000000000000000000000";

    [Fact]
    public async Task ServerListening_ReportsPlayerCountChangesWithStableSessionData()
    {
        var messageBroker = new TestMessageBroker();
        var uploader = new RecordingTelemetryUploader();
        var ticks = new ManualReportTicks();
        using var sessionCancellation = new CancellationTokenSource();
        using var reporter = CreateReporter(messageBroker, uploader, sessionCancellation, ticks);

        messageBroker.Publish(this, new ConnectedPlayersChanged(5));
        Assert.Null(ticks.ScheduledInterval);
        Assert.Empty(uploader.Statuses);

        messageBroker.Publish(this, new ServerListening());
        await ticks.AdvanceAsync();
        Assert.Equal(TimeSpan.FromSeconds(30), ticks.ScheduledInterval);
        Assert.Equal(5, Assert.Single(uploader.Statuses).PlayerCount);

        messageBroker.Publish(this, new ConnectedPlayersChanged(3));
        await ticks.AdvanceAsync();
        Assert.Collection(uploader.Statuses,
            status => Assert.Equal(5, status.PlayerCount),
            status => Assert.Equal(3, status.PlayerCount));
        Assert.All(uploader.Statuses, status =>
        {
            Assert.Equal(SessionId, status.SessionId);
            Assert.Equal(ModInformation.Version.ToString(), status.ModVersion);
            Assert.Equal(ModInformation.Commit, status.Commit);
            Assert.Equal(StartedAt, status.StartedAt);
        });
    }

    [Fact]
    public async Task UnchangedPlayerCount_SkipsTwoTicksAndRefreshesOnTheThird()
    {
        var messageBroker = new TestMessageBroker();
        var uploader = new RecordingTelemetryUploader();
        var ticks = new ManualReportTicks();
        using var sessionCancellation = new CancellationTokenSource();
        using var reporter = CreateReporter(messageBroker, uploader, sessionCancellation, ticks);

        messageBroker.Publish(this, new ServerListening());
        await ticks.AdvanceAsync();
        Assert.Single(uploader.Statuses);

        await ticks.AdvanceAsync();
        await ticks.AdvanceAsync();
        Assert.Single(uploader.Statuses);

        await ticks.AdvanceAsync();
        Assert.Collection(uploader.Statuses,
            status => Assert.Equal(0, status.PlayerCount),
            status => Assert.Equal(0, status.PlayerCount));
    }

    [Fact]
    public async Task FailedRefresh_RetriesOnTheNextTick()
    {
        var messageBroker = new TestMessageBroker();
        var uploader = new RecordingTelemetryUploader();
        var ticks = new ManualReportTicks();
        using var sessionCancellation = new CancellationTokenSource();
        using var reporter = CreateReporter(messageBroker, uploader, sessionCancellation, ticks);

        messageBroker.Publish(this, new ServerListening());
        await ticks.AdvanceAsync();
        await ticks.AdvanceAsync();
        await ticks.AdvanceAsync();
        Assert.Single(uploader.Statuses);

        uploader.UploadSucceeds = false;
        await ticks.AdvanceAsync();
        Assert.Equal(2, uploader.Statuses.Count);

        uploader.UploadSucceeds = true;
        await ticks.AdvanceAsync();
        Assert.Equal(3, uploader.Statuses.Count);

        await ticks.AdvanceAsync();
        Assert.Equal(3, uploader.Statuses.Count);
    }

    private static ServerTelemetryReporter CreateReporter(
        TestMessageBroker messageBroker,
        RecordingTelemetryUploader uploader,
        CancellationTokenSource sessionCancellation,
        ManualReportTicks ticks) =>
        new(
            messageBroker,
            uploader,
            sessionCancellation,
            ServerTelemetryReporter.ReportInterval,
            () => StartedAt,
            () => SessionId,
            ticks.WaitAsync);

    /// <summary>Lets the test release a tick and wait for the reporter to schedule the next one.</summary>
    private sealed class ManualReportTicks
    {
        private readonly Channel<TaskCompletionSource<bool>> scheduledTicks =
            Channel.CreateUnbounded<TaskCompletionSource<bool>>();
        private TaskCompletionSource<bool>? nextTick;

        public TimeSpan? ScheduledInterval { get; private set; }

        public Task WaitAsync(TimeSpan interval, CancellationToken cancellationToken)
        {
            ScheduledInterval = interval;
            var tick = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            scheduledTicks.Writer.TryWrite(tick);
            return tick.Task.WaitAsync(cancellationToken);
        }

        public async Task AdvanceAsync()
        {
            nextTick ??= await WaitForScheduledTickAsync();
            nextTick.SetResult(true);
            nextTick = await WaitForScheduledTickAsync();
        }

        private Task<TaskCompletionSource<bool>> WaitForScheduledTickAsync() =>
            scheduledTicks.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
    }

    /// <summary>Records attempted uploads and lets tests control their outcome.</summary>
    private sealed class RecordingTelemetryUploader : IServerTelemetryUploader
    {
        public bool IsConfigured => true;
        public bool UploadSucceeds { get; set; } = true;
        public List<ServerTelemetryStatus> Statuses { get; } = new();

        public Task<ServerTelemetryUploadResult> UploadAsync(
            ServerTelemetryStatus status,
            CancellationToken cancellationToken)
        {
            Statuses.Add(status);
            return Task.FromResult(new ServerTelemetryUploadResult(UploadSucceeds, true, string.Empty));
        }
    }
}
