#if DEBUG
using Common;
using Common.Messaging;
using Coop.Core.Server.Services.Instances;
using Coop.Core.Server.Services.Instances.Handlers;
using Coop.Tests.Mocks;
using GameInterface.Services.MapEvents;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using Missions;
using Missions.Battles;
using Missions.Messages;
using Moq;
using System;
using System.Linq;
using TaleWorlds.Core;
using Xunit;

namespace Coop.Tests.Missions.Battles;

[Collection(nameof(ModInformationRoleCollection))]
public class NavalLabIsolationTests
{
    private static NavalLabSessionStore CreateStore()
    {
        var id = Guid.NewGuid();
        var store = new NavalLabSessionStore();
        store.Install(new NavalLabManifest("naval-lab:" + id.ToString("N"), id, new[] { "A", "B" },
            Enumerable.Range(0, 10).Select(_ => Guid.NewGuid()).ToArray(), new[] { Guid.NewGuid(), Guid.NewGuid() }));
        return store;
    }

    [Fact]
    public void FixtureCasualty_IsRejectedBeforeCampaignObjectResolution()
    {
        bool previousRole = ModInformation.IsServer;
        ModInformation.IsServer = true;
        try
        {
            using var broker = new MessageBroker();
            var objects = new Mock<IObjectManager>(MockBehavior.Strict);
            var store = CreateStore();
            var type = typeof(MissionModule).Assembly.GetType("Missions.Battles.BattleCasualtyHandler", true)!;
            using var handler = (IDisposable)Activator.CreateInstance(type, broker, objects.Object, store)!;
            broker.Publish(this, new NetworkRequestBattleCasualty("live-party", "live-troop", false, store.Current.InstanceId));
            GameThread.Run(() => { }, blocking: true);
            Assert.Equal("casualty", store.CampaignWriteBlocker);
            objects.VerifyNoOtherCalls();
        }
        finally { ModInformation.IsServer = previousRole; }
    }

    [Fact]
    public void FixtureResult_IsRejectedBeforeCompletionAccounting()
    {
        using var broker = new MessageBroker();
        var network = new TestNetwork();
        var peer = network.CreatePeer();
        var player = new Player("A", "", "", "", "");
        var players = new Mock<IPlayerManager>();
        players.Setup(manager => manager.TryGetPlayer(peer, out player)).Returns(true);
        var missions = new Mock<IMissionManager>(MockBehavior.Strict);
        var tracker = new Mock<IBattleCompletionTracker>(MockBehavior.Strict);
        var objects = new Mock<IObjectManager>(MockBehavior.Strict);
        var store = CreateStore();
        using var handler = new ServerBattleCompletionHandler(broker, missions.Object, objects.Object,
            players.Object, Mock.Of<IBattleHostRegistry>(), tracker.Object, store);
        broker.Publish(peer, new NetworkBattleResultReady(store.Current.InstanceId, BattleState.AttackerVictory, 1));
        GameThread.Run(() => { }, blocking: true);
        Assert.Equal("result", store.CampaignWriteBlocker);
        missions.VerifyNoOtherCalls();
        tracker.VerifyNoOtherCalls();
        objects.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FixtureDeparture_CannotConcludeOrResolveACampaignObject(bool empty)
    {
        using var broker = new MessageBroker();
        var missions = new Mock<IMissionManager>(MockBehavior.Strict);
        var objects = new Mock<IObjectManager>(MockBehavior.Strict);
        var store = CreateStore();
        using var handler = new ServerBattleCompletionHandler(broker, missions.Object, objects.Object,
            Mock.Of<IPlayerManager>(), Mock.Of<IBattleHostRegistry>(), Mock.Of<IBattleCompletionTracker>(), store);
        broker.Publish(this, new MissionMemberDeparted("A", store.Current.InstanceId, true, empty));
        GameThread.Run(() => { }, blocking: true);
        missions.VerifyNoOtherCalls();
        objects.VerifyNoOtherCalls();
        Assert.Null(store.CampaignWriteBlocker);
    }
}
#endif
