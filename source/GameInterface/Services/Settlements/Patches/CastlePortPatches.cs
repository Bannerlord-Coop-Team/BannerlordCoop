using Common;
using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Settlements.Patches;

/// <summary>
/// Hides the castle port menu options on clients, because coop only syncs town ports and blockades so far (#4605).
/// </summary>
[HarmonyPatch]
[HarmonyPatchCategory(GameInterface.HARMONY_NAVAL_CATEGORY)]
internal class CastlePortPatches
{
    // Castle menu only, so no settlement check
    [HarmonyPatch(typeof(PlayerTownVisitCampaignBehavior), nameof(PlayerTownVisitCampaignBehavior.game_menu_castle_disembark_on_condition))]
    [HarmonyPostfix]
    public static void CastleDisembarkOnConditionPostfix(ref bool __result)
    {
        if (ModInformation.IsClient) __result = false;
    }

    // Shared by the town, castle and village menus
    [HarmonyPatch(typeof(PlayerTownVisitCampaignBehavior), nameof(PlayerTownVisitCampaignBehavior.game_menu_set_sail_leave_on_condition))]
    [HarmonyPostfix]
    public static void SetSailLeaveOnConditionPostfix(ref bool __result)
    {
        if (IsClientAtCastle()) __result = false;
    }

    // menu_siege_strategies is shared by town and castle sieges
    [HarmonyPatch(typeof(EncounterGameMenuBehavior), nameof(EncounterGameMenuBehavior.menu_defender_siege_break_out_from_port_on_condition))]
    [HarmonyPostfix]
    public static void BreakOutFromPortOnConditionPostfix(ref bool __result)
    {
        if (IsClientAtCastle()) __result = false;
    }

    [HarmonyPatch(typeof(EncounterGameMenuBehavior), nameof(EncounterGameMenuBehavior.menu_sally_out_from_port_on_condition))]
    [HarmonyPostfix]
    public static void SallyOutFromPortOnConditionPostfix(ref bool __result)
    {
        if (IsClientAtCastle()) __result = false;
    }

    private static bool IsClientAtCastle() => ModInformation.IsClient && Settlement.CurrentSettlement?.IsCastle == true;
}
