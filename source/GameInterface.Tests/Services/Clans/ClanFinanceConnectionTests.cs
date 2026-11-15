using Common;
using Common.Messaging;
using Common.Network.Messages;
using Common.Util;
using Coop.Tests.Mocks;
using GameInterface.Services.Clans;
using GameInterface.Services.Clans.Handlers;
using GameInterface.Services.Clans.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using Moq;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem;
using Xunit;

namespace GameInterface.Tests.Services.Clans;

[Collection(ModInformationRoleCollection.Name)]
public class ClanFinanceConnectionTests
{
    static ClanFinanceConnectionTests()
    {
        RuntimeHelpers.RunModuleConstructor(typeof(TestNetwork).Module.ModuleHandle);
    }

    // A session player has a controller id only, so it has no hero to report or refresh.
    [Fact]
    public void ConnectionChange_OfAPlayerWithNoHero_SendsNoNullId_AndLooksUpNoHero()
    {
        bool wasServer = ModInformation.IsServer;
        ModInformation.IsServer = true;
        try
        {
            using var broker = new MessageBroker();
            using var network = new TestNetwork();
            var peer = network.CreatePeer();
            var sessionPlayer = new Player("session-player", null, null, null, null);
            var playerManager = new Mock<IPlayerManager>();
            playerManager.SetupGet(manager => manager.Players).Returns(new[] { sessionPlayer });
            playerManager.Setup(manager => manager.IsConnected(sessionPlayer)).Returns(false);
            var objectManager = new Mock<IObjectManager>();
            using var handler = new ClanFinanceHandler(
                broker, objectManager.Object, network, Mock.Of<IClanFinance>(), playerManager.Object);

            broker.Publish(this, new PlayerConnectionStateChanged());
            GameThread.Run(() => { }, blocking: true);

            var sent = Assert.IsType<NetworkClanFinanceConnectionsChanged>(
                Assert.Single(network.SentNetworkMessages[peer.Id]));
            Assert.Empty(sent.DisconnectedHeroIds);
            Hero hero;
            objectManager.Verify(manager => manager.TryGetObjectWithLogging<Hero>(It.IsAny<string>(), out hero), Times.Never);
        }
        finally
        {
            ModInformation.IsServer = wasServer;
        }
    }
}
