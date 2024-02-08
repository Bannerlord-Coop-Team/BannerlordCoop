using HarmonyLib;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Towns.Patches.Disabled;

[HarmonyPatch(typeof(MerchantArmyOfPoachersIssueBehavior))]
internal class DisableMerchantArmyOfPoachersIssueBehavior
{
    [HarmonyPatch(nameof(MerchantArmyOfPoachersIssueBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
