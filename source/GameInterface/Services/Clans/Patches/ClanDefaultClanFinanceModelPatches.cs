using Common.Messaging;
using GameInterface.Services.Clans.Interfaces;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.UI.Notifications.Messages;
using HarmonyLib;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Clans.Patches;

[HarmonyPatch(typeof(DefaultClanFinanceModel))]
[HarmonyPatchCategory(GameInterface.HARMONY_GAME_STARTED_CATEGORY)]
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

    [ThreadStatic]
    private static float initialRecentEventsMorale;

    [HarmonyPatch(nameof(DefaultClanFinanceModel.ApplyMoraleEffect))]
    [HarmonyPrefix]
    public static void ApplyMoraleEffectPrefix(DefaultClanFinanceModel __instance, MobileParty mobileParty, int wage, int paymentAmount)
    {
        initialRecentEventsMorale = mobileParty.RecentEventsMorale;
    }

    [HarmonyPatch(nameof(DefaultClanFinanceModel.ApplyMoraleEffect))]
    [HarmonyPostfix]
    public static void ApplyMoraleEffectPostfix(DefaultClanFinanceModel __instance, MobileParty mobileParty, int wage, int paymentAmount)
    {
        if (paymentAmount < wage && wage > 0 && mobileParty.IsPlayerParty())
        {
            MessageBroker.Instance.Publish(__instance, new NotifyMoraleLossDueToFunds(mobileParty, mobileParty.RecentEventsMorale - initialRecentEventsMorale));
        }
    }
}
