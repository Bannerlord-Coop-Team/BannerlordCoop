using GameInterface.Services.Clans.Interfaces;
using GameInterface.Services.Clans.Extensions;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;

namespace GameInterface.Services.Clans.Patches;

[HarmonyPatch(typeof(DefaultClanFinanceModel))]
[HarmonyPatchCategory(DeferredCategory)]
internal class DefaultClanFinanceModelPatches
{
    internal const string DeferredCategory = "CoopClanFinanceDeferred";

    [HarmonyPatch(nameof(DefaultClanFinanceModel.AddExpenseFromLeaderParty))]
    [HarmonyPrefix]
    private static bool AddExpenseFromLeaderPartyPrefix(DefaultClanFinanceModel __instance, Clan clan, ExplainedNumber goldChange, bool applyWithdrawals, ref int __result)
    {
        if (clan == null || !clan.IsPlayerClan()) return true;
        if (!ContainerProvider.TryResolve<IDefaultClanFinanceModelInterface>(out var financeModelInterface)) return true;

        __result = financeModelInterface.AddExpenseFromLeaderParty(__instance, clan, goldChange, applyWithdrawals);

        return false;
    }

    [HarmonyPatch(nameof(DefaultClanFinanceModel.CalculateClanIncomeInternal))]
    [HarmonyPrefix]
    public static bool CalculateClanIncomeInternalPrefix(DefaultClanFinanceModel __instance, Clan clan, ref ExplainedNumber goldChange, bool applyWithdrawals = false, bool includeDetails = false)
    {
        if (clan == null || !clan.IsPlayerClan()) return true;
        if (!ContainerProvider.TryResolve<IDefaultClanFinanceModelInterface>(out var financeModelInterface)) return true;

        financeModelInterface.CalculateClanIncomeInternal(__instance, clan, ref goldChange, applyWithdrawals, includeDetails);

        return false;
    }
}
