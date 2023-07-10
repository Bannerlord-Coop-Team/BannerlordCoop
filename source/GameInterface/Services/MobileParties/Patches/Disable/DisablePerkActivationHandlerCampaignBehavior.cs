using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.MobileParties.Patches;

[HarmonyPatch(typeof(PerkActivationHandlerCampaignBehavior))]
internal class DisablePerkActivationHandlerCampaignBehavior
{
    [HarmonyPatch(nameof(PerkActivationHandlerCampaignBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
