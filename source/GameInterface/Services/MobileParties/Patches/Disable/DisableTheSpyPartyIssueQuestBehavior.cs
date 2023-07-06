using HarmonyLib;
using SandBox.Issues;

namespace GameInterface.Services.MobileParties.Patches;

[HarmonyPatch(typeof(TheSpyPartyIssueQuestBehavior))]
internal class DisableTheSpyPartyIssueQuestBehavior
{
    [HarmonyPatch(nameof(TheSpyPartyIssueQuestBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
