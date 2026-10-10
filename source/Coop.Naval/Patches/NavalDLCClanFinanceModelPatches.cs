using GameInterface;
using GameInterface.Services.Clans;
using HarmonyLib;
using NavalDLC.GameComponents;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;

namespace Coop.Naval.Patches;

[HarmonyPatch(typeof(NavalDLCClanFinanceModel))]
internal class NavalDLCClanFinanceModelPatches
{
    // The default gold tick is gated for player clans, so also skip the Coastal Guard Edict garrison withdrawals
    [HarmonyPatch(nameof(NavalDLCClanFinanceModel.CalculateClanGoldChange))]
    [HarmonyPrefix]
    private static bool CalculateClanGoldChangePrefix(NavalDLCClanFinanceModel __instance, ref ExplainedNumber __result, Clan clan, bool includeDescriptions, bool applyWithdrawals, bool includeDetails)
    {
        ContainerProvider.TryResolve<IClanFinance>(out var finance);

        // Same gate as DefaultClanFinanceModelPatches.CalculateClanGoldChangePrefix
        if (clan.Leader?.Clan == clan && finance.CanChangeGold(clan.Leader)) return true;

        __result = __instance.BaseModel.CalculateClanGoldChange(clan, includeDescriptions, applyWithdrawals, includeDetails);
        return false;
    }

    // Use the default model's patched AddPartyExpense so the player clan check, wage morale sync and notification apply
    [HarmonyPatch(nameof(NavalDLCClanFinanceModel.AddPartyExpense))]
    [HarmonyPrefix]
    private static bool AddPartyExpensePrefix(NavalDLCClanFinanceModel __instance, ref int __result, MobileParty party, Clan clan, ExplainedNumber goldChange, bool applyWithdrawals)
    {
        if (__instance.BaseModel is not DefaultClanFinanceModel defaultModel) return true;

        __result = defaultModel.AddPartyExpense(party, clan, goldChange, applyWithdrawals);
        return false;
    }
}
