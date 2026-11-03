using HarmonyLib;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.GameMenus.GameMenuInitializationHandlers;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.SiegeEvents.Patches;

/// <summary>
/// The request_meeting menus take their background from PlayerEncounter.EncounterSettlement, which is null once a
/// sally out closed the local encounter while the player is still in the besieged settlement, so the init threw and
/// the menu fell back to the previous one.
/// </summary>
[HarmonyPatch(typeof(DefaultEncounter), nameof(DefaultEncounter.game_menu_town_menu_request_meeting_on_init))]
internal class RequestMeetingMenuBackgroundPatch
{
    [HarmonyPrefix]
    private static bool Prefix(MenuCallbackArgs args)
    {
        if (PlayerEncounter.EncounterSettlement != null) return true;

        var waitMesh = Settlement.CurrentSettlement?.SettlementComponent?.WaitMeshName;
        if (waitMesh != null)
            args.MenuContext.SetBackgroundMeshName(waitMesh);

        return false;
    }
}
