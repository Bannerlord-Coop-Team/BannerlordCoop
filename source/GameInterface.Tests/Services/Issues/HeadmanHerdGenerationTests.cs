using Common.Util;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using Moq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
using Xunit;

namespace GameInterface.Tests.Services.Issues;

[Collection(ModInformationRoleCollection.Name)]
public class HeadmanHerdGenerationTests
{
    [Theory]
    [InlineData(5f, true, true)]
    [InlineData(5f, false, false)]
    [InlineData(20f, true, false)]
    [InlineData(10f, true, true)]
    public void GenerationUsesNearestConnectedPlayerWithStableTieBreaking(float distanceA, bool connectedA, bool expectA)
    {
        var giver = ObjectHelper.SkipConstructor<Hero>();
        giver._stayingInSettlement = ObjectHelper.SkipConstructor<Settlement>();
        giver.CurrentSettlement._position = new CampaignVec2(new Vec2(0, 0), true);
        var heroA = ObjectHelper.SkipConstructor<Hero>();
        var heroB = ObjectHelper.SkipConstructor<Hero>();
        heroA._heroState = heroB._heroState = Hero.CharacterStates.Active;
        var partyA = ObjectHelper.SkipConstructor<MobileParty>();
        var partyB = ObjectHelper.SkipConstructor<MobileParty>();
        partyA._position = new CampaignVec2(new Vec2(distanceA, 0), true);
        partyB._position = new CampaignVec2(new Vec2(10, 0), true);
        partyA.IsActive = partyB.IsActive = true;
        var playerA = new Player("A", "hero-A", "party-A", "clan-A", "character-A");
        var playerB = new Player("B", "hero-B", "party-B", "clan-B", "character-B");
        var players = new Mock<IPlayerManager>();
        players.SetupGet(x => x.Players).Returns(new[] { playerB, playerA });
        players.Setup(x => x.IsConnected(playerA)).Returns(connectedA);
        players.Setup(x => x.IsConnected(playerB)).Returns(true);
        var objects = new Mock<IObjectManager>();
        objects.Setup(x => x.TryGetObject("hero-A", out heroA)).Returns(true);
        objects.Setup(x => x.TryGetObject("hero-B", out heroB)).Returns(true);
        objects.Setup(x => x.TryGetObject("party-A", out partyA)).Returns(true);
        objects.Setup(x => x.TryGetObject("party-B", out partyB)).Returns(true);
        var context = new HeadmanHerdGenerationContext(players.Object, objects.Object);
        var previous = ResolvedMainHeroContext.ResolvedMainHero;

        Assert.True(context.TryEnter(giver, out var scope));
        using (scope)
            Assert.Same(expectA ? heroA : heroB, ResolvedMainHeroContext.ResolvedMainHero);
        Assert.Same(previous, ResolvedMainHeroContext.ResolvedMainHero);
    }

    [Fact]
    public void NoRegisteredPlayerDefersGenerationWithoutSubstitution()
    {
        var giver = ObjectHelper.SkipConstructor<Hero>();
        giver._stayingInSettlement = ObjectHelper.SkipConstructor<Settlement>();
        var players = new Mock<IPlayerManager>();
        players.SetupGet(x => x.Players).Returns(System.Array.Empty<Player>());
        var objects = new Mock<IObjectManager>(MockBehavior.Strict);
        var context = new HeadmanHerdGenerationContext(players.Object, objects.Object);
        var previous = ResolvedMainHeroContext.ResolvedMainHero;

        Assert.False(context.TryEnter(giver, out var scope));
        Assert.Null(scope);
        Assert.Same(previous, ResolvedMainHeroContext.ResolvedMainHero);
        objects.VerifyNoOtherCalls();
    }
}
