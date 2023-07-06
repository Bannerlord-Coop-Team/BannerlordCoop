using HarmonyLib;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.MobileParties.Patches;

[HarmonyPatch(typeof(LordsNeedsTutorIssueBehavior))]
internal class DisableLordsNeedsTutorIssueBehavior
{
    [HarmonyPatch(nameof(LordsNeedsTutorIssueBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
