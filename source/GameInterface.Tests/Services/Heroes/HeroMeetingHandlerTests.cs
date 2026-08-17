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
using Moq;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem;
using Xunit;

namespace GameInterface.Tests.Services.Heroes;

[Collection(ModInformationRoleCollection.Name)]
public class HeroMeetingHandlerTests
{
    static HeroMeetingHandlerTests()
    {
        RuntimeHelpers.RunModuleConstructor(typeof(TestNetwork).Module.ModuleHandle);
    }

    [Fact]
    public void NetworkPlayerMetHero_UsesSendingPeersRegisteredHero()
    {
        var wasServer = ModInformation.IsServer;
        ModInformation.IsServer = true;
        try
        {
            using var messageBroker = new MessageBroker();
            using var network = new TestNetwork();
            var peer = network.CreatePeer();
            var registeredPlayer = new Player(
                "controller-1",
                "registered-player-hero",
                "player-party",
                "player-clan",
                "player-character");
            var playerManager = new Mock<IPlayerManager>();
            playerManager
                .Setup(manager => manager.TryGetPlayer(peer, out registeredPlayer))
                .Returns(true);

            Hero playerHero = ObjectHelper.SkipConstructor<Hero>();
            Hero metHero = ObjectHelper.SkipConstructor<Hero>();
            var objectManager = new Mock<IObjectManager>();
            objectManager
                .Setup(manager => manager.TryGetObjectWithLogging<Hero>(registeredPlayer.HeroId, out playerHero))
                .Returns(true);
            objectManager
                .Setup(manager => manager.TryGetObjectWithLogging<Hero>("met-hero", out metHero))
                .Returns(true);
            var meetingData = new Mock<ISessionHeroMeetingDataInterface>();

            using var handler = new HeroMeetingHandler(
                messageBroker,
                objectManager.Object,
                network,
                playerManager.Object,
                meetingData.Object);

            messageBroker.Publish(peer, new NetworkPlayerMetHero("stale-player-hero", "met-hero", 1351));
            GameThread.Run(() => { }, blocking: true);

            meetingData.Verify(data => data.RecordMeeting(registeredPlayer.HeroId, "met-hero", 1351));
            meetingData.Verify(
                data => data.RecordMeeting("stale-player-hero", It.IsAny<string>(), It.IsAny<long>()),
                Times.Never);
        }
        finally
        {
            ModInformation.IsServer = wasServer;
        }
    }
}
