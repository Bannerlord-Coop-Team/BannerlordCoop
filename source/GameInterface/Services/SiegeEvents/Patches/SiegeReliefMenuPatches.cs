using GameInterface.Services.Heroes.Extensions;
using HarmonyLib;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace GameInterface.Services.SiegeEvents.Patches;

/// <summary>
/// Greys out the two hostile-arrival options on the join-siege menu ("Assault the siege camp" and
/// "Break in to help the defenders") when a player is besieging, or a player is defending and the
/// walls assault has already begun. Those options open a plain field battle that this co-op siege
/// sync does not drive, so they must not run against a player-involved siege.
/// </summary>
[HarmonyPatch]
internal class SiegeReliefMenuPatches
{
    private static readonly TextObject DisabledTooltip = new("{=!}Not available while a player is involved in this siege.");

    [HarmonyPatch(typeof(EncounterGameMenuBehavior), "attack_besieger_side_on_condition")]
    [HarmonyPostfix]
    private static void AttackBesiegerConditionPostfix(MenuCallbackArgs args, bool __result) => DisableForPlayerSiege(args, __result);

    [HarmonyPatch(typeof(EncounterGameMenuBehavior), "break_in_to_help_defender_side_on_condition")]
    [HarmonyPostfix]
    private static void BreakInConditionPostfix(MenuCallbackArgs args, bool __result) => DisableForPlayerSiege(args, __result);

    private static void DisableForPlayerSiege(MenuCallbackArgs args, bool __result)
    {
        // Only touch the option when vanilla would show it, and only to disable it.
        if (!__result) return;
        if (!ShouldBlock(Settlement.CurrentSettlement)) return;

        args.IsEnabled = false;
        args.Tooltip = DisabledTooltip;
    }

    private static bool ShouldBlock(Settlement settlement)
    {
        var siegeEvent = settlement?.SiegeEvent;
        if (siegeEvent == null) return false;

        // A player is besieging: never open an uncontrolled field battle against a player-led camp.
        if (SideHasPlayer(siegeEvent.BesiegerCamp?.GetInvolvedPartiesForEventType())) return true;

        // A player is defending and the walls assault is running: a relief/break-in would collide with
        // the live co-op siege mission.
        if (settlement.Party.MapEvent != null && SideHasPlayer(settlement.GetInvolvedPartiesForEventType())) return true;

        return false;
    }

    private static bool SideHasPlayer(IEnumerable<PartyBase> parties)
    {
        if (parties == null) return false;

        foreach (var party in parties)
        {
            if (party.LeaderHero != null && party.LeaderHero.IsPlayerHero()) return true;
        }

        return false;
    }
}
