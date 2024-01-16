using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Settlements.Patches.Disable;


[HarmonyPatch(typeof(SallyOutsCampaignBehavior))]
internal class DisableSallyOutsCampaignBehavior
{
    [HarmonyPatch(nameof(SallyOutsCampaignBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
