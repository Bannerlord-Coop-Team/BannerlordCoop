using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Clans.Patches;

[HarmonyPatch(typeof(PeaceOfferCampaignBehavior))]
internal class DisablePeaceOfferCampaignBehavior
{
    [HarmonyPatch(nameof(PeaceOfferCampaignBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
