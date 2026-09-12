using Common.Util;
using GameInterface.Services.Tournaments.UI;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.TournamentGames;
using Xunit;

namespace E2E.Tests.Services.Tournaments;

public class CoopTournamentCampaignBehaviorTests
{
    [Fact]
    public void SupportedTournament_RequiresExactFightTournamentGameType()
    {
        var standardTournament = ObjectHelper.SkipConstructor<FightTournamentGame>();
        var moddedTournament = ObjectHelper.SkipConstructor<ModdedFightTournamentGame>();

        Assert.True(CoopTournamentCampaignBehavior.IsSupportedTournament(standardTournament));
        Assert.False(CoopTournamentCampaignBehavior.IsSupportedTournament(null));
        Assert.False(CoopTournamentCampaignBehavior.IsSupportedTournament(moddedTournament));
    }

    private sealed class ModdedFightTournamentGame : FightTournamentGame
    {
        private ModdedFightTournamentGame(Town town) : base(town)
        {
        }
    }
}
