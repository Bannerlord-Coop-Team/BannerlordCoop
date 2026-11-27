using Autofac;
using Common;
using Coop.Core.Server.Connections;
using Coop.Core.Server.Connections.States;
using Coop.Tests.Mocks;
using GameInterface.Services.Heroes.Interfaces;
using LiteNetLib;
using Moq;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Coop.Tests.Server.Connections.States;

/// <summary>Isolates the test that pauses the shared game-thread pump and shortens its timeout.</summary>
[CollectionDefinition(nameof(TransferSaveSnapshotCollection), DisableParallelization = true)]
public class TransferSaveSnapshotCollection { }

/// <summary>Exercises a snapshot that finishes after the poll thread stops waiting.</summary>
[Collection(nameof(TransferSaveSnapshotCollection))]
public class TransferSaveSnapshotTests
{
    private readonly ITestOutputHelper output;

    public TransferSaveSnapshotTests(ITestOutputHelper output)
    {
        this.output = output;
    }

    [Fact]
    public async Task SnapshotFinishesAfterTimeout_DoesNotRestartCatchUpOrLoad()
    {
        var serverComponent = new ServerTestComponent(output);
        var container = serverComponent.Container;
        var playerPeer = container.Resolve<TestNetwork>().CreatePeer();
        var context = container.Resolve<ConnectionContext>();
        var queue = context.ConnectionMessageQueue;
        queue.RegisterPeer(playerPeer);
        var saveStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var releaseSave = new ManualResetEventSlim();
        container.Resolve<Mock<ISaveInterface>>()
            .Setup(save => save.SaveCurrentGame())
            .Returns(() =>
            {
                saveStarted.SetResult(true);
                releaseSave.Wait();
                return new SaveResults(true, new byte[] { 1, 2, 3 }, "12345");
            });
        var logic = new Mock<IConnectionLogic>();
        logic.SetupGet(value => value.Peer).Returns(playerPeer);
        var acknowledgedPlayer = serverComponent.BindPeerToPlayer(playerPeer);

        var testTimeout = TimeSpan.FromSeconds(10);
        TransferSaveState state;
        try
        {
            using var shortWait = GameThread.Instance.LimitFrameDrain(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
            var transfer = Task.Run(() => new TransferSaveState(
                logic.Object, context.MessageBroker, context.Network, context.CoopSessionProvider, context.SaveInterface,
                queue, context.Coalescer, context.AttachmentIdMapper, context.ServerOptionsProvider, context.LootShipHold,
                context.PlayerManager, acknowledgedPlayer));

            await saveStarted.Task.WaitAsync(testTimeout);
            state = await transfer.WaitAsync(testTimeout);
            Assert.Equal(ConnectionState.ShutdownRequested, playerPeer.ConnectionState);
        }
        finally
        {
            releaseSave.Set();
            // Drain the late snapshot before checking whether it reopened the replay queue.
            GameThread.Run(() => { }, blocking: true);
        }

        state.Load();
        Assert.False(queue.TryGetCatchUpPendingBytes(playerPeer, out _));
        logic.Verify(value => value.SetState<LoadingState>(), Times.Never);
    }
}
