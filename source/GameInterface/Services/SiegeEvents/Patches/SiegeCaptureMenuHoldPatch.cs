using HarmonyLib;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.SiegeEvents.Patches;

/// <summary>
/// Vanilla holds menu_settlement_taken (the Devastate/Pillage/Mercy choice) open by pausing the campaign
/// until the player picks. A co-op client can't pause — time is server-driven — so its settlement
/// encounter keeps advancing and rolls the menu out to the town menu before the player chooses. While a
/// capture choice is pending for a settlement, redirect the settlement-entry menu back to
/// menu_settlement_taken, enforcing the same hold in a co-op-compatible way. Released when the player picks.
/// </summary>
[HarmonyPatch]
internal static class SiegeCaptureMenuHoldPatch
{
    private static readonly HashSet<Settlement> pendingChoice = new HashSet<Settlement>();

    internal static void HoldFor(Settlement settlement)
    {
        if (settlement != null) pendingChoice.Add(settlement);
    }

    internal static void Release(Settlement settlement)
    {
        if (settlement != null) pendingChoice.Remove(settlement);
    }

    [HarmonyPatch(typeof(EncounterGameMenuBehavior), "game_menu_town_outside_on_init")]
    [HarmonyPrefix]
    private static bool TownOutsideInitPrefix() => RedirectIfHeld();

    private static bool RedirectIfHeld()
    {
        var settlement = Settlement.CurrentSettlement;
        if (settlement != null && pendingChoice.Contains(settlement))
        {
            GameMenu.SwitchToMenu("menu_settlement_taken");
            return false;
        }

        return true;
    }
}
