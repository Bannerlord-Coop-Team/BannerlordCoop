using HarmonyLib;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Towns.Patches.Disabled;

[HarmonyPatch(typeof(ArtisanOverpricedGoodsIssueBehavior))]
internal class DisableArtisanOverpricedGoodsIssueBehavior
{
    [HarmonyPatch(nameof(ArtisanOverpricedGoodsIssueBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
