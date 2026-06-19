using GameInterface.Services.MobileParties.Extensions;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Localization;

namespace GameInterface.Services.MapEvents.Patches;

/// <summary>
/// Greys out encounter-menu options for a player who joins a map event another player already started.
/// The joining client cannot act; the battle is driven by the attacker (the player leading the attacker side),
/// and the joiner is pulled along by that player's choice (into the mission via <c>NetworkStartAttackMission</c>,
/// or back to the map when the event finalizes).
/// </summary>
/// <remarks>
/// One shared postfix is applied to each option callback named in <see cref="DisabledConditionMethods"/> on
/// <see cref="EncounterGameMenuBehavior"/> (the "encounter", "army_encounter" and "join_encounter" menus). The list is
/// curated rather than reflected over every <c>*_on_condition</c> so options that should stay enabled can simply be
/// removed from it. Only the joiner is affected — the attacker-side leader keeps their options so they can select the
/// action. Complements <see cref="DisablePvpEncounterAttackPatch"/>, which only touches attack-style options.
/// </remarks>
[HarmonyPatch]
internal class DisableJoinerEncounterOptionsPatch
{
    private static readonly TextObject WaitingTooltip = new("{=!}Waiting for the attacker to choose an action.");

    // Option condition callbacks on EncounterGameMenuBehavior to disable for the joining player.
    // Remove an entry to keep that option enabled for the joiner. (The trailing comment is the in-game option label.)
    private static readonly string[] DisabledConditionMethods =
    {
        // "encounter" menu
        "game_menu_encounter_attack_on_condition",                  // Attack!
        "game_menu_encounter_order_attack_on_condition",            // Send your troops to attack
        "game_menu_encounter_leave_your_soldiers_behind_on_condition", // Leave your soldiers behind
        "game_menu_encounter_surrender_on_condition",               // Surrender
        "game_menu_encounter_abandon_army_on_condition",            // Abandon your army
        "game_menu_encounter_capture_the_enemy_on_condition",       // Capture the enemy
        "game_menu_encounter_army_lead_inf_on_condition",           // Lead the infantry
        "game_menu_encounter_army_lead_arc_on_condition",           // Lead the archers
        "game_menu_encounter_army_lead_cav_on_condition",           // Lead the cavalry
        "game_menu_encounter_army_lead_har_on_condition",           // Lead the horse archers

        // "army_encounter" menu
        "game_menu_army_attack_on_condition",                       // Attack army
        "game_menu_army_join_on_condition",                         // Join army
        "game_menu_army_leave_on_condition",                        // Leave
        "game_menu_army_talk_to_leader_on_condition",               // Talk to the army leader
        "game_menu_army_talk_to_other_members_on_condition",        // Talk to other members

        // "join_encounter" menu
        "game_menu_join_encounter_abandon_army_on_condition",       // Abandon your army
        "game_menu_join_sally_out_event_on_condition",              // Join the sally out
        "game_menu_join_siege_event_on_condition",                  // Join the siege

        // Shared: actually launches the battle mission.
        "launch_mission_on_condition",                              // (starts the battle)
    };

    static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (var name in DisabledConditionMethods)
        {
            var method = AccessTools.Method(typeof(EncounterGameMenuBehavior), name);
            if (method != null)
                yield return method;
        }
    }

    [HarmonyPostfix]
    static void Postfix(MenuCallbackArgs __0, bool __result)
    {
        // The option is already hidden/unavailable; nothing to do.
        if (__result == false) return;

        if (!IsWaitingJoiner()) return;

        __0.IsEnabled = false;
        __0.Tooltip = WaitingTooltip;
    }

    /// <summary>
    /// True when this client's main party is in (or about to join) a player-started map event that it does not lead the
    /// attacker side of — i.e. it joined someone else's battle and must wait for that player.
    /// </summary>
    private static bool IsWaitingJoiner()
        => WaitsForAttacker(MapEvent.PlayerMapEvent) || WaitsForAttacker(PlayerEncounter.EncounteredBattle);

    private static bool WaitsForAttacker(MapEvent mapEvent)
    {
        // Guard like MapEventVisibilityClientPatch: on a client the sides are wired up after the event is created.
        if (mapEvent?.AttackerSide == null || mapEvent.DefenderSide == null) return false;

        var mainParty = MobileParty.MainParty;
        if (mainParty == null) return false;

        var attackerLeader = mapEvent.AttackerSide.LeaderParty?.MobileParty;
        if (attackerLeader == null) return false;

        // We lead the attacker side (we started the event): keep our options so we can select the action.
        if (attackerLeader == mainParty) return false;

        // Only defer to a player starter; an AI-led attacker keeps native handling.
        return attackerLeader.IsPlayerParty();
    }
}
