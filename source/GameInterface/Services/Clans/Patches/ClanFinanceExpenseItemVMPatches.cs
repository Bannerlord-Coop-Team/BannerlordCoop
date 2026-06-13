using Common.Util;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement;

namespace GameInterface.Services.Clans.Patches;

/// <summary>
/// Allows party wage limit slider to be temporarily managed client side until the ClanPartiesVM is finalized
/// This way only one message has to be sent to update wage limits for lord parties and garrisons
/// </summary>
[HarmonyPatch]
internal class ClanFinanceExpenseItemVMPatches
{
    private static IEnumerable<MethodBase> TargetMethods() => new MethodBase[]
    {
        //AccessTools.Method(typeof(ClanFinanceExpenseItemVM), nameof(ClanFinanceExpenseItemVM.OnCurrentWageLimitUpdated)),
        //AccessTools.Method(typeof(ClanFinanceExpenseItemVM), nameof(ClanFinanceExpenseItemVM.OnUnlimitedWageToggled))
    };

    static void Prefix()
    {
        AllowedThread.AllowThisThread();
    }

    static void Postfix()
    {
        AllowedThread.RevokeThisThread();
    }
}
