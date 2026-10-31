using Common.Messaging;
using GameInterface.Services.MapEvents.Handlers;
using GameInterface.Services.Missions;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using Serilog;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.Core;

namespace GameInterface.Services.MapEvents;

/// <summary>
/// [Server] Ends battles nothing else will end. Vanilla only simulates while both sides have healthy troops,
/// so once a live mission leaves one side with none and nobody reports a result, the battle stays forever.
/// </summary>
public interface IStalledMapEventResolver
{
    /// <summary>True while a live mission claimed the battle and still has members in it.</summary>
    bool HasOccupiedBattleMission(MapEvent mapEvent);

    /// <summary>True while an occupied mission, or a server simulation, owns the battle.</summary>
    bool IsBattleModeBusy(MapEvent mapEvent);

    /// <summary>True while a player party in the battle has a connected owner, whose own encounter ends it.</summary>
    bool HasConnectedPlayerParty(MapEvent mapEvent);

    /// <summary>Ends the battle like vanilla would have if it is stalled and no mission or player owns it.</summary>
    bool TryResolve(MapEvent mapEvent);

    /// <summary>Ends every stalled battle with no player party in it.</summary>
    /// <returns>How many stalled battles were ended.</returns>
    int ResolveStalledMapEvents();
}

/// <inheritdoc cref="IStalledMapEventResolver"/>
internal sealed class StalledMapEventResolver : IStalledMapEventResolver
{
    private readonly ILogger logger;
    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly IPlayerManager playerManager;
    private readonly IMissionMembershipRegistry missionMembership;

    // Missions are server only, so the client container has no membership registry.
    public StalledMapEventResolver(
        ILogger logger,
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        IPlayerManager playerManager,
        IMissionMembershipRegistry missionMembership = null)
    {
        this.logger = logger;
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.playerManager = playerManager;
        this.missionMembership = missionMembership;
    }

    public bool HasOccupiedBattleMission(MapEvent mapEvent) =>
        missionMembership != null &&
        objectManager.TryGetId(mapEvent, out var mapEventId) &&
        ServerBattleModeArbiter.IsMissionClaimed(mapEventId) &&
        missionMembership.IsInstanceOccupied(mapEventId);

    public bool IsBattleModeBusy(MapEvent mapEvent)
    {
        if (HasOccupiedBattleMission(mapEvent))
            return true;

        return objectManager.TryGetId(mapEvent, out var mapEventId) &&
            ServerBattleModeArbiter.TryGetMode(mapEventId, out var mode) &&
            mode == BattleStartMode.Simulation;
    }

    public bool TryResolve(MapEvent mapEvent)
    {
        // An offline player's battle gets here only from Update, once the update gate's grace period is over.
        if (!IsStalled(mapEvent) || IsBattleModeBusy(mapEvent) || HasConnectedPlayerParty(mapEvent))
            return false;

        logger.Warning(
            "Ending stalled map event {MapEventId} ({BattleType}) with {AttackerTroops} healthy attackers and {DefenderTroops} healthy defenders",
            mapEvent.StringId,
            mapEvent.EventType,
            mapEvent.AttackerSide.RecalculateMemberCountOfSide(),
            mapEvent.DefenderSide.RecalculateMemberCountOfSide());

        // The same pair PlayerEncounter uses after a siege mission. The finalize postfix tells the clients,
        // except during the load repairs, which run while originals are allowed.
        mapEvent.CheckIfOneSideHasLost();
        LoadRepairMapEventFinalizer.FinalizeEvent(mapEvent, messageBroker, objectManager);
        return true;
    }

    public int ResolveStalledMapEvents()
    {
        if (Campaign.Current?.MapEventManager == null)
            return 0;

        var resolved = 0;
        foreach (var mapEvent in Campaign.Current.MapEventManager.MapEvents.ToArray())
        {
            try
            {
                // A player battle is left to Update, which waits out the offline grace period first.
                if (!HasPlayerParty(mapEvent) && TryResolve(mapEvent))
                    resolved++;
            }
            catch (Exception e)
            {
                logger.Error(e, "Failed to end stalled map event {MapEventId}", mapEvent?.StringId);
            }
        }

        return resolved;
    }

    // Matches the only cases vanilla Update cannot end: its simulation already ran once and now needs healthy
    // troops on both sides. Raids are left to their component, which ends them by village hit points.
    private static bool IsStalled(MapEvent mapEvent) =>
        mapEvent != null &&
        !mapEvent.IsFinalized &&
        !mapEvent._isFinishCalled &&
        mapEvent.BattleState == BattleState.None &&
        mapEvent.FirstUpdateIsDone &&
        !mapEvent.IsRaidHostileAction() &&
        mapEvent.AttackerSide != null &&
        mapEvent.DefenderSide != null &&
        (mapEvent.AttackerSide.RecalculateMemberCountOfSide() == 0 ||
            mapEvent.DefenderSide.RecalculateMemberCountOfSide() == 0);

    public bool HasConnectedPlayerParty(MapEvent mapEvent) =>
        mapEvent.InvolvedParties.Any(party =>
            party != null &&
            party.IsMobile &&
            !party.MobileParty.IsControlledByThisInstance() &&
            !playerManager.IsOwnerOfPartyDisconnected(party.MobileParty));

    private static bool HasPlayerParty(MapEvent mapEvent) =>
        mapEvent.InvolvedParties.Any(party =>
            party != null &&
            party.IsMobile &&
            !party.MobileParty.IsControlledByThisInstance());
}
