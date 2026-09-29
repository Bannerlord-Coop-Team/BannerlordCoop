using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Tests.Utils;
using Coop.Core.Client;
using Coop.Core.Client.States;
using Coop.Core.Common;
using Coop.Core.Common.Services.Connection.Messages;
using Coop.Core.Server.Connections.Messages;
using GameInterface.Services.Entity;
using GameInterface.Services.GameDebug.Messages;
using GameInterface.Services.GameState.Interfaces;
using GameInterface.Services.Modules;
using GameInterface.Services.UI.Interfaces;
using Moq;
using System.Collections.Concurrent;

namespace E2E.Tests.Services.UI;

/// <summary>
/// Verifies a module-validation rejection still releases the forced loading screen and ends coop once when
/// the finalizer's blocking hide times out, and that a <see cref="CoopFinalizer.SetCloseText"/> text still shows
/// once. Lives in E2E because it needs an isolated game-thread queue.
/// </summary>
public class CoopFinalizerTests
{
    private const string Reason = "Wrong version of module 'Coop'";
    private const string CloseText = "The server is restarting. Try again in a few minutes.";
    private static readonly TimeSpan ShortTimeout = TimeSpan.FromMilliseconds(200);
    private static readonly TimeSpan LongTimeout = TimeSpan.FromSeconds(10);

    [Fact]
    public void RejectionWhoseHideTimesOutBeforeItStarts_ReleasesTheLoadingScreenAndEndsCoopOnce()
    {
        using var fixture = new Fixture();
        using var shortTimeout = GameThread.Instance.LimitFrameDrain(TimeSpan.FromSeconds(1), ShortTimeout);

        // Nothing pumps while the rejection waits, so its blocking hide expires before it starts.
        Rejection rejection = fixture.RejectOnWorker();
        Exception? failure = rejection.Join();
        Assert.True(fixture.LoadingScreen.Forced);
        Assert.Empty(fixture.Broker.GetMessagesFromType<EndCoopMode>());

        fixture.Pump();
        fixture.Pump();

        Assert.False(fixture.LoadingScreen.Forced, "the forced loading screen was never released");
        Assert.Equal(1, fixture.LoadingScreen.HideCount);
        Assert.Single(fixture.Broker.GetMessagesFromType<EndCoopMode>());
        var popup = Assert.Single(fixture.Broker.GetMessagesFromType<SendPopupMessage>());
        Assert.Contains(Reason, popup.Text);
        Assert.Null(failure);
        Assert.Equal(0, fixture.Queue.Count);
    }

