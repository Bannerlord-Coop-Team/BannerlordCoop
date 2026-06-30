using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.HeroDevelopers.Handlers;
using GameInterface.Services.HeroDevelopers.Messages;
using GameInterface.Services.ObjectManager;
using Moq;
using TaleWorlds.CampaignSystem;
using Xunit;

namespace GameInterface.Tests.Services.HeroDevelopers;

/// <summary>
/// Covers raw-XP messages that reach the game thread while campaign state is being torn down.
/// </summary>
public class HeroDeveloperHandlerTests
{
    private readonly Mock<IObjectManager> objectManager = new();
    private readonly HeroDeveloperHandler handler;

    public HeroDeveloperHandlerTests()
    {
        handler = new HeroDeveloperHandler(
            new Mock<IMessageBroker>().Object,
            objectManager.Object,
            new Mock<INetwork>().Object);
    }

    [Fact]
    public void ChangeRawXp_NullCampaign_DoesNotResolveHero()
    {
        var message = CreateMessage();

        handler.ChangeRawXp(message, null!);

        Hero unresolvedHero = null!;
        objectManager.Verify(
            manager => manager.TryGetObjectWithLogging("hero-1", out unresolvedHero),
            Times.Never);
    }

    [Fact]
    public void ChangeRawXp_NullHeroDeveloper_SkipsApply()
    {
        var message = CreateMessage();
        var campaign = ObjectHelper.SkipConstructor<Campaign>();
        var hero = ObjectHelper.SkipConstructor<Hero>();
        objectManager
            .Setup(manager => manager.TryGetObjectWithLogging("hero-1", out hero))
            .Returns(true);

        handler.ChangeRawXp(message, campaign);

        objectManager.Verify(
            manager => manager.TryGetObjectWithLogging("hero-1", out hero),
            Times.Once);
    }

    private static NetworkRawXpGainClients CreateMessage() =>
        new(new NetworkRawXpGainServer("hero-1", 10f, shouldNotify: false));
}
