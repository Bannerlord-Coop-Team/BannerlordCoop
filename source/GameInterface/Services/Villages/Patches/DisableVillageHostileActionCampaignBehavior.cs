using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Villages.Patches;

[HarmonyPatch(typeof(VillageHostileActionCampaignBehavior))]
internal class DisableVillageHostileActionCampaignBehavior
{
    [HarmonyPatch(nameof(VillageHostileActionCampaignBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
