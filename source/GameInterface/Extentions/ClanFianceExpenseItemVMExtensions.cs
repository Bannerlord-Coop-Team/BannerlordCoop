using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement;

namespace GameInterface.Extentions;
public static class ClanFianceExpenseItemVMExtensions
{
    public static void OnCurrentWageLimitUpdated(this ClanFinanceExpenseItemVM vm, int newValue)
    {
        var updateWage = AccessTools.Method(typeof(ClanFinanceExpenseItemVM), "OnCurrentWageLimitUpdated");
        updateWage.Invoke(vm, new object[] { newValue });
    }

    public static void OnUnlimitedWageToggled(this ClanFinanceExpenseItemVM vm, bool newValue)
    {
        var unlimitedToggle = AccessTools.Method(typeof(ClanFinanceExpenseItemVM), "OnUnlimitedWageToggled");
        unlimitedToggle.Invoke(vm, new object[] { newValue });
    }
}
