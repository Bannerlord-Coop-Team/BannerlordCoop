using Common.Logging;
using GameInterface.Configuration;
using GameInterface.Services.MobilePartyAIs.Patches;
using Serilog;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.MapEvents;

/// <summary>
/// Recreates the battle-type selection performed by
/// <c>TaleWorlds.CampaignSystem.Encounters.PlayerEncounter.StartBattleInternal</c> so the server can create the
/// authoritative <see cref="MapEvent"/> on a client's behalf.
/// </summary>
/// <remarks>
/// The branch order here mirrors vanilla <c>StartBattleInternal</c> exactly. The "force" decisions come from the
/// requesting client (<see cref="BattleCreationFlags"/>); the settlement/siege/blockade decisions are derived from
/// the parties' own state, which is already synchronized on the server.
/// </remarks>
internal sealed class MapEventBattleFactory
{
    private static readonly ILogger Logger = LogManager.GetLogger<MapEventBattleFactory>();

    private MapEventBattleFactory() { }

    /// <summary>
    /// Creates the <see cref="MapEvent"/> the supplied parties would produce in <c>StartBattleInternal</c>.
    /// Must be called on the main thread with synchronization patches enabled.
    /// </summary>
    /// <returns>The created <see cref="MapEvent"/>, or null if no proper type could be determined.</returns>
    public static MapEvent CreateMapEvent(PartyBase attacker, PartyBase defender, BattleCreationFlags flags)
    {
        // Names the branch and the reason, because "the battle came out with the wrong sides" is otherwise
        // indistinguishable between "we built it wrong" and "we never built it and something else did".
        Logger.Information(
            "[BattleFactoryDiag] request attacker={Attacker}(side={AttackerSide},besieging={AttackerBesieging}) " +
            "defender={Defender}(side={DefenderSide},besieging={DefenderBesieging}) forced={Forced}",
            Describe(attacker), attacker?.MapEventSide?.MissionSide, attacker?.MobileParty?.BesiegedSettlement?.StringId,
            Describe(defender), defender?.MapEventSide?.MissionSide, defender?.MobileParty?.BesiegedSettlement?.StringId,
            flags.IsForced);

        // A player relieving a friendly besieged settlement must fight the BESIEGERS, not the settlement.
        // The request arrives here naming the settlement as defender, which would create a siege of the
        // player's own castle and, because both are the same faction, split one faction across both sides -
        // the player literally fighting their own troops. Redirect to the besieger camp so the factory's
        // existing BesiegedSettlement branch builds a SiegeOutside battle: a field fight outside the walls,
        // player versus besiegers, which is also the state that lets the garrison sortie to help.
        Settlement reliefTarget = null;
        if (TryRedirectSiegeRelief(attacker, ref defender, out reliefTarget))
        {
            Logger.Information(
                "[BattleFactoryDiag] relief redirect: defender settlement -> besieger camp leader {Defender}",
                Describe(defender));
        }
        else if (attacker?.MapFaction != null && ReferenceEquals(attacker.MapFaction, defender?.MapFaction))
        {
            // Not blocked outright: two players of one faction fighting each other is a supported PvP case.
            // Logged because for an AI-side battle it means the sides are about to be built from one faction.
            Logger.Warning(
                "[BattleFactoryDiag] attacker and defender share faction {Faction}: {Attacker} vs {Defender}",
                attacker.MapFaction.StringId, Describe(attacker), Describe(defender));
        }

        if (!CanCreateMapEvent(attacker, defender, flags))
        {
            Logger.Warning(
                "[BattleFactoryDiag] REJECTED: {Reason}. Attacker={Attacker}, Defender={Defender}",
                WhyCannotCreate(attacker, defender, flags),
                attacker?.MobileParty?.StringId ?? attacker?.Settlement?.StringId,
                defender?.MobileParty?.StringId ?? defender?.Settlement?.StringId);
            return null;
        }

        var mapEventManager = Campaign.Current.MapEventManager;

        if (TryCreateForcedMapEvent(attacker, defender, flags, mapEventManager, out var mapEvent))
            return mapEvent;

        if (defender.IsSettlement)
            return CreateSettlementMapEvent(attacker, defender, flags, mapEventManager);

        if (TryCreateAmbushOrBlockadeMapEvent(attacker, defender, flags, out mapEvent))
            return mapEvent;

        if (TryCreateMobileSettlementMapEvent(attacker, defender, mapEventManager, out mapEvent))
        {
            Logger.Information("[BattleFactoryDiag] branch=MobileSettlement -> {Type}", mapEvent?._mapEventType);
            PullGarrisonIntoRelief(mapEvent, attacker, reliefTarget);
            return mapEvent;
        }

        Logger.Information("[BattleFactoryDiag] branch=FieldBattle");
        return CreateFieldBattleEvent(attacker, defender, mapEventManager);
    }

