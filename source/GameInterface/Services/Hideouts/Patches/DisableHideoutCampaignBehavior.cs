using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Hideouts.Patches;

[HarmonyPatch(typeof(HideoutCampaignBehavior))]
internal class DisableHideoutCampaignBehavior
{
    [HarmonyPatch(nameof(HideoutCampaignBehavior.RegisterEvents))]
    static bool Prefix() => true;
}
