using HarmonyLib;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Villages.Patches;

[HarmonyPatch(typeof(LandlordTrainingForRetainersIssueBehavior))]
internal class DisableLandlordTrainingForRetainersIssueBehavior
{
    [HarmonyPatch(nameof(LandlordTrainingForRetainersIssueBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
