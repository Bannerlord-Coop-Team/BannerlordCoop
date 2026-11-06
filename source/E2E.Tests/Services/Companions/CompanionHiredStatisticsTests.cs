using Common.Util;
using E2E.Tests.Util;
using GameInterface.CoopSessionData;
using GameInterface.Services.CampaignService.Statistics.Data;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using SandBox.CampaignBehaviors;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Companions;

/// <summary>
/// Players trading companions fire NewCompanionAdded without AddCompanionAction, so only an actual hire counts.
/// </summary>
public class CompanionHiredStatisticsTests : SyncTestBase
{
    public CompanionHiredStatisticsTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public void AddCompanionAction_CountsForClanLeader_TradedCompanionDoesNot()
    {
        Server.Call(() =>
        {
            var playerHero = GameObjectCreator.CreateInitializedObject<Hero>();
            playerHero.Clan.SetLeader(playerHero);
            Assert.True(Server.ObjectManager.TryGetId(playerHero, out var playerHeroId));
            Assert.True(Server.Resolve<IPlayerManager>().AddPlayer(
                new Player("PlayerOne", playerHeroId, string.Empty, string.Empty, string.Empty)));

            var playerStatistics = Server.Resolve<ICoopSessionProvider>().CoopSession.StatisticsPlayerData.PlayerStatistics;
            var statistics = new StatisticsData();
            playerStatistics[playerHeroId] = statistics;

            var companion = GameObjectCreator.CreateInitializedObject<Hero>();
            AddCompanionAction.Apply(playerHero.Clan, companion);
            Assert.Equal(1, statistics.NumberOfCompanionsHired);

            ObjectHelper.SkipConstructor<StatisticsCampaignBehavior>().OnNewCompanionAdded(companion);
            Assert.Equal(1, statistics.NumberOfCompanionsHired);
        });
    }
}
