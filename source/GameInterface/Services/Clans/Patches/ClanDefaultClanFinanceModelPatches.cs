using Common;
using Common.Messaging;
using GameInterface.Configuration;
using GameInterface.Services.Clans.Extensions;
using GameInterface.Services.Clans.Interfaces;
using GameInterface.Services.Heroes.Extensions;
using GameInterface.Services.MapEvents.Patches;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.Players;
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
        // A player who is committed to a MapEvent cannot manage, forage or
        // advance on the campaign map. Pause only that leader party's wage;
        // the rest of the clan finance calculation (caravans, workshops,
        // tributes and other parties) must continue normally.
        if (clan?.Leader?.IsPlayerHero() == true &&
            clan.Leader.PartyBelongedTo?.MapEvent != null)
        {
            __result = 0;
            return false;
        }
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

    [HarmonyPatch(nameof(DefaultClanFinanceModel.CalculateClanExpensesInternal))]
    [HarmonyPrefix]
    public static bool CalculateClanExpensesInternalPrefix(DefaultClanFinanceModel __instance, Clan clan, ref ExplainedNumber goldChange, bool applyWithdrawals = false, bool includeDetails = false)
    {
        // Calculate for non-player clans normally
        if (clan == null || !clan.IsPlayerClan()) return true;

        ContainerProvider.TryResolve<IDefaultClanFinanceModelInterface>(out var financeModelInterface);

        financeModelInterface.CalculateClanExpensesForPlayerClan(__instance, clan, ref goldChange, applyWithdrawals, includeDetails);

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

    [HarmonyPatch(nameof(DefaultClanFinanceModel.AddPartyExpense))]
    [HarmonyPrefix]
    public static bool AddPartyExpensePrefix(DefaultClanFinanceModel __instance, ref int __result, MobileParty party, Clan clan, ExplainedNumber goldChange, bool applyWithdrawals)
    {
        if (party?.MapEvent != null && party.IsPlayerParty())
        {
            __result = 0;
            return false;
        }
        ContainerProvider.TryResolve<IDefaultClanFinanceModelInterface>(out var financeModelInterface);

        __result = financeModelInterface.AddPartyExpense(__instance, party, clan, goldChange, applyWithdrawals);

        return false;
    }

    [HarmonyPatch(nameof(DefaultClanFinanceModel.CalculateClanGoldChange))]
    [HarmonyPrefix]
    public static bool CalculateClanGoldChangePrefix(Clan clan)
    {
        // Calculate gold change for AI led clans normally
        if (clan.Leader == null || !clan.Leader.IsPlayerHero()) return true;

        ContainerProvider.TryResolve<IPlayerManager>(out var playerManager);

        // Don't tick gold change for disconnected players based on config
        if (ModInformation.IsServer
            && !ModConfigProvider.ModOptions.GoldFoodInfluenceChangeForDisconnectedPlayers
            && playerManager.IsOwnerOfHeroDisconnected(clan.Leader)) return false;

        // Don't tick gold change when clan leader is in a settlement based on config
        if (clan.Leader.CurrentSettlement != null
            && !ModConfigProvider.ModOptions.GoldFoodInfluenceChangeInSettlements) return false;

        var clanLeaderMapEvent = clan.Leader.PartyBelongedTo?.MapEvent;

        // Clan leader not in a map event, calculate gold change normally
        if (clanLeaderMapEvent == null) return true;

        // Realm applies the rest of the clan ledger normally while a player is
        // in battle. The two party-expense prefixes above remove only wages for
        // player-owned parties that are actually in a MapEvent, so caravan and
        // workshop income, tributes, garrisons and unrelated parties continue
        // to update instead of freezing the whole clan economy.
        return true;
    }
}
