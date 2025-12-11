using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using GameInterface;

namespace GameInterface.Services.Villages.Patches;

[HarmonyPatch(typeof(VillagerCampaignBehavior))]
internal class DisableVillagerCampaignBehavior
{
    [HarmonyPatch(nameof(VillagerCampaignBehavior.RegisterEvents))]
    static bool Prefix() => ModInformation.IsServer;
}
