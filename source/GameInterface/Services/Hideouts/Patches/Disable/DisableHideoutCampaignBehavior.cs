using Common;
using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Hideouts.Patches.Disable;

[HarmonyPatch(typeof(HideoutCampaignBehavior))]
internal class DisableHideoutCampaignBehavior
{
    [HarmonyPatch(nameof(HideoutCampaignBehavior.RegisterEvents))]
    static bool Prefix() => ModInformation.IsServer;
}
