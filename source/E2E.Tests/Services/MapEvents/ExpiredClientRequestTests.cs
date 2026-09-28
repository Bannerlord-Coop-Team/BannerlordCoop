using Common;
using Common.Messaging;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using GameInterface.Services.MapEvents.Messages.Leave;
using GameInterface.Services.MapEvents.Messages.Start;
using GameInterface.Services.MobileParties.Messages.Behavior;
using GameInterface.Services.PlayerCaptivityService.Messages;
using HarmonyLib;
using LiteNetLib;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
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

    [Fact]
    public void ClientLeave_ExpiredOnTheServer_LeavesOnceOnTheNextPump()
    {
        var partyBaseId = JoinPartyToNewBattle();
        var client = Clients.First();
        var request = new NetworkRequestLeaveBattle(partyBaseId);

        // A client that broke its siege camp waits for this leave to finish its menus.
        Server.NetworkSentMessages.Clear();
        Server.Call(() =>
        {
            using (GameThread.Instance.LimitFrameDrain(TimeSpan.FromSeconds(1), TimeSpan.FromMilliseconds(200)))
                new PollerReceive<NetworkRequestLeaveBattle>(Server, client.NetPeer, request).AssertReturned();

            Assert.True(Server.ObjectManager.TryGetObject<PartyBase>(partyBaseId, out var party));
            Assert.NotNull(party.MapEvent);
            Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkPartyLeftBattle>());
            RunQueuedActions(Server);
        }, LeaveDisabledMethods());

        AssertLeftOnce(partyBaseId);
    }

    [Fact]
    public void ClientLeave_TimingOutWhileItRuns_LeavesOnce()
    {
        var partyBaseId = JoinPartyToNewBattle();
        var client = Clients.First();
        var request = new NetworkRequestLeaveBattle(partyBaseId);

        Server.NetworkSentMessages.Clear();
        Server.Call(() =>
        {
            using (GameThread.Instance.LimitFrameDrain(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2)))
                RunHeldUntilThePollerGivesUp<NetworkRequestLeaveBattle, PartyBehaviorChangeAttempted>(client, request).AssertReturned();

            RunQueuedActions(Server);
        }, LeaveDisabledMethods());

        AssertLeftOnce(partyBaseId);
    }

    [Fact]
    public void CaptivityRelease_ExpiredOnTheServer_ReleasesAndRepliesOnceOnTheNextPump()
    {
        var (heroId, partyId, captorPartyId, request) = CaptureAndRequestRelease();
        var client = Clients.First();

        // The client already cleared its captivity state and waits for the reply without a deadline.
        Server.NetworkSentMessages.Clear();
        Server.Call(() =>
        {
            using (GameThread.Instance.LimitFrameDrain(TimeSpan.FromSeconds(1), TimeSpan.FromMilliseconds(200)))
                new PollerReceive<NetworkEndPlayerCaptivityAttempted>(Server, client.NetPeer, request).AssertReturned();

            AssertCaptivity(Server, heroId, captorPartyId);
            Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkPlayerCaptivityEnded>());
            RunQueuedActions(Server);
        }, ReleaseDisabledMethods());

        AssertReleasedOnce(heroId, partyId);
    }

    [Fact]
    public void CaptivityRelease_TimingOutWhileItRuns_ReleasesAndRepliesOnce()
    {
        var (heroId, partyId, _, request) = CaptureAndRequestRelease();
        var client = Clients.First();

        Server.NetworkSentMessages.Clear();
        Server.Call(() =>
        {
            using (GameThread.Instance.LimitFrameDrain(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2)))
                RunHeldUntilThePollerGivesUp<NetworkEndPlayerCaptivityAttempted, PlayerPartyReleasedFromCaptivity>(client, request).AssertReturned();

            RunQueuedActions(Server);
        }, ReleaseDisabledMethods());

        AssertReleasedOnce(heroId, partyId);
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

    /// <summary>Joins a new party to the attacker side of a new battle and returns its <see cref="PartyBase"/> id.</summary>
    private string JoinPartyToNewBattle()
    {
        var mapEventPartyId = JoinPartyToSide(CreateServerMapEventSide());
        string? partyBaseId = null;
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEventParty>(mapEventPartyId, out var mapEventParty));
            Assert.True(Server.ObjectManager.TryGetId(mapEventParty.Party, out partyBaseId));
        }, MapEventDisabledMethods);

        Assert.NotNull(partyBaseId);
        return partyBaseId!;
    }

    // The campaign-map locatable scan is unavailable headlessly.
    private IReadOnlyList<MethodBase> LeaveDisabledMethods() => MapEventDisabledMethods
        .Append(AccessTools.Method(typeof(MapEvent), "ResetUnsuitablePartiesThatWereTargetingThisMapEvent"))
        .ToList();

    private void AssertLeftOnce(string partyBaseId)
    {
        Assert.Equal(partyBaseId, Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkPartyLeftBattle>()).PartyId);
        foreach (var instance in Clients.Prepend(Server))
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<PartyBase>(partyBaseId, out var party));
                Assert.Null(party.MapEvent);
            });
        }
    }

    /// <summary>Captures a connected player and returns the release request its client sends after an escape.</summary>
    private (string heroId, string partyId, string captorPartyId, NetworkEndPlayerCaptivityAttempted request) CaptureAndRequestRelease()
    {
        var (heroId, partyId) = CreatePlayerHeroParty("MyControllerId");
        TestEnvironment.ConnectRegisteredPlayer(Clients.First(), "MyControllerId");
        var captorPartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        DefeatPlayerPartyInBattle(heroId, partyId, captorPartyId);
        AssertCaptivity(Server, heroId, captorPartyId);

        CampaignVec2 position = default;
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            position = party.Position;
        });

        var request = new NetworkEndPlayerCaptivityAttempted(
            heroId, partyId, position, EndCaptivityDetail.ReleasedAfterEscape, null, 0);
        return (heroId, partyId, captorPartyId, request);
    }

    // An escape releases from a still-active captor, and the disengage from it pathfinds on a live map scene.
    private IReadOnlyList<MethodBase> ReleaseDisabledMethods() => MapEventDisabledMethods
        .Append(AccessTools.Method(typeof(MobileParty), nameof(MobileParty.TeleportPartyToOutSideOfEncounterRadius)))
        .ToList();

    private void AssertReleasedOnce(string heroId, string partyId)
    {
        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkPlayerCaptivityEnded>());
        AssertCaptivity(Server, heroId, null);
        AssertPlayerPartyRestored(Server, heroId, partyId);
    }

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
