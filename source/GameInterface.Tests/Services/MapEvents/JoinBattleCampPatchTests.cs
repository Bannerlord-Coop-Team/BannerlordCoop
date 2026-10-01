using GameInterface.Services.MapEvents.Patches;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using TaleWorlds.CampaignSystem.Encounters;
using Xunit;

namespace GameInterface.Tests.Services.MapEvents;

public class JoinBattleCampPatchTests
{
    [Fact]
    public void MultiplePatchOwners_KeepOneCampReplacement_WhenEitherOwnerIsRemoved()
    {
        var target = AccessTools.Method(typeof(PlayerEncounter), "JoinBattleInternal");
        var transpiler = AccessTools.Method(typeof(PlayerEncounterPatches), "JoinBattleCampTranspiler");
        var apply = AccessTools.Method(typeof(PlayerEncounterPatches), "ApplyJoinBattleCamp");
        var first = new Harmony($"{nameof(JoinBattleCampPatchTests)}.first.{Guid.NewGuid()}");
        var second = new Harmony($"{nameof(JoinBattleCampPatchTests)}.second.{Guid.NewGuid()}");
        try
        {
            first.Patch(target, transpiler: new HarmonyMethod(transpiler));
            second.Patch(target, transpiler: new HarmonyMethod(transpiler));
            Assert.Single(PatchProcessor.GetCurrentInstructions(target).Where(instruction => instruction.Calls(apply)));

            first.Unpatch(target, HarmonyPatchType.Transpiler, first.Id);
            Assert.Single(PatchProcessor.GetCurrentInstructions(target).Where(instruction => instruction.Calls(apply)));
        }
        finally
        {
            first.Unpatch(target, HarmonyPatchType.Transpiler, first.Id);
            second.Unpatch(target, HarmonyPatchType.Transpiler, second.Id);
        }
    }

    [Fact]
    public void MissingCampAssignment_StillRejectsUnsupportedBody()
    {
        var transpiler = AccessTools.Method(typeof(PlayerEncounterPatches), "JoinBattleCampTranspiler");
        var instructions = new[] { new CodeInstruction(OpCodes.Ret) };
        var rewritten = (IEnumerable<CodeInstruction>)transpiler.Invoke(null, new object[] { instructions });

        Assert.Throws<InvalidOperationException>(() => rewritten.ToList());
    }
}
