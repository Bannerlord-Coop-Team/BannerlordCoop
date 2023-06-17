using HarmonyLib;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Villages.Patches;

[HarmonyPatch(typeof(HeadmanNeedsToDeliverAHerdIssueBehavior))]
internal class DisableHeadmanNeedsToDeliverAHerdIssueBehavior
{
    [HarmonyPatch(nameof(HeadmanNeedsToDeliverAHerdIssueBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
