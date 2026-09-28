using Common;
using Common.Messaging;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using GameInterface.Services.MapEvents.Messages.Leave;
using GameInterface.Services.MapEvents.Messages.Start;
using HarmonyLib;
using LiteNetLib;
using System.Reflection;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using Xunit.Abstractions;

namespace E2E.Tests.Services.MapEvents;

/// <summary>
/// Client requests whose blocking game-thread call timed out on the server's poller. An expired call never runs,
/// so it must not leave a pending request behind that refuses every later attempt, and a request the client
/// doesn't send again must still run once.
/// </summary>
public class ExpiredClientRequestTests : MapEventTestBase
{
    private static readonly TimeSpan LongTimeout = TimeSpan.FromSeconds(10);

    public ExpiredClientRequestTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void ClientFinalize_ExpiredOnTheServer_FinalizesOnceOnTheNextPump()
    {
        var mapEventCtx = CreateServerMapEvent();
        var client = Clients.First();
        var request = new NetworkMapEventFinalizeAttempted(mapEventCtx.MapEventId);

        // A leaving side leader already closed its menu, so nothing asks for this finalize again.
        Server.NetworkSentMessages.Clear();
        Server.Call(() =>
        {
            using (GameThread.Instance.LimitFrameDrain(TimeSpan.FromSeconds(1), TimeSpan.FromMilliseconds(200)))
                new PollerReceive<NetworkMapEventFinalizeAttempted>(Server, client.NetPeer, request).AssertReturned();

            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(mapEventCtx.MapEventId, out _), "the expired finalize ran");
            Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkMapEventFinalized>());
            RunQueuedActions(Server);
        }, MapEventDisabledMethods);

        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkMapEventFinalized>());
        Assert.False(Server.ObjectManager.TryGetObject<MapEvent>(mapEventCtx.MapEventId, out _));
        foreach (var instance in Clients)
            Assert.False(instance.ObjectManager.TryGetObject<MapEvent>(mapEventCtx.MapEventId, out _));
    }

    [Fact]
    public void ClientFinalize_TimingOutWhileItRuns_FinalizesOnce()
    {
        var mapEventCtx = CreateServerMapEvent();
        var client = Clients.First();
        var request = new NetworkMapEventFinalizeAttempted(mapEventCtx.MapEventId);

        Server.NetworkSentMessages.Clear();
        Server.Call(() =>
        {
            using (GameThread.Instance.LimitFrameDrain(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2)))
                RunHeldUntilThePollerGivesUp<NetworkMapEventFinalizeAttempted, MapEventFinalized>(client, request).AssertReturned();

            RunQueuedActions(Server);
        }, MapEventDisabledMethods);

        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkMapEventFinalized>());
        Assert.False(Server.ObjectManager.TryGetObject<MapEvent>(mapEventCtx.MapEventId, out _));
    }

    [Fact]
    public void ClientJoin_ExpiredOnTheServer_IsRejectedSoTheClientCanRequestAgain()
    {
        var mapEventCtx = CreateServerMapEvent();
        var (_, partyId) = CreatePlayerHeroParty("PlayerOne");
        var client = Clients.First();
        TestEnvironment.ConnectRegisteredPlayer(client, "PlayerOne");
        var request = RequestJoin(client, mapEventCtx.MapEventId, partyId);

        Server.NetworkSentMessages.Clear();
        Server.InternalMessages.Clear();
        Server.Call(() =>
        {
            using var shortTimeout = GameThread.Instance.LimitFrameDrain(TimeSpan.FromSeconds(1), TimeSpan.FromMilliseconds(200));
            new PollerReceive<NetworkRequestJoinBattle>(Server, client.NetPeer, request).AssertTimedOut();
        }, MapEventDisabledMethods);
        Server.PumpGameThread();
        client.PumpGameThread();

        var reply = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkJoinBattleReply>());
        Assert.Equal(request.RequestId, reply.RequestId);
        Assert.False(reply.Accepted);
        Assert.Empty(Server.InternalMessages.GetMessages<BattleJoinAccepted>());

        // The rejection reached the client and cleared its pending join, so the next attempt reaches the server.
        var retry = RequestJoin(client, mapEventCtx.MapEventId, partyId);
        Assert.NotEqual(request.RequestId, retry.RequestId);
        Server.NetworkSentMessages.Clear();
        Server.Call(() => Server.SimulateMessage(client.NetPeer, retry), MapEventDisabledMethods);

        Assert.True(Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkJoinBattleReply>()).Accepted);
    }

    [Fact]
    public void ClientJoin_TimingOutWhileItRuns_RepliesOnceWithTheJoinResult()
    {
        var mapEventCtx = CreateServerMapEvent();
        var (_, partyId) = CreatePlayerHeroParty("PlayerOne");
        var client = Clients.First();
        TestEnvironment.ConnectRegisteredPlayer(client, "PlayerOne");
        var request = RequestJoin(client, mapEventCtx.MapEventId, partyId);

        Server.NetworkSentMessages.Clear();
        Server.Call(() =>
        {
            using var shortTimeout = GameThread.Instance.LimitFrameDrain(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2));
            RunHeldUntilThePollerGivesUp<NetworkRequestJoinBattle, BattleJoinAccepted>(client, request).AssertTimedOut();
        }, MapEventDisabledMethods);
        Server.PumpGameThread();

        var reply = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkJoinBattleReply>());
        Assert.Equal(request.RequestId, reply.RequestId);
        Assert.True(reply.Accepted);
    }

    /// <summary>Joins the client's party to the attacker side and returns the request it sent, undelivered.</summary>
    private NetworkRequestJoinBattle RequestJoin(EnvironmentInstance client, string mapEventId, string partyId)
    {
        client.NetworkSentMessages.Clear();
        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<MapEvent>(mapEventId, out var mapEvent));
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            party.Party.MapEventSide = mapEvent.AttackerSide;
            Assert.Null(party.MapEvent);
        }, WithoutNetworkDelivery());

        return Assert.Single(client.NetworkSentMessages.GetMessages<NetworkRequestJoinBattle>());
    }

    private IReadOnlyList<MethodBase> WithoutNetworkDelivery() => MapEventDisabledMethods
        .Append(AccessTools.Method(typeof(TestNetworkRouter), nameof(TestNetworkRouter.SendReliablePayload),
            new[] { typeof(NetPeer), typeof(NetPeer), typeof(byte[]) }))
        .ToList();

    /// <summary>
    /// Receives <paramref name="request"/> on a poller and runs its queued action, holding the game thread inside it
    /// on <typeparamref name="THold"/> until the poller gave up on both waits. Call inside the server's scope.
    /// </summary>
    private PollerReceive<TRequest> RunHeldUntilThePollerGivesUp<TRequest, THold>(EnvironmentInstance client, TRequest request)
        where TRequest : IMessage
        where THold : IMessage
    {
        PollerReceive<TRequest>? receive = null;
        bool gaveUpWhileItRan = false;
        Action<MessagePayload<THold>> hold = _ => gaveUpWhileItRan = receive!.TryJoin(LongTimeout);
        var broker = Server.Resolve<IMessageBroker>();
        broker.Subscribe(hold);
        try
        {
            receive = new PollerReceive<TRequest>(Server, client.NetPeer, request);
            RunNextQueuedAction(Server);
        }
        finally
        {
            broker.Unsubscribe(hold);
        }

        Assert.True(gaveUpWhileItRan, "the poller did not time out while the request ran");
        return receive;
    }

    /// <summary>Runs the next blocking action the poller queued. Call inside the instance's scope.</summary>
    private static void RunNextQueuedAction(EnvironmentInstance instance)
    {
        Assert.True(SpinWait.SpinUntil(() => instance.PendingGameThreadActionCount > 0, LongTimeout),
            "the poller queued no game-thread action");
        GameThread.Instance.Update(TimeSpan.Zero);
    }

    /// <summary>Runs everything queued inside the instance's scope, so its disabled methods stay in place.</summary>
    private static void RunQueuedActions(EnvironmentInstance instance)
    {
        for (int pass = 0; instance.PendingGameThreadActionCount > 0; pass++)
        {
            Assert.True(pass < 10, "the game-thread queue did not drain");
            GameThread.Instance.Update(TimeSpan.Zero);
        }
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

        public bool TryJoin(TimeSpan timeout) => thread.Join(timeout);

        public void AssertTimedOut()
        {
            Assert.True(thread.Join(LongTimeout), "the poller did not give up after the blocking timeout");
            Assert.IsType<TimeoutException>(failure?.GetBaseException());
        }

        /// <summary>The poller gave up without throwing, because the expired work was queued again.</summary>
        public void AssertReturned()
        {
            Assert.True(thread.Join(LongTimeout), "the poller did not give up after the blocking timeout");
            Assert.Null(failure);
        }
    }
}