    [Fact]
    public void RejectionWhoseHideTimesOutWhileRunning_EndsCoopOnceWithoutASecondHide()
    {
        var logs = new ConcurrentQueue<string>();
        Action<string> callback = logs.Enqueue;
        OutputSinkManager.AddLogCallback(callback);
        try
        {
            using var fixture = new Fixture();
            using var shortTimeout = GameThread.Instance.LimitFrameDrain(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2));
            Rejection rejection = fixture.RejectOnWorker();
            fixture.WaitUntilQueued();

            // The hide starts and holds the game thread until the rejection gave up on both waits.
            bool rejectionReturnedDuringHide = false;
            fixture.LoadingScreen.DuringHide = () => rejectionReturnedDuringHide = rejection.TryJoin(LongTimeout);
            fixture.Pump();
            fixture.Pump();

            Assert.True(rejectionReturnedDuringHide, "the rejection did not time out while the hide ran");
            Assert.Contains(logs, log => log.Contains("CoopFinalizer.Finalize") && log.Contains("started just before its"));
            Assert.Equal(1, fixture.LoadingScreen.HideCount);
            Assert.False(fixture.LoadingScreen.Forced);
            Assert.Single(fixture.Broker.GetMessagesFromType<EndCoopMode>());
            Assert.Single(fixture.Broker.GetMessagesFromType<SendPopupMessage>());
            Assert.Null(rejection.Join());
            Assert.Equal(0, fixture.Queue.Count);
        }
        finally
        {
            OutputSinkManager.RemoveLogCallback(callback);
        }
    }

    [Fact]
    public void RejectionWhoseHideTimedOut_AfterItsSessionEnded_QueuesNoStaleTeardown()
    {
        using var fixture = new Fixture();
        using var shortTimeout = GameThread.Instance.LimitFrameDrain(TimeSpan.FromSeconds(1), ShortTimeout);
        Assert.Null(fixture.RejectOnWorker().Join());

        // Another teardown ended this session first, so that teardown owns the cleanup.
        fixture.Session.Cancel();
        fixture.Pump();

        Assert.Equal(0, fixture.LoadingScreen.HideCount);
        Assert.Empty(fixture.Broker.GetMessagesFromType<EndCoopMode>());
        Assert.Empty(fixture.Broker.GetMessagesFromType<SendPopupMessage>());
        Assert.Equal(0, fixture.Queue.Count);
    }

    [Fact]
    public void RejectionWhoseQueuedHideThrows_StillEndsCoopOnce()
    {
        using var fixture = new Fixture();
        using var shortTimeout = GameThread.Instance.LimitFrameDrain(TimeSpan.FromSeconds(1), ShortTimeout);

        // The native disable throws after the forced flag was cleared, as LoadingInterface.HideLoadingScreen can.
        fixture.LoadingScreen.HideFailure = new InvalidOperationException("the native loading window failed to close");
        Assert.Null(fixture.RejectOnWorker().Join());
        fixture.Pump();
        fixture.Pump();

        Assert.Equal(1, fixture.LoadingScreen.HideCount);
        Assert.Single(fixture.Broker.GetMessagesFromType<EndCoopMode>());
        Assert.Single(fixture.Broker.GetMessagesFromType<SendPopupMessage>());
        Assert.Equal(0, fixture.Queue.Count);
    }

    [Fact]
    public void RejectionWhoseSessionEndsDuringTheHideWait_ThrowsAtOnceAndQueuesNoTeardown()
    {
        using var fixture = new Fixture();
        Rejection rejection = fixture.RejectOnWorker();
        fixture.WaitUntilQueued();

        // The network shutdown waits for this poll callback, so it must not wait out the 30 s timeout.
        fixture.Session.Cancel();
        Assert.True(rejection.TryJoin(TimeSpan.FromSeconds(5)), "the rejection kept waiting after its session ended");
        fixture.Pump();

        Assert.IsType<OperationCanceledException>(rejection.Join());
        Assert.Equal(0, fixture.LoadingScreen.HideCount);
        Assert.Empty(fixture.Broker.GetMessagesFromType<EndCoopMode>());
        Assert.Equal(0, fixture.Queue.Count);
    }

    [Fact]
    public void RejectionWithACloseTextWhoseHideTimesOutBeforeItStarts_ShowsTheCloseTextOnce()
    {
        using var fixture = new Fixture();
        using var shortTimeout = GameThread.Instance.LimitFrameDrain(TimeSpan.FromSeconds(1), ShortTimeout);

        fixture.Finalizer.SetCloseText(CloseText);
        Assert.Null(fixture.RejectOnWorker().Join());
        fixture.Pump();
        fixture.Pump();
        fixture.Finalizer.ShowCloseText();

        Assert.Equal(CloseText, Assert.Single(fixture.Broker.GetMessagesFromType<SendPopupMessage>()).Text);
        Assert.Single(fixture.Broker.GetMessagesFromType<EndCoopMode>());
        Assert.Equal(1, fixture.LoadingScreen.HideCount);
        Assert.Equal(0, fixture.Queue.Count);
    }

    [Fact]
    public void CloseTextSetAfterTheHideTimedOut_StillWinsInTheQueuedTeardown()
    {
        using var fixture = new Fixture();
        using var shortTimeout = GameThread.Instance.LimitFrameDrain(TimeSpan.FromSeconds(1), ShortTimeout);
        Assert.Null(fixture.RejectOnWorker().Join());

        // A disconnect reaches the poller while the rejection's teardown still waits in the queue.
        fixture.Finalizer.SetCloseText(CloseText);
        fixture.Pump();
        fixture.Pump();
        fixture.Finalizer.ShowCloseText();

        Assert.Equal(CloseText, Assert.Single(fixture.Broker.GetMessagesFromType<SendPopupMessage>()).Text);
        Assert.Single(fixture.Broker.GetMessagesFromType<EndCoopMode>());
        Assert.Equal(0, fixture.Queue.Count);
    }

    [Fact]
    public void RejectionWithACloseTextWhoseHideTimesOutWhileRunning_ShowsTheCloseTextOnce()
    {
        using var fixture = new Fixture();
        using var shortTimeout = GameThread.Instance.LimitFrameDrain(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2));
        fixture.Finalizer.SetCloseText(CloseText);
        Rejection rejection = fixture.RejectOnWorker();
        fixture.WaitUntilQueued();

        bool rejectionReturnedDuringHide = false;
        fixture.LoadingScreen.DuringHide = () => rejectionReturnedDuringHide = rejection.TryJoin(LongTimeout);
        fixture.Pump();
        fixture.Pump();

        Assert.True(rejectionReturnedDuringHide, "the rejection did not time out while the hide ran");
        Assert.Null(rejection.Join());
        Assert.Equal(1, fixture.LoadingScreen.HideCount);
        Assert.Equal(CloseText, Assert.Single(fixture.Broker.GetMessagesFromType<SendPopupMessage>()).Text);
        Assert.Single(fixture.Broker.GetMessagesFromType<EndCoopMode>());
        Assert.Equal(0, fixture.Queue.Count);
    }

    [Fact]
    public void RejectionWithACloseTextWhoseHideTimedOut_AfterItsSessionEnded_LeavesTheCloseTextToShowOnce()
    {
        using var fixture = new Fixture();
        using var shortTimeout = GameThread.Instance.LimitFrameDrain(TimeSpan.FromSeconds(1), ShortTimeout);
        fixture.Finalizer.SetCloseText(CloseText);
        Assert.Null(fixture.RejectOnWorker().Join());

        // The dropped teardown never reached its popup, so the disconnect handler's ShowCloseText still shows it.
        fixture.Session.Cancel();
        fixture.Pump();
        Assert.Empty(fixture.Broker.GetMessagesFromType<SendPopupMessage>());
        fixture.Finalizer.ShowCloseText();
        fixture.Finalizer.ShowCloseText();

        Assert.Equal(CloseText, Assert.Single(fixture.Broker.GetMessagesFromType<SendPopupMessage>()).Text);
        Assert.Empty(fixture.Broker.GetMessagesFromType<EndCoopMode>());
        Assert.Equal(0, fixture.Queue.Count);
    }

    /// <summary>A client in module validation with the forced loading screen up, on its own game-thread queue.</summary>
    private sealed class Fixture : IDisposable
    {
        private readonly int previousGameThreadId = GameThread.Instance.GameThreadId;
        private readonly IDisposable queueScope;
        private readonly ValidateModuleState state;

        public GameThread.QueueContext Queue { get; } = new();
        public CancellationTokenSource Session { get; } = new();
        public TestMessageBroker Broker { get; } = new();
        public ForcedLoadingScreen LoadingScreen { get; } = new();
        public CoopFinalizer Finalizer { get; }

        public Fixture()
        {
            queueScope = GameThread.ActivateQueue(Queue);
            GameThread.Instance.MarkGameThread();

            Finalizer = new CoopFinalizer(Broker, LoadingScreen);
            var logic = new Mock<IClientLogic>();
            state = new ValidateModuleState(
                logic.Object,
                Broker,
                Mock.Of<INetwork>(),
                Mock.Of<IControllerIdProvider>(),
                Finalizer,
                Mock.Of<IGameStateInterface>(),
                Mock.Of<IModuleInfoProvider>());
            logic.SetupGet(value => value.State).Returns(state);
            logic.Setup(value => value.Disconnect()).Callback(() => state.Disconnect());
            LoadingScreen.ShowLoadingScreen("Joining", "Validating modules...");
            Broker.Clear();
        }

        /// <summary>Delivers the server's rejection on a worker inside the session, as the network poller does.</summary>
        public Rejection RejectOnWorker() => new(() =>
        {
            using (GameThread.ActivateCancellation(Session.Token))
            {
                state.Handle_NetworkModuleVersionsValidated(new MessagePayload<NetworkModuleVersionsValidated>(
                    this, new NetworkModuleVersionsValidated(false, Reason)));
            }
        });

        public void WaitUntilQueued() =>
            Assert.True(SpinWait.SpinUntil(() => Queue.Count > 0, LongTimeout), "the hide was never queued");

        public void Pump() => GameThread.Instance.Update(TimeSpan.Zero);

        public void Dispose()
        {
            try
            {
                state.Dispose();
                GameThread.Instance.DiscardQueuedActions(Queue);
            }
            finally
            {
                queueScope.Dispose();
                GameThread.Instance.RestoreGameThread(previousGameThreadId);
                Session.Dispose();
            }
        }
    }

    /// <summary>
    /// The rejection on its own thread. Task.Run is avoided because a waiting test thread, which is the marked
    /// game thread, may run the task inline.
    /// </summary>
    private sealed class Rejection
    {
        private readonly Thread thread;
        private Exception? failure;

        public Rejection(Action deliver)
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
            Assert.True(thread.Join(LongTimeout), "the rejection did not return");
            return failure;
        }
    }

    /// <summary>Models the forced flag the real loading interface sets on show and clears on hide.</summary>
    private sealed class ForcedLoadingScreen : ILoadingInterface
    {
        private int hideCount;

        public bool Forced { get; private set; }
        public int HideCount => Volatile.Read(ref hideCount);
        public Action? DuringHide { get; set; }
        public Exception? HideFailure { get; set; }
        public bool IsLoadingScreenAvailable => true;

        public void ShowLoadingScreen() => Forced = true;

        public void ShowLoadingScreen(string titleText, string descriptionText = "") => ShowLoadingScreen();

        public void SetLoadingMessage(string titleText, string descriptionText = "")
        {
        }

        public void HideLoadingScreen()
        {
            Interlocked.Increment(ref hideCount);
            DuringHide?.Invoke();
            Forced = false;
            if (HideFailure != null) throw HideFailure;
        }
    }
}
