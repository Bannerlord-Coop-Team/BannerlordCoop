using GameInterface.Services.Issues.Interfaces;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Patches;

using Issue = HeadmanNeedsToDeliverAHerdIssueBehavior.HeadmanNeedsToDeliverAHerdIssue;

[HarmonyPatch(typeof(Issue), MethodType.Constructor, typeof(Hero))]
internal static class HeadmanHerdIssueConstructorPatch
{
    [HarmonyTranspiler]
    internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        var baseConstructor = AccessTools.Constructor(typeof(IssueBase), new[] { typeof(Hero), typeof(CampaignTime) });
        var replicaGetter = AccessTools.PropertyGetter(typeof(HeadmanHerdIssueReplicaScope), nameof(HeadmanHerdIssueReplicaScope.IsActive));
        var matches = 0;
        foreach (var instruction in instructions)
        {
            yield return instruction;
            if (instruction.opcode != OpCodes.Call || !Equals(instruction.operand, baseConstructor)) continue;
            matches++;
            var generate = generator.DefineLabel();
            // Keep base initialization, but never reroll destination or herd on a receiving client.
            yield return new CodeInstruction(OpCodes.Call, replicaGetter);
            yield return new CodeInstruction(OpCodes.Brfalse, generate);
            yield return new CodeInstruction(OpCodes.Ret);
            var continuation = new CodeInstruction(OpCodes.Nop);
            continuation.labels.Add(generate);
            yield return continuation;
        }
        if (matches != 1)
            throw new InvalidOperationException("Deliver the Herd constructor no longer has exactly one IssueBase initialization");
    }
}
