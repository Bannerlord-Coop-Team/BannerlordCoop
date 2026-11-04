using Common;
using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.MapEvents.Patches;

[HarmonyPatch(typeof(BattleCampaignBehavior))]
internal class BattleCampaignBehaviorPatches
{
    [HarmonyPatch(nameof(BattleCampaignBehavior.OnHeroCombatHit))]
    [HarmonyPrefix]
    static bool OnHeroCombatHitPrefix() => ModInformation.IsServer;

    [HarmonyPatch(nameof(BattleCampaignBehavior.OnCollectLootItems))]
    [HarmonyPrefix]
    static bool OnCollectLootItemsPrefix() => ModInformation.IsServer;
}
