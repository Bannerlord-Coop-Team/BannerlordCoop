using Common;
using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.Localization;

namespace GameInterface.Services.Villages.Patches;

/// <summary>
/// Greys out the "Take a hostile action" option on the village settlement menu for the client.
/// </summary>
[HarmonyPatch(typeof(VillageHostileActionCampaignBehavior), "game_menu_village_hostile_action_on_condition")]
internal class DisableVillageHostileActionMenuPatch
{
    private static readonly TextObject DisabledTooltip = new("{=!}Hostile actions are not yet supported in Co-op.");

    [HarmonyPostfix]
    private static void Postfix(MenuCallbackArgs args, bool __result)
    {
        if (ModInformation.IsServer) return;

        // The option is already hidden by the game (ex. the player's own village), nothing to do.
        if (__result == false) return;

        args.IsEnabled = false;
        args.Tooltip = DisabledTooltip;
    }
}
