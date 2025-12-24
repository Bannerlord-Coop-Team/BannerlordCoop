using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.MobileParties.Patches.Disable;

[HarmonyPatch(typeof(PartiesBuyHorseCampaignBehavior))]
internal class DisablePartiesBuyHorseCampaignBehavior
{
    [HarmonyPatch(nameof(PartiesBuyHorseCampaignBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
