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
    public void TryGetClan_EmptyHeroId_ReturnsFalse()
    {
        var membership = new ChatFactionMembership(Mock.Of<IObjectManager>());
        var player = new Player("controller", string.Empty, "party", "clan-1", "character");

        Assert.False(membership.TryGetClan(player, out var clan));
        Assert.Null(clan);
    }

    [Fact]
    public void TryGetClan_MissingHero_ReturnsFalse()
    {
        var objectManager = new Mock<IObjectManager>();
        Hero hero = null;
        objectManager
            .Setup(manager => manager.TryGetObject<Hero>("hero-1", out hero))
            .Returns(false);
        var membership = new ChatFactionMembership(objectManager.Object);
        var player = new Player("controller", "hero-1", "party", "stale-clan", "character");

        Assert.False(membership.TryGetClan(player, out var clan));
        Assert.Null(clan);
    }

    [Fact]
    public void TryGetKingdom_MissingHero_ReturnsFalse()
    {
        var objectManager = new Mock<IObjectManager>();
        Hero hero = null;
        objectManager
            .Setup(manager => manager.TryGetObject<Hero>("hero-1", out hero))
            .Returns(false);
        var membership = new ChatFactionMembership(objectManager.Object);
        var player = new Player("controller", "hero-1", "party", "stale-clan", "character");

        Assert.False(membership.TryGetKingdom(player, out var kingdom));
        Assert.Null(kingdom);
    }
}
