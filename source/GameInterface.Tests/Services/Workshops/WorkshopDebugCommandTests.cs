using Common.Util;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Workshops.Commands;
using Moq;
using TaleWorlds.CampaignSystem;
using Xunit;

namespace GameInterface.Tests.Services.Workshops;

public class WorkshopDebugCommandTests
{
    [Fact]
    public void ResolveHero_RegistryId_ReturnsRegisteredPlayerHero()
    {
        Hero playerHero = ObjectHelper.SkipConstructor<Hero>();
        var objectManager = new Mock<IObjectManager>();
        objectManager
            .Setup(manager => manager.TryGetObject<Hero>("Hero_Player2863", out playerHero))
            .Returns(true);

        Hero result = WorkshopDebugCommand.ResolveHero("Hero_Player2863", objectManager.Object);

        Assert.Same(playerHero, result);
    }
}
