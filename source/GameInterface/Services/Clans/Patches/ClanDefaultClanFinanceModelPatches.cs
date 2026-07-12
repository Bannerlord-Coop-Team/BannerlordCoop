using GameInterface.Services.Clans.Interfaces;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;

namespace GameInterface.Services.Clans.Patches;

[HarmonyPatch(typeof(DefaultClanFinanceModel))]
internal class DefaultClanFinanceModelPatches
{
    [HarmonyPatch(nameof(DefaultClanFinanceModel.AddExpenseFromLeaderParty))]
    [HarmonyPrefix]
    private static bool AddExpenseFromLeaderPartyPrefix(DefaultClanFinanceModel __instance, Clan clan, ExplainedNumber goldChange, bool applyWithdrawals, ref int __result)
    {
        ContainerProvider.TryResolve<IDefaultClanFinanceModelInterface>(out var financeModelInterface);

        __result = financeModelInterface.AddExpenseFromLeaderParty(__instance, clan, goldChange, applyWithdrawals);

        return false;
    }

    [HarmonyPatch(nameof(DefaultClanFinanceModel.CalculateClanIncomeInternal))]
    [HarmonyPrefix]
    public static bool CalculateClanIncomeInternalPrefix(DefaultClanFinanceModel __instance, Clan clan, ref ExplainedNumber goldChange, bool applyWithdrawals = false, bool includeDetails = false)
    {
        ContainerProvider.TryResolve<IDefaultClanFinanceModelInterface>(out var financeModelInterface);

        financeModelInterface.CalculateClanIncomeInternal(__instance, clan, ref goldChange, applyWithdrawals, includeDetails);

        return false;
    }
}
