using Common.Logging;
using GameInterface.Services.Heroes.Extensions;
using HarmonyLib;
using Serilog;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Utils;

/// <summary>
/// Reusable transpiler helper that rewrites reference comparisons against
/// <see cref="Hero.MainHero"/> into the equivalent
/// <see cref="HeroExtensions.IsPlayerHero(Hero)"/> check, so that game logic which
/// special-cases the single local <see cref="Hero.MainHero"/> instead treats every
/// player-controlled hero as "the player" in co-op.
///
/// The rewrite is driven entirely by the IL comparison opcode that follows the
/// <c>get_MainHero</c> call, so it preserves the original truth value at every site
/// regardless of how the comparison was spelled in C#:
/// <list type="bullet">
///   <item><c>ceq</c>            (value form, <c>== MainHero</c>) -> <c>call IsPlayerHero</c></item>
///   <item><c>beq[.s]</c>        (branch if equal)               -> <c>call IsPlayerHero; brtrue[.s]</c></item>
///   <item><c>bne.un[.s]</c>     (branch if not equal)           -> <c>call IsPlayerHero; brfalse[.s]</c></item>
/// </list>
/// A value-form <c>!=</c> (<c>ceq; ldc.i4.0; ceq</c>) keeps its trailing negation, so it
/// becomes <c>!IsPlayerHero()</c>.
/// </summary>
/// <remarks>
/// Apply this from the <see cref="HarmonyTranspiler"/> of any method that compares a
/// <see cref="Hero"/> against <see cref="Hero.MainHero"/>:
/// <code>
/// [HarmonyTranspiler]
/// static IEnumerable&lt;CodeInstruction&gt; Transpiler(IEnumerable&lt;CodeInstruction&gt; instructions)
///     =&gt; MainHeroComparisonTranspiler.ReplaceMainHeroComparisonsWithIsPlayerHero(instructions);
/// </code>
/// </remarks>
public class MainHeroComparisonTranspiler
{
    private static readonly ILogger Logger = LogManager.GetLogger<MainHeroComparisonTranspiler>();

    private MainHeroComparisonTranspiler() { }

    /// <summary>
    /// Replaces every reference comparison against <see cref="Hero.MainHero"/> with the
    /// equivalent <see cref="HeroExtensions.IsPlayerHero(Hero)"/> check.
    /// </summary>
    public static IEnumerable<CodeInstruction> ReplaceMainHeroComparisonsWithIsPlayerHero(
        IEnumerable<CodeInstruction> instructions)
    {
        var isPlayerHero = AccessTools.Method(
            typeof(HeroExtensions),
            nameof(HeroExtensions.IsPlayerHero),
            new[] { typeof(Hero) });

        var codes = new List<CodeInstruction>(instructions);
        var result = new List<CodeInstruction>(codes.Count);
        var replacements = 0;

        for (var i = 0; i < codes.Count; i++)
        {
            var current = codes[i];

            // Look for: call/callvirt Hero::get_MainHero  immediately followed by a comparison.
            if (!IsMainHeroGetter(current) || i + 1 >= codes.Count)
            {
                result.Add(current);
                continue;
            }

            var comparison = codes[i + 1];

            // The IsPlayerHero call consumes the value already on the stack (the left operand
            // that was loaded just before get_MainHero), so the get_MainHero push is dropped.
            // Labels on the dropped getter and on the comparison move onto the new call.
            var callIsPlayerHero = new CodeInstruction(OpCodes.Call, isPlayerHero);
            callIsPlayerHero.labels.AddRange(current.labels);
            callIsPlayerHero.labels.AddRange(comparison.labels);
            callIsPlayerHero.blocks.AddRange(comparison.blocks);

            if (comparison.opcode == OpCodes.Ceq)
            {
                // value-form: x == MainHero  ->  x.IsPlayerHero()
                // A trailing "ldc.i4.0; ceq" (the != negation) is left untouched and still applies.
                result.Add(callIsPlayerHero);
                i++; // consume the ceq
                replacements++;
            }
            else if (comparison.opcode == OpCodes.Beq || comparison.opcode == OpCodes.Beq_S)
            {
                // branch if equal to MainHero  ->  branch if IsPlayerHero
                result.Add(callIsPlayerHero);
                result.Add(new CodeInstruction(
                    comparison.opcode == OpCodes.Beq ? OpCodes.Brtrue : OpCodes.Brtrue_S,
                    comparison.operand));
                i++;
                replacements++;
            }
            else if (comparison.opcode == OpCodes.Bne_Un || comparison.opcode == OpCodes.Bne_Un_S)
            {
                // branch if NOT equal to MainHero  ->  branch if NOT IsPlayerHero
                result.Add(callIsPlayerHero);
                result.Add(new CodeInstruction(
                    comparison.opcode == OpCodes.Bne_Un ? OpCodes.Brfalse : OpCodes.Brfalse_S,
                    comparison.operand));
                i++;
                replacements++;
            }
            else
            {
                // Unrecognized usage of get_MainHero (e.g. stored, passed as an argument).
                // Leave it alone rather than risk corrupting the method body.
                Logger.Warning(
                    "Skipping a Hero.MainHero use that is not a direct comparison (followed by {OpCode})",
                    comparison.opcode);
                result.Add(current);
            }
        }

        if (replacements == 0)
        {
            Logger.Warning(
                "{Transpiler} made no replacements; the target method may have changed",
                nameof(ReplaceMainHeroComparisonsWithIsPlayerHero));
        }
        else
        {
            Logger.Debug(
                "{Transpiler} replaced {Count} Hero.MainHero comparison(s) with IsPlayerHero()",
                nameof(ReplaceMainHeroComparisonsWithIsPlayerHero), replacements);
        }

        return result;
    }

    private static bool IsMainHeroGetter(CodeInstruction instruction)
    {
        return (instruction.opcode == OpCodes.Call || instruction.opcode == OpCodes.Callvirt)
            && instruction.operand is MethodInfo method
            && method.DeclaringType == typeof(Hero)
            && method.Name == "get_MainHero";
    }
}
