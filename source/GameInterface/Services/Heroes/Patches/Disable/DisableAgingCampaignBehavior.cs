using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using GameInterface;

namespace GameInterface.Services.Heroes.Patches.Disable;

[HarmonyPatch(typeof(AgingCampaignBehavior))]
internal class DisableAgingCampaignBehavior
{
    [HarmonyPatch(nameof(AgingCampaignBehavior.RegisterEvents))]
    static bool Prefix() => ModInformation.IsServer;
}
