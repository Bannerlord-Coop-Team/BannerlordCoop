using Common;
using Common.Messaging;
using Common.Network;
using Coop.Tests.Mocks;
using GameInterface.Services.CampaignService.Data;
using GameInterface.Services.CampaignService.Handlers;
using GameInterface.Services.CampaignService.Interfaces;
using GameInterface.Services.CampaignService.Messages;
using Moq;
using System.Runtime.CompilerServices;
using Xunit;

namespace GameInterface.Tests.Services.CampaignService;

[Collection(ModInformationRoleCollection.Name)]
public class ServerOptionsSyncTests
{
    static ServerOptionsSyncTests()
    {
        RuntimeHelpers.RunModuleConstructor(typeof(TestNetwork).Module.ModuleHandle);
    }

    [Fact]
    public void JoinInitialization_AppliesBattleSize()
    {
        var broker = new MessageBroker();
        var optionsProvider = new Mock<IServerOptionsProvider>();
        var expected = new ServerOptions(2, 600);
        using var handler = new UpdateCampaignOptionsHandler(
            broker,
            Mock.Of<INetwork>(),
            optionsProvider.Object);

        broker.Publish(this, new InitializeServerOptionsOnClient(expected));
        FlushGameThread();

        optionsProvider.Verify(provider => provider.ApplyServerOptions(expected), Times.Once);
    }

    [Fact]
    public void LiveUpdate_AppliesBattleSize()
    {
        var broker = new MessageBroker();
        var optionsProvider = new Mock<IServerOptionsProvider>();
        var expected = new ServerOptions(2, 300);
        using var handler = new UpdateCampaignOptionsHandler(
            broker,
            Mock.Of<INetwork>(),
            optionsProvider.Object);

        broker.Publish(this, new NetworkUpdateOtherOptions(expected));
        FlushGameThread();

        optionsProvider.Verify(provider => provider.ApplyServerOptions(expected), Times.Once);
    }

    [Fact]
    public void UpdateRequest_BroadcastsConsolidatedServerOptions()
    {
        var broker = new MessageBroker();
        var network = new Mock<INetwork>();
        var optionsProvider = new Mock<IServerOptionsProvider>();
        var expected = new ServerOptions(2, 800);
        optionsProvider.Setup(provider => provider.GetServerOptions()).Returns(expected);
        IMessage sent = null;
        network.Setup(value => value.SendAll(It.IsAny<IMessage>()))
            .Callback<IMessage>(message => sent = message);
        using var handler = new UpdateCampaignOptionsHandler(
            broker,
            network.Object,
            optionsProvider.Object);

        broker.Publish(this, new UpdateOtherOptions());
        FlushGameThread();

        var update = Assert.IsType<NetworkUpdateOtherOptions>(sent);
        Assert.Same(expected, update.ServerOptions);
    }

    private static void FlushGameThread()
    {
        GameThread.Run(() => { }, blocking: true);
    }
}
