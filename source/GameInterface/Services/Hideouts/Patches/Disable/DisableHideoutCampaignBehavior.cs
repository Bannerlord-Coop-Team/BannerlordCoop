using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Hideouts.Patches.Disable;

[HarmonyPatch(typeof(HideoutCampaignBehavior))]
internal class DisableHideoutCampaignBehavior
{
    [HarmonyPatch(nameof(HideoutCampaignBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
