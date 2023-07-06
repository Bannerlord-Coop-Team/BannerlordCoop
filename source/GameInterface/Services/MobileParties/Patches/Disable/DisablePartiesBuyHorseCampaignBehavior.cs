using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.MobileParties.Patches;

[HarmonyPatch(typeof(PartiesBuyHorseCampaignBehavior))]
internal class DisablePartiesBuyHorseCampaignBehavior
{
    [HarmonyPatch(nameof(PartiesBuyHorseCampaignBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
