﻿using Common;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.MobileParties.Messages;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement;

namespace GameInterface.Services.MobileParties.Patches;


/// <summary>
/// Used to allow a party to modify wages
/// </summary>
[HarmonyPatch(typeof(ClanFinanceExpenseItemVM))]
internal class WageChangesSettlementPatch
{
    private static MethodInfo MobileParty_SetWagePaymentLimit = typeof(MobileParty).GetMethod(nameof(MobileParty.SetWagePaymentLimit));

    private static MethodInfo MobileParty_SetWagePaymentLimitOverride =
        typeof(WageChangesSettlementPatch).GetMethod(nameof(SetWagePaymentLimitOverride), BindingFlags.NonPublic | BindingFlags.Static);

    // TODO: Needs more testing to verify it works
    static IEnumerable<MethodBase> TargetMethods()
    {
        // if possible use nameof() or SymbolExtensions.GetMethodInfo() here
        yield return AccessTools.Method(typeof(ClanFinanceExpenseItemVM), "OnUnlimitedWageToggled");
        yield return AccessTools.Method(typeof(ClanFinanceExpenseItemVM), "OnCurrentWageLimitUpdated");

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
        if (ModInformation.IsServer || AllowedThread.IsThisThreadAllowed())
        {
            instance.SetWagePaymentLimit(newValue);
            return;
        }

        // Publish -> ClientHandler -> ServerHandler -> OtherClientHandle

        // This event doesn't exist and should be a IResponse from the server, there will also need to be a network message

        var message = new ChangedWagePaymentLimit(instance.StringId, newValue);
        MessageBroker.Instance.Publish(instance, message);

    }

}
