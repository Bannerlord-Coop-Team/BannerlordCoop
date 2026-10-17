using HarmonyLib;
using System.Collections.Generic;
using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using Issue = TaleWorlds.CampaignSystem.Issues.HeadmanNeedsToDeliverAHerdIssueBehavior.HeadmanNeedsToDeliverAHerdIssue;

namespace GameInterface.Services.Issues.Quests.HeadmanNeedsToDeliverAHerd.Patches;

[HarmonyPatch]
internal class HeadmanHerdNavigationPatch
{
    private static MethodBase TargetMethod()
    {
        return typeof(Issue).GetNestedTypes(AccessTools.all)
            .SelectMany(AccessTools.GetDeclaredMethods)
            .Single(method => method.Name.StartsWith("<.ctor>") && method.ReturnType == typeof(bool) &&
                method.GetParameters().Length == 1 && method.GetParameters()[0].ParameterType == typeof(Settlement));
    }

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var matcher = new CodeMatcher(instructions);
        var mainParty = AccessTools.PropertyGetter(typeof(MobileParty), nameof(MobileParty.MainParty));
        var navigation = AccessTools.PropertyGetter(typeof(MobileParty), nameof(MobileParty.NavigationCapability));
        var replaced = 0;

        // Issue generation has no player context; vanilla's default land route selects the destination.
        while (matcher.MatchStartForward(CodeMatch.Calls(mainParty), CodeMatch.Calls(navigation)).IsValid)
        {
            replaced++;
            matcher.SetInstructionAndAdvance(new CodeInstruction(OpCodes.Ldc_I4, (int)MobileParty.NavigationType.Default));
            matcher.SetInstructionAndAdvance(new CodeInstruction(OpCodes.Nop));
        }

        if (replaced != 2) throw new InvalidOperationException("The herd destination predicate no longer has two player navigation reads");
        return matcher.InstructionEnumeration();
    }
}
