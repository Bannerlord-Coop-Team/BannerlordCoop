using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Battles.Patches.Disable;

[HarmonyPatch(typeof(CampaignBattleRecoveryBehavior))]
internal class DisableCampaignBattleRecoveryBehavior
{
    [HarmonyPatch(nameof(CampaignBattleRecoveryBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
