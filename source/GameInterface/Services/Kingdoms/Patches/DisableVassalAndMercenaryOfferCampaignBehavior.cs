using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Kingdoms.Patches;

[HarmonyPatch(typeof(VassalAndMercenaryOfferCampaignBehavior))]
internal class DisableVassalAndMercenaryOfferCampaignBehavior
{
    [HarmonyPatch(nameof(VassalAndMercenaryOfferCampaignBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
