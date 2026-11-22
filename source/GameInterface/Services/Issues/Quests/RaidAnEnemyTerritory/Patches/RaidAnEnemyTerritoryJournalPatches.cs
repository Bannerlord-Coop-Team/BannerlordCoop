using Common;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.UI.Notifications;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.LogEntries;
using TaleWorlds.CampaignSystem.ViewModelCollection.Quests;
using Quest = TaleWorlds.CampaignSystem.Issues.RaidAnEnemyTerritoryIssueBehavior.RaidAnEnemyTerritoryQuest;

namespace GameInterface.Services.Issues.Quests.RaidAnEnemyTerritory.Patches;

/// <summary>Keeps ownership on raid quest archives created by the accepting client.</summary>
[HarmonyPatch(typeof(JournalLogsCampaignBehavior), nameof(JournalLogsCampaignBehavior.CreateRelatedLog), new[] { typeof(IssueBase) })]
[HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
internal sealed class RaidAnEnemyTerritoryJournalCreationPatch
{
    [HarmonyPostfix]
    private static void Postfix(IssueBase issue, ref JournalLogEntry __result)
    {
        if (ModInformation.IsClient && issue.IssueQuest is Quest quest &&
            ContainerProvider.TryResolve<IQuestJournalOwnerResolver>(out var resolver))
        {
            __result = resolver.CreateOwnedEntry(__result, issue, quest);
        }
    }
}

[HarmonyPatch(typeof(QuestsVM), MethodType.Constructor, typeof(Action))]
[HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
internal sealed class RaidAnEnemyTerritoryJournalViewPatch
{
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var quests = AccessTools.PropertyGetter(typeof(QuestManager), nameof(QuestManager.Quests));
        var visibleQuests = AccessTools.Method(typeof(RaidAnEnemyTerritoryJournalViewPatch), nameof(GetVisibleQuests));
        var matches = 0;
        foreach (var instruction in instructions)
        {
            if (instruction.Calls(quests))
            {
                matches++;
                instruction.opcode = OpCodes.Call;
                instruction.operand = visibleQuests;
            }
            yield return instruction;
        }

        if (matches != 1) throw new InvalidOperationException("Raid quest journal call site changed.");
    }

    // Filter the journal's input before vanilla chooses a row. The quest manager still keeps every mirror.
    private static IEnumerable<QuestBase> GetVisibleQuests(QuestManager manager)
    {
        if (!ModInformation.IsClient)
        {
            return manager.Quests;
        }

        ContainerProvider.TryResolve<IIssueOwnerResolver>(out var owners);
        return manager.Quests.Where(quest => quest is not Quest ||
            (owners != null && IssueManager.GetIssueOfQuest(quest) is IssueBase issue && owners.IsLocalOwner(issue)));
    }
}
