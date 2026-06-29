using Common;
using GameInterface.Services.ObjectManager;
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
/// Enforces mission-XOR-simulation on the encounter menu: a map event shared by several players is resolved either
/// as a live battle mission or as an auto-resolve simulation, never both at once.
/// <list type="bullet">
/// <item>Once the server claims the event for a live mission (<see cref="BattleModeRegistry.IsMission"/>, set from the
/// server's <see cref="Messages.Start.NetworkBattleModeSet"/> broadcast), the auto-resolve options are greyed out for
/// everyone still at the menu.</item>
/// <item>Once it claims the event for an auto-resolve (<see cref="BattleModeRegistry.IsSimulation"/>), the
/// mission-start options are greyed out.</item>
/// </list>
/// Every other option (join the mission, leave, talk, surrender) is left untouched, so a joiner can still act —
/// only the wrong-mode battle-start is blocked.
/// </summary>
/// <remarks>
/// One shared postfix is applied to each option-condition callback in <see cref="MissionStartConditions"/> and
/// <see cref="SimulationStartConditions"/> on <see cref="EncounterGameMenuBehavior"/>; the patched method name tells
/// the postfix which bucket the option is in. Move an entry between the lists (or drop it) to re-classify an option.
/// Complements <see cref="DisablePvpEncounterAttackPatch"/>.
/// </remarks>
[HarmonyPatch]
internal class BattleModeEncounterOptionsPatch
{
    private static readonly TextObject MissionUnderwayTooltip = new("{=!}A battle is already underway.");
    private static readonly TextObject SimulationUnderwayTooltip = new("{=!}A battle simulation is already underway.");

    // Options that launch the live battle mission — greyed out while an auto-resolve simulation is underway for the
    // event. launch_mission is the shared final launch every mission path funnels through (the robust catch-all).
    // (Trailing comment is the in-game option label.)
    private static readonly HashSet<string> MissionStartConditions = new()
    {
        "game_menu_encounter_attack_on_condition",        // Attack!
        "game_menu_encounter_army_lead_inf_on_condition", // Lead the infantry
        "game_menu_encounter_army_lead_arc_on_condition", // Lead the archers
        "game_menu_encounter_army_lead_cav_on_condition", // Lead the cavalry
        "game_menu_encounter_army_lead_har_on_condition", // Lead the horse archers
        "game_menu_army_attack_on_condition",             // Attack army
        "launch_mission_on_condition",                    // (shared: launches the battle mission)
    };

    // Options that start the auto-resolve simulation — greyed out while a live mission is underway for the event.
    private static readonly HashSet<string> SimulationStartConditions = new()
    {
        "game_menu_encounter_order_attack_on_condition",  // Send your troops to attack
    };

    static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (var name in MissionStartConditions)
        {
            var method = AccessTools.Method(typeof(EncounterGameMenuBehavior), name);
            if (method != null)
                yield return method;
        }

        foreach (var name in SimulationStartConditions)
        {
            var method = AccessTools.Method(typeof(EncounterGameMenuBehavior), name);
            if (method != null)
                yield return method;
        }
    }

    [HarmonyPostfix]
    static void Postfix(MenuCallbackArgs __0, bool __result, MethodBase __originalMethod)
    {
        // The option is already hidden/unavailable; nothing to do.
        if (__result == false) return;

        // The headless server never opens the encounter menu; the mode trackers are client state.
        if (ModInformation.IsServer) return;

        var mapEvent = PlayerEncounter.Battle ?? MobileParty.MainParty?.MapEvent;
        if (mapEvent == null) return;

        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager)) return;
        if (!objectManager.TryGetId(mapEvent, out var mapEventId)) return;

        var name = __originalMethod.Name;

        // A live mission is underway for this event → block starting an auto-resolve simulation.
        if (SimulationStartConditions.Contains(name) && BattleModeRegistry.IsMission(mapEventId))
        {
            __0.IsEnabled = false;
            __0.Tooltip = MissionUnderwayTooltip;
            return;
        }

        // An auto-resolve simulation is underway for this event → block starting a live mission.
        if (MissionStartConditions.Contains(name) && BattleModeRegistry.IsSimulation(mapEventId))
        {
            __0.IsEnabled = false;
            __0.Tooltip = SimulationUnderwayTooltip;
        }
    }
}
