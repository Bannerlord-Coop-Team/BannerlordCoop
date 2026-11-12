using Common;
using Common.Messaging;
using Common.Util;
using Coop.Tests.Mocks;
using GameInterface.Services.Heroes.Handlers;
using GameInterface.Services.Heroes.Interfaces;
using GameInterface.Services.Heroes.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using LiteNetLib;
using Moq;
using System;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem;
using Xunit;

namespace GameInterface.Tests.Services.Heroes;

[Collection(ModInformationRoleCollection.Name)]
public class HeroMeetingHandlerTests : IDisposable
{
    private readonly bool wasServer = ModInformation.IsServer;
    private readonly MessageBroker messageBroker = new MessageBroker();
    private readonly TestNetwork network = new TestNetwork();
    private readonly Player registeredPlayer = new Player(
        "controller-1",
        "registered-player-hero",
        "player-party",
        "player-clan",
        "player-character");
    private readonly Hero playerHero = ObjectHelper.SkipConstructor<Hero>();
    private readonly Hero metHero = ObjectHelper.SkipConstructor<Hero>();
    private readonly Mock<ISessionHeroMeetingDataInterface> meetingData = new Mock<ISessionHeroMeetingDataInterface>();
    private readonly Mock<IHeroRelationsInterface> heroRelations = new Mock<IHeroRelationsInterface>();
    private readonly NetPeer peer;
    private readonly HeroMeetingHandler handler;

    static HeroMeetingHandlerTests()
    {
        RuntimeHelpers.RunModuleConstructor(typeof(TestNetwork).Module.ModuleHandle);
    }

    public HeroMeetingHandlerTests()
    {
        ModInformation.IsServer = true;

        peer = network.CreatePeer();
        var player = registeredPlayer;
        var playerManager = new Mock<IPlayerManager>();
        playerManager
            .Setup(manager => manager.TryGetPlayer(peer, out player))
            .Returns(true);

        var resolvedPlayerHero = playerHero;
        var resolvedMetHero = metHero;
        var objectManager = new Mock<IObjectManager>();
        objectManager
            .Setup(manager => manager.TryGetObjectWithLogging<Hero>(registeredPlayer.HeroId, out resolvedPlayerHero))
            .Returns(true);
        objectManager
            .Setup(manager => manager.TryGetObjectWithLogging<Hero>("met-hero", out resolvedMetHero))
            .Returns(true);

        handler = new HeroMeetingHandler(
            messageBroker,
            objectManager.Object,
            network,
            playerManager.Object,
            meetingData.Object,
            heroRelations.Object);
    }

    public void Dispose()
    {
        handler.Dispose();
        network.Dispose();
        messageBroker.Dispose();
        ModInformation.IsServer = wasServer;
    }

    [Fact]
    public void NetworkPlayerMetHero_UsesSendingPeersRegisteredHero()
    {
        messageBroker.Publish(peer, new NetworkPlayerMetHero("stale-player-hero", "met-hero", 1351));
        GameThread.Run(() => { }, blocking: true);

        meetingData.Verify(data => data.RecordMeeting(registeredPlayer.HeroId, "met-hero", 1351));
        meetingData.Verify(
            data => data.RecordMeeting("stale-player-hero", It.IsAny<string>(), It.IsAny<long>()),
            Times.Never);
    }

    [Fact]
    public void NetworkPlayerMetHero_AppliesRelationOnlyOnFirstMeeting()
    {
        meetingData
            .SetupSequence(data => data.RecordMeeting(registeredPlayer.HeroId, "met-hero", It.IsAny<long>()))
            .Returns(true)
            .Returns(false);

        messageBroker.Publish(peer, new NetworkPlayerMetHero(registeredPlayer.HeroId, "met-hero", 1351));
        messageBroker.Publish(peer, new NetworkPlayerMetHero(registeredPlayer.HeroId, "met-hero", 1352));
        GameThread.Run(() => { }, blocking: true);

        meetingData.Verify(data => data.RecordMeeting(registeredPlayer.HeroId, "met-hero", It.IsAny<long>()), Times.Exactly(2));
        heroRelations.Verify(relations => relations.ApplyFirstMeetingRelation(playerHero, metHero), Times.Once);
    }
}
