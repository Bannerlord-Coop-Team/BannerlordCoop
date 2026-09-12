using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Localization;

namespace GameInterface.Services.SiegeEvents.Patches;

/// <summary>
/// Restricts starting an assault to the synced BesiegerCamp leader. Participation in an active assault
/// remains on the vanilla encounter flow.
/// </summary>
[HarmonyPatch(typeof(SiegeEventCampaignBehavior))]
internal static class SiegeAssaultCommandGatePatch
{
    [HarmonyPatch(nameof(SiegeEventCampaignBehavior.game_menu_siege_strategies_lead_assault_on_condition))]
    [HarmonyPrefix]
    private static bool LeadAssaultConditionPrefix(MenuCallbackArgs args, ref bool __result) =>
        ValidateSiegeGraph(args, GameMenuOption.LeaveType.LeadAssault, ref __result);

    [HarmonyPatch(nameof(SiegeEventCampaignBehavior.game_menu_siege_strategies_order_assault_on_condition))]
    [HarmonyPrefix]
    private static bool OrderAssaultConditionPrefix(MenuCallbackArgs args, ref bool __result) =>
        ValidateSiegeGraph(args, GameMenuOption.LeaveType.OrderTroopsToAttack, ref __result);

    [HarmonyPatch(nameof(SiegeEventCampaignBehavior.game_menu_siege_strategies_lead_assault_on_condition))]
    [HarmonyPostfix]
    private static void LeadAssaultConditionPostfix(MenuCallbackArgs args, bool __result) => DisableForCoBesieger(args, __result);

    [HarmonyPatch(nameof(SiegeEventCampaignBehavior.game_menu_siege_strategies_order_assault_on_condition))]
    [HarmonyPostfix]
    private static void OrderAssaultConditionPostfix(MenuCallbackArgs args, bool __result) => DisableForCoBesieger(args, __result);

    private static void DisableForCoBesieger(MenuCallbackArgs args, bool result)
    {
        if (!result) return;

        var settlement = MobileParty.MainParty?.BesiegedSettlement;
        if (settlement == null) return;

        var leader = settlement.SiegeEvent?.BesiegerCamp?.LeaderParty;
        if (leader == MobileParty.MainParty) return;

        args.IsEnabled = false;
        args.Tooltip = new TextObject("{=!}Only the siege leader can command the assault.");
    }

    private static bool ValidateSiegeGraph(
        MenuCallbackArgs args,
        GameMenuOption.LeaveType leaveType,
        ref bool result)
    {
        args.optionLeaveType = leaveType;

        var siegeEvent = PlayerSiege.PlayerSiegeEvent;
        var settlement = siegeEvent?.BesiegedSettlement;
        if (settlement?.SiegeEvent == siegeEvent &&
            siegeEvent.BesiegerCamp?.SiegeEvent == siegeEvent &&
            (PlayerSiege.PlayerSide != TaleWorlds.Core.BattleSideEnum.Attacker ||
             (MobileParty.MainParty?.BesiegerCamp == siegeEvent.BesiegerCamp &&
              siegeEvent.BesiegerCamp.SiegeEngines != null &&
              settlement.SiegeEngines != null)))
        {
            return true;
        }

        result = false;
        return false;
    }
}
