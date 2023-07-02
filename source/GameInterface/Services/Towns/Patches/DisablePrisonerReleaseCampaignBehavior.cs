using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Towns.Patches;

[HarmonyPatch(typeof(PrisonerReleaseCampaignBehavior))]
internal class DisablePrisonerReleaseCampaignBehavior
{
    [HarmonyPatch(nameof(PrisonerReleaseCampaignBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
