using Common;
using Common.Messaging;
using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
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
    private static bool FundingPrefix(Issue __instance)
    {
        if (ModInformation.IsClient) return false;

        if (Hero.MainHero.Gold < __instance.CostOfToolsForAlternativeSolution)
        {
            throw new InvalidOperationException("The owner can no longer afford the tools");
        }

        return true;
    }

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

    // These are compiled when patched, before the sync patches exist, so the hearth setter and AddPower
    // inlined into them would skip syncing. Those calls are routed through the helpers below instead.
    [HarmonyPatch(typeof(Quest), nameof(Quest.FinishQuestSuccess1))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> SyncedWritesTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        var hearthSetter = AccessTools.PropertySetter(typeof(Village), nameof(Village.Hearth));
        var addPower = AccessTools.Method(typeof(Hero), nameof(Hero.AddPower));
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

    [HarmonyPatch(typeof(Quest), nameof(Quest.OnTimedOut))]
    [HarmonyPrefix]
    private static bool TimeoutPrefix(Quest __instance)
    {
        if (ModInformation.IsServer)
        {
            // Run the timeout's mutations through the current sync patches.
            __instance.AddLog(__instance.QuestTimeOutFailLog);
            __instance.QuestGiver.AddPower(-10f);
            __instance.QuestGiver.CurrentSettlement.Village.Hearth -= 30f;
            ChangeRelationAction.ApplyPlayerRelation(__instance.QuestGiver, -5);
        }

        return false;
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
}
