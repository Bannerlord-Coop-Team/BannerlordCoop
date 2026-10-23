using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.SiegeEvents.Patches;

/// <summary>
/// Vanilla holds menu_settlement_taken (the Devastate/Pillage/Mercy choice) open by pausing the campaign
/// until the player picks. A co-op client can't pause — time is server-driven — so its settlement
/// encounter keeps advancing and rolls the menu out to the fortification menu before the player chooses. While
/// a capture choice is pending for a settlement, redirect the settlement-entry menu back to
/// menu_settlement_taken, enforcing the same hold in a co-op-compatible way. Released when the player picks.
/// </summary>
[HarmonyPatch]
internal static class SiegeCaptureMenuHoldPatch
{
    // Held capture choices, with the defeated owner the server snapshotted for the consequence text (null if unknown).
    private static readonly Dictionary<Settlement, Clan> pendingChoice = new Dictionary<Settlement, Clan>();

    /// <summary>Holds the capture choice for a settlement; a null previous owner keeps one already held.</summary>
    internal static void HoldFor(Settlement settlement, Clan previousOwnerClan = null)
    {
        if (settlement == null) return;
        if (previousOwnerClan == null && pendingChoice.ContainsKey(settlement)) return;

        pendingChoice[settlement] = previousOwnerClan;
    }

    internal static void Release(Settlement settlement)
    {
        if (settlement != null) pendingChoice.Remove(settlement);
    }

    /// <summary>True while this client holds the capture choice for the settlement.</summary>
    internal static bool IsHeld(Settlement settlement) => settlement != null && pendingChoice.ContainsKey(settlement);

    /// <summary>The defeated owner held with the settlement's capture choice, or null.</summary>
    internal static Clan GetHeldPreviousOwner(Settlement settlement)
    {
        if (settlement == null) return null;
        return pendingChoice.TryGetValue(settlement, out var previousOwnerClan) ? previousOwnerClan : null;
    }

    /// <summary>
    /// The capture a finished stale encounter menu belongs to. With no encounter left to name it, only an
    /// unambiguous single held capture qualifies.
    /// </summary>
    internal static bool TryGetOnlyHeldSettlement(out Settlement settlement)
    {
        settlement = pendingChoice.Count == 1 ? pendingChoice.Keys.First() : null;
        return settlement != null;
    }

    [HarmonyPatch(typeof(EncounterGameMenuBehavior), nameof(EncounterGameMenuBehavior.game_menu_town_outside_on_init))]
    [HarmonyPrefix]
    private static bool TownOutsideInitPrefix() => RedirectIfHeld();

    [HarmonyPatch(typeof(EncounterGameMenuBehavior), nameof(EncounterGameMenuBehavior.game_menu_castle_outside_on_init))]
    [HarmonyPrefix]
    private static bool CastleOutsideInitPrefix() => RedirectIfHeld();

    private static bool RedirectIfHeld()
    {
        var settlement = Settlement.CurrentSettlement;
        if (IsHeld(settlement))
        {
            GameMenu.SwitchToMenu("menu_settlement_taken");
            return false;
        }

        return true;
    }
}
