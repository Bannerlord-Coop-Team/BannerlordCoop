using Common.Util;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.ViewModelCollection;

namespace GameInterface.Services.TroopRosters.Patches;

/// <summary>
/// The map tooltip refreshers in <see cref="TooltipRefresherCollection"/> (RefreshMobilePartyTooltip,
/// RefreshSettlementTooltip, RefreshArmyTooltip, RefreshEncounterTooltip, AddEncounterParties, ...)
/// build throwaway scratch rosters via <see cref="TroopRoster.CreateDummyTroopRoster"/> and populate
/// them with <see cref="TroopRoster.AddToCounts"/> purely to render counts. These rosters are UI
/// scratch and must not be registered or replicated.
///
/// This transpiler targets every method of the collection (and its compiler-generated lambda /
/// local-function display classes) that actually builds such a roster, and brackets each
/// CreateDummyTroopRoster / AddToCounts call with <see cref="AllowedThread.AllowThisThread"/> /
/// <see cref="AllowedThread.RevokeThisThread"/> so the coop patches treat the work as an allowed,
/// non-replicated operation. The allowance is ref-counted (tight brackets nest safely) and
/// AllowThisThread/RevokeThisThread are stack-neutral (no args, no return).
/// </summary>
[HarmonyPatch]
internal class TooltipRefresherDummyRosterPatch
{
    private static readonly MethodInfo CreateDummyMethod =
        AccessTools.Method(typeof(TroopRoster), nameof(TroopRoster.CreateDummyTroopRoster));
    private static readonly MethodInfo AddToCountsMethod =
        AccessTools.Method(typeof(TroopRoster), nameof(TroopRoster.AddToCounts));
    private static readonly MethodInfo AllowMethod =
        AccessTools.Method(typeof(AllowedThread), nameof(AllowedThread.AllowThisThread));
    private static readonly MethodInfo RevokeMethod =
        AccessTools.Method(typeof(AllowedThread), nameof(AllowedThread.RevokeThisThread));

    private static IEnumerable<MethodBase> TargetMethods()
    {
        var rootType = typeof(TooltipRefresherCollection);

        var types = new List<Type> { rootType };
        types.AddRange(rootType.GetNestedTypes(AccessTools.all));

        foreach (var type in types)
        {
            foreach (var method in type.GetMethods(AccessTools.all | BindingFlags.DeclaredOnly))
            {
                if (method.IsAbstract || method.ContainsGenericParameters) continue;
                if (method.GetMethodBody() == null) continue;

                if (BuildsDummyRoster(method))
                    yield return method;
            }
        }
    }

    /// <summary>
    /// Reads the original IL (without patching) to keep only methods that create or populate a
    /// dummy roster, so we don't needlessly transpile unrelated tooltip methods.
    /// </summary>
    private static bool BuildsDummyRoster(MethodInfo method)
    {
        try
        {
            var probe = new DynamicMethod("probe", typeof(void), Type.EmptyTypes);
            var instructions = PatchProcessor.GetOriginalInstructions(method, probe.GetILGenerator());
            return instructions.Any(i => i.Calls(CreateDummyMethod) || i.Calls(AddToCountsMethod));
        }
        catch
        {
            return false;
        }
    }

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        foreach (var instruction in instructions)
        {
            // Bracket both the creation and the population of the scratch roster. AllowThisThread /
            // RevokeThisThread take no args and touch no stack values, so inserting Allow immediately
            // before the call (args already pushed) and Revoke immediately after (result still on the
            // stack) is stack-neutral.
            if (instruction.Calls(CreateDummyMethod) || instruction.Calls(AddToCountsMethod))
            {
                // Keep any incoming branch target / exception-block on the first emitted instruction.
                var allow = new CodeInstruction(OpCodes.Call, AllowMethod);
                allow.labels.AddRange(instruction.labels);
                allow.blocks.AddRange(instruction.blocks);
                instruction.labels.Clear();
                instruction.blocks.Clear();

                yield return allow;            // AllowedThread.AllowThisThread()
                yield return instruction;      // CreateDummyTroopRoster() / AddToCounts(...)
                yield return new CodeInstruction(OpCodes.Call, RevokeMethod); // AllowedThread.RevokeThisThread()
            }
            else
            {
                yield return instruction;
            }
        }
    }
}
