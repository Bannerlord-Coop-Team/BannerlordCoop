using GameInterface.AutoSync;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Settlements;
using Quest = TaleWorlds.CampaignSystem.Issues.ArtisanCantSellProductsAtAFairPriceIssueBehavior.ArtisanCantSellProductsAtAFairPriceIssueQuest;

namespace GameInterface.Services.Issues.Quests.ArtisanProducts;

/// <summary>Rebinds artisan outcome callbacks after town field synchronization is installed.</summary>
internal class ArtisanProductsSync : IAutoSync
{
    public ArtisanProductsSync(AutoSyncRegistry registry)
    {
        registry.AddTargetMethod(typeof(Town), AccessTools.Method(typeof(Quest), nameof(Quest.OnCompleteWithSuccess)));
        registry.AddTargetMethod(typeof(Town), AccessTools.Method(typeof(Quest), nameof(Quest.OnFailed)));
        registry.AddTargetMethod(typeof(Town), AccessTools.Method(typeof(Quest), nameof(Quest.OnTimedOut)));
    }
}
