using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using GameInterface;

namespace GameInterface.Services.Villages.Patches;

[HarmonyPatch(typeof(VillageHostileActionCampaignBehavior))]
internal class DisableVillageHostileActionCampaignBehavior
{
    [HarmonyPatch(nameof(VillageHostileActionCampaignBehavior.RegisterEvents))]
    static bool Prefix() => ModInformation.IsServer;
}
