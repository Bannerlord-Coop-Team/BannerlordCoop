using HarmonyLib;
using SandBox.CampaignBehaviors;

namespace GameInterface.Services.Towns.Patches.Disabled;

[HarmonyPatch(typeof(TownMerchantsCampaignBehavior))]
internal class DisableTownMerchantsCampaignBehavior
{
    [HarmonyPatch(nameof(TownMerchantsCampaignBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
