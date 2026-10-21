using Common.Messaging;
using Common.Util;
using GameInterface.Services.Clans;
using GameInterface.Services.Heroes.RomanceFlow;
using GameInterface.Services.Players;
using Moq;
using TaleWorlds.CampaignSystem;
using Xunit;

namespace GameInterface.Tests.Services.Heroes.RomanceFlow;

/// <summary>
/// Covers divorce eligibility and which player leaves a shared clan after divorce.
/// </summary>
public class PlayerDivorceRulesTests
{
    private readonly Mock<IPlayerManager> players = new();
    private readonly Mock<IClanLeaveRules> leave = new(MockBehavior.Strict);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CanDivorce_AcceptsMutualLivingPlayerOrNpcSpouses(bool playerSpouse)
    {
        var first = CreateHero();
        var second = CreateHero();
        Marry(first, second);
        players.Setup(manager => manager.Contains(first)).Returns(true);
        players.Setup(manager => manager.Contains(second)).Returns(playerSpouse);

        Assert.True(Rules().CanDivorce(first, second, out var reason));
        Assert.Null(reason);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    public void CanDivorce_RejectsInvalidPairsWithoutChangingSpouses(int invalidPair)
    {
        var first = CreateHero();
        var second = CreateHero();
        Marry(first, second);
        players.Setup(manager => manager.Contains(first)).Returns(invalidPair != 3);
        if (invalidPair == 0) first = null;
        if (invalidPair == 1) second = null;
        if (invalidPair == 2) second = first;
        if (invalidPair == 4) first!._heroState = Hero.CharacterStates.Dead;
        if (invalidPair == 5) second!._heroState = Hero.CharacterStates.Dead;
        if (invalidPair == 6) first!._spouse = null;
        if (invalidPair == 7) second!._spouse = null;
        var firstSpouse = first?.Spouse;
        var secondSpouse = second?.Spouse;

        Assert.False(Rules().CanDivorce(first, second, out var reason));
        Assert.False(Rules().TryApply(first, second, out var separated, out _));
        Assert.False(separated);
        Assert.NotEmpty(reason);
        Assert.Same(firstSpouse, first?.Spouse);
        Assert.Same(secondSpouse, second?.Spouse);
        leave.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void TrySeparateClans_UsesNonleaderRegardlessOfArgumentOrder(bool leaderFirst, bool departureAccepted)
    {
        var leader = CreateHero();
        var member = CreateHero();
        var clan = ObjectHelper.SkipConstructor<Clan>();
        clan._leader = leader;
        leader._clan = clan;
        member._clan = clan;
        players.Setup(manager => manager.Contains(leader)).Returns(true);
        players.Setup(manager => manager.Contains(member)).Returns(true);
        leave.Setup(rules => rules.TryApply(member, false)).Returns(departureAccepted);

        Assert.Equal(departureAccepted,
            Rules().TrySeparateClans(leaderFirst ? leader : member, leaderFirst ? member : leader));

        leave.Verify(rules => rules.TryApply(member, false), Times.Once);
        leave.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TrySeparateClans_LeavesNpcMembershipUnchanged(bool sharedClan)
    {
        var first = CreateHero();
        var second = CreateHero();
        first._clan = ObjectHelper.SkipConstructor<Clan>();
        second._clan = sharedClan ? first.Clan : ObjectHelper.SkipConstructor<Clan>();
        players.Setup(manager => manager.Contains(first)).Returns(true);

        Assert.True(Rules().TrySeparateClans(first, second));
        leave.VerifyNoOtherCalls();
    }

    [Fact]
    public void TrySeparateClans_LeavesAlreadyIndependentPlayersUnchanged()
    {
        var first = CreateHero();
        var second = CreateHero();
        first._clan = ObjectHelper.SkipConstructor<Clan>();
        second._clan = ObjectHelper.SkipConstructor<Clan>();
        players.Setup(manager => manager.Contains(first)).Returns(true);
        players.Setup(manager => manager.Contains(second)).Returns(true);

        Assert.True(Rules().TrySeparateClans(first, second));
        leave.VerifyNoOtherCalls();
    }

    [Fact]
    public void TrySeparateClans_RefusesToMovePlayersBeforeDivorce()
    {
        var first = CreateHero();
        var second = CreateHero();
        Marry(first, second);
        players.Setup(manager => manager.Contains(first)).Returns(true);
        players.Setup(manager => manager.Contains(second)).Returns(true);

        Assert.False(Rules().TrySeparateClans(first, second));
        leave.VerifyNoOtherCalls();
    }

    private PlayerDivorceRules Rules() => new(players.Object, leave.Object, Mock.Of<IMessageBroker>());

    private static Hero CreateHero()
    {
        var hero = ObjectHelper.SkipConstructor<Hero>();
        hero._heroState = Hero.CharacterStates.Active;
        return hero;
    }

    private static void Marry(Hero first, Hero second)
    {
        first._spouse = second;
        second._spouse = first;
    }
}
