using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Battles.Patches;

[HarmonyPatch(typeof(BattleCampaignBehavior))]
internal class DisableBattleCampaignBehavior
{
    [HarmonyPatch(nameof(BattleCampaignBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
