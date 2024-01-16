using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.MobileParties.Patches;

[HarmonyPatch(typeof(PartiesSellPrisonerCampaignBehavior))]
internal class DisablePartiesSellPrisonerCampaignBehavior
{
    [HarmonyPatch(nameof(PartiesSellPrisonerCampaignBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
