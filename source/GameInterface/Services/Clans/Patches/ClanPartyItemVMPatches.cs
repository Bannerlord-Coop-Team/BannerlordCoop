using Common.Messaging;
using GameInterface.Services.Clans.Messages;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement;
using TaleWorlds.Core.ViewModelCollection.Selector;

namespace GameInterface.Services.Clans.Patches;

[HarmonyPatch(typeof(ClanPartyItemVM))]
internal class ClanPartyItemVMPatches
{
    [HarmonyPatch(nameof(ClanPartyItemVM.UpdateProperties))]
    [HarmonyTranspiler]
    internal static IEnumerable<CodeInstruction> UpdatePropertiesTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        var instructionList = instructions.ToList();
        int existingReplacementCount = Enumerable.Range(0, instructionList.Count)
            .Count(index => IsClanLeaderFallback(instructionList, index));
        if (existingReplacementCount == 1)
        {
            foreach (CodeInstruction instruction in instructionList)
                yield return instruction;

            yield break;
        }

        if (existingReplacementCount > 1)
            throw new InvalidOperationException($"Expected one clan leader fallback in {nameof(ClanPartyItemVM.UpdateProperties)}, found {existingReplacementCount}.");

        var replacementCount = 0;

        for (int i = 0; i < instructionList.Count; i++)
        {
            var instruction = instructionList[i];
            if (i + 1 < instructionList.Count &&
                TryGetPropertyGetter(instruction, typeof(Hero), nameof(Hero.Clan), typeof(Clan), out MethodInfo clanGetter) &&
                TryGetPropertyGetter(instructionList[i + 1], typeof(Clan), nameof(Clan.Leader), typeof(Hero), out MethodInfo clanLeaderGetter) &&
                clanGetter.ReturnType == clanLeaderGetter.DeclaringType &&
                clanLeaderGetter.ReturnType == clanGetter.DeclaringType)
            {
                Label readClanLeader = generator.DefineLabel();
                Label useClanLeader = generator.DefineLabel();
                Label lookupComplete = generator.DefineLabel();
                LocalBuilder clanLeader = generator.DeclareLocal(clanLeaderGetter.ReturnType);

                var duplicateLeader = new CodeInstruction(OpCodes.Dup);
                duplicateLeader.labels.AddRange(instruction.labels);
                instruction.labels.Clear();
                duplicateLeader.blocks.AddRange(instruction.blocks);
                instruction.blocks.Clear();
                yield return duplicateLeader;
                yield return instruction;
                yield return new CodeInstruction(OpCodes.Dup);
                yield return new CodeInstruction(OpCodes.Brtrue_S, readClanLeader);
                yield return new CodeInstruction(OpCodes.Pop);
                yield return new CodeInstruction(OpCodes.Br_S, lookupComplete);

                var clanLeaderInstruction = instructionList[++i];
                clanLeaderInstruction.labels.Add(readClanLeader);
                yield return clanLeaderInstruction;
                yield return new CodeInstruction(OpCodes.Dup);
                yield return new CodeInstruction(OpCodes.Brtrue_S, useClanLeader);
                yield return new CodeInstruction(OpCodes.Pop);
                yield return new CodeInstruction(OpCodes.Br_S, lookupComplete);

                var storeClanLeader = new CodeInstruction(OpCodes.Stloc, clanLeader);
                storeClanLeader.labels.Add(useClanLeader);
                yield return storeClanLeader;
                yield return new CodeInstruction(OpCodes.Pop);
                yield return new CodeInstruction(OpCodes.Ldloc, clanLeader);

                var completedLookup = new CodeInstruction(OpCodes.Nop);
                completedLookup.labels.Add(lookupComplete);
                yield return completedLookup;
                replacementCount++;
                continue;
            }

            yield return instruction;
        }

