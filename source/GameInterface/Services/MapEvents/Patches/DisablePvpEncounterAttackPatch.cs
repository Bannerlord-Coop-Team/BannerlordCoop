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
/// Greys out every battle-start "attack"/"help" encounter option when the battle would involve more than one player
/// party (PvP). Player-vs-player battles are not yet functional in co-op, so the options are disabled for everyone
/// (host and clients) rather than letting a player start a battle that cannot be resolved.
/// </summary>
/// <remarks>
/// One shared postfix is applied to every attack-style option condition on the "encounter", "army_encounter" and
/// "join_encounter" menus (all share the <c>bool (MenuCallbackArgs)</c> signature). The
/// <see cref="EncounterAttackConsequencePatch"/> additionally swallows the click as a backstop.
/// </remarks>
[HarmonyPatch]
internal class DisablePvpEncounterAttackPatch
{
    private static readonly TextObject DisabledTooltip = new("{=!}Player-vs-player battles are not yet supported in Co-op.");

    // Attack-style option conditions across the encounter / army-encounter / join-encounter menus.
    private static readonly string[] AttackConditionMethods =
    {
        "game_menu_encounter_attack_on_condition",        // "Attack!"
        "game_menu_army_attack_on_condition",             // "Attack army"
        "game_menu_join_encounter_help_attackers_on_condition", // join a battle on the attacker side
        "game_menu_join_encounter_help_defenders_on_condition", // join a battle on the defender side
    };

    static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (var name in AttackConditionMethods)
        {
            var method = AccessTools.Method(typeof(EncounterGameMenuBehavior), name);
            if (method != null)
                yield return method;
        }
    }

    [HarmonyPostfix]
    static void Postfix(MenuCallbackArgs args, bool __result)
    {
        // The option is already hidden/unavailable; nothing to do.
        if (__result == false) return;

        if (!IsPlayerVsPlayer()) return;

        args.IsEnabled = false;
        args.Tooltip = DisabledTooltip;
    }

    /// <summary>
    /// PvP when the battle the player is in (<see cref="MapEvent.PlayerMapEvent"/>) or is about to join
    /// (<see cref="PlayerEncounter.EncounteredBattle"/>) already contains a player party other than the main party —
    /// i.e. acting would put more than one player party in the battle.
    /// </summary>
    private static bool IsPlayerVsPlayer()
        => HasOtherPlayerParty(MapEvent.PlayerMapEvent) || HasOtherPlayerParty(PlayerEncounter.EncounteredBattle);

    private static bool HasOtherPlayerParty(MapEvent mapEvent)
    {
        // Guard the enumeration like MapEventVisibilityClientPatch: on a client the sides are wired up after the
        // event is created, so reading them can dereference an unassigned side mid-construction.
        if (mapEvent?.AttackerSide == null || mapEvent.DefenderSide == null) return false;

        return SideHasOtherPlayerParty(mapEvent.AttackerSide) || SideHasOtherPlayerParty(mapEvent.DefenderSide);
    }

    private static bool SideHasOtherPlayerParty(MapEventSide side)
    {
        foreach (var mapEventParty in side.Parties)
        {
            var mobileParty = mapEventParty.Party?.MobileParty;
            if (mobileParty != null && mobileParty != MobileParty.MainParty && mobileParty.IsPlayerParty())
                return true;
        }

        return false;
    }
}
