using Common;
using Common.Logging;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Helpers;
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
/// from the server's <see cref="Messages.Start.NetworkBattleModeSet"/>. The surrender option gets the inverse
/// treatment: forced available for a defender that cannot fight (see <see cref="PostfixSurrenderCondition"/>) and
/// refused while either mode owns the event. Other options (join, leave, talk) are untouched.
/// </summary>
/// <remarks>
/// One shared postfix per option-condition in <see cref="MissionStartConditions"/> / <see cref="SimulationStartConditions"/> /
/// <see cref="SurrenderCondition"/>; the patched method name selects the bucket.
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

    // Handled inversely to the start buckets: forced available / refused rather than only greyed.
    private const string SurrenderCondition = "game_menu_encounter_surrender_on_condition"; // Surrender.

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

        var surrender = AccessTools.Method(typeof(EncounterGameMenuBehavior), SurrenderCondition);
        if (surrender != null)
            yield return surrender;
    }

    [HarmonyPostfix]
    static void Postfix(MenuCallbackArgs __0, ref bool __result, MethodBase __originalMethod)
    {
        // Server never opens the menu; mode trackers are client state.
        if (ModInformation.IsServer) return;

        var name = __originalMethod.Name;

        if (name == SurrenderCondition)
        {
            PostfixSurrenderCondition(__0, ref __result);
            return;
        }

        // Already unavailable — nothing to do.
        if (__result == false) return;

        if (!TryGetCurrentMapEventId(out var mapEventId)) return;

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

    /// <summary>
    /// An incapacitated defender — wounded main hero, no healthy member left in its own party, on a battle side it
    /// cannot walk away from — must always be able to surrender: it cannot attack (wound), cannot send troops
    /// (nothing healthy of its own to send, and the wrong-mode gate above), and cannot afford the get-away troop
    /// sacrifice. Native only shows surrender when the WHOLE side has no healthy member left
    /// (<c>DefenderSide.TroopCount == own NumberOfHealthyMembers</c>) or when morale is broken, but a client's view
    /// of its side can still count parties whose casualties or departure have not synced yet, which hides the
    /// option and soft-locks the player. Conversely, while ANY player resolves the event (live mission or
    /// auto-resolve), a surrender would conclude the battle under them, so a claimed event refuses the option until
    /// the claim releases (<see cref="Messages.Start.NetworkBattleModeSet"/>); the server mirrors this refusal
    /// authoritatively via <see cref="ServerBattleModeArbiter"/> in <c>PlayerCaptivityServerHandler</c>.
    /// </summary>
    private static void PostfixSurrenderCondition(MenuCallbackArgs args, ref bool __result)
    {
        if (!__result && IsIncapacitatedDefender())
        {
            args.optionLeaveType = GameMenuOption.LeaveType.Surrender;
            __result = true;
        }

        if (!__result) return;

        if (!TryGetCurrentMapEventId(out var mapEventId)) return;

        if (BattleModeRegistry.IsMission(mapEventId))
        {
            args.IsEnabled = false;
            args.Tooltip = MissionUnderwayTooltip;
        }
        else if (BattleModeRegistry.IsSimulation(mapEventId))
        {
            args.IsEnabled = false;
            args.Tooltip = SimulationUnderwayTooltip;
        }
    }

    /// <summary>Wounded main hero with no healthy member in its own party, on a battle side native gives no plain
    /// leave — the state that has no other usable encounter option.</summary>
    private static bool IsIncapacitatedDefender()
    {
        if (MobileParty.MainParty?.MapEvent == null) return false;
        if (Hero.MainHero?.IsWounded != true) return false;
        if (PartyBase.MainParty.NumberOfHealthyMembers != 0) return false;

        return !MapEventHelper.CanMainPartyLeaveBattleCommonCondition();
    }

    private static bool TryGetCurrentMapEventId(out string mapEventId)
    {
        mapEventId = null;

        var mapEvent = GetPlayerEncounterBattle() ?? MobileParty.MainParty?.MapEvent;
        if (mapEvent == null) return false;

        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager)) return false;

        return objectManager.TryGetId(mapEvent, out mapEventId);
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

    private static MapEvent GetPlayerEncounterBattle()
    {
        try
        {
            return PlayerEncounter.Battle;
        }
        catch (NullReferenceException)
        {
            return null;
        }
    }

    private static string DescribeEncounterState()
    {
        var encounter = PlayerEncounter.Current;
        return $"menu={Campaign.Current?.CurrentMenuContext?.GameMenu?.StringId ?? "<none>"}; encounter={(encounter != null)}; mainPartyMapEvent={(MobileParty.MainParty?.MapEvent != null)}; battle={(GetPlayerEncounterBattle() != null)}; attacker={(encounter?._attackerParty != null)}; defender={(encounter?._defenderParty != null)}";
    }
}