    /// <summary>
    /// Swaps a friendly besieged settlement for the camp besieging it, so "relieve the siege" builds a battle
    /// against the besiegers. Leaves everything else alone: a settlement we are at war with is a real siege,
    /// and a besieger already in a battle is a join, not a creation.
    /// </summary>
    private static bool TryRedirectSiegeRelief(PartyBase attacker, ref PartyBase defender, out Settlement besieged)
    {
        besieged = null;
        if (attacker == null || defender?.IsSettlement != true) return false;

        var settlement = defender.Settlement;
        var siegeEvent = settlement?.SiegeEvent;
        if (siegeEvent == null) return false;

        var attackerFaction = attacker.MapFaction;
        var settlementFaction = settlement.MapFaction;
        if (attackerFaction == null || settlementFaction == null) return false;

        // At war with it means this really is our siege of their settlement.
        if (settlementFaction.IsAtWarWith(attackerFaction)) return false;

        var besiegerLeader = siegeEvent.BesiegerCamp?.LeaderParty?.Party;
        if (besiegerLeader == null || ReferenceEquals(besiegerLeader, attacker)) return false;

        // Already fighting: this would be a join rather than a new battle, and CanCreateMapEvent would
        // reject it anyway. Leave the request untouched so the rejection reason stays honest.
        if (besiegerLeader.MapEventSide != null) return false;

        defender = besiegerLeader;
        besieged = settlement;
        return true;
    }


    /// <summary>
    /// Sends the besieged settlement's defenders out to fight beside the relief force.
    /// </summary>
    /// <remarks>
    /// A garrison only leaves the walls through a sally-out, and SallyOutsCampaignBehavior checks that on the
    /// settlement's hourly tick. Campaign time is paused while a player sits in the relief battle, so that
    /// check cannot fire and the defenders the player came to rescue never appear. This performs the sortie
    /// the situation calls for, at the one moment it can: when the relief battle is created.
    ///
    /// The roster comes from GetInvolvedPartiesForEventType(SallyOut), so it is exactly who vanilla would
    /// send on a sortie - and it honours militiaJoinsSallyOut for free. The settlement's own party is skipped:
    /// a fortification cannot march out to a field battle.
    /// </remarks>
    private static void PullGarrisonIntoRelief(MapEvent mapEvent, PartyBase attacker, Settlement besieged)
    {
        if (besieged == null || mapEvent == null) return;
        if (!ModConfigProvider.ModOptions.GarrisonJoinsSiegeRelief) return;

        var reliefSide = attacker?.MapEventSide;
        if (reliefSide == null)
        {
            Logger.Warning("[BattleFactoryDiag] relief force has no side yet; garrison not pulled in");
            return;
        }

        var joined = 0;
        foreach (var party in besieged.GetInvolvedPartiesForEventType(MapEvent.BattleTypes.SallyOut))
        {
            // Never the fortification itself, and never a party already committed elsewhere.
            if (party?.IsMobile != true || party.MapEventSide != null) continue;
            if (party.MobileParty?.IsActive != true) continue;

            reliefSide.AddNearbyPartyToPlayerMapEvent(party.MobileParty);
            joined++;
        }

        Logger.Information(
            "[BattleFactoryDiag] garrison sortie for relief of {Settlement}: {Joined} party(s) joined the relief side",
            besieged.StringId, joined);
    }

