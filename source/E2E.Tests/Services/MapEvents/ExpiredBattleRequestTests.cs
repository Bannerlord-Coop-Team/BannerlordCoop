using Common;
using Common.Messaging;
using E2E.Tests.Environment.Instance;
using GameInterface.Services.MapEvents.Messages.Leave;
using LiteNetLib;
using TaleWorlds.CampaignSystem.MapEvents;
using Xunit.Abstractions;

namespace E2E.Tests.Services.MapEvents;

/// <summary>
/// Battle requests whose blocking game-thread call timed out on the server's poller. An expired call never runs,
/// so it must not leave a mark behind that refuses every later attempt.
/// </summary>
public class ExpiredBattleRequestTests : MapEventTestBase
{
    private static readonly TimeSpan LongTimeout = TimeSpan.FromSeconds(10);

    public ExpiredBattleRequestTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void ClientFinalize_ExpiredOnTheServer_LeavesNoMarkSoTheRetryFinalizes()
    {
        var mapEventCtx = CreateServerMapEvent();
        var client = Clients.First();
        var request = new NetworkMapEventFinalizeAttempted(mapEventCtx.MapEventId);

        // The shared-hideout and raid-reset checks run, then the stalled game thread lets the finalize expire.
        Server.Call(() =>
        {
            using var shortTimeout = GameThread.Instance.LimitFrameDrain(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
            var receive = new PollerReceive<NetworkMapEventFinalizeAttempted>(Server, client.NetPeer, request);
            RunNextQueuedAction(Server);
            RunNextQueuedAction(Server);
            receive.AssertTimedOut();
        }, MapEventDisabledMethods);
        Server.PumpGameThread();
        Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(mapEventCtx.MapEventId, out _), "the expired finalize ran");

        Server.Call(() => Server.SimulateMessage(client.NetPeer, request), MapEventDisabledMethods);

        Assert.False(Server.ObjectManager.TryGetObject<MapEvent>(mapEventCtx.MapEventId, out _));
        foreach (var instance in Clients)
            Assert.False(instance.ObjectManager.TryGetObject<MapEvent>(mapEventCtx.MapEventId, out _));
    }

    /// <summary>Runs the next blocking action the poller queued. Call inside the instance's scope.</summary>
    private static void RunNextQueuedAction(EnvironmentInstance instance)
    {
        Assert.True(SpinWait.SpinUntil(() => instance.PendingGameThreadActionCount > 0, LongTimeout),
            "the poller queued no game-thread action");
        GameThread.Instance.Update(TimeSpan.Zero);
    }

    /// <summary>
    /// A message received on its own thread inside the instance's scope, as the network poller delivers it. That
    /// thread is not the marked game thread, so blocking game-thread work queues until the test runs it.
    /// </summary>
    private sealed class PollerReceive<T> where T : IMessage
    {
        private readonly Thread thread;
        private Exception? failure;

        public PollerReceive(EnvironmentInstance instance, NetPeer source, T message)
        {
            var broker = instance.Resolve<IMessageBroker>();
            thread = new Thread(() =>
            {
                try
                {
                    broker.Publish(source, message);
                }
                catch (Exception exception)
                {
                    failure = exception;
                }
            }) { IsBackground = true };
            thread.Start();
        }

        public void AssertTimedOut()
        {
            Assert.True(thread.Join(LongTimeout), "the poller did not give up after the blocking timeout");
            Assert.IsType<TimeoutException>(failure?.GetBaseException());
        }
    }
}
