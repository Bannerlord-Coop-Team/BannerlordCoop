using Common.Messaging;
using Common.Network;
using Common.Tests.Utils;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.BattleSize;
using GameInterface.Services.MapEvents.Handlers;
using GameInterface.Services.MapEvents.Logging;
using GameInterface.Services.MapEvents.Messages.Start;
using GameInterface.Services.MapEvents.TroopSupply;
using GameInterface.Services.Players;
using GameInterface.Surrogates;
using Moq;
using TaleWorlds.CampaignSystem.MapEvents;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions;

public class BattleMissionStartBattleSizeTests : MissionTestEnvironment
{
    public BattleMissionStartBattleSizeTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public void FieldMissionStart_FreezesBattleSizePerMissionClaim()
    {
        var (mapEventId, _) = SetupCoopBattle("attacker", "defender");
        var messageBroker = new TestMessageBroker();
        var network = new Mock<INetwork>();
        var reserveBuilder = new Mock<IBattleTroopReserveBuilder>();
        var battleSizeProvider = new ServerBattleSizeProvider();
        var startMessages = new List<NetworkStartAttackMission>();

        network.Setup(n => n.SendAll(It.IsAny<IMessage>()))
            .Callback<IMessage>(message =>
            {
                if (message is NetworkStartAttackMission startMessage)
                    startMessages.Add(startMessage);
            });

        try
        {
            Server.Call(() =>
            {
                Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(mapEventId, out var mapEvent));

                using var handler = new BattleMissionStartHandler(
                    messageBroker,
                    Server.ObjectManager,
                    Server.Resolve<IPlayerManager>(),
                    network.Object,
                    Server.Resolve<IMapEventLogger>(),
                    Server.Resolve<IBattleMissionInitializerResolver>(),
                    battleSizeProvider,
                    reserveBuilder.Object);

                battleSizeProvider.SetBattleSize(300);
                messageBroker.Publish(Server.NetPeer, new NetworkBattleStartRequest(
                    "first-request",
                    (int)BattleStartMode.Mission,
                    mapEventId,
                    "missing-party"));

                var firstStart = Assert.Single(startMessages);
                Assert.Equal("missing-party", firstStart.InitiatingPartyId);
                Assert.Equal(300, firstStart.BattleSize);

                battleSizeProvider.SetBattleSize(800);
                messageBroker.Publish(Server.NetPeer, new NetworkBattleStartRequest(
                    "later-request",
                    (int)BattleStartMode.Mission,
                    mapEventId,
                    "missing-party"));

                Assert.Collection(
                    startMessages,
                    start => Assert.Equal(300, start.BattleSize),
                    start => Assert.Equal(300, start.BattleSize));

                Assert.True(ServerBattleModeArbiter.ReleaseMission(mapEventId));
                messageBroker.Publish(Server.NetPeer, new NetworkBattleStartRequest(
                    "restarted-mission-request",
                    (int)BattleStartMode.Mission,
                    mapEventId,
                    "missing-party"));

                Assert.Collection(
                    startMessages,
                    start => Assert.Equal(300, start.BattleSize),
                    start => Assert.Equal(300, start.BattleSize),
                    start => Assert.Equal(800, start.BattleSize));
                reserveBuilder.Verify(builder => builder.PreparePlan(mapEvent, 300), Times.Exactly(2));
                reserveBuilder.Verify(builder => builder.PreparePlan(mapEvent, 800), Times.Once);
            }, MapEventDisabledMethods);
        }
        finally
        {
            ServerBattleModeArbiter.Release(mapEventId);
        }
    }

    [Fact]
    public void SiegeMissionStart_RebuildsSnapshotForRestartedMissionClaim()
    {
        using var handler = new BattleMissionStartHandler(
            new TestMessageBroker(),
            Server.ObjectManager,
            Server.Resolve<IPlayerManager>(),
            Mock.Of<INetwork>(),
            Server.Resolve<IMapEventLogger>(),
            Server.Resolve<IBattleMissionInitializerResolver>(),
            new ServerBattleSizeProvider(),
            Mock.Of<IBattleTroopReserveBuilder>());

        var firstStart = handler.GetOrCreateSiegeMissionSnapshot(
            "siege-battle",
            isNewMissionClaim: true,
            () => CreateSiegeStart(300));
        var sameMissionStart = handler.GetOrCreateSiegeMissionSnapshot(
            "siege-battle",
            isNewMissionClaim: false,
            () => CreateSiegeStart(800));
        var restartedMissionStart = handler.GetOrCreateSiegeMissionSnapshot(
            "siege-battle",
            isNewMissionClaim: true,
            () => CreateSiegeStart(800));

        Assert.Equal(300, firstStart.BattleSize);
        Assert.Same(firstStart, sameMissionStart);
        Assert.Equal(800, restartedMissionStart.BattleSize);
        Assert.NotSame(firstStart, restartedMissionStart);
    }

    private static NetworkStartSiegeMission CreateSiegeStart(int battleSize)
    {
        return new NetworkStartSiegeMission(
            "siege-battle",
            0,
            Array.Empty<float>(),
            Array.Empty<SiegeEngineState>(),
            Array.Empty<SiegeEngineState>(),
            initiatingPartyId: null,
            battleSize);
    }
}