    private static string Describe(PartyBase party)
        => party == null ? "<null>" : $"{party.Id}[{party.MapFaction?.StringId ?? "?"}]";

    /// <summary>Mirrors CanCreateMapEvent so a refusal says which condition stopped it.</summary>
    private static string WhyCannotCreate(PartyBase attacker, PartyBase defender, BattleCreationFlags flags)
    {
        if (attacker == null) return "attacker is null";
        if (defender == null) return "defender is null";
        if (ReferenceEquals(attacker, defender)) return "attacker and defender are the same party";
        if (attacker.MapEventSide != null) return "attacker is ALREADY in a map event";
        if (defender.MapEventSide != null) return "defender is ALREADY in a map event";

        if (DefaultMobilePartyAIModelPatches.IsAttackPrevented(attacker.MobileParty, defender.MobileParty))
            return "attack prevented by the AI model";

        if (!WillCreateFieldBattle(attacker, defender, flags)) return "<none>";

        if (attacker.MobileParty?.IsActive != true) return "attacker is not active";
        if (defender.MobileParty?.IsActive != true) return "defender is not active";
        if (attacker.MobileParty.CurrentSettlement != null) return "attacker is inside a settlement";
        if (defender.MobileParty.CurrentSettlement != null) return "defender is inside a settlement";

        return "<none>";
    }

    internal static bool CanCreateMapEvent(PartyBase attacker, PartyBase defender, BattleCreationFlags flags)
    {
        if (attacker == null || defender == null || ReferenceEquals(attacker, defender) ||
            attacker.MapEventSide != null || defender.MapEventSide != null) return false;

        if (DefaultMobilePartyAIModelPatches.IsAttackPrevented(
                attacker.MobileParty,
                defender.MobileParty))
            return false;

        if (!WillCreateFieldBattle(attacker, defender, flags)) return true;
        var attackerMobileParty = attacker.MobileParty;
        var defenderMobileParty = defender.MobileParty;
        return attackerMobileParty?.IsActive == true && defenderMobileParty?.IsActive == true &&
            attackerMobileParty.CurrentSettlement == null && defenderMobileParty.CurrentSettlement == null;
    }

    private static bool WillCreateFieldBattle(PartyBase attacker, PartyBase defender, BattleCreationFlags flags)
    {
        if (flags.ForceRaid || flags.ForceSallyOut || flags.ForceVolunteers || flags.ForceSupplies)
            return false;

        if (defender.IsSettlement)
            return false;

        if (flags.IsSallyOutAmbush || flags.ForceBlockadeAttack || flags.ForceBlockadeSallyOutAttack)
            return false;

        if (attacker.IsMobile && attacker.MobileParty.CurrentSettlement?.SiegeEvent != null)
            return false;

        return !defender.IsMobile || defender.MobileParty.BesiegedSettlement == null;
    }

    private static bool TryCreateForcedMapEvent(
        PartyBase attacker,
        PartyBase defender,
        BattleCreationFlags flags,
        MapEventManager mapEventManager,
        out MapEvent mapEvent)
    {
        mapEvent = null;
        if (flags.ForceRaid)
        {
            mapEvent = RaidEventComponent.CreateRaidEvent(attacker, defender).MapEvent;
            return true;
        }

        if (flags.ForceSallyOut)
        {
            mapEvent = mapEventManager.StartSallyOutMapEvent(attacker, defender);
            return true;
        }

        if (flags.ForceVolunteers)
        {
            mapEvent = ForceVolunteersEventComponent.CreateForceSuppliesEvent(attacker, defender).MapEvent;
            return true;
        }

        if (flags.ForceSupplies)
        {
            mapEvent = ForceSuppliesEventComponent.CreateForceSuppliesEvent(attacker, defender).MapEvent;
            return true;
        }

        return false;
    }

