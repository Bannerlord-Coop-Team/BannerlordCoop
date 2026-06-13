using Autofac;
using Coop.Core.Client.Messages;
using Coop.Core.Client.Services.Heroes.Messages;
using GameInterface.Services.Heroes.Interfaces;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Coop.Tests.Client.Services.Heroes;

public class RemotePlayerHeroHandlerTests
{
    private readonly ClientTestComponent clientComponent;
    private readonly Mock<IPlayerManager> playerManagerMock;
    private readonly Mock<IHeroInterface> heroInterfaceMock;

    private static readonly Player ExistingPlayer = new("other", "hero1", "party1", "clan1", "char1");

    public RemotePlayerHeroHandlerTests(ITestOutputHelper output)
    {
        clientComponent = new ClientTestComponent(output);
        playerManagerMock = clientComponent.Container.Resolve<Mock<IPlayerManager>>();
        heroInterfaceMock = clientComponent.Container.Resolve<Mock<IHeroInterface>>();
    }

    [Fact]
    public void ExistingPlayers_BeforeCampaignReady_RegisterOnCampaignEntry()
    {
        // While loading, the snapshot is held; the player objects do not exist yet.
        clientComponent.TestMessageBroker.Publish(this, new NetworkExistingPlayers(new[] { ExistingPlayer }));
        playerManagerMock.Verify(m => m.AddPlayer(ExistingPlayer), Times.Never());

        clientComponent.TestMessageBroker.Publish(this, new ClientCampaignEntered());

        // Registered once the campaign exists — registry record only, no hero unpacking
        // (the hero came inside the transfer save).
        playerManagerMock.Verify(m => m.AddPlayer(ExistingPlayer), Times.Once());
        heroInterfaceMock.Verify(m => m.ClientUnpackHero(It.IsAny<byte[]>(), It.IsAny<Player>()), Times.Never());
    }

    [Fact]
    public void ExistingPlayers_AfterCampaignReady_RegisterImmediately()
    {
        clientComponent.TestMessageBroker.Publish(this, new ClientCampaignEntered());

        clientComponent.TestMessageBroker.Publish(this, new NetworkExistingPlayers(new[] { ExistingPlayer }));

        playerManagerMock.Verify(m => m.AddPlayer(ExistingPlayer), Times.Once());
    }
}
