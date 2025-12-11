using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using GameInterface;

namespace GameInterface.Services.Villages.Patches;

[HarmonyPatch(typeof(VillageHealCampaignBehavior))]
internal class DisableVillageHealCampaignBehavior
{
    [HarmonyPatch(nameof(VillageHealCampaignBehavior.RegisterEvents))]
    static bool Prefix() => ModInformation.IsServer;
}