    private static MapEvent CreateSettlementMapEvent(
        PartyBase attacker,
        PartyBase defender,
        BattleCreationFlags flags,
        MapEventManager mapEventManager)
    {
        if (defender.Settlement.IsFortification)
            return mapEventManager.StartSiegeMapEvent(attacker, defender);

        if (defender.Settlement.IsVillage)
            return RaidEventComponent.CreateRaidEvent(attacker, defender).MapEvent;

        if (defender.Settlement.IsHideout)
            return HideoutEventComponent.CreateHideoutEvent(attacker, defender, flags.ForceHideoutSendTroops).MapEvent;

        Logger.Error(
            "Proper map event type could not be determined for settlement battle. Attacker={Attacker}, Defender={Defender}",
            attacker.Name,
            defender.Name);
        return null;
    }

    private static bool TryCreateAmbushOrBlockadeMapEvent(
        PartyBase attacker,
        PartyBase defender,
        BattleCreationFlags flags,
        out MapEvent mapEvent)
    {
        mapEvent = null;
        if (flags.IsSallyOutAmbush)
        {
            mapEvent = SiegeAmbushEventComponent.CreateSiegeAmbushEvent(attacker, defender).MapEvent;
            return true;
        }

        if (flags.ForceBlockadeAttack)
        {
            mapEvent = BlockadeBattleMapEvent.CreateBlockadeBattleMapEvent(attacker, defender, false).MapEvent;
            return true;
        }

        if (flags.ForceBlockadeSallyOutAttack)
        {
            mapEvent = BlockadeBattleMapEvent.CreateBlockadeBattleMapEvent(attacker, defender, true).MapEvent;
            return true;
        }

        return false;
    }

    private static bool TryCreateMobileSettlementMapEvent(
        PartyBase attacker,
        PartyBase defender,
        MapEventManager mapEventManager,
        out MapEvent mapEvent)
    {
        mapEvent = null;
        if (attacker.IsMobile
            && attacker.MobileParty.CurrentSettlement != null
            && attacker.MobileParty.CurrentSettlement.SiegeEvent != null)
        {
            if (attacker.MobileParty.IsTargetingPort)
                mapEvent = BlockadeBattleMapEvent.CreateBlockadeBattleMapEvent(attacker, defender, true).MapEvent;
            else
                mapEvent = mapEventManager.StartSallyOutMapEvent(attacker, defender);

            return true;
        }

        if (defender.IsMobile && defender.MobileParty.BesiegedSettlement != null)
        {
            mapEvent = mapEventManager.StartSiegeOutsideMapEvent(attacker, defender);
            return true;
        }

        return false;
    }

    private static MapEvent CreateFieldBattleEvent(PartyBase attacker, PartyBase defender, MapEventManager mapEventManager)
    {
        var mapEvent = new MapEvent();
        if (Campaign.Current?.VisualCreator?.MapEventVisualCreator == null)
            mapEvent.MapEventVisual = HeadlessMapEventVisual.Instance;

        mapEvent.Initialize(
            attacker,
            defender,
            new FieldBattleEventComponent(mapEvent),
            MapEvent.BattleTypes.FieldBattle);

        if (!mapEventManager.MapEvents.Contains(mapEvent))
            mapEventManager.OnMapEventCreated(mapEvent);

        return mapEvent;
    }

    private sealed class HeadlessMapEventVisual : IMapEventVisual
    {
        public static readonly HeadlessMapEventVisual Instance = new HeadlessMapEventVisual();

        public void Initialize(CampaignVec2 position, bool isVisible) { }
        public void OnMapEventEnd() { }
        public void SetVisibility(bool isVisible) { }
    }
}
