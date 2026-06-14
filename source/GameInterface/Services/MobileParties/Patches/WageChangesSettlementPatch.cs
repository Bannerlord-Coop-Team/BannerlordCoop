using Common;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.MobileParties.Messages;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement;

namespace GameInterface.Services.MobileParties.Patches;

/// <summary>
/// Used to allow modifying a party's wages
/// </summary>
[HarmonyPatch(typeof(ClanFinanceExpenseItemVM))]
internal class WageChangesSettlementPatch
{
    private static readonly MethodInfo MobileParty_SetWagePaymentLimit = typeof(MobileParty).GetMethod(nameof(MobileParty.SetWagePaymentLimit));

    private static readonly MethodInfo MobileParty_SetWagePaymentLimitOverride =
        typeof(WageChangesSettlementPatch).GetMethod(nameof(SetWagePaymentLimitOverride), BindingFlags.NonPublic | BindingFlags.Static);

    static IEnumerable<MethodBase> TargetMethods()
    {
        // if possible use nameof() or SymbolExtensions.GetMethodInfo() here
        yield return AccessTools.Method(typeof(ClanFinanceExpenseItemVM), nameof(ClanFinanceExpenseItemVM.OnUnlimitedWageToggled));
        yield return AccessTools.Method(typeof(ClanFinanceExpenseItemVM), nameof(ClanFinanceExpenseItemVM.OnCurrentWageLimitUpdated));

    }

    [HarmonyTranspiler]
    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        foreach (var instruction in instructions)
        {
            if (instruction.opcode == OpCodes.Callvirt && instruction.operand as MethodInfo == MobileParty_SetWagePaymentLimit)
            {
                yield return new CodeInstruction(OpCodes.Call, MobileParty_SetWagePaymentLimitOverride);
                continue;
            }
            yield return instruction;
        }
    }

    private static void SetWagePaymentLimitOverride(MobileParty instance, int newValue)
    {
        if (ModInformation.IsServer || CallOriginalPolicy.IsOriginalAllowed())
        {
            instance.SetWagePaymentLimit(newValue);
            return;
        }

        var message = new WagePaymentLimitSet(instance, newValue);
        MessageBroker.Instance.Publish(instance, message);
    }
}
