using HarmonyLib;
using SandBox.ViewModelCollection;
using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using TaleWorlds.CampaignSystem;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.Armies.Patches;

[HarmonyPatch(typeof(SPOrderOfBattleVM), nameof(SPOrderOfBattleVM.GetAgentTooltip))]
internal static class CaptainTooltipPatches
{
    [HarmonyTranspiler]
    internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var findHero = AccessTools.Method(typeof(Hero), nameof(Hero.FindFirst));
        var getCaptainHero = AccessTools.Method(typeof(CaptainTooltipPatches), nameof(GetCaptainHero));
        var replacements = 0;
        foreach (var instruction in instructions)
        {
            if (!instruction.Calls(findHero))
            {
                yield return instruction;
                continue;
            }

            // Hero and character StringIds come from separate co-op registry counters.
            yield return new CodeInstruction(OpCodes.Pop).MoveLabelsFrom(instruction).MoveBlocksFrom(instruction);
            yield return new CodeInstruction(OpCodes.Ldarg_1);
            yield return new CodeInstruction(OpCodes.Call, getCaptainHero);
            replacements++;
        }

        if (replacements != 1) throw new InvalidOperationException("Captain tooltip hero lookup changed.");
    }

    internal static Hero GetCaptainHero(Agent agent) => (agent.Character as CharacterObject)?.HeroObject;
}
