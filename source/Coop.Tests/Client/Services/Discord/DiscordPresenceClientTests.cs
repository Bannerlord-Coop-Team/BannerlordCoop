using Coop.Core.Client.Services.Discord;
using DiscordRPC;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Coop.Tests.Client.Services.Discord;

public class DiscordPresenceClientTests : IAsyncLifetime
{
    private readonly FakeDiscordRpcConnection connection = new FakeDiscordRpcConnection();
    private readonly DiscordPresenceClient client;
    private readonly DateTime startedAt = new DateTime(2026, 9, 7, 12, 0, 0, DateTimeKind.Utc);

    public DiscordPresenceClientTests()
    {
        client = new DiscordPresenceClient(connection);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReadyOverlappingUpdate_ReappliesLatestInsteadOfInitialNullOrStaleCampaign(bool reconnect)
    {
        if (reconnect)
        {
            client.SetPresence("In a co-op campaign", "1 player", startedAt);
            await Drain();
        }
        var capturedByReady = connection.CurrentPresence;

        client.SetPresence("Fighting a battle", "3 players", startedAt);
        await Drain();
        connection.CompleteReady(capturedByReady);
        await Drain();

        Assert.Equal("Fighting a battle", connection.CurrentPresence!.Details);
        Assert.Equal("3 players", connection.CurrentPresence.State);
        Assert.Equal(startedAt, connection.CurrentPresence.Timestamps.Start);
        Assert.Equal(DiscordPresenceClient.ArtworkKey, connection.CurrentPresence.Assets.LargeImageKey);
        Assert.Equal(1, connection.InitializeCalls);
    }

    [Fact]
    public async Task ReadyOverlappingClear_ReappliesNullInsteadOfStaleActivity()
    {
        client.SetPresence("Fighting a battle", "3 players", startedAt);
        await Drain();
        var capturedByReady = connection.CurrentPresence;

        client.ClearPresence();
        await Drain();
        connection.CompleteReady(capturedByReady);
        await Drain();

        Assert.Null(connection.CurrentPresence);
    }

    [Fact]
    public async Task ReadyQueuedBehindClear_ReadsSnapshotWhenItsActionRuns()
    {
        client.SetPresence("In a co-op campaign", "1 player", startedAt);
        await Drain();
        var capturedByReady = connection.CurrentPresence;
        var enteredWrite = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var releaseWrite = new ManualResetEventSlim();
        connection.BeforeNextWrite = () =>
        {
            enteredWrite.SetResult(true);
            if (!releaseWrite.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException();
        };

        client.SetPresence("Fighting a battle", "3 players", startedAt);
        try
        {
            await enteredWrite.Task.WaitAsync(TimeSpan.FromSeconds(10));
            client.ClearPresence();
            connection.CompleteReady(capturedByReady);
        }
        finally
        {
            releaseWrite.Set();
        }
        await Drain();

        Assert.Null(connection.CurrentPresence);
    }

    [Fact]
    public async Task DisposeAfterReady_ClearsAndIgnoresInFlightCallbacksAndLaterRequests()
    {
        client.SetPresence("Fighting a battle", "3 players", startedAt);
        await Drain();
        var inFlightReady = connection.CaptureReadyCallback();
        connection.CompleteReady(null);
        client.Dispose();
        await Drain();
        int writes = connection.WriteCalls;

        inFlightReady();
        client.SetPresence("In a co-op campaign", "1 player", startedAt);
        client.ClearPresence();
        client.Dispose();
        await Drain();

        Assert.Null(connection.CurrentPresence);
        Assert.Equal(writes, connection.WriteCalls);
        Assert.Equal(1, connection.DisposeCalls);
        Assert.Equal(0, connection.ReadySubscriberCount);
    }

    [Fact]
    public async Task ClearAndDisposeBeforeFirstActivity_DoNotInitializeDiscord()
    {
        client.ClearPresence();
        client.Dispose();
        await Drain();

        Assert.Equal(0, connection.InitializeCalls);
        Assert.Equal(0, connection.WriteCalls);
        Assert.Equal(1, connection.DisposeCalls);
    }

    private Task Drain() => client.PendingWork.WaitAsync(TimeSpan.FromSeconds(10));

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        client.Dispose();
        await Drain();
    }

    private sealed class FakeDiscordRpcConnection : IDiscordRpcConnection
    {
        public event Action? Ready;
        public RichPresence? CurrentPresence;
        public Action? BeforeNextWrite;
        public int InitializeCalls;
        public int WriteCalls;
        public int DisposeCalls;
        public int ReadySubscriberCount => Ready?.GetInvocationList().Length ?? 0;

        public bool Initialize()
        {
            InitializeCalls++;
            return true;
        }

        public void SetPresence(RichPresence? presence)
        {
            Interlocked.Exchange(ref BeforeNextWrite, null)?.Invoke();
            CurrentPresence = presence;
            WriteCalls++;
        }

        public Action CaptureReadyCallback() => Ready!;

        public void CompleteReady(RichPresence? capturedPresence)
        {
            // DiscordRPC 1.6.1.70 synchronizes its captured value before invoking OnReady.
            CurrentPresence = capturedPresence;
            Ready?.Invoke();
        }

        public void Dispose()
        {
            DisposeCalls++;
            CurrentPresence = null;
        }
    }
}
