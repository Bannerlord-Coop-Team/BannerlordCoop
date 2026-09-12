using Common.Logging;
using GameInterface.Services.MobilePartyAIs.Patches;
using Serilog;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;

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
        if (!CanCreateMapEvent(attacker, defender, flags))
        {
            Logger.Warning(
                "Rejecting map event creation for unavailable parties. Attacker={Attacker}, Defender={Defender}",
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
            return mapEvent;

        return CreateFieldBattleEvent(attacker, defender, mapEventManager);
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
