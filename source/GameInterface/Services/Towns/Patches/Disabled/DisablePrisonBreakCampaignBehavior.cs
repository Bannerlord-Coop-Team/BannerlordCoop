using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Towns.Patches.Disabled;

[HarmonyPatch(typeof(PrisonBreakCampaignBehavior))]
internal class DisablePrisonBreakCampaignBehavior
{
    [HarmonyPatch(nameof(PrisonBreakCampaignBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
