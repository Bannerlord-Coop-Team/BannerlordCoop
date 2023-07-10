using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.MobileParties.Patches;

[HarmonyPatch(typeof(PartiesSellLootCampaignBehavior))]
internal class DisablePartiesSellLootCampaignBehavior
{
    [HarmonyPatch(nameof(PartiesSellLootCampaignBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
