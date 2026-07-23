using Common;
using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.MapEvents.Patches.Disable;

[HarmonyPatch(typeof(CampaignBattleRecoveryBehavior))]
internal class DisableCampaignBattleRecoveryBehavior
{
    [HarmonyPatch(nameof(CampaignBattleRecoveryBehavior.RegisterEvents))]
    static bool Prefix() => ModInformation.IsServer;
}
