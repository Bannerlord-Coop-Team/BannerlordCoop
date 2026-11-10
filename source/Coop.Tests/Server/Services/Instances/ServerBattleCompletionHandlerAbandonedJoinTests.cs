using Common;
using Common.Messaging;
using Common.Tests.Utils;
using Coop.Core.Server.Services.Instances;
using Coop.Core.Server.Services.Instances.Handlers;
using GameInterface.Services.Entity;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.MapEvents.Messages.Start;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.PlayerCaptivityService.Messages;
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

/// <summary>An emptied battle with an accepted join still pending waits for that join to enter, cancel, refuse or expire.</summary>
public class ServerBattleCompletionHandlerAbandonedJoinTests : IDisposable
{
    private const string MapEventId = "map-event-abandoned";
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
    private readonly NetPeer hostPeer;

    /// <summary>Puts the host in the battle with its result already reported, so emptying it concludes it.</summary>
    public ServerBattleCompletionHandlerAbandonedJoinTests()
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

        BattleState abandonedState = BattleState.DefenderVictory;
        int abandonedEpoch = 1;
        int abandonedMemberCount = 1;
        completionTracker
            .Setup(tracker => tracker.TryConcludeAbandoned(
                MapEventId,
                out abandonedState,
                out abandonedEpoch,
                out abandonedMemberCount))
            .Returns(true);

        handler = new ServerBattleCompletionHandler(
            broker,
            missionManager,
            Mock.Of<IObjectManager>(),
            playerManager,
            hostRegistry.Object,
            completionTracker.Object);

        hostPeer = RegisterPlayer(HostControllerId, port: 52300);
        Assert.True(missionManager.TryEnterMission(hostPeer, HostControllerId, MapEventId, out _));
    }

    /// <summary>Unsubscribes the handler under test.</summary>
    public void Dispose() => handler.Dispose();

    [Fact]
    public void HostLeavingWithoutAPendingJoin_ConcludesTheBattle()
    {
        HostLeaves();

        Assert.Single(broker.Messages.GetMessages<AuthoritativeBattleConclusionRequested>());
    }

    [Fact]
    public void HostLeavingWithAPendingJoin_HoldsTheConclusion()
    {
        Reserve(RegisterPlayer(JoinerControllerId, port: 52301), out _);

        HostLeaves();

        Assert.Empty(broker.Messages.GetMessages<AuthoritativeBattleConclusionRequested>());
    }

    [Fact]
    public void CancelledJoin_ConcludesTheHeldBattle()
    {
        Reserve(RegisterPlayer(JoinerControllerId, port: 52302), out var reservationId);
        HostLeaves();
        Assert.Empty(broker.Messages.GetMessages<AuthoritativeBattleConclusionRequested>());

        Publish(this, new BattleJoinCancelled(MapEventId, JoinerControllerId, reservationId));

        Assert.Single(broker.Messages.GetMessages<AuthoritativeBattleConclusionRequested>());
    }

    [Fact]
    public void RefusedOpen_ConcludesTheHeldBattle()
    {
        var joinerPeer = RegisterPlayer(JoinerControllerId, port: 52303);
        Reserve(joinerPeer, out _);
        HostLeaves();
        Assert.Empty(broker.Messages.GetMessages<AuthoritativeBattleConclusionRequested>());

        Publish(joinerPeer, new NetworkFieldBattleOpenRefused(MapEventId));

        Assert.Single(broker.Messages.GetMessages<AuthoritativeBattleConclusionRequested>());
    }

    [Fact]
    public void ExpiredJoin_ConcludesTheHeldBattle()
    {
        handler.JoinReservationTimeout = TimeSpan.Zero;
        Reserve(RegisterPlayer(JoinerControllerId, port: 52304), out _);
        HostLeaves();
        Assert.Empty(broker.Messages.GetMessages<AuthoritativeBattleConclusionRequested>());

        Publish(this, new CampaignTick());

        Assert.Single(broker.Messages.GetMessages<AuthoritativeBattleConclusionRequested>());
    }

    [Fact]
    public void EnteredJoin_KeepsTheBattleOpenForItsOwnResult()
    {
        var joinerPeer = RegisterPlayer(JoinerControllerId, port: 52305);
        Reserve(joinerPeer, out _);
        HostLeaves();

        Assert.True(missionManager.TryEnterMission(joinerPeer, JoinerControllerId, MapEventId, out _));
        Publish(this, new MissionMemberEntered(JoinerControllerId, MapEventId, isFirstMember: true));
        Publish(this, new CampaignTick());

        Assert.Empty(broker.Messages.GetMessages<AuthoritativeBattleConclusionRequested>());
    }

    // Removes the host from the mission and tells the handler, as the membership handler does.
    private void HostLeaves()
    {
        Assert.True(missionManager.TryLeaveMission(hostPeer, HostControllerId, MapEventId, out _));
        Publish(this, new MissionMemberDeparted(
            HostControllerId, MapEventId, wasRetreat: false, isInstanceEmpty: true, withdraws: false));
    }

    // Publishes a message and waits for the handler's game-thread work to run.
    private void Publish<T>(object source, T message) where T : IMessage
    {
        broker.Publish(source, message);
        GameThread.Run(() => { }, blocking: true);
    }

    // Accepts a join for the given peer, which the handler holds as pending until it resolves.
    private void Reserve(NetPeer peer, out Guid reservationId)
    {
        reservationId = Guid.NewGuid();
        Publish(peer, new BattleJoinAccepted(MapEventId, JoinerControllerId, reservationId));
    }

    // Registers a connected player for the controller and returns its peer.
    private NetPeer RegisterPlayer(string controllerId, int port)
    {
        var peer = CreatePeer(port);
        Assert.True(playerManager.AddPlayer(
            new Player(controllerId, string.Empty, string.Empty, string.Empty, string.Empty)));
        playerManager.SetPeer(controllerId, peer);
        return peer;
    }

    // Builds a detached peer for the given port.
    private static NetPeer CreatePeer(int port) => (NetPeer)PeerConstructor.Invoke(new object[]
    {
        new NetManager(null),
        new IPEndPoint(IPAddress.Loopback, port),
        port,
    });
}
