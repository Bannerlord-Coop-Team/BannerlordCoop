using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.MobileParties.Patches.Disable;

[HarmonyPatch(typeof(DiscardItemsCampaignBehavior))]
internal class DisableDiscardItemsCampaignBehavior
{
    [HarmonyPatch(nameof(DiscardItemsCampaignBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