        if (replacementCount != 1)
            throw new InvalidOperationException($"Expected one clan leader lookup in {nameof(ClanPartyItemVM.UpdateProperties)}, found {replacementCount}.");
    }

    private static bool IsClanLeaderFallback(IReadOnlyList<CodeInstruction> instructions, int index)
    {
        if (index + 13 >= instructions.Count)
            return false;

        if (!TryGetPropertyGetter(instructions[index], typeof(Hero), nameof(Hero.Clan), typeof(Clan), out MethodInfo clanGetter) ||
            !TryGetPropertyGetter(instructions[index + 5], typeof(Clan), nameof(Clan.Leader), typeof(Hero), out MethodInfo clanLeaderGetter) ||
            clanGetter.ReturnType != clanLeaderGetter.DeclaringType ||
            clanLeaderGetter.ReturnType != clanGetter.DeclaringType)
        {
            return false;
        }

        if (instructions[index + 1].opcode != OpCodes.Dup ||
            instructions[index + 2].opcode != OpCodes.Brtrue_S ||
            instructions[index + 3].opcode != OpCodes.Pop ||
            instructions[index + 4].opcode != OpCodes.Br_S ||
            instructions[index + 6].opcode != OpCodes.Dup ||
            instructions[index + 7].opcode != OpCodes.Brtrue_S ||
            instructions[index + 8].opcode != OpCodes.Pop ||
            instructions[index + 9].opcode != OpCodes.Br_S ||
            instructions[index + 10].opcode != OpCodes.Stloc ||
            instructions[index + 11].opcode != OpCodes.Pop ||
            instructions[index + 12].opcode != OpCodes.Ldloc ||
            instructions[index + 13].opcode != OpCodes.Nop)
        {
            return false;
        }

        return instructions[index + 2].operand is Label readClanLeader &&
               instructions[index + 5].labels.Contains(readClanLeader) &&
               instructions[index + 7].operand is Label useClanLeader &&
               instructions[index + 10].labels.Contains(useClanLeader) &&
               instructions[index + 4].operand is Label nullClanComplete &&
               instructions[index + 9].operand is Label nullLeaderComplete &&
               nullClanComplete.Equals(nullLeaderComplete) &&
               instructions[index + 13].labels.Contains(nullClanComplete) &&
               instructions[index + 10].operand is LocalBuilder storedClanLeader &&
               ReferenceEquals(instructions[index + 12].operand, storedClanLeader);
    }

    private static bool TryGetPropertyGetter(
        CodeInstruction instruction,
        Type declaringType,
        string propertyName,
        Type returnType,
        out MethodInfo method)
    {
        if (instruction.opcode != OpCodes.Call && instruction.opcode != OpCodes.Callvirt)
        {
            method = null;
            return false;
        }

        method = instruction.operand as MethodInfo;
        if (method == null)
            return false;

        return method.Name == $"get_{propertyName}" &&
               method.DeclaringType?.FullName == declaringType.FullName &&
               method.ReturnType.FullName == returnType.FullName;
    }

    [HarmonyPatch(nameof(ClanPartyItemVM.UpdatePartyBehaviorSelectionUpdate))]
    [HarmonyPrefix]
    public static bool UpdatePartyBehaviorSelectionUpdatePrefix(ref ClanPartyItemVM __instance, SelectorVM<SelectorItemVM> s)
    {
        if (s.SelectedIndex != (int)__instance.Party.MobileParty.Objective)
        {
            // Manage setting the party behavior on the server
            var message = new PartyBehaviorUpdatedOnSelection(__instance.Party.MobileParty, (MobileParty.PartyObjective)s.SelectedIndex);
            MessageBroker.Instance.Publish(__instance, message);
        }

        return false;
    }
    
    [HarmonyPatch(nameof(ClanPartyItemVM.OnAutoRecruitChanged))]
    [HarmonyPrefix]
    public static bool OnAutoRecruitChangedPrefix(ref ClanPartyItemVM __instance, bool value)
    {
        if (__instance.Party.IsMobile && __instance.Party.MobileParty.IsGarrison)
        {
            Settlement homeSettlement = __instance.Party.MobileParty.HomeSettlement;
            if (homeSettlement?.Town != null)
            {
                // Manage setting auto recruitment on the server
                var message = new AutoRecruitChangedForSettlement(__instance.Party.MobileParty.HomeSettlement, value);
                MessageBroker.Instance.Publish(__instance, message);
            }
        }

        return false;
    }

}
