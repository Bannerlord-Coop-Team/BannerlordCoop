using Common.Logging;
using GameInterface.Services.MobileParties.Extensions;
using HarmonyLib;
using Serilog;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Utils;

/// <summary>
/// Reusable transpiler helper that rewrites reference comparisons against
/// <see cref="MobileParty.MainParty"/> into the equivalent
/// <see cref="MobilePartyExtensions.IsPlayerParty(MobileParty)"/> check, so that game logic
/// which special-cases the single local <see cref="MobileParty.MainParty"/> instead treats
/// every player-controlled party as "the player" in co-op.
///
/// The rewrite is driven entirely by the IL comparison opcode that follows the
/// <c>get_MainParty</c> call, so it preserves the original truth value at every site
/// regardless of how the comparison was spelled in C#:
/// <list type="bullet">
///   <item><c>ceq</c>            (value form, <c>== MainParty</c>) -> <c>call IsPlayerParty</c></item>
///   <item><c>beq[.s]</c>        (branch if equal)                -> <c>call IsPlayerParty; brtrue[.s]</c></item>
///   <item><c>bne.un[.s]</c>     (branch if not equal)            -> <c>call IsPlayerParty; brfalse[.s]</c></item>
/// </list>
/// A value-form <c>!=</c> (<c>ceq; ldc.i4.0; ceq</c>) keeps its trailing negation, so it
/// becomes <c>!IsPlayerParty()</c>.
/// </summary>
/// <remarks>
/// Apply this from the <see cref="HarmonyTranspiler"/> of any method that compares a
/// <see cref="MobileParty"/> against <see cref="MobileParty.MainParty"/>:
/// <code>
/// [HarmonyTranspiler]
/// static IEnumerable&lt;CodeInstruction&gt; Transpiler(IEnumerable&lt;CodeInstruction&gt; instructions)
///     =&gt; MainPartyComparisonTranspiler.ReplaceMainPartyComparisonsWithIsPlayerParty(instructions);
/// </code>
/// </remarks>
public class MainPartyComparisonTranspiler
{
    private static readonly ILogger Logger = LogManager.GetLogger<MainPartyComparisonTranspiler>();

    private MainPartyComparisonTranspiler() { }

    /// <summary>
    /// Replaces every reference comparison against <see cref="MobileParty.MainParty"/> with the
    /// equivalent <see cref="MobilePartyExtensions.IsPlayerParty(MobileParty)"/> check.
    /// </summary>
    public static IEnumerable<CodeInstruction> ReplaceMainPartyComparisonsWithIsPlayerParty(
        IEnumerable<CodeInstruction> instructions)
    {
        var isPlayerParty = AccessTools.Method(
            typeof(MobilePartyExtensions),
            nameof(MobilePartyExtensions.IsPlayerParty),
            new[] { typeof(MobileParty) });

        var codes = new List<CodeInstruction>(instructions);
        var result = new List<CodeInstruction>(codes.Count);
        var replacements = 0;

        for (var i = 0; i < codes.Count; i++)
        {
            var current = codes[i];

            // Look for: call/callvirt MobileParty::get_MainParty  immediately followed by a comparison.
            if (!IsMainPartyGetter(current) || i + 1 >= codes.Count)
            {
                result.Add(current);
                continue;
            }

            var comparison = codes[i + 1];

            // The IsPlayerParty call consumes the value already on the stack (the left operand
            // that was loaded just before get_MainParty), so the get_MainParty push is dropped.
            // Labels on the dropped getter and on the comparison move onto the new call.
            var callIsPlayerParty = new CodeInstruction(OpCodes.Call, isPlayerParty);
            callIsPlayerParty.labels.AddRange(current.labels);
            callIsPlayerParty.labels.AddRange(comparison.labels);
            callIsPlayerParty.blocks.AddRange(comparison.blocks);

            if (comparison.opcode == OpCodes.Ceq)
            {
                // value-form: x == MainParty  ->  x.IsPlayerParty()
                // A trailing "ldc.i4.0; ceq" (the != negation) is left untouched and still applies.
                result.Add(callIsPlayerParty);
                i++; // consume the ceq
                replacements++;
            }
            else if (comparison.opcode == OpCodes.Beq || comparison.opcode == OpCodes.Beq_S)
            {
                // branch if equal to MainParty  ->  branch if IsPlayerParty
                result.Add(callIsPlayerParty);
                result.Add(new CodeInstruction(
                    comparison.opcode == OpCodes.Beq ? OpCodes.Brtrue : OpCodes.Brtrue_S,
                    comparison.operand));
                i++;
                replacements++;
            }
            else if (comparison.opcode == OpCodes.Bne_Un || comparison.opcode == OpCodes.Bne_Un_S)
            {
                // branch if NOT equal to MainParty  ->  branch if NOT IsPlayerParty
                result.Add(callIsPlayerParty);
                result.Add(new CodeInstruction(
                    comparison.opcode == OpCodes.Bne_Un ? OpCodes.Brfalse : OpCodes.Brfalse_S,
                    comparison.operand));
                i++;
                replacements++;
            }
            else
            {
                // Unrecognized usage of get_MainParty (e.g. stored, passed as an argument).
                // Leave it alone rather than risk corrupting the method body.
                Logger.Warning(
                    "Skipping a MobileParty.MainParty use that is not a direct comparison (followed by {OpCode})",
                    comparison.opcode);
                result.Add(current);
            }
        }

        if (replacements == 0)
        {
            Logger.Warning(
                "{Transpiler} made no replacements; the target method may have changed",
                nameof(ReplaceMainPartyComparisonsWithIsPlayerParty));
        }
        else
        {
            Logger.Debug(
                "{Transpiler} replaced {Count} MobileParty.MainParty comparison(s) with IsPlayerParty()",
                nameof(ReplaceMainPartyComparisonsWithIsPlayerParty), replacements);
        }

        return result;
    }

    private static bool IsMainPartyGetter(CodeInstruction instruction)
    {
        return (instruction.opcode == OpCodes.Call || instruction.opcode == OpCodes.Callvirt)
            && instruction.operand is MethodInfo method
            && method.DeclaringType == typeof(MobileParty)
            && method.Name == "get_MainParty";
    }
}
