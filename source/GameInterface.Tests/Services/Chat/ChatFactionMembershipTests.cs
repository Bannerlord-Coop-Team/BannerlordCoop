using GameInterface.Services.Chat;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players.Data;
using Moq;
using TaleWorlds.CampaignSystem;
using Xunit;

namespace GameInterface.Tests.Services.Chat;

public class ChatFactionMembershipTests
{
    [Fact]
    public void GetClan_EmptyHeroId_ReturnsNull()
    {
        var membership = new ChatFactionMembership(Mock.Of<IObjectManager>());
        var player = new Player("controller", string.Empty, "party", "clan-1", "character");

        Assert.Null(membership.GetClan(player));
    }

    [Fact]
    public void GetClan_MissingHero_ReturnsNull()
    {
        var objectManager = new Mock<IObjectManager>();
        Hero hero = null;
        objectManager
            .Setup(manager => manager.TryGetObject<Hero>("hero-1", out hero))
            .Returns(false);
        var membership = new ChatFactionMembership(objectManager.Object);
        var player = new Player("controller", "hero-1", "party", "stale-clan", "character");

        Assert.Null(membership.GetClan(player));
    }

    [Fact]
    public void GetKingdom_MissingHero_ReturnsNull()
    {
        var objectManager = new Mock<IObjectManager>();
        Hero hero = null;
        objectManager
            .Setup(manager => manager.TryGetObject<Hero>("hero-1", out hero))
            .Returns(false);
        var membership = new ChatFactionMembership(objectManager.Object);
        var player = new Player("controller", "hero-1", "party", "stale-clan", "character");

        Assert.Null(membership.GetKingdom(player));
    }
}
