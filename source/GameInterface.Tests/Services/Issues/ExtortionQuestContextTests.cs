using Common.Util;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Issues;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using Moq;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using Xunit;

namespace GameInterface.Tests.Services.Issues;

public class ExtortionQuestContextTests
{
    [Fact]
    public void MissingPlayerObjectsCannotUseTheCurrentPlayersContext()
    {
        var giver = ObjectHelper.SkipConstructor<Hero>();
        var ownership = new IssueOwnershipRegistry();
        ownership.SetOwner(giver, "quest-player");
        var players = new Mock<IPlayerManager>();
        var objects = new Mock<IObjectManager>();
        var player = new Player("quest-player", "player-hero", "missing-party", "clan", "character");
        var hero = ObjectHelper.SkipConstructor<Hero>();
        players.Setup(p => p.TryGetPlayer("quest-player", out player)).Returns(true);
        objects.Setup(o => o.TryGetObjectWithLogging<Hero>("player-hero", out hero)).Returns(true);
        var context = new ExtortionQuestContext(ownership, players.Object, objects.Object);
        var previous = ResolvedMainHeroContext.ResolvedMainHero;

        Assert.False(context.TryEnter(giver, out var scope));
        Assert.Null(scope);
        Assert.Same(previous, ResolvedMainHeroContext.ResolvedMainHero);
    }

    [Fact]
    public void NestedQuestFailureRestoresThePreviousPlayersContext()
    {
        var ownership = new IssueOwnershipRegistry();
        var players = new Mock<IPlayerManager>();
        var objects = new Mock<IObjectManager>();
        var firstGiver = ObjectHelper.SkipConstructor<Hero>();
        var secondGiver = ObjectHelper.SkipConstructor<Hero>();
        var firstHero = Register(ownership, players, objects, firstGiver, "first");
        var secondHero = Register(ownership, players, objects, secondGiver, "second");
        var context = new ExtortionQuestContext(ownership, players.Object, objects.Object);
        var previous = ResolvedMainHeroContext.ResolvedMainHero;
        var previouslyAuthorized = IssueFinalizeAuthorityGuard.IsActive;

        Assert.True(context.TryEnter(firstGiver, out var outer));
        using (outer)
        {
            Assert.Same(firstHero, ResolvedMainHeroContext.ResolvedMainHero);
            Assert.True(IssueFinalizeAuthorityGuard.IsActive);
            Assert.False(AllowedThread.IsThisThreadAllowed());
            Assert.Throws<InvalidOperationException>((Action)(() =>
            {
                Assert.True(context.TryEnter(secondGiver, out var inner));
                using (inner)
                {
                    Assert.Same(secondHero, ResolvedMainHeroContext.ResolvedMainHero);
                    throw new InvalidOperationException();
                }
            }));
            Assert.Same(firstHero, ResolvedMainHeroContext.ResolvedMainHero);
            Assert.True(IssueFinalizeAuthorityGuard.IsActive);
        }
        outer.Dispose();
        Assert.Same(previous, ResolvedMainHeroContext.ResolvedMainHero);
        Assert.Equal(previouslyAuthorized, IssueFinalizeAuthorityGuard.IsActive);
    }

    private static Hero Register(IssueOwnershipRegistry ownership, Mock<IPlayerManager> players,
        Mock<IObjectManager> objects, Hero giver, string id)
    {
        var hero = ObjectHelper.SkipConstructor<Hero>();
        var party = ObjectHelper.SkipConstructor<MobileParty>();
        var player = new Player(id, id + "-hero", id + "-party", id + "-clan", id + "-character");
        ownership.SetOwner(giver, id);
        players.Setup(p => p.TryGetPlayer(id, out player)).Returns(true);
        objects.Setup(o => o.TryGetObjectWithLogging<Hero>(player.HeroId, out hero)).Returns(true);
        objects.Setup(o => o.TryGetObjectWithLogging<MobileParty>(player.MobilePartyId, out party)).Returns(true);
        return hero;
    }
}
