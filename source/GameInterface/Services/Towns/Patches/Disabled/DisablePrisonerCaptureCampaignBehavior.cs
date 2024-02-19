using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Towns.Patches.Disabled;

[HarmonyPatch(typeof(PrisonerCaptureCampaignBehavior))]
internal class DisablePrisonerCaptureCampaignBehavior
{
    [HarmonyPatch(nameof(PrisonerCaptureCampaignBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
