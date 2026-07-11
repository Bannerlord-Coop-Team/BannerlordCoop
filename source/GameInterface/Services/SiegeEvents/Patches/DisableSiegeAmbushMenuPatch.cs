using Common;
using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.GameMenus;
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

    [HarmonyPostfix]
    private static void Postfix(MenuCallbackArgs args, bool __result)
    {
        if (!__result) return;

        args.IsEnabled = false;
        args.Tooltip = DisabledTooltip;
    }
}
