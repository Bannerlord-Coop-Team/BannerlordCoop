using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Banners.Patches;

[HarmonyPatch(typeof(BannerCampaignBehavior))]
internal class DisableBannerCampaignBehavior
{
    [HarmonyPatch(nameof(BannerCampaignBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
