using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Towns.Patches.Disabled;

[HarmonyPatch(typeof(GovernorCampaignBehavior))]
internal class DisableGovernorCampaignBehavior
{
    [HarmonyPatch(nameof(GovernorCampaignBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
