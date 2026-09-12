using Common.Util;
using GameInterface.Services.Tournaments;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.TournamentGames;
using TaleWorlds.Core;
using TaleWorlds.Library;
using Xunit;

namespace GameInterface.Tests.Services.Tournaments;

public class TournamentPrizePersistenceTests
{
    [Fact]
    public void SolePlayerLeaveAndRejoin_ReusesLockedPrize()
    {
        var settlement = ObjectHelper.SkipConstructor<Settlement>();
        var townOwner = ObjectHelper.SkipConstructor<PartyBase>();
        townOwner.Settlement = settlement;
        var town = ObjectHelper.SkipConstructor<Town>();
        town._owner = townOwner;
        var prize = ObjectHelper.SkipConstructor<ItemObject>();
        var tournamentGame = ObjectHelper.SkipConstructor<FightTournamentGame>();
        tournamentGame.Prize = prize;
        tournamentGame._lastRecordedLordCountForTournamentPrize = 1;

        var hero = ObjectHelper.SkipConstructor<Hero>();
        var heroCharacter = ObjectHelper.SkipConstructor<CharacterObject>();
        heroCharacter.HeroObject = hero;
        var frozenCharacters = new MBList<CharacterObject> { heroCharacter };

        ItemObject rejoinedPrize = TournamentGameInterface.LockPrizeForFrozenRoster(
            town,
            tournamentGame,
            frozenCharacters);

        Assert.Same(prize, rejoinedPrize);
        Assert.Same(prize, tournamentGame.Prize);
        Assert.Equal(1, tournamentGame._lastRecordedLordCountForTournamentPrize);
    }
}
