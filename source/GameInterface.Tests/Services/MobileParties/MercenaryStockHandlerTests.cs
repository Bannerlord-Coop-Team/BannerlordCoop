using Common;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.GameState.Messages;
using GameInterface.Services.MobileParties.Handlers;
using GameInterface.Services.MobileParties.Messages;
using GameInterface.Services.ObjectManager;
using Moq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using Xunit;

namespace GameInterface.Tests.Services.MobileParties;

public class MercenaryStockHandlerTests
{
    private readonly Mock<IMessageBroker> messageBroker = new();
    private readonly Mock<IObjectManager> objectManager = new();
    private readonly Mock<INetwork> network = new();
    private readonly MercenaryStockHandler handler;

    private object? sentMessage;

    public MercenaryStockHandlerTests()
    {
        handler = new MercenaryStockHandler(messageBroker.Object, objectManager.Object, network.Object);

        network.Setup(n => n.SendAll(It.IsAny<IMessage>()))
            .Callback<IMessage>(message => sentMessage = message);
    }

    [Fact]
    public void Handle_CampaignReady_Client_RequestsStockSync()
    {
        var wasServer = ModInformation.IsServer;
        ModInformation.IsServer = false;
        try
        {
            handler.Handle_CampaignReady(new MessagePayload<CampaignReady>(this, new CampaignReady()));

            Assert.IsType<NetworkRequestMercenaryStockSync>(sentMessage!);
            network.Verify(n => n.SendAll(It.IsAny<IMessage>()), Times.Once);
        }
        finally
        {
            ModInformation.IsServer = wasServer;
        }
    }

    [Fact]
    public void Handle_CampaignReady_Server_DoesNotRequestStockSync()
    {
        var wasServer = ModInformation.IsServer;
        ModInformation.IsServer = true;
        try
        {
            handler.Handle_CampaignReady(new MessagePayload<CampaignReady>(this, new CampaignReady()));

            Assert.Null(sentMessage);
            network.Verify(n => n.SendAll(It.IsAny<IMessage>()), Times.Never);
        }
        finally
        {
            ModInformation.IsServer = wasServer;
        }
    }

    [Fact]
    public void Handle_MercenaryStockChanged_Server_SendsStockUpdate()
    {
        var wasServer = ModInformation.IsServer;
        ModInformation.IsServer = true;
        try
        {
            var town = new Town();
            var troop = new CharacterObject();
            SetupId(town, "town-1");
            SetupId(troop, "troop-1");

            handler.Handle_MercenaryStockChanged(Payload(town, troop, 12));

            var sent = Assert.IsType<NetworkUpdateMercenaryStock>(sentMessage!);
            Assert.Equal("town-1", sent.TownId);
            Assert.Equal("troop-1", sent.TroopTypeId);
            Assert.Equal(12, sent.Number);
            network.Verify(n => n.SendAll(It.IsAny<IMessage>()), Times.Once);
        }
        finally
        {
            ModInformation.IsServer = wasServer;
        }
    }

    [Fact]
    public void Handle_MercenaryStockChanged_Client_DoesNotSend()
    {
        var wasServer = ModInformation.IsServer;
        ModInformation.IsServer = false;
        try
        {
            var town = new Town();
            var troop = new CharacterObject();

            handler.Handle_MercenaryStockChanged(Payload(town, troop, 12));

            Assert.Null(sentMessage);
            network.Verify(n => n.SendAll(It.IsAny<IMessage>()), Times.Never);
        }
        finally
        {
            ModInformation.IsServer = wasServer;
        }
    }

    [Fact]
    public void Handle_MercenaryStockChanged_UnresolvableTroop_DoesNotSend()
    {
        var wasServer = ModInformation.IsServer;
        ModInformation.IsServer = true;
        try
        {
            var town = new Town();
            var troop = new CharacterObject();
            SetupId(town, "town-1");
            SetupNoId(troop);

            handler.Handle_MercenaryStockChanged(Payload(town, troop, 12));

            Assert.Null(sentMessage);
            network.Verify(n => n.SendAll(It.IsAny<IMessage>()), Times.Never);
        }
        finally
        {
            ModInformation.IsServer = wasServer;
        }
    }

    private MessagePayload<MercenaryStockChanged> Payload(Town town, CharacterObject troop, int number) =>
        new(this, new MercenaryStockChanged(town, troop, number));

    private void SetupId(object obj, string id) =>
        objectManager.Setup(o => o.TryGetIdWithLogging(obj, out id)).Returns(true);

    private void SetupNoId(object obj)
    {
        string unused = string.Empty;
        objectManager.Setup(o => o.TryGetIdWithLogging(obj, out unused)).Returns(false);
    }
}
