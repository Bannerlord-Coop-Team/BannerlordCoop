using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.CampaignService.Patches;

[HarmonyPatch(typeof(TeleportationCampaignBehavior))]
internal class DisableTeleportationCampaignBehavior
{
    [HarmonyPatch(nameof(TeleportationCampaignBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
