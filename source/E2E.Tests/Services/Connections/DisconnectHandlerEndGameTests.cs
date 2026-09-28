using Common;
using Common.Tests.Utils;
using Common.Util;
using Coop.Core.Client.Messages;
using Coop.Core.Client.Services.Connection.Handlers;
using Coop.Core.Common;
using Coop.Core.Common.Services.Connection.Messages;
using GameInterface.Services.GameDebug.Messages;
using GameInterface.Services.GameState.Interfaces;
using GameInterface.Services.UI.Interfaces;
using LiteNetLib;
using Moq;
using System.Collections.Concurrent;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace E2E.Tests.Services.Connections;

/// <summary>
/// Verifies a client disconnect still leaves the campaign once and then ends coop when its blocking
/// <see cref="GameStateInterface.EndGame"/> times out. Lives in E2E because it needs an isolated game-thread queue.
/// </summary>
public class DisconnectHandlerEndGameTests
{
    private const string TimeoutText =
        "Connection to the co-op server timed out.\nCheck your internet connection and try joining again.";
    private static readonly TimeSpan ShortTimeout = TimeSpan.FromMilliseconds(200);
    private static readonly TimeSpan LongTimeout = TimeSpan.FromSeconds(10);

    [Fact]
    public void DisconnectWhoseEndGameTimesOutBeforeItStarts_LeavesTheCampaignOnceThenEndsCoop()
    {
        using var fixture = new Fixture();
        using var shortTimeout = GameThread.Instance.LimitFrameDrain(TimeSpan.FromSeconds(1), ShortTimeout);

        // Nothing pumps while the poller waits, so EndGame and then the finalizer's hide expire before they start.
        Assert.Null(fixture.DisconnectOnWorker().Join());
        Assert.Empty(fixture.Order);

        fixture.PumpUntilIdle();

        Assert.Equal(new[] { "EndGame", "HideLoadingScreen" }, fixture.Order);
        Assert.Single(fixture.Broker.GetMessagesFromType<EndCoopMode>());
        Assert.Equal(TimeoutText, Assert.Single(fixture.Broker.GetMessagesFromType<SendPopupMessage>()).Text);
    }

    [Fact]
    public void DisconnectWhoseEndGameTimesOutWhileItRuns_LeavesTheCampaignOnceThenEndsCoop()
    {
        using var fixture = new Fixture();
        using var shortTimeout = GameThread.Instance.LimitFrameDrain(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
        Disconnect? disconnect = null;
        bool pollerReturnedDuringEndGame = false;
        fixture.DuringEndGame = () => pollerReturnedDuringEndGame = disconnect!.TryJoin(LongTimeout);
        disconnect = fixture.DisconnectOnWorker();
        fixture.WaitUntilQueued();

        // EndGame starts and holds the game thread until the poller gave up on it and on the finalizer's hide.
        fixture.PumpUntilIdle();

        Assert.True(pollerReturnedDuringEndGame, "the poller did not time out while EndGame ran");
        Assert.Null(disconnect.Join());
        Assert.Equal(new[] { "EndGame", "HideLoadingScreen" }, fixture.Order);
        Assert.Single(fixture.Broker.GetMessagesFromType<EndCoopMode>());
        Assert.Single(fixture.Broker.GetMessagesFromType<SendPopupMessage>());
    }

    /// <summary>A connected client in a campaign, on its own game-thread queue.</summary>
    private sealed class Fixture : IDisposable
    {
        private readonly int previousGameThreadId = GameThread.Instance.GameThreadId;
        private readonly Campaign previousCampaign = Campaign.Current;
        private readonly Game previousGame = Game._current;
        private readonly IDisposable queueScope;
        private readonly CancellationTokenSource session = new();
        private readonly GameThread.QueueContext queue = new();
        private readonly ConcurrentQueue<string> order = new();
        private readonly DisconnectHandler handler;

        public TestMessageBroker Broker { get; } = new();
        public Action? DuringEndGame { get; set; }
        public string[] Order => order.ToArray();

        public Fixture()
        {
            queueScope = GameThread.ActivateQueue(queue);
            GameThread.Instance.MarkGameThread();

            // GoToMainMenu only ends a running game.
            Campaign.Current = ObjectHelper.SkipConstructor<Campaign>();
            Game._current = ObjectHelper.SkipConstructor<Game>();

            var loadingScreen = new Mock<ILoadingInterface>();
            loadingScreen.Setup(value => value.HideLoadingScreen()).Callback(() => order.Enqueue("HideLoadingScreen"));
            var gameState = new GameStateInterface(Broker, () =>
            {
                DuringEndGame?.Invoke();
                order.Enqueue("EndGame");
            });
            handler = new DisconnectHandler(Broker, new CoopFinalizer(Broker, loadingScreen.Object), gameState);
        }

        /// <summary>Delivers the disconnect on a worker inside the session, as the network poller does.</summary>
        public Disconnect DisconnectOnWorker() => new(() =>
        {
            using (GameThread.ActivateCancellation(session.Token))
            {
                Broker.Publish(this, new NetworkDisconnected(new DisconnectInfo { Reason = DisconnectReason.Timeout }, null));
            }
        });

        public void WaitUntilQueued() =>
            Assert.True(SpinWait.SpinUntil(() => queue.Count > 0, LongTimeout), "EndGame was never queued");

        public void PumpUntilIdle()
        {
            for (int pump = 0; pump < 10 && queue.Count > 0; pump++)
                GameThread.Instance.Update(TimeSpan.Zero);
            Assert.Equal(0, queue.Count);
        }

        public void Dispose()
        {
            try
            {
                handler.Dispose();
                GameThread.Instance.DiscardQueuedActions(queue);
            }
            finally
            {
                Campaign.Current = previousCampaign;
                Game._current = previousGame;
                queueScope.Dispose();
                GameThread.Instance.RestoreGameThread(previousGameThreadId);
                session.Dispose();
            }
        }
    }

    /// <summary>
    /// The disconnect on its own thread. Task.Run is avoided because a waiting test thread, which is the marked
    /// game thread, may run the task inline.
    /// </summary>
    private sealed class Disconnect
    {
        private readonly Thread thread;
        private Exception? failure;

        public Disconnect(Action deliver)
        {
            thread = new Thread(() =>
            {
                try
                {
                    deliver();
                }
                catch (Exception exception)
                {
                    failure = exception;
                }
            }) { IsBackground = true };
            thread.Start();
        }

        public bool TryJoin(TimeSpan timeout) => thread.Join(timeout);

        public Exception? Join()
        {
            Assert.True(thread.Join(LongTimeout), "the disconnect did not return");
            return failure;
        }
    }
}
