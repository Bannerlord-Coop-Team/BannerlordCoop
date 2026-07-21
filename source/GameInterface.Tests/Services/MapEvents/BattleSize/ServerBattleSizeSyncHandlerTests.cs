using Common;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.MapEvents.BattleSize;
using GameInterface.Services.UI.CoopOptions;
using GameInterface.Services.UI.Messages;
using Moq;
using Xunit;

namespace GameInterface.Tests.Services.MapEvents.BattleSize;

[Collection(ModInformationRoleCollection.Name)]
public class ServerBattleSizeSyncHandlerTests
{
    [Fact]
    public void ServerSelection_AppliesAndBroadcastsBattleSize()
    {
        bool wasServer = ModInformation.IsServer;
        ModInformation.IsServer = true;

        try
        {
            var messageBroker = new MessageBroker();
            var network = new Mock<INetwork>();
            var optionsStore = new Mock<ICoopOptionsStore>();
            var battleSizeProvider = new ServerBattleSizeProvider();
            optionsStore.Setup(m => m.LoadOrDefault()).Returns(new CoopOptionsData());

            IMessage sentMessage = null!;
            network.Setup(m => m.SendAll(It.IsAny<IMessage>()))
                .Callback<IMessage>(message => sentMessage = message);

            using var handler = new ServerBattleSizeSyncHandler(
                messageBroker,
                network.Object,
                optionsStore.Object,
                battleSizeProvider);

            messageBroker.Publish(this, new ServerBattleSizeSelected(650));

            Assert.Equal(650, battleSizeProvider.BattleSize);
            var update = Assert.IsType<NetworkBattleSizeChanged>(sentMessage);
            Assert.Equal(650, update.BattleSize);
        }
        finally
        {
            ModInformation.IsServer = wasServer;
        }
    }

    [Fact]
    public void ClientUpdate_AppliesServerBattleSizeWithoutBroadcasting()
    {
        bool wasServer = ModInformation.IsServer;
        ModInformation.IsServer = false;

        try
        {
            var messageBroker = new MessageBroker();
            var network = new Mock<INetwork>();
            var optionsStore = new Mock<ICoopOptionsStore>();
            var battleSizeProvider = new ServerBattleSizeProvider();

            using var handler = new ServerBattleSizeSyncHandler(
                messageBroker,
                network.Object,
                optionsStore.Object,
                battleSizeProvider);

            messageBroker.Publish(this, new NetworkBattleSizeChanged(725));

            Assert.Equal(725, battleSizeProvider.BattleSize);
            network.Verify(m => m.SendAll(It.IsAny<IMessage>()), Times.Never);
            optionsStore.Verify(m => m.LoadOrDefault(), Times.Never);
        }
        finally
        {
            ModInformation.IsServer = wasServer;
        }
    }
}
