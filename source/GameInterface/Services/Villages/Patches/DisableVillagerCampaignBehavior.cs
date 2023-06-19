using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Villages.Patches;

[HarmonyPatch(typeof(VillagerCampaignBehavior))]
internal class DisableVillagerCampaignBehavior
{
    [HarmonyPatch(nameof(VillagerCampaignBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
