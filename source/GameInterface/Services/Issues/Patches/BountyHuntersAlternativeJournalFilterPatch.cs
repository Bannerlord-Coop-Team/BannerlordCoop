using Common;
using GameInterface.Services.Issues.Generic;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.ViewModelCollection.Quests;

namespace GameInterface.Services.Issues.Patches;

/// <summary>Hides other players' bounty alternatives before the journal selects an entry.</summary>
[HarmonyPatch(typeof(QuestsVM), MethodType.Constructor, new[] { typeof(Action) })]
internal class BountyHuntersAlternativeJournalFilterPatch
{
    private static readonly FieldInfo IssuesField = AccessTools.Field(typeof(IssueManager), nameof(IssueManager.Issues));

    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> FilterJournalIssues(IEnumerable<CodeInstruction> instructions)
    {
        var matches = 0;
        foreach (var instruction in instructions)
        {
            yield return instruction;
            if (!instruction.LoadsField(IssuesField)) continue;
            yield return CodeInstruction.Call(typeof(BountyHuntersAlternativeJournalFilterPatch), nameof(Filter));
            matches++;
        }
        if (matches != 1) throw new InvalidOperationException("The journal issue enumeration changed");
    }

    internal static IEnumerable<KeyValuePair<Hero, IssueBase>> Filter(IEnumerable<KeyValuePair<Hero, IssueBase>> issues)
    {
        if (ModInformation.IsClient)
        {
            ContainerProvider.TryResolve<IIssueOwnershipRegistry>(out var ownership);
            return issues.Where(entry => entry.Value is not CapturedByBountyHuntersIssueBehavior.CapturedByBountyHuntersIssue
                || (ownership != null && ownership.IsLocalPeerOwner(entry.Key)));
        }
        return issues;
    }
}
