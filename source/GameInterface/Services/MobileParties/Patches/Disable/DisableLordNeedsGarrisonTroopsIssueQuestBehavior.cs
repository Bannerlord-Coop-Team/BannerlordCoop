using HarmonyLib;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.MobileParties.Patches;

[HarmonyPatch(typeof(LordNeedsGarrisonTroopsIssueQuestBehavior))]
internal class DisableLordNeedsGarrisonTroopsIssueQuestBehavior
{
    [HarmonyPatch(nameof(LordNeedsGarrisonTroopsIssueQuestBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
