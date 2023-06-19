using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Villages.Patches;

[HarmonyPatch(typeof(VillageGoodProductionCampaignBehavior))]
internal class DisableVillageGoodProductionCampaignBehavior
{
    [HarmonyPatch(nameof(VillageGoodProductionCampaignBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
