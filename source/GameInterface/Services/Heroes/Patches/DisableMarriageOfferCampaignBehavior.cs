using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Heroes.Patches;

[HarmonyPatch(typeof(MarriageOfferCampaignBehavior))]
internal class DisableMarriageOfferCampaignBehavior
{
    [HarmonyPatch(nameof(MarriageOfferCampaignBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
