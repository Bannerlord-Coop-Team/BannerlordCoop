using E2E.Tests.Environment.Instance;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.Core;

namespace E2E.Tests.Util;

/// <summary>
/// Installs the game models the vanilla troop xp cap reads, since the harness Campaign boots without models.
/// </summary>
internal static class XpCapModels
{
    public static void Install(EnvironmentInstance instance)
    {
        instance.Call(() =>
        {
            if (Campaign.Current.Models != null) return;

            var models = new List<GameModel>
            {
                new DefaultCharacterStatsModel(),
                new DefaultPartyTroopUpgradeModel(),
                new DefaultPrisonerRecruitmentCalculationModel(),
            };
            Campaign.Current._gameModels = instance.GameInstance.Game.AddGameModelsManager<GameModels>(models);
        });
    }
}
