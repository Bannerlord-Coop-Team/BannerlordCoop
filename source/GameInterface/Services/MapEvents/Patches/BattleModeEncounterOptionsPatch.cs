using Common;
using Common.Logging;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Localization;

namespace GameInterface.Services.MapEvents.Patches;

/// <summary>
/// Enforces mission-XOR-simulation on the encounter menu of a shared map event: while it is claimed for a live
/// mission (<see cref="BattleModeRegistry.IsMission"/>) the auto-resolve options grey out, and while claimed for an
/// auto-resolve (<see cref="BattleModeRegistry.IsSimulation"/>) the mission-start options grey out. The mode comes
/// from the server's <see cref="Messages.Start.NetworkBattleModeSet"/>. Other options (join, leave, talk) are
/// untouched — only the wrong-mode battle-start is blocked.
/// </summary>
/// <remarks>
/// One shared postfix per option-condition in <see cref="MissionStartConditions"/> / <see cref="SimulationStartConditions"/>;
/// the patched method name selects the bucket.
/// </remarks>
[HarmonyPatch]
internal class BattleModeEncounterOptionsPatch
{
    private static readonly ILogger Logger = LogManager.GetLogger<BattleModeEncounterOptionsPatch>();
    private static readonly TextObject MissionUnderwayTooltip = new("{=!}A battle is already underway.");
    private static readonly TextObject SimulationUnderwayTooltip = new("{=!}A battle simulation is already underway.");
    private static readonly TextObject EncounterUnavailableTooltip = new("{=!}The battle encounter is no longer available.");

    // Live-mission launch options, greyed while a simulation runs (launch_mission is the shared catch-all every
    // mission path funnels through). Trailing comment = in-game label.
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

    // Auto-resolve options, greyed while a live mission runs.
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
        // Already unavailable — nothing to do.
        if (__result == false) return;

        // Server never opens the menu; mode trackers are client state.
        if (ModInformation.IsServer) return;

        var mapEvent = BattleTrace.GetPlayerEncounterBattleForTrace() ?? MobileParty.MainParty?.MapEvent;
        if (mapEvent == null) return;

        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager)) return;
        if (!objectManager.TryGetId(mapEvent, out var mapEventId)) return;

        var name = __originalMethod.Name;

        // Live mission underway → block starting a simulation.
        if (SimulationStartConditions.Contains(name) && BattleModeRegistry.IsMission(mapEventId))
        {
            __0.IsEnabled = false;
            __0.Tooltip = MissionUnderwayTooltip;
            return;
        }

        // Simulation underway → block starting a live mission.
        if (MissionStartConditions.Contains(name) && BattleModeRegistry.IsSimulation(mapEventId))
        {
            __0.IsEnabled = false;
            __0.Tooltip = SimulationUnderwayTooltip;
        }
    }

    [HarmonyFinalizer]
    static Exception Finalizer(MenuCallbackArgs __0, MethodBase __originalMethod, ref bool __result, Exception __exception)
    {
        if (__exception == null) return null;
        if (ModInformation.IsServer) return __exception;
        if (!IsEncounterMenuRefresh()) return __exception;

        __result = false;
        if (__0 != null)
        {
            __0.IsEnabled = false;
            __0.Tooltip = EncounterUnavailableTooltip;
        }

        Logger.Warning(
            __exception,
            "[PvPEncounterClose] Suppressed encounter menu option condition exception; method={Method} state={State}",
            __originalMethod?.Name ?? "<unknown>",
            DescribeEncounterState());
        return null;
    }

    private static bool IsEncounterMenuRefresh()
        => Campaign.Current?.CurrentMenuContext?.GameMenu?.StringId == "encounter" ||
           PlayerEncounter.Current != null ||
           MobileParty.MainParty?.MapEvent != null;

    private static string DescribeEncounterState()
    {
        var encounter = PlayerEncounter.Current;
        return $"menu={Campaign.Current?.CurrentMenuContext?.GameMenu?.StringId ?? "<none>"}; encounter={(encounter != null)}; mainPartyMapEvent={(MobileParty.MainParty?.MapEvent != null)}; battle={(BattleTrace.GetPlayerEncounterBattleForTrace() != null)}; attacker={(encounter?._attackerParty != null)}; defender={(encounter?._defenderParty != null)}";
    }
}
