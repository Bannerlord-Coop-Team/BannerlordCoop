using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Localization;

namespace GameInterface.Services.SiegeEvents.Patches;

/// <summary>
/// A co-besieger (a party besieging a settlement it does not lead) must not command the assault; only the
/// BesiegerCamp leader leads or orders it. The vanilla encounter-menu command conditions can't tell leader from
/// follower in co-op (the local leader lookup resolves to the local main party on every machine), so both
/// besiegers get the enabled command options. Grey them out here for a non-leader besieger, keyed on the synced
/// BesiegerCamp.LeaderParty which resolves identically on every machine. Non-siege encounters and the actual
/// leader are untouched, and single-player is unaffected since a player leads its own siege.
/// </summary>
[HarmonyPatch(typeof(EncounterGameMenuBehavior))]
internal static class SiegeAssaultCommandGatePatch
{
    [HarmonyPatch("game_menu_encounter_attack_on_condition")]
    [HarmonyPostfix]
    private static void AttackConditionPostfix(MenuCallbackArgs args, ref bool __result) => DisableForCoBesieger(args, ref __result);

    [HarmonyPatch("game_menu_encounter_order_attack_on_condition")]
    [HarmonyPostfix]
    private static void OrderAttackConditionPostfix(MenuCallbackArgs args, ref bool __result) => DisableForCoBesieger(args, ref __result);

    [HarmonyPatch("game_menu_town_besiege_continue_siege_on_condition")]
    [HarmonyPostfix]
    private static void ContinueSiegeConditionPostfix(MenuCallbackArgs args, ref bool __result) => DisableForCoBesieger(args, ref __result);

    private static void DisableForCoBesieger(MenuCallbackArgs args, ref bool __result)
    {
        if (!__result) return;

        var settlement = MobileParty.MainParty?.BesiegedSettlement;
        if (settlement == null) return;

        var leader = settlement.SiegeEvent?.BesiegerCamp?.LeaderParty;
        if (leader == MobileParty.MainParty) return;

        args.IsEnabled = false;
        args.Tooltip = new TextObject("{=!}Only the siege leader can command the assault.");
    }
}
