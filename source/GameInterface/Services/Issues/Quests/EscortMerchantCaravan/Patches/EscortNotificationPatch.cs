using Common;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using Quest = TaleWorlds.CampaignSystem.Issues.EscortMerchantCaravanIssueBehavior.EscortMerchantCaravanIssueQuest;

namespace GameInterface.Services.Issues.Quests.EscortMerchantCaravan.Patches;

[HarmonyPatch]
internal class EscortNotificationPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.DeclaredMethod(typeof(Quest), nameof(Quest.CheckEncounterForBanditParty));
        yield return AccessTools.DeclaredMethod(typeof(Quest), nameof(Quest.NotifyPlayerOrCancelTheQuestIfCaravanIsFar));
        yield return AccessTools.DeclaredMethod(typeof(Quest), nameof(Quest.TryToFindAndSetTargetToNextSettlement));
    }

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var original = AccessTools.Method(typeof(MBInformationManager), nameof(MBInformationManager.AddQuickInformation));
        var replacement = AccessTools.Method(typeof(EscortNotificationPatch), nameof(Notify));
        var count = 0;
        foreach (var instruction in instructions)
        {
            if (instruction.Calls(original))
            {
                count++;
                instruction.opcode = OpCodes.Call;
                instruction.operand = replacement;
            }
            yield return instruction;
        }
        if (count != 1) throw new InvalidOperationException("Escort notification call site changed.");
    }

    private static void Notify(TextObject text, int extraTimeInMs, BasicCharacterObject announcer,
        Equipment equipment, string soundEventPath)
    {
        var quest = EscortQuestEventPatches.CurrentQuest;
        if (ModInformation.IsServer && quest != null && ContainerProvider.TryResolve<IEscortProgress>(out var progress))
            progress.Notify(quest, text, extraTimeInMs);
    }
}
