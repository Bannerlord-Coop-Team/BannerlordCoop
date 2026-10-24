using GameInterface.Services.PrisonBreaks.Interfaces;
using HarmonyLib;
using SandBox.CampaignBehaviors;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace GameInterface.Services.PrisonBreaks.Patches;

/// <summary>
/// Disables the dungeon and prison break menu options while another player is in a prison break mission in the same settlement.
/// </summary>
[HarmonyPatch]
internal class PrisonBreakDungeonAccessPatches
{
    [HarmonyPatch(typeof(PlayerTownVisitCampaignBehavior), nameof(PlayerTownVisitCampaignBehavior.game_menu_go_dungeon_on_condition))]
    [HarmonyPostfix]
    private static void GoToTownDungeonPostfix(MenuCallbackArgs args, bool __result) => BlockDuringPrisonBreak(args, __result);

    [HarmonyPatch(typeof(PlayerTownVisitCampaignBehavior), nameof(PlayerTownVisitCampaignBehavior.game_menu_castle_go_to_the_dungeon_on_condition))]
    [HarmonyPostfix]
    private static void GoToCastleDungeonPostfix(MenuCallbackArgs args, bool __result) => BlockDuringPrisonBreak(args, __result);

    [HarmonyPatch(typeof(PlayerTownVisitCampaignBehavior), nameof(PlayerTownVisitCampaignBehavior.game_menu_castle_enter_the_dungeon_on_condition))]
    [HarmonyPostfix]
    private static void EnterDungeonPostfix(MenuCallbackArgs args, bool __result) => BlockDuringPrisonBreak(args, __result);

    [HarmonyPatch(typeof(PrisonBreakCampaignBehavior), nameof(PrisonBreakCampaignBehavior.game_menu_stage_prison_break_on_condition))]
    [HarmonyPostfix]
    private static void StagePrisonBreakPostfix(MenuCallbackArgs args, bool __result) => BlockDuringPrisonBreak(args, __result);

    private static void BlockDuringPrisonBreak(MenuCallbackArgs args, bool isShown)
    {
        if (!isShown) return;
        if (!ContainerProvider.TryResolve<IPrisonBreakCampaignBehaviorInterface>(out var behaviorInterface)) return;
        if (!behaviorInterface.IsPrisonBreakActive(Settlement.CurrentSettlement)) return;

        args.IsEnabled = false;
        args.Tooltip = GameTexts.FindText("str_coop_prison_break_in_progress");
    }
}
