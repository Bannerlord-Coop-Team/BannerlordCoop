using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.MobileParties.Patches.Disable;

[HarmonyPatch(typeof(MobilePartyTrainingBehavior))]
internal class DisableMobilePartyTrainingBehavior
{
    [HarmonyPatch(nameof(MobilePartyTrainingBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
