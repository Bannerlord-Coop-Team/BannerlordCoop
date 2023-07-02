using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.MobileParties.Patches;

[HarmonyPatch(typeof(DiscardItemsCampaignBehavior))]
internal class DisableDiscardItemsCampaignBehavior
{
    [HarmonyPatch(nameof(DiscardItemsCampaignBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
