using GameInterface.Services.Clans.Extensions;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Clans.Patches;

[HarmonyPatch(typeof(DefaultClanFinanceModel))]
internal class ClanDefaultClanFinanceModelPatches
{
    [HarmonyPatch(nameof(DefaultClanFinanceModel.AddExpenseFromLeaderParty))]
    [HarmonyPrefix]
    private static bool AddExpenseFromLeaderPartyPrefix(DefaultClanFinanceModel __instance, Clan clan, ExplainedNumber goldChange, bool applyWithdrawals, ref int __result)
    {
        Hero leader = clan.Leader;
        MobileParty mobileParty = (leader != null) ? leader.PartyBelongedTo : null;
        if (mobileParty != null)
        {
            int num = clan.Gold + (int)goldChange.ResultNumber;
            if (num < 2000 && applyWithdrawals && !clan.IsPlayerClan()) // Vanilla runs clan != Clan.PlayerClan, which is always true on server
            {
                num = 0;
            }
            __result = -__instance.CalculatePartyWage(mobileParty, num, applyWithdrawals);
        }
        else
        {
            __result = 0;
        }
        return false;
    }
}
