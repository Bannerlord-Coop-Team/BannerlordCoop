using GameInterface.AutoSync;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Settlements;
using Quest = TaleWorlds.CampaignSystem.Issues.ArtisanOverpricedGoodsIssueBehavior.ArtisanOverpricedGoodsIssueQuest;

namespace GameInterface.Services.Issues.Quests.ArtisanOverpricedGoods;

/// <summary>Rebinds outcome callbacks after their field synchronization is installed.</summary>
internal class ArtisanOverpricedGoodsSync : IAutoSync
{
    public ArtisanOverpricedGoodsSync(AutoSyncRegistry registry)
    {
        registry.AddTargetMethod(typeof(Town), AccessTools.Method(typeof(Quest), nameof(Quest.OnCompleteWithSuccess)));
        registry.AddTargetMethod(typeof(Town), AccessTools.Method(typeof(Quest), nameof(Quest.OnFailed)));
        registry.AddTargetMethod(typeof(Town), AccessTools.Method(typeof(Quest), nameof(Quest.OnTimedOut)));
        registry.AddTargetMethod(typeof(Town), AccessTools.Method(typeof(Quest), nameof(Quest.AcceptCounterOffer)));
    }
}
