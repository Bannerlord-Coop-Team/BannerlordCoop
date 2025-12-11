using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using GameInterface;

namespace GameInterface.Services.Heroes.Patches.Disable;

[HarmonyPatch(typeof(MarriageOfferCampaignBehavior))]
internal class DisableMarriageOfferCampaignBehavior
{
    [HarmonyPatch(nameof(MarriageOfferCampaignBehavior.RegisterEvents))]
    static bool Prefix() => ModInformation.IsServer;
}
