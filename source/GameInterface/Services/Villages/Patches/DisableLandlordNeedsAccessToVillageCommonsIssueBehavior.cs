using HarmonyLib;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Villages.Patches;

[HarmonyPatch(typeof(LandlordNeedsAccessToVillageCommonsIssueBehavior))]
internal class DisableLandlordNeedsAccessToVillageCommonsIssueBehavior
{
    [HarmonyPatch(nameof(LandlordNeedsAccessToVillageCommonsIssueBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
