using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.MobileParties.Patches.Disable;

[HarmonyPatch(typeof(PartiesSellLootCampaignBehavior))]
internal class DisablePartiesSellLootCampaignBehavior
{
    [HarmonyPatch(nameof(PartiesSellLootCampaignBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
