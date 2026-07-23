using HarmonyLib;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Villages.Patches;

[HarmonyPatch(typeof(LandLordCompanyOfTroubleIssueBehavior))]
internal class DisableLandLordCompanyOfTroubleIssueBehavior
{
    [HarmonyPatch(nameof(LandLordCompanyOfTroubleIssueBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
