using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Barters.Patches;

[HarmonyPatch(typeof(RansomOfferCampaignBehavior))]
internal class DisableRansomOfferCampaignBehavior
{
    [HarmonyPatch(nameof(RansomOfferCampaignBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
