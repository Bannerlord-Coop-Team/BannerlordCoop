using Common;
using Common.Messaging;
using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Localization;
using Issue = TaleWorlds.CampaignSystem.Issues.VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssue;
using Quest = TaleWorlds.CampaignSystem.Issues.VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssueQuest;

namespace GameInterface.Services.Issues.Quests.VillageNeedsTools.Patches;

/// <summary>Routes delivery to the server and keeps replay from applying personal penalties twice.</summary>
[HarmonyPatch]
internal class VillageNeedsToolsOutcomePatches
{
    [HarmonyPatch(typeof(Issue), nameof(Issue.AlternativeSolutionStartConsequence))]
    [HarmonyPrefix]
    private static bool FundingPrefix() => ModInformation.IsServer;

    [HarmonyPatch(typeof(Quest), nameof(Quest.FinishQuestSuccess1))]
    [HarmonyPrefix]
    private static bool FinishQuestSuccessPrefix(Quest __instance)
    {
        return !ContainerProvider.TryResolve<IOutcomeDispatcher>(out var dispatcher)
            || dispatcher.BeforeQuestBranch(__instance, (byte)VillageNeedsToolsBranch.DeliverTools);
    }

    [HarmonyPatch(typeof(Quest), nameof(Quest.OnFailed))]
    [HarmonyPrefix]
    private static bool PenaltyPrefix() => ModInformation.IsServer;

    // These compile before the journal and sync patches exist, so inlined calls must go through helpers.
    [HarmonyPatch(typeof(Quest), nameof(Quest.FinishQuestSuccess1))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> SyncedWritesTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        var hearthSetter = AccessTools.PropertySetter(typeof(Village), nameof(Village.Hearth));
        var addPower = AccessTools.Method(typeof(Hero), nameof(Hero.AddPower));
        var addLog = AccessTools.Method(typeof(QuestBase), nameof(QuestBase.AddLog));
        foreach (var instruction in instructions)
        {
            if (instruction.Calls(hearthSetter))
            {
                instruction.opcode = OpCodes.Call;
                instruction.operand = AccessTools.Method(typeof(VillageNeedsToolsOutcomePatches), nameof(SetHearth));
            }
            else if (instruction.Calls(addPower))
            {
                instruction.opcode = OpCodes.Call;
                instruction.operand = AccessTools.Method(typeof(VillageNeedsToolsOutcomePatches), nameof(AddPower));
            }
            else if (instruction.Calls(addLog))
            {
                instruction.opcode = OpCodes.Call;
                instruction.operand = AccessTools.Method(typeof(VillageNeedsToolsOutcomePatches), nameof(AddLog));
            }
            yield return instruction;
        }
    }

    [HarmonyPatch(typeof(Quest), nameof(Quest.OnFailed))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> PenaltyWritesTranspiler(IEnumerable<CodeInstruction> instructions)
        => SyncedWritesTranspiler(instructions);

    // Never inlined, so the calls reach the current sync patches
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void SetHearth(Village village, float hearth) => village.Hearth = hearth;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void AddPower(Hero hero, float power) => hero.AddPower(power);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static JournalLog AddLog(QuestBase quest, TextObject text, bool hideInformation) => quest.AddLog(text, hideInformation);

    [HarmonyPatch(typeof(Quest), nameof(Quest.OnTimedOut))]
    [HarmonyPrefix]
    private static bool TimeoutPrefix() => ModInformation.IsServer;

    [HarmonyPatch(typeof(Quest), nameof(Quest.OnTimedOut))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> TimeoutWritesTranspiler(IEnumerable<CodeInstruction> instructions)
        => SyncedWritesTranspiler(instructions);

    // The server's daily issue tick has no player context, but the timeout relation penalty is owner-scoped.
    [HarmonyPatch(typeof(QuestBase), nameof(QuestBase.CompleteQuestWithTimeOut))]
    private static class TimeoutOwnerScopePatch
    {
        [HarmonyPrefix]
        private static void Prefix(QuestBase __instance, out IDisposable __state)
        {
            __state = null;
            if (ModInformation.IsClient || !(__instance is Quest quest) || !quest.IsOngoing ||
                !ContainerProvider.TryResolve<IVillageNeedsToolsService>(out var service)) return;

            __state = service.OpenOwnerScope(quest);
        }

        [HarmonyFinalizer]
        private static void Finalizer(IDisposable __state) => __state?.Dispose();
    }

    [HarmonyPatch(typeof(QuestBase), nameof(QuestBase.AddLog))]
    [HarmonyPostfix]
    private static void JournalPostfix(QuestBase __instance, TextObject text)
    {
        if (ModInformation.IsServer && __instance is Quest quest && text != null)
        {
            MessageBroker.Instance.Publish(quest, new VillageNeedsToolsJournalAdded(quest, text.ToString()));
        }
    }

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnIssueUpdated))]
    [HarmonyPostfix]
    private static void AlternativeJournalPostfix(IssueBase issue, IssueBase.IssueUpdateDetails details)
    {
        if (ModInformation.IsServer && issue is Issue tools &&
            (details == IssueBase.IssueUpdateDetails.SentTroopsFinishedQuest || details == IssueBase.IssueUpdateDetails.SentTroopsFailedQuest))
        {
            MessageBroker.Instance.Publish(tools, new VillageNeedsToolsAlternativeEnding(tools, details));
        }
    }
}
