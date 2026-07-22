#if DEBUG
using Common.Messaging;
using GameInterface.Registry.Auto;
using GameInterface.Services.MapEventSides.Messages;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MapEvents;

public static class RaidDebugFixture
{
    private static readonly Dictionary<MapEvent, MissionParticipant> missionParticipants = new();

    public static void RegisterMissionParticipant(MapEvent mapEvent, string controllerId, MobileParty party)
    {
        missionParticipants[mapEvent] = new MissionParticipant(controllerId, party, new MapEventParty(party.Party));
    }

    public static bool TryGetMissionParticipant(MapEvent mapEvent, string controllerId, out MobileParty party)
    {
        party = null;
        if (!missionParticipants.TryGetValue(mapEvent, out var participant) ||
            participant.ControllerId != controllerId)
            return false;

        party = participant.Party;
        return true;
    }

    public static bool TryGetMissionParticipant(MapEvent mapEvent, out MobileParty party)
    {
        party = null;
        if (!missionParticipants.TryGetValue(mapEvent, out var participant))
            return false;

        party = participant.Party;
        return true;
    }

    public static bool TryGetMissionReserveParty(MapEvent mapEvent, string controllerId, out MapEventParty reserveParty)
    {
        reserveParty = null;
        if (!missionParticipants.TryGetValue(mapEvent, out var participant) ||
            participant.ControllerId != controllerId)
            return false;

        reserveParty = participant.ReserveParty;
        return true;
    }

    public static bool TryGetMissionReserveParty(MapEvent mapEvent, out MapEventParty reserveParty)
    {
        reserveParty = null;
        if (!missionParticipants.TryGetValue(mapEvent, out var participant))
            return false;

        reserveParty = participant.ReserveParty;
        return true;
    }

    public static void UnregisterMissionParticipant(MapEvent mapEvent)
    {
        if (mapEvent == null || !missionParticipants.TryGetValue(mapEvent, out var participant))
            return;

        missionParticipants.Remove(mapEvent);
        MessageBroker.Instance.Publish(participant.ReserveParty,
            new InstanceDestroyed<MapEventParty>(participant.ReserveParty));
    }

    public static int DetachNonSettlementDefenders(MapEvent mapEvent)
    {
        var defenderSide = mapEvent.DefenderSide;
        var settlementParty = mapEvent.MapEventSettlement?.Party;
        var detachedCount = 0;

        for (var i = defenderSide._battleParties.Count - 1; i >= 0; i--)
        {
            var mapEventParty = defenderSide._battleParties[i];
            var party = mapEventParty.Party;
            if (party == settlementParty)
                continue;

            defenderSide._battleParties.RemoveAt(i);
            defenderSide._mapEvent.RemoveInvolvedPartyInternal(mapEventParty);
            if (party._mapEventSide == defenderSide)
                party._mapEventSide = null;
            MessageBroker.Instance.Publish(defenderSide, new MapEventPartyRemoved(defenderSide, mapEventParty));
            detachedCount++;
        }

        return detachedCount;
    }

    private sealed class MissionParticipant
    {
        public string ControllerId { get; }
        public MobileParty Party { get; }
        public MapEventParty ReserveParty { get; }

        public MissionParticipant(string controllerId, MobileParty party, MapEventParty reserveParty)
        {
            ControllerId = controllerId;
            Party = party;
            ReserveParty = reserveParty;
        }
    }
}
#endif
