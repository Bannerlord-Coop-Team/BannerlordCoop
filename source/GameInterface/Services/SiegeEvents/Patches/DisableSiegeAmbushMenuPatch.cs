using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace GameInterface.Services.SiegeEvents.Patches;

/// <summary>
/// Greys out the "Ambush" option on the siege menu. The ambush mission opens the siege scene through
/// a path the co-op mission launcher does not cover yet.
/// </summary>
[HarmonyPatch(typeof(SiegeAmbushCampaignBehavior), "menu_siege_strategies_ambush_condition")]
internal class DisableSiegeAmbushMenuPatch
{
    private static readonly TextObject DisabledTooltip = new("{=!}Siege ambushes are not yet supported in Co-op.");

    [HarmonyPrefix]
    private static bool Prefix(MenuCallbackArgs args, ref bool __result)
    {
        __result = PlayerSiege.PlayerSiegeEvent != null && PlayerSiege.PlayerSide == BattleSideEnum.Defender;
        if (!__result) return false;

        args.optionLeaveType = GameMenuOption.LeaveType.SiegeAmbush;
        args.IsEnabled = false;
        args.Tooltip = DisabledTooltip;
        return false;
    }
}
