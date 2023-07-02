using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Towns.Patches;

[HarmonyPatch(typeof(TownMerchantsCampaignBehavior))]
internal class DisableTownMerchantsCampaignBehavior
{
    [HarmonyPatch(nameof(TownMerchantsCampaignBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
