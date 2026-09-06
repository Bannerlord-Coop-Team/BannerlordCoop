using Common;
using Common.Logging;
using GameInterface.Configuration;
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
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace GameInterface.Services.MapEvents.Patches;

/// <summary>
/// Enforces mission-XOR-simulation on the encounter menu of a shared map event: while it is claimed for a live
/// mission (<see cref="BattleModeRegistry.IsMission"/>) the auto-resolve options grey out, and while claimed for an
/// auto-resolve (<see cref="BattleModeRegistry.IsSimulation"/>) the mission-start options grey out. The mode comes
/// from the server's <see cref="Messages.Start.NetworkBattleModeSet"/>. The surrender option gets the inverse
/// treatment: forced available for a defender that cannot fight (see <see cref="PostfixSurrenderCondition"/>) and
/// refused while either mode owns the event. Abandon army is hidden while a destroyed battle waits for deferred
/// encounter cleanup; other options (join, regular leave, talk) are untouched.
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
    private static readonly TextObject LowMoraleTooltip = new("{=xnRtINwH}Your men lack the courage to continue the battle without you. (Low Morale)");
    private static readonly TextObject WoundedTooltip = new("{=UL8za0AO}You are wounded.");
    private static readonly TextObject RaftTooltip = new("{=x9ePfpw5}You are on a raft, in desperate circumstances, and cannot fight");
    private const string AttackCondition = "game_menu_encounter_attack_on_condition";
    private const string OrderAttackCondition = "game_menu_encounter_order_attack_on_condition";

    // Live-mission launch options, greyed while a simulation runs (launch_mission is the shared catch-all every
    // mission path funnels through). Trailing comment = in-game label.
    private static readonly HashSet<string> MissionStartConditions = new()
    {
        AttackCondition,                                   // Attack!
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
        OrderAttackCondition,  // Send your troops to attack
    };

    // Handled inversely to the start buckets: forced available / refused rather than only greyed.
    private const string SurrenderCondition = "game_menu_encounter_surrender_on_condition"; // Surrender.
    private const string AbandonArmyCondition = "game_menu_encounter_abandon_army_on_condition"; // Abandon army.

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

        var abandonArmy = AccessTools.Method(typeof(EncounterGameMenuBehavior), AbandonArmyCondition);
        if (abandonArmy != null)
            yield return abandonArmy;
    }

    [HarmonyPrefix]
    static bool Prefix(MethodBase __originalMethod, ref bool __result)
    {
        if (ModInformation.IsServer || __originalMethod.Name != AbandonArmyCondition) return true;
        if (!ShouldSkipAbandonArmyCondition(MobileParty.MainParty)) return true;

        __result = false;
        return false;
    }

    internal static bool ShouldSkipAbandonArmyCondition(MobileParty mainParty)
    {
        var army = mainParty?.Army;
        return army != null && army.LeaderParty != mainParty && mainParty.MapEvent == null;
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

        if (SimulationStartConditions.Contains(name))
        {
            EnableAlliedSimulationForIncapacitatedPlayer(__0, __result);

            if (name == OrderAttackCondition && !__result && ShouldShowClientSiegeAttackOption())
                __result = TryApplyClientSiegeOrderAttackOptionState(__0);
        }

        if (name == AttackCondition && !__result && ShouldShowClientSiegeAttackOption())
        {
            __result = true;
            ApplyClientSiegeAttackOptionState(__0);
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

    internal static bool ShouldShowClientSiegeAttackOption()
    {
        var mainParty = MobileParty.MainParty?.Party;
        var mapEvent = mainParty?.MapEvent;
        var playerSide = mainParty?.MapEventSide;
        if (mapEvent?.IsSiegeAssault != true || playerSide == null)
            return false;

        if (!ReferenceEquals(PlayerEncounter.Battle, mapEvent))
            return false;

        var tracker = mapEvent.TroopUpgradeTracker;
        if (tracker?._mapEventParties == null)
            return false;

        var snapshotParties = new List<MapEventParty>();
        foreach (var side in mapEvent._sides ?? Array.Empty<MapEventSide>())
        {
            if (side?.Parties == null)
                return false;

            foreach (var party in side.Parties)
                snapshotParties.Add(party);
        }

        if (tracker._mapEventParties.Count != snapshotParties.Count)
            return false;

        foreach (var party in snapshotParties)
        {
            if (!tracker._mapEventParties.Contains(party))
                return false;
        }

        MapEventSide opponentSide;
        if (ReferenceEquals(playerSide, mapEvent.AttackerSide))
            opponentSide = mapEvent.DefenderSide;
        else if (ReferenceEquals(playerSide, mapEvent.DefenderSide))
            opponentSide = mapEvent.AttackerSide;
        else
            return false;

        if (opponentSide?.LeaderParty == null)
            return false;

        foreach (var party in opponentSide.Parties)
        {
            if (party?.Troops == null)
                continue;

            foreach (var troop in party.Troops)
            {
                if (!troop.IsWounded && !troop.IsKilled)
                    return true;
            }
        }

        return false;
    }

    private static void ApplyClientSiegeAttackOptionState(MenuCallbackArgs args)
    {
        if (Hero.MainHero?.IsWounded == true && !ModConfigProvider.ModOptions.PlayerWoundedBattleEntry)
        {
            args.Tooltip = WoundedTooltip;
            args.IsEnabled = false;
        }

        var mapEvent = MobileParty.MainParty?.MapEvent;
        var opponentSide = MobileParty.MainParty?.Party?.OpponentSide ?? BattleSideEnum.None;
        var opponentInRaft = false;
        if (mapEvent != null && opponentSide != BattleSideEnum.None)
        {
            foreach (var party in mapEvent.PartiesOnSide(opponentSide))
            {
                if (party.Party.MobileParty?.IsInRaftState != true)
                    continue;

                opponentInRaft = true;
                break;
            }
        }

        if (MobileParty.MainParty?.IsInRaftState != true && !opponentInRaft)
            return;

        args.Tooltip = RaftTooltip;
        args.IsEnabled = false;
    }

    private static bool TryApplyClientSiegeOrderAttackOptionState(MenuCallbackArgs args)
    {
        var mapEvent = GetPlayerEncounterBattle() ?? MobileParty.MainParty?.MapEvent;
        var encounter = PlayerEncounter.Current;
        var playerSide = MobileParty.MainParty?.MapEventSide;
        if (mapEvent == null || encounter == null || playerSide == null)
            return false;

        var isNavalOrder = mapEvent.IsNavalMapEvent ||
                           (MapEventHelper.IsNavalRaid(mapEvent) && mapEvent.PlayerSide == BattleSideEnum.Attacker);
        args.optionLeaveType = isNavalOrder
            ? GameMenuOption.LeaveType.OrderShipsToAttack
            : GameMenuOption.LeaveType.OrderTroopsToAttack;

        foreach (var party in mapEvent.PartiesOnSide(encounter.OpponentSide))
        {
            if (party.Party.MobileParty?.IsInRaftState == true)
                return false;

            break;
        }

        MenuHelper.CheckEnemyAttackableHonorably(args);

        var orderableTroops = 0;
        foreach (var party in playerSide.Parties)
        {
            if (party.Party.IsMobile && party.Party.MobileParty.IsInRaftState)
                continue;

            orderableTroops += CountOrderableTroops(party.Party);
        }

        if (orderableTroops <= 0)
            return false;

        if (!MobileParty.MainParty.IsInRaftState && CountOrderableTroops(PartyBase.MainParty) > 0)
        {
            MBTextManager.SetTextVariable(
                "SEND_TROOPS_TEXT",
                isNavalOrder ? "{=NFnS5YqQ}Send ships." : "{=QfMeoKOm}Send troops.");
        }
        else
        {
            MBTextManager.SetTextVariable("SEND_TROOPS_TEXT", "{=jo3UHKMD}Leave it to the others.");
        }

        if (mapEvent.IsInvulnerable)
            mapEvent.IsInvulnerable = false;

        if (!MobilePartyHelper.CanPartyAttackWithCurrentMorale(MobileParty.MainParty))
        {
            args.Tooltip = LowMoraleTooltip;
            args.IsEnabled = false;
        }
        else
        {
            var mapFaction = PlayerEncounter.EncounteredParty.MapFaction;
            if (mapFaction == null || mapFaction.NotAttackableByPlayerUntilTime.IsPast)
                args.Tooltip = TooltipHelper.GetSendTroopsPowerContextTooltipForMapEvent();
        }

        return true;
    }

    private static int CountOrderableTroops(PartyBase party)
    {
        var count = 0;
        foreach (var element in party.MemberRoster.GetTroopRoster())
        {
            if (element.Character.IsHero)
            {
                if (element.Character != CharacterObject.PlayerCharacter && !element.Character.HeroObject.IsWounded)
                    count++;
            }
            else
            {
                count += element.Number - element.WoundedNumber;
            }
        }

        return count;
    }

    /// <summary>Native uses the empty main party's morale to disable "Leave it to the others" even when healthy
    /// allied parties can resolve the field battle. Restore that option without changing any other native gate.</summary>
    private static void EnableAlliedSimulationForIncapacitatedPlayer(MenuCallbackArgs args, bool isShown)
    {
        if (!isShown || args.IsEnabled) return;
        if (args.Tooltip?.HasSameValue(LowMoraleTooltip) != true) return;
        if (Hero.MainHero?.IsWounded != true) return;
        if (PartyBase.MainParty.NumberOfHealthyMembers != 0) return;

        var mapEvent = GetPlayerEncounterBattle() ?? MobileParty.MainParty?.MapEvent;
        if (mapEvent == null || !mapEvent.IsFieldBattle || mapEvent.IsNavalMapEvent || mapEvent.MapEventSettlement != null)
            return;

        if (!HasHealthyAlliedParty(MobileParty.MainParty.MapEventSide)) return;
        if (IsEnemyTemporarilyProtected()) return;

        args.IsEnabled = true;
        args.Tooltip = TooltipHelper.GetSendTroopsPowerContextTooltipForMapEvent();
    }

    private static bool HasHealthyAlliedParty(MapEventSide side)
    {
        if (side == null) return false;

        foreach (var party in side.Parties)
        {
            if (party.Party != PartyBase.MainParty && party.Party.NumberOfHealthyMembers > 0)
                return true;
        }

        return false;
    }

    private static bool IsEnemyTemporarilyProtected()
    {
        var mainParty = MobileParty.MainParty;
        if (mainParty.Army != null && mainParty.Army.LeaderParty != mainParty) return false;
        if (PlayerEncounter.PlayerIsDefender) return false;

        return PlayerEncounter.EncounteredParty?.MapFaction?.NotAttackableByPlayerUntilTime.IsFuture == true;
    }

    /// <summary>
    /// An incapacitated defender — wounded main hero, no healthy member left in its own party, on a battle side it
    /// cannot walk away from — must always be able to surrender: it cannot attack (wound), may have no allied party
    /// available to simulate, and cannot afford the get-away troop sacrifice. Native only shows surrender when the
    /// WHOLE side has no healthy member left
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
