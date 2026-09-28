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
/// Battle requests whose blocking game-thread call timed out on the server's poller. An expired call never runs,
/// so it must not leave a mark or a pending request behind that refuses every later attempt.
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

            // The join starts and holds the game thread until the poller gave up on both waits.
            PollerReceive<NetworkRequestJoinBattle>? receive = null;
            bool gaveUpWhileJoinRan = false;
            Action<MessagePayload<BattleJoinAccepted>> holdJoin = _ => gaveUpWhileJoinRan = receive!.TryJoin(LongTimeout);
            var broker = Server.Resolve<IMessageBroker>();
            broker.Subscribe(holdJoin);
            try
            {
                receive = new PollerReceive<NetworkRequestJoinBattle>(Server, client.NetPeer, request);
                RunNextQueuedAction(Server);
            }
            finally
            {
                broker.Unsubscribe(holdJoin);
            }

            Assert.True(gaveUpWhileJoinRan, "the poller did not time out while the join ran");
            receive!.AssertTimedOut();
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

        public bool TryJoin(TimeSpan timeout) => thread.Join(timeout);

        public void AssertTimedOut()
        {
            Assert.True(thread.Join(LongTimeout), "the poller did not give up after the blocking timeout");
            Assert.IsType<TimeoutException>(failure?.GetBaseException());
        }
    }
}
