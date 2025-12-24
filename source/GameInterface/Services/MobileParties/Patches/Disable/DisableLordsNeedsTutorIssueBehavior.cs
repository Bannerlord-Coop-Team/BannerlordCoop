using HarmonyLib;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.MobileParties.Patches.Disable;

[HarmonyPatch(typeof(LordsNeedsTutorIssueBehavior))]
internal class DisableLordsNeedsTutorIssueBehavior
{
    [HarmonyPatch(nameof(LordsNeedsTutorIssueBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
