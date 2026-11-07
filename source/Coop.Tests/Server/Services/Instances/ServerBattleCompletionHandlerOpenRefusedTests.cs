using Common;
using Common.Tests.Utils;
using Coop.Core.Server.Services.Instances;
using Coop.Core.Server.Services.Instances.Handlers;
using GameInterface.Services.Entity;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.MapEvents.Messages.Start;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using LiteNetLib;
using Missions.Messages;
using Moq;
using Serilog;
using System;
using System.Collections.Generic;
using System.Net;
using System.Reflection;
using TaleWorlds.Core;
using Xunit;

namespace Coop.Tests.Server.Services.Instances;

/// <summary>
/// A client that refuses to open a field battle tells the server, which releases that player's pending join so the
/// battle's conclusion does not wait for the reservation to expire.
/// </summary>
public class ServerBattleCompletionHandlerOpenRefusedTests : IDisposable
{
    private const string MapEventId = "map-event-1";
    private const string HostControllerId = "host";
    private const string JoinerControllerId = "joiner";

    private static readonly ConstructorInfo PeerConstructor = typeof(NetPeer).GetConstructor(
        BindingFlags.NonPublic | BindingFlags.Instance,
        binder: null,
        new[] { typeof(NetManager), typeof(IPEndPoint), typeof(int) },
        modifiers: null)!;

    private readonly TestMessageBroker broker = new();
    private readonly Mock<IBattleHostRegistry> hostRegistry = new();
    private readonly Mock<IBattleCompletionTracker> completionTracker = new();
    private readonly PlayerManager playerManager;
    private readonly MissionManager missionManager;
    private readonly ServerBattleCompletionHandler handler;

    public ServerBattleCompletionHandlerOpenRefusedTests()
    {
        playerManager = new PlayerManager(
            Mock.Of<ILogger>(),
            Mock.Of<IObjectManager>(),
            Mock.Of<IControllerIdProvider>());
        missionManager = new MissionManager(playerManager);

        var assignment = new BattleHostAssignment(HostControllerId, Array.Empty<string>(), epoch: 1);
        hostRegistry
            .Setup(registry => registry.TryGet(MapEventId, out assignment))
            .Returns(true);

        // The host already reported its result, so the battle concludes as soon as no join holds it.
        completionTracker
            .Setup(tracker => tracker.TryReconcile(
                MapEventId,
                It.IsAny<IReadOnlyCollection<string>>(),
                HostControllerId,
                1,
                out It.Ref<BattleState>.IsAny,
                It.IsAny<bool>(),
                It.IsAny<bool>()))
            .Returns((
                string _,
                IReadOnlyCollection<string> _,
                string _,
                int _,
                out BattleState concludedState,
                bool canConclude,
                bool _) =>
            {
                concludedState = BattleState.DefenderVictory;
                return canConclude;
            });

        handler = new ServerBattleCompletionHandler(
            broker,
            missionManager,
            Mock.Of<IObjectManager>(),
            playerManager,
            hostRegistry.Object,
            completionTracker.Object);

        var hostPeer = RegisterPlayer(HostControllerId, port: 52200);
        Assert.True(missionManager.TryEnterMission(hostPeer, HostControllerId, MapEventId, out _));
    }

    public void Dispose() => handler.Dispose();

    // The handler marshals its work onto the game thread, which this assembly's pump runs. A blocking no-op queued
    // behind that work returns once the work itself has run.
    private static void Drain() => GameThread.Run(() => { }, blocking: true);

    [Fact]
    public void RefusedOpenReleasesTheSendersJoinAndConcludesTheBattle()
    {
        var peer = RegisterPlayer(JoinerControllerId, port: 52201);
        Reserve(peer, MapEventId, JoinerControllerId);
        AssertHeld();

        broker.Publish(peer, new NetworkFieldBattleOpenRefused(MapEventId));
        Drain();

        Assert.Single(broker.Messages.GetMessages<AuthoritativeBattleConclusionRequested>());
    }

    [Fact]
    public void RefusedOpenReleasesEveryJoinTheSenderHoldsForThatBattle()
    {
        var peer = RegisterPlayer(JoinerControllerId, port: 52202);
        Reserve(peer, MapEventId, JoinerControllerId);
        Reserve(peer, MapEventId, JoinerControllerId);

        broker.Publish(peer, new NetworkFieldBattleOpenRefused(MapEventId));
        Drain();

        Assert.Single(broker.Messages.GetMessages<AuthoritativeBattleConclusionRequested>());
    }

    [Fact]
    public void PeerWithNoPlayerIsIgnored()
    {
        var peer = RegisterPlayer(JoinerControllerId, port: 52203);
        Reserve(peer, MapEventId, JoinerControllerId);

        broker.Publish(CreatePeer(52204), new NetworkFieldBattleOpenRefused(MapEventId));
        Drain();

        AssertHeld();
    }

    [Fact]
    public void PeerReplacedByAReconnectIsIgnored()
    {
        var stalePeer = RegisterPlayer(JoinerControllerId, port: 52205);
        Reserve(stalePeer, MapEventId, JoinerControllerId);
        playerManager.SetPeer(JoinerControllerId, CreatePeer(52206));

        broker.Publish(stalePeer, new NetworkFieldBattleOpenRefused(MapEventId));
        Drain();

        AssertHeld();
    }

    [Fact]
    public void RefusalKeepsAnotherPlayersJoin()
    {
        var sender = RegisterPlayer(JoinerControllerId, port: 52207);
        var otherPlayer = RegisterPlayer("other-player", port: 52208);
        Reserve(otherPlayer, MapEventId, "other-player");

        broker.Publish(sender, new NetworkFieldBattleOpenRefused(MapEventId));
        Drain();

        AssertHeld();
    }

    [Fact]
    public void RefusalForAnotherBattleKeepsTheJoin()
    {
        var sender = RegisterPlayer(JoinerControllerId, port: 52209);
        Reserve(sender, MapEventId, JoinerControllerId);

        broker.Publish(sender, new NetworkFieldBattleOpenRefused("other-map-event"));
        Drain();

        AssertHeld();
    }

    // Asks the handler to conclude again, which it may only do once no join is pending.
    private void AssertHeld()
    {
        broker.Publish(this, new BattleHostAssignmentChanged(MapEventId));
        Drain();
        Assert.Empty(broker.Messages.GetMessages<AuthoritativeBattleConclusionRequested>());
    }

    private NetPeer RegisterPlayer(string controllerId, int port)
    {
        var peer = CreatePeer(port);
        Assert.True(playerManager.AddPlayer(
            new Player(controllerId, string.Empty, string.Empty, string.Empty, string.Empty)));
        playerManager.SetPeer(controllerId, peer);
        return peer;
    }

    private void Reserve(NetPeer peer, string mapEventId, string controllerId)
    {
        broker.Publish(peer, new BattleJoinAccepted(mapEventId, controllerId, Guid.NewGuid()));
        Drain();
    }

    private static NetPeer CreatePeer(int port) => (NetPeer)PeerConstructor.Invoke(new object[]
    {
        new NetManager(null),
        new IPEndPoint(IPAddress.Loopback, port),
        port,
    });
}
