using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Towns.Patches;

[HarmonyPatch(typeof(PrisonBreakCampaignBehavior))]
internal class DisablePrisonBreakCampaignBehavior
{
    [HarmonyPatch(nameof(PrisonBreakCampaignBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
