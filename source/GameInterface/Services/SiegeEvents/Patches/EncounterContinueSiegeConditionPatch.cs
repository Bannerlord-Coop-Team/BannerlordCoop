using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace GameInterface.Services.SiegeEvents.Patches;

/// <summary>
/// Vanilla game_menu_town_besiege_continue_siege_on_condition indexes the encountered battle's sides with
/// PartyBase.MainParty.Side. A client whose main party is not in that map event has Side None (-1), so the
/// lookup throws and the whole encounter menu falls back to ExitToLast. Without a side the player cannot lead it.
/// </summary>
[HarmonyPatch(typeof(EncounterGameMenuBehavior))]
internal class EncounterContinueSiegeConditionPatch
{
    [HarmonyPatch(nameof(EncounterGameMenuBehavior.game_menu_town_besiege_continue_siege_on_condition))]
    [HarmonyPrefix]
    private static bool Prefix(MenuCallbackArgs args, ref bool __result)
    {
        if (PartyBase.MainParty.Side != BattleSideEnum.None) return true;

        args.optionLeaveType = GameMenuOption.LeaveType.Continue;
        __result = false;
        return false;
    }
}
