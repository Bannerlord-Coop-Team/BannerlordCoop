using HarmonyLib;
using SandBox.Issues;
using GameInterface.Policies;

namespace GameInterface.Services.MobileParties.Patches;

[HarmonyPatch(typeof(TheSpyPartyIssueQuestBehavior))]
internal class DisableTheSpyPartyIssueQuestBehavior
{
    [HarmonyPatch(nameof(TheSpyPartyIssueQuestBehavior.RegisterEvents))]
    static bool Prefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return false;
    }
}
