using Common;
using Common.Network;
using Common.Tests.Utils;
using Common.Util;
using Coop.Core.Server.Services.Instances;
using Coop.Core.Server.Services.Instances.Handlers;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.MapEvents.TroopSupply;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using GameInterface.Services.SiegeEvents.Messages;
using LiteNetLib;
using Missions.Messages;
using Moq;
using System;
using System.Net;
using System.Reflection;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using Xunit;

namespace Coop.Tests.Server.Services.Instances;

/// <summary>
/// The walls of a player-led siege assault conclude into the keep, which is fought as a second mission on the same
/// map event id, so the walls instance must not leave a conclusion record behind.
/// </summary>
public class ServerBattleCompletionHandlerSiegeKeepTests : IDisposable
{
    private const string MapEventId = "map-event-keep";
    private const string HostControllerId = "host";

    private static readonly ConstructorInfo PeerConstructor = typeof(NetPeer).GetConstructor(
        BindingFlags.NonPublic | BindingFlags.Instance,
        binder: null,
        new[] { typeof(NetManager), typeof(IPEndPoint), typeof(int) },
        modifiers: null)!;

    private readonly TestMessageBroker broker = new();
    private readonly Mock<IObjectManager> objectManager = new();
    private readonly Mock<IPlayerManager> playerManager = new();
    private readonly Mock<IBattleHostRegistry> hostRegistry = new();
    private readonly MissionManager missionManager;
    private readonly ServerBattleCompletionHandler handler;
    private readonly NetPeer peer;

    public ServerBattleCompletionHandlerSiegeKeepTests()
    {
        peer = CreatePeer(52200);
        var player = new Player(HostControllerId, "hero", "party", "clan", "character");
        playerManager.Setup(manager => manager.TryGetPlayer(peer, out player)).Returns(true);
        var mappedPeer = peer;
        playerManager.Setup(manager => manager.TryGetPeer(HostControllerId, out mappedPeer)).Returns(true);

        var assignment = new BattleHostAssignment(HostControllerId, Array.Empty<string>(), epoch: 1);
        hostRegistry.Setup(registry => registry.TryGet(MapEventId, out assignment)).Returns(true);

        missionManager = new MissionManager(playerManager.Object);
        handler = new ServerBattleCompletionHandler(
            broker,
            Mock.Of<INetwork>(),
            missionManager,
            objectManager.Object,
            playerManager.Object,
            hostRegistry.Object,
            new BattleCompletionTracker(),
            Mock.Of<IBattleTroopReserveBuilder>());
    }

    public void Dispose() => handler.Dispose();

    [Fact]
    public void WallsPullBack_OpensTheKeepAndLeavesItsInstanceFreeForTheKeepMission()
    {
        GiveTheMapEventSiegeAssaultWalls();
        Assert.True(missionManager.TryEnterMission(peer, HostControllerId, MapEventId, out _));

        broker.Publish(peer, new NetworkBattleResultReady(MapEventId, BattleState.DefenderPullBack, 1));
        Drain();

        var request = Assert.Single(broker.Messages.GetMessages<AuthoritativeSiegeKeepRequested>());
        Assert.Equal(1, request.HostEpoch);
        Assert.Empty(broker.Messages.GetMessages<AuthoritativeSiegeAmbushCompletionRequested>());

        broker.Publish(this, new SiegeKeepStageOpened(MapEventId));
        Drain();

        Assert.True(missionManager.TryLeaveMission(peer, HostControllerId, MapEventId, out _));
        Assert.True(missionManager.TryEnterMission(CreatePeer(52201), "keep-player", MapEventId, out _));
    }

    private void GiveTheMapEventSiegeAssaultWalls()
    {
        var component = ObjectHelper.SkipConstructor<SiegeAssaultEventComponent>();
        component._eventType = MapEvent.BattleTypes.Siege;
        component.MapEventSettlement = ObjectHelper.SkipConstructor<Settlement>();
        var mapEvent = ObjectHelper.SkipConstructor<MapEvent>();
        mapEvent.Component = component;
        Assert.True(mapEvent.IsSiegeAssault);
        Assert.Equal(Settlement.SiegeState.OnTheWalls, mapEvent.MapEventSettlement.CurrentSiegeState);
        objectManager.Setup(manager => manager.TryGetObject(MapEventId, out mapEvent)).Returns(true);
    }

    private static NetPeer CreatePeer(int port) => (NetPeer)PeerConstructor.Invoke(new object[]
    {
        new NetManager(null),
        new IPEndPoint(IPAddress.Loopback, port),
        port - 52000,
    });

    // The handler marshals its work onto the game thread, which this assembly's pump runs.
    private static void Drain() => GameThread.Run(() => { }, blocking: true);
}
