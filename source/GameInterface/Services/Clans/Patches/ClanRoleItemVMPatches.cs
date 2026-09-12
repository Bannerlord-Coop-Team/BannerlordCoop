using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement;

namespace GameInterface.Services.Clans.Patches;

[HarmonyPatch(typeof(ClanRoleItemVM))]
internal class ClanRoleItemVMPatches
{
    [HarmonyPatch(nameof(ClanRoleItemVM.Refresh))]
    [HarmonyTranspiler]
    internal static IEnumerable<CodeInstruction> RefreshTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        var instructionList = instructions.ToList();
        int existingReplacementCount = Enumerable.Range(0, instructionList.Count)
            .Count(index => IsNullSafeMemberLookup(instructionList, index));
        if (existingReplacementCount == 1)
        {
            foreach (CodeInstruction instruction in instructionList)
                yield return instruction;

            yield break;
        }

        if (existingReplacementCount > 1)
            throw new InvalidOperationException($"Expected one null-safe role member lookup in {nameof(ClanRoleItemVM.Refresh)}, found {existingReplacementCount}.");

        int replacementCount = 0;
        for (int index = 0; index < instructionList.Count; index++)
        {
            CodeInstruction instruction = instructionList[index];
            if (index > 0 && index + 2 < instructionList.Count &&
                IsLoadLocal(instructionList[index - 1]) &&
                IsLoadLocal(instructionList[index + 2]) &&
                TryGetPropertyGetter(
                    instruction,
                    typeof(ClanRoleMemberItemVM),
                    nameof(ClanRoleMemberItemVM.Member),
                    typeof(ClanPartyMemberItemVM),
                    out MethodInfo memberGetter) &&
                TryGetPropertyGetter(
                    instructionList[index + 1],
                    typeof(ClanPartyMemberItemVM),
                    nameof(ClanPartyMemberItemVM.HeroObject),
                    typeof(Hero),
                    out MethodInfo heroGetter) &&
                memberGetter.ReturnType == heroGetter.DeclaringType)
            {
                Label readHero = generator.DefineLabel();
                Label lookupComplete = generator.DefineLabel();

                yield return instruction;
                yield return new CodeInstruction(OpCodes.Dup);
                yield return new CodeInstruction(OpCodes.Brtrue_S, readHero);
                yield return new CodeInstruction(OpCodes.Pop);
                yield return new CodeInstruction(OpCodes.Ldnull);
                yield return new CodeInstruction(OpCodes.Br_S, lookupComplete);

                CodeInstruction heroGetterInstruction = instructionList[++index];
                heroGetterInstruction.labels.Add(readHero);
                yield return heroGetterInstruction;

                var completedLookup = new CodeInstruction(OpCodes.Nop);
                completedLookup.labels.Add(lookupComplete);
                yield return completedLookup;
                replacementCount++;
                continue;
            }

            yield return instruction;
        }

        if (replacementCount != 1)
            throw new InvalidOperationException($"Expected one role member hero lookup in {nameof(ClanRoleItemVM.Refresh)}, found {replacementCount}.");
    }

    private static bool IsNullSafeMemberLookup(IReadOnlyList<CodeInstruction> instructions, int index)
    {
        if (index == 0 || index + 8 >= instructions.Count ||
            !IsLoadLocal(instructions[index - 1]) ||
            !IsLoadLocal(instructions[index + 8]))
            return false;

        if (!TryGetPropertyGetter(
                instructions[index],
                typeof(ClanRoleMemberItemVM),
                nameof(ClanRoleMemberItemVM.Member),
                typeof(ClanPartyMemberItemVM),
                out MethodInfo memberGetter) ||
            !TryGetPropertyGetter(
                instructions[index + 6],
                typeof(ClanPartyMemberItemVM),
                nameof(ClanPartyMemberItemVM.HeroObject),
                typeof(Hero),
                out MethodInfo heroGetter) ||
            memberGetter.ReturnType != heroGetter.DeclaringType)
        {
            return false;
        }

        if (instructions[index + 1].opcode != OpCodes.Dup ||
            instructions[index + 2].opcode != OpCodes.Brtrue_S ||
            instructions[index + 3].opcode != OpCodes.Pop ||
            instructions[index + 4].opcode != OpCodes.Ldnull ||
            instructions[index + 5].opcode != OpCodes.Br_S ||
            instructions[index + 7].opcode != OpCodes.Nop)
        {
            return false;
        }

        return instructions[index + 2].operand is Label readHero &&
               instructions[index + 6].labels.Contains(readHero) &&
               instructions[index + 5].operand is Label lookupComplete &&
               instructions[index + 7].labels.Contains(lookupComplete);
    }

    private static bool IsLoadLocal(CodeInstruction instruction)
    {
        return instruction.opcode == OpCodes.Ldloc ||
               instruction.opcode == OpCodes.Ldloc_S ||
               instruction.opcode == OpCodes.Ldloc_0 ||
               instruction.opcode == OpCodes.Ldloc_1 ||
               instruction.opcode == OpCodes.Ldloc_2 ||
               instruction.opcode == OpCodes.Ldloc_3;
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
}
