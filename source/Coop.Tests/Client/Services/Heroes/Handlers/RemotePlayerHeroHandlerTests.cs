using Common.Tests.Utils;
using Coop.Core.Client.Services.Heroes.Handlers;
using Coop.Core.Client.Services.Heroes.Messages;
using GameInterface.Services.Heroes.Interfaces;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using Moq;
using Xunit;

namespace Coop.Tests.Client.Services.Heroes.Handlers;

public class RemotePlayerHeroHandlerTests
{
    private readonly TestMessageBroker messageBroker = new();
    private readonly Mock<IHeroInterface> heroInterface = new();
    private readonly Mock<IPlayerManager> playerManager = new();
    private readonly RemotePlayerHeroHandler handler;

    public RemotePlayerHeroHandlerTests()
    {
        handler = new RemotePlayerHeroHandler(messageBroker, heroInterface.Object, playerManager.Object);
    }

    private static NetworkNewPlayerHeroCreated NewHeroMessage(out Player player, out byte[] heroData)
    {
        player = new Player("ctrl", "hero1", "party1", "clan1", "char1");
        heroData = new byte[] { 1, 2, 3 };
        return new NetworkNewPlayerHeroCreated("ctrl", player, heroData);
    }

    [Fact]
    public void NewPlayerHeroCreated_RegistersAndUnpacksImmediately()
    {
        var message = NewHeroMessage(out var player, out var heroData);
        playerManager.Setup(x => x.AddPlayer(player)).Returns(true);

        // No campaign-ready gate any more: the server queue already withheld this until the client is
        // in the campaign, so it is handled the instant it arrives.
        messageBroker.Publish(this, message);

        playerManager.Verify(x => x.AddPlayer(player), Times.Once);
        heroInterface.Verify(x => x.ClientUnpackHero(heroData, player), Times.Once);
    }

    [Fact]
    public void DuplicatePlayer_DoesNotUnpack()
    {
        var message = NewHeroMessage(out var player, out _);
        playerManager.Setup(x => x.AddPlayer(player)).Returns(false);

        messageBroker.Publish(this, message);

        // A duplicate registration is logged and skipped — never unpacked a second time.
        heroInterface.Verify(x => x.ClientUnpackHero(It.IsAny<byte[]>(), It.IsAny<Player>()), Times.Never);
    }
}
