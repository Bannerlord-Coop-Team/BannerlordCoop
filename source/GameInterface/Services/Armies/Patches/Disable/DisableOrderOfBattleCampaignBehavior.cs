using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Armies.Patches.Disable;

[HarmonyPatch(typeof(OrderOfBattleCampaignBehavior))]
internal class DisableOrderOfBattleCampaignBehavior
{
    [HarmonyPatch(nameof(OrderOfBattleCampaignBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
